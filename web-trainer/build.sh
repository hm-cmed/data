#!/usr/bin/env bash
# Wraps src/page.html (an artifact-style fragment) into a standalone HTML document
# that can be opened directly in Chrome / Edge (double-click) or hosted on any static server.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
src="$here/src/page.html"
out="$here/dissection-trainer.html"
title_line="$(head -n 1 "$src")"
{
  printf '<!doctype html>\n<html lang="ja">\n<head>\n<meta charset="utf-8">\n'
  printf '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n'
  printf '%s\n</head>\n<body>\n' "$title_line"
  tail -n +2 "$src"
  printf '\n</body>\n</html>\n'
} > "$out"
echo "built $out ($(wc -c < "$out") bytes)"
