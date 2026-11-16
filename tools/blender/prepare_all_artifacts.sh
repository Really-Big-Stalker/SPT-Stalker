#!/usr/bin/env bash
# Runs prepare_for_tarkov.py on every generated artifact model.
# Usage: tools/blender/prepare_all_artifacts.sh [blender-or-python-with-bpy]
# Default runner: "blender -b -P". If you pass a Python with the bpy module installed, it is used directly.
set -euo pipefail
cd "$(dirname "$0")/../.."
RUNNER="${1:-blender}"
for glb in art/artifacts/models/*.glb; do
  name="$(basename "$glb" .glb)"
  if [[ "$RUNNER" == *blender* ]]; then
    "$RUNNER" -b -P tools/blender/prepare_for_tarkov.py -- --input "$glb" --name "$name"
  else
    "$RUNNER" -W ignore tools/blender/prepare_for_tarkov.py --input "$glb" --name "$name"
  fi
done
