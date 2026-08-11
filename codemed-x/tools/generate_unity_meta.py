#!/usr/bin/env python3
"""Unity の .meta ファイルを生成する（未生成のものだけ）。

Unity を開かずにスクリプトを追加すると .meta が無いままコミットされ、
別のマシンで開いたときに GUID が振り直されて Prefab / SerializedObject の参照が切れる。
それを避けるため、パスから決定的に GUID を作って .meta を用意しておく。

    python3 codemed-x/tools/generate_unity_meta.py [--check]

--check は生成せず、不足している .meta があれば終了コード 1 を返す（CI 用）。
Unity で一度開いたあとは Unity 側が正としてこのファイルを管理する。
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

ASSETS_ROOT = Path(__file__).resolve().parents[1] / "unity" / "Assets"

FOLDER_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundlePath:
"""

SCRIPT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundlePath:
"""

ASMDEF_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundlePath:
"""

DEFAULT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundlePath:
"""


def guid_for(path: Path) -> str:
    """パスから決定的な 32 桁 hex GUID を作る（再実行しても同じ値になる）。"""
    relative = path.relative_to(ASSETS_ROOT.parent).as_posix()
    return hashlib.md5(("codemed-x:" + relative).encode("utf-8")).hexdigest()


def template_for(path: Path) -> str:
    if path.is_dir():
        return FOLDER_TEMPLATE
    if path.suffix == ".cs":
        return SCRIPT_TEMPLATE
    if path.suffix == ".asmdef":
        return ASMDEF_TEMPLATE
    return DEFAULT_TEMPLATE


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="生成せず不足を報告するだけ")
    args = parser.parse_args()

    if not ASSETS_ROOT.exists():
        print(f"Assets フォルダが見つかりません: {ASSETS_ROOT}", file=sys.stderr)
        return 1

    targets = [ASSETS_ROOT] + sorted(
        p for p in ASSETS_ROOT.rglob("*") if p.suffix != ".meta"
    )

    missing = []
    for path in targets:
        meta_path = path.with_name(path.name + ".meta")
        if meta_path.exists():
            continue

        missing.append(meta_path)
        if not args.check:
            meta_path.write_text(
                template_for(path).format(guid=guid_for(path)), encoding="utf-8"
            )

    if args.check:
        if missing:
            print(f"✗ .meta が不足しています ({len(missing)} 件):")
            for path in missing:
                print(f"  - {path.relative_to(ASSETS_ROOT.parent)}")
            return 1
        print("✓ すべてのアセットに .meta があります")
        return 0

    print(f"✓ .meta を {len(missing)} 件生成しました（既存分は変更していません）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
