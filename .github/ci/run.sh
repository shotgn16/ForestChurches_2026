#!/usr/bin/env bash
# Runs a command and, if it fails, reports its last lines (and any error lines) as a GitHub
# annotation, so the cause can be read from the run summary or the API without opening the logs.
# Usage: bash .github/ci/run.sh "<title>" <command> [args...]
title="$1"; shift
log="$(mktemp)"
set +e
"$@" 2>&1 | tee "$log"
status=${PIPESTATUS[0]}
set -e
if [ "$status" -ne 0 ]; then
  errors="$(grep -E ' error |error [A-Z]+[0-9]+|: error|Exception|FAILED|Failed |MSB[0-9]+' "$log" | sort -u | head -40)"
  tail_lines="$(tail -40 "$log")"
  msg="$(printf 'Exit %s\n--- error lines ---\n%s\n--- last lines ---\n%s' "$status" "$errors" "$tail_lines")"
  msg="${msg//'%'/'%25'}"; msg="${msg//$'\r'/}"; msg="${msg//$'\n'/'%0A'}"
  echo "::error title=${title}::${msg}"
fi
exit "$status"
