#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GODOT_BIN="${GODOT_BIN:-godot}"
ADDRESS="${ADDRESS:-127.0.0.1}"
PORT="${PORT:-7700}"
CLIENTS="${CLIENTS:-2}"
START_DELAY="${START_DELAY:-2}"
SCENE="res://scenes/tests/test_player_item_room_lan.tscn"
LOG_DIR="$ROOT_DIR/.tmp/test-logs"

mkdir -p "$LOG_DIR"

pids=()

cleanup() {
    if [ "${#pids[@]}" -gt 0 ]; then
        kill "${pids[@]}" 2>/dev/null || true
    fi
}

trap cleanup EXIT INT TERM

echo "Launching player/item LAN test from $ROOT_DIR"
echo "Godot: $GODOT_BIN"
echo "Server: $ADDRESS:$PORT"
echo "Clients: $CLIENTS"
echo "Logs: $LOG_DIR"

"$GODOT_BIN" --path "$ROOT_DIR" "$SCENE" -- --role host --port "$PORT" > "$LOG_DIR/player-item-host.log" 2>&1 &
pids+=("$!")

sleep "$START_DELAY"

for client_index in $(seq 1 "$CLIENTS"); do
    "$GODOT_BIN" --path "$ROOT_DIR" "$SCENE" -- --role client --address "$ADDRESS" --port "$PORT" > "$LOG_DIR/player-item-client-$client_index.log" 2>&1 &
    pids+=("$!")
    sleep 0.5
done

echo "Started instances. Press Ctrl+C in this terminal to stop them."
wait
