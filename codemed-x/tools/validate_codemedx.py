#!/usr/bin/env python3
"""Codemed-x のデータ定義を検証する。

1. schema/objectives.csv の評価項目が、本リポジトリの医学教育モデル・コア・カリキュラム
   (2022/ja/outcomes/layer*.csv) に実在する index / id を指しているか
2. samples/*.jsonl, samples/*.json が JSON Schema に適合するか
   (jsonschema 未インストール時はこの検証のみスキップする)
3. サンプルで使われている objective_id / scenario_id が objectives.csv と整合するか

使い方:
    python3 codemed-x/tools/validate_codemedx.py
終了コード 0 = 全件合格、1 = 違反あり。
"""

from __future__ import annotations

import csv
import io
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CODEMEDX = REPO_ROOT / "codemed-x"
OUTCOMES = REPO_ROOT / "2022" / "ja" / "outcomes"

SCENARIO_ID_RE = re.compile(r"^[a-z0-9_]+_v[0-9]+$")
OBJECTIVE_ID_RE = re.compile(r"^[A-Z]{3}-[A-Z]{3}-[0-9]{2}$")


def read_csv(path: Path) -> list[dict]:
    # コアカリの csv は Excel 互換のため UTF-8 BOM 付き
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def load_core_curriculum() -> dict[str, str]:
    """コアカリの id -> index の対応表を返す。"""
    index_by_id: dict[str, str] = {}
    for layer in range(1, 5):
        path = OUTCOMES / f"layer{layer}.csv"
        if not path.exists():
            raise SystemExit(f"コアカリのデータが見つかりません: {path}")
        for row in read_csv(path):
            index_by_id[row["id"]] = row["index"]
    return index_by_id


def validate_objectives(errors: list[str]) -> dict[str, dict]:
    index_by_id = load_core_curriculum()
    objectives_path = CODEMEDX / "schema" / "objectives.csv"
    objectives: dict[str, dict] = {}

    for line_no, row in enumerate(read_csv(objectives_path), start=2):
        oid = row["objective_id"].strip()
        where = f"objectives.csv:{line_no} ({oid or '<empty>'})"

        if not OBJECTIVE_ID_RE.match(oid):
            errors.append(f"{where}: objective_id の書式が不正 (AAA-BBB-01 形式)")
        if oid in objectives:
            errors.append(f"{where}: objective_id が重複している")
        if not SCENARIO_ID_RE.match(row["scenario_id"].strip()):
            errors.append(f"{where}: scenario_id の書式が不正 (例 welfare_abuse_v1)")
        if not row["label"].strip():
            errors.append(f"{where}: label が空")

        indices = [s for s in row["corecurriculum_index"].split(";") if s]
        ids = [s for s in row["corecurriculum_ids"].split(";") if s]
        if not ids:
            errors.append(f"{where}: corecurriculum_ids が空")
        if len(indices) != len(ids):
            errors.append(
                f"{where}: corecurriculum_index({len(indices)}件) と "
                f"corecurriculum_ids({len(ids)}件) の数が一致しない"
            )

        for idx, cid in zip(indices, ids):
            actual = index_by_id.get(cid)
            if actual is None:
                errors.append(f"{where}: コアカリに存在しない id: {cid}")
            elif actual != idx:
                errors.append(
                    f"{where}: id {cid} のインデックスは {actual} だが {idx} と記載されている"
                )

        objectives[oid] = row

    return objectives


def load_schemas():
    try:
        import jsonschema  # noqa: F401
        from jsonschema import Draft202012Validator
        from referencing import Registry, Resource
    except ImportError:
        return None

    schema_dir = CODEMEDX / "schema"
    resources = []
    for path in sorted(schema_dir.glob("*.json")):
        schema = json.loads(path.read_text(encoding="utf-8"))
        resources.append((path.name, Resource.from_contents(schema)))
    registry = Registry().with_resources(resources)

    def validator_for(name: str):
        schema = json.loads((schema_dir / name).read_text(encoding="utf-8"))
        return Draft202012Validator(schema, registry=registry)

    return validator_for


def validate_samples(objectives: dict[str, dict], errors: list[str]) -> None:
    validator_for = load_schemas()
    if validator_for is None:
        print("  [skip] jsonschema 未インストールのため JSON Schema 検証を省略 "
              "(pip install jsonschema)")
        event_validator = batch_validator = None
    else:
        event_validator = validator_for("training-event.schema.json")
        batch_validator = validator_for("training-event-batch.schema.json")

    samples = CODEMEDX / "samples"

    def check_event(event: dict, where: str) -> None:
        if event_validator is not None:
            for err in sorted(event_validator.iter_errors(event), key=str):
                errors.append(f"{where}: スキーマ違反 {list(err.path)}: {err.message}")
        oid = event.get("objective_id", "")
        if oid and oid not in objectives:
            errors.append(f"{where}: objectives.csv にない objective_id: {oid}")
        elif oid and objectives[oid]["scenario_id"] != event.get("scenario_id"):
            errors.append(
                f"{where}: objective_id {oid} は "
                f"{objectives[oid]['scenario_id']} 用で scenario_id と不一致"
            )

    for path in sorted(samples.glob("*.jsonl")):
        for line_no, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if not line.strip():
                continue
            check_event(json.loads(line), f"{path.name}:{line_no}")

    for path in sorted(samples.glob("*.json")):
        batch = json.loads(path.read_text(encoding="utf-8"))
        if batch_validator is not None:
            for err in sorted(batch_validator.iter_errors(batch), key=str):
                errors.append(f"{path.name}: スキーマ違反 {list(err.path)}: {err.message}")
        for i, event in enumerate(batch.get("events", [])):
            check_event(event, f"{path.name}:events[{i}]")


def main() -> int:
    errors: list[str] = []

    print("[1/2] 評価項目とコアカリ id の対応を検証")
    objectives = validate_objectives(errors)
    print(f"  評価項目 {len(objectives)} 件を読み込み")

    print("[2/2] サンプルイベントを検証")
    validate_samples(objectives, errors)

    if errors:
        print(f"\n✗ {len(errors)} 件の問題:")
        for e in errors:
            print(f"  - {e}")
        return 1

    print("\n✓ 検証に合格しました")
    return 0


if __name__ == "__main__":
    sys.exit(main())
