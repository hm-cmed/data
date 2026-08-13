#!/usr/bin/env python3
"""型名を隠してしまうメンバー名を検出する（Unity が無い環境向けの静的チェック）。

C# では、あるクラスに型と同じ名前のメンバーがあると、そのクラス内で
`型名.静的メンバー` と書けなくなる。

    public static class ErrorTypes { public const string None = "none"; }

    public sealed class ScoreCard
    {
        public IReadOnlyList<string> ErrorTypes { get; }        // 型名を隠す
        public void Record(string e = ErrorTypes.None) { ... }  // CS1061
    }

同名でも許されるのは、メンバーの型がその型自身である場合だけ
（C# 言語仕様の "Identical simple names and type names"、通称 color-color 規則）。

    public ObjectiveCatalog ObjectiveCatalog { get; }  // これは合法

Unity を開かずにこの種のコンパイルエラーを潰すために使う。

    python3 codemed-x/tools/check_csharp_shadowing.py
"""

from __future__ import annotations

import io
import re
import sys
from pathlib import Path

RUNTIME_ROOT = Path(__file__).resolve().parents[1] / "unity" / "Assets" / "CodemedX"

TYPE_DECLARATION = re.compile(
    r"^\s*(?:public|internal|private|protected)\s+"
    r"(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+)*"
    r"(?:class|struct|enum|interface)\s+(\w+)",
    re.M,
)

MEMBER_DECLARATION = re.compile(
    r"^\s*(?:public|internal|protected|private)\s+"
    r"(?:static\s+|readonly\s+|const\s+|virtual\s+|override\s+|sealed\s+|abstract\s+|new\s+|event\s+)*"
    r"([\w.]+(?:<[^>()=;]*>)?(?:\[\])?)\s+"   # 型
    r"(\w+)\s*"                               # メンバー名
    r"(?=[{;=(])",
    re.M,
)


def strip_noise(source: str) -> str:
    """文字列・文字リテラル・コメントを潰す（誤検出を防ぐため）。"""
    text = re.sub(r"'(?:\\.|[^'\\])'", "CHR", source)
    text = re.sub(r'@"(?:[^"]|"")*"', "STR", text)
    text = re.sub(r'"(?:\\.|[^"\\])*"', "STR", text)
    text = re.sub(r"//[^\n]*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return text


def main() -> int:
    files = sorted(RUNTIME_ROOT.rglob("*.cs"))
    if not files:
        print(f"C# ファイルが見つかりません: {RUNTIME_ROOT}", file=sys.stderr)
        return 1

    sources = {path: strip_noise(io.open(path, encoding="utf-8").read()) for path in files}

    type_names = set()
    for text in sources.values():
        type_names.update(TYPE_DECLARATION.findall(text))

    problems = []
    for path, text in sources.items():
        for declared_type, member_name in MEMBER_DECLARATION.findall(text):
            if member_name not in type_names:
                continue

            # メンバーの型がその型自身なら合法（color-color 規則）。
            if declared_type.split(".")[-1] == member_name:
                continue

            # 型宣言そのもの（class Foo など）を拾ってしまった場合は除外。
            if declared_type in ("class", "struct", "enum", "interface"):
                continue

            uses_static_access = re.search(
                r"(?<![\w.])" + re.escape(member_name) + r"\.\w", text
            )

            problems.append((path, member_name, declared_type, bool(uses_static_access)))

    if not problems:
        print(f"✓ {len(files)} ファイル / 型 {len(type_names)} 件を検査。型名を隠すメンバーはありません")
        return 0

    print(f"✗ 型名を隠すメンバーが {len(problems)} 件あります:")
    for path, member_name, declared_type, uses_static_access in problems:
        relative = path.relative_to(RUNTIME_ROOT.parents[2])
        severity = "コンパイルエラー" if uses_static_access else "潜在的な衝突"
        print(f"  - [{severity}] {relative}")
        print(f"      メンバー {declared_type} {member_name} が同名の型 {member_name} を隠しています")
        if uses_static_access:
            print(f"      同じファイル内で {member_name}.〜 と書かれているため実際にビルドが通りません")

    return 1


if __name__ == "__main__":
    sys.exit(main())
