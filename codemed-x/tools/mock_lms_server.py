#!/usr/bin/env python3
"""ローカル開発用のモック LMS 受信サーバー。

Unity から実際に POST して疎通と JSON の形を確かめるために使う。
受信したバッチはスキーマ検証したうえで JSON Lines として保存し、
batch_id で重複排除する（本番の API Gateway と同じ冪等性を再現する）。

    python3 codemed-x/tools/mock_lms_server.py --port 8787
    # Unity 側の EventLoggerSettings.endpointUrl に http://<PCのIP>:8787/events を設定する
    # 実機（Quest）から叩く場合は PC のファイアウォールで当該ポートを開ける

保存先: codemed-x/samples/received_events.jsonl（--out で変更可）
"""

from __future__ import annotations

import argparse
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

CODEMEDX = Path(__file__).resolve().parents[1]

_seen_batch_ids: set[str] = set()
_validator = None
_output_path: Path


def build_validator():
    """jsonschema があればバッチスキーマの検証器を返す。無ければ None。"""
    try:
        from jsonschema import Draft202012Validator
        from referencing import Registry, Resource
    except ImportError:
        print("[warn] jsonschema 未インストールのためスキーマ検証を行いません "
              "(pip install jsonschema)", file=sys.stderr)
        return None

    schema_dir = CODEMEDX / "schema"
    registry = Registry().with_resources([
        (path.name, Resource.from_contents(json.loads(path.read_text(encoding="utf-8"))))
        for path in sorted(schema_dir.glob("*.json"))
    ])
    schema = json.loads((schema_dir / "training-event-batch.schema.json").read_text(encoding="utf-8"))
    return Draft202012Validator(schema, registry=registry)


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _respond(self, status: int, body: dict) -> None:
        payload = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_POST(self) -> None:  # noqa: N802 (BaseHTTPRequestHandler の規約)
        length = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(length)

        try:
            batch = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            self._respond(400, {"error": "invalid_json", "detail": str(error)})
            return

        if _validator is not None:
            errors = [
                {"path": list(e.path), "message": e.message}
                for e in sorted(_validator.iter_errors(batch), key=str)
            ]
            if errors:
                # 400 はクライアント側で再送しても通らないので恒久的失敗として扱われる。
                self._respond(400, {"error": "schema_violation", "violations": errors[:10]})
                print(f"[400] スキーマ違反 {len(errors)} 件: {errors[0]['message']}")
                return

        batch_id = batch.get("batch_id", "")
        if batch_id in _seen_batch_ids:
            # 再送は冪等に受理する（同じ batch_id は二重に書かない）。
            self._respond(200, {"status": "duplicate_ignored", "batch_id": batch_id})
            print(f"[200] 重複バッチを無視: {batch_id}")
            return

        _seen_batch_ids.add(batch_id)
        events = batch.get("events", [])
        with _output_path.open("a", encoding="utf-8") as f:
            for event in events:
                f.write(json.dumps(event, ensure_ascii=False) + "\n")

        self._respond(200, {"status": "accepted", "batch_id": batch_id, "accepted": len(events)})
        print(f"[200] {len(events)} 件受理 batch_id={batch_id} retry={batch.get('retry_count', 0)}")

    def do_GET(self) -> None:  # noqa: N802
        self._respond(200, {
            "status": "ok",
            "batches_received": len(_seen_batch_ids),
            "output": str(_output_path),
        })

    def log_message(self, fmt, *args):
        pass  # 独自の 1 行ログのみ出す


def main() -> int:
    global _validator, _output_path

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--out", default=str(CODEMEDX / "samples" / "received_events.jsonl"))
    args = parser.parse_args()

    _validator = build_validator()
    _output_path = Path(args.out)
    _output_path.parent.mkdir(parents=True, exist_ok=True)

    server = HTTPServer((args.host, args.port), Handler)
    print(f"モック LMS を起動しました: http://{args.host}:{args.port}/  -> {_output_path}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n停止しました")
    return 0


if __name__ == "__main__":
    sys.exit(main())
