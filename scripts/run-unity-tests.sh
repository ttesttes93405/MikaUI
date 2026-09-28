#!/usr/bin/env bash
set -uo pipefail

usage() {
  echo "Usage: $0 PlayMode|EditMode" >&2
  echo "Optional: UNITY_EDITOR_PATH=/path/to/Unity UNITY_TEST_TIMEOUT_SECONDS=1800" >&2
}

if [[ $# -ne 1 ]]; then
  usage
  exit 2
fi

case "$1" in
  PlayMode|playmode) mode=PlayMode ;;
  EditMode|editmode) mode=EditMode ;;
  *) usage; exit 2 ;;
esac

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_path="$repo_root/MikaUI.Unity"
version_file="$project_path/ProjectSettings/ProjectVersion.txt"

if [[ ! -f "$version_file" ]]; then
  echo "Unity project version file not found: $version_file" >&2
  exit 2
fi

unity_version="$(sed -n 's/^m_EditorVersion: //p' "$version_file" | head -n 1)"
if [[ -z "$unity_version" ]]; then
  echo "Cannot read the Unity Editor version from $version_file" >&2
  exit 2
fi

unity_editor="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$unity_editor" ]]; then
  echo "Unity Editor not found or not executable: $unity_editor" >&2
  exit 2
fi

timeout_seconds="${UNITY_TEST_TIMEOUT_SECONDS:-1800}"
if [[ ! "$timeout_seconds" =~ ^[0-9]+$ ]]; then
  echo "UNITY_TEST_TIMEOUT_SECONDS must be a positive integer." >&2
  exit 2
fi
timeout_seconds=$((10#$timeout_seconds))
if (( timeout_seconds < 1 )); then
  echo "UNITY_TEST_TIMEOUT_SECONDS must be a positive integer." >&2
  exit 2
fi

if ! processes="$(ps -axo pid=,comm= 2>/dev/null)"; then
  echo "ERROR: Cannot check whether Unity Editor is already running." >&2
  exit 2
fi
existing_editor_pid="$(printf '%s\n' "$processes" | awk '/\/Unity\.app\/Contents\/MacOS\/Unity$/ { print $1; exit }')"
if [[ -n "$existing_editor_pid" ]]; then
  echo "ERROR: Unity Editor is already running (PID $existing_editor_pid). Close it before running tests." >&2
  exit 8
fi

run_dir="$repo_root/artifacts/unity-tests/${mode}-$(date +%Y%m%d-%H%M%S)-$$"
mkdir -p "$run_dir" || exit 2
result_file="$run_dir/results.xml"
log_file="$run_dir/unity.log"
stdout_file="$run_dir/stdout.log"

echo "Running Unity $unity_version $mode tests"
echo "Project: $project_path"
echo "Reports: $run_dir"

"$unity_editor" \
  -batchmode \
  -projectPath "$project_path" \
  -runTests \
  -testPlatform "$mode" \
  -testResults "$result_file" \
  -logFile "$log_file" \
  >"$stdout_file" 2>&1 &
unity_pid=$!

stop_unity() {
  if kill -0 "$unity_pid" 2>/dev/null; then
    kill -TERM "$unity_pid" 2>/dev/null || true
    for (( attempt=0; attempt<5; attempt++ )); do
      kill -0 "$unity_pid" 2>/dev/null || break
      sleep 1
    done
    if kill -0 "$unity_pid" 2>/dev/null; then
      kill -KILL "$unity_pid" 2>/dev/null || true
    fi
  fi
  wait "$unity_pid" 2>/dev/null || true
}

trap 'echo "Interrupted; stopping Unity Editor." >&2; stop_unity; exit 130' INT TERM

license_error='\[Licensing::Module\].*(Timed-out|Licensing initialization failed|connection with the Unity Licensing Client has been lost)'
started_at="$(date +%s)"
while kill -0 "$unity_pid" 2>/dev/null; do
  if [[ -f "$log_file" ]] && grep -Eqi "$license_error" "$log_file" && [[ ! -f "$result_file" ]]; then
    echo "ERROR: Unity Licensing Client did not respond. $mode tests did not start." >&2
    echo "Check Unity Hub > Settings > Licenses and the log: $log_file" >&2
    stop_unity
    exit 3
  fi

  elapsed=$(( $(date +%s) - started_at ))
  if (( elapsed >= timeout_seconds )); then
    echo "ERROR: Unity test run exceeded ${timeout_seconds}s. Editor was stopped." >&2
    echo "Log: $log_file" >&2
    stop_unity
    exit 4
  fi
  sleep 2
done

wait "$unity_pid"
unity_exit_code=$?
trap - INT TERM

if [[ ! -s "$result_file" ]]; then
  if [[ -f "$log_file" ]] && grep -Eqi "$license_error" "$log_file"; then
    echo "ERROR: Unity Licensing Client did not respond. $mode tests did not start." >&2
    echo "Log: $log_file" >&2
    exit 3
  fi
  echo "ERROR: Unity exited with code $unity_exit_code but produced no test report." >&2
  echo "Log: $log_file" >&2
  exit 5
fi

test_run="$(sed -n '/<test-run /{p;q;}' "$result_file")"
attribute() {
  printf '%s\n' "$test_run" | sed -n "s/.* $1=\"\([^\"]*\)\".*/\1/p"
}

result="$(attribute result)"
total="$(attribute total)"
passed="$(attribute passed)"
failed="$(attribute failed)"
skipped="$(attribute skipped)"
inconclusive="$(attribute inconclusive)"

if [[ -z "$result" || ! "$total" =~ ^[0-9]+$ || ! "$passed" =~ ^[0-9]+$ || ! "$failed" =~ ^[0-9]+$ ]]; then
  echo "ERROR: Cannot read the Unity test result summary: $result_file" >&2
  exit 5
fi

echo "Result: $result | total=$total passed=$passed failed=$failed skipped=$skipped inconclusive=$inconclusive"
echo "Report: $result_file"
echo "Log: $log_file"

if (( total == 0 )); then
  echo "ERROR: No tests were discovered for $mode." >&2
  exit 5
fi
if [[ "$result" != Passed ]] || (( failed > 0 )); then
  awk -F ' name="' '/<test-case / && /result="Failed"/ { split($2, parts, "\""); print "Failed test: " parts[1] }' "$result_file"
  exit 6
fi
if (( unity_exit_code != 0 )); then
  echo "ERROR: Tests passed, but Unity exited with code $unity_exit_code." >&2
  exit 7
fi
