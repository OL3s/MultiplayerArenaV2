#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCENE="res://scenes/tests/test_default_arena_lan.tscn" CLIENTS="${CLIENTS:-1}" "$SCRIPT_DIR/launch-player-item-room-lan.sh"
