#!/usr/bin/env python3
"""
Hook built Unity bundles into the mod.

Copy the bundles the Asset Bundle Browser built (item_sptstalker_<key>.bundle) into
src/SPTStalker/bundles/, then run:

    python3 tools/link_bundles.py

It writes src/SPTStalker/bundles.json (the manifest SPT reads to serve bundles to the game) and sets
"prefabPath" in artifacts.json for every artifact whose bundle is present. Artifacts without a bundle keep
their placeholder model. Run it again whenever you add or remove bundles.
"""
import json
from pathlib import Path

MOD = Path(__file__).resolve().parent.parent / "src" / "SPTStalker"
BUNDLES = MOD / "bundles"
ARTIFACTS = MOD / "data" / "artifacts" / "artifacts.json"

found = sorted(p.relative_to(BUNDLES).as_posix() for p in BUNDLES.rglob("*.bundle")) if BUNDLES.exists() else []

manifest = MOD / "bundles.json"
if found:
    manifest.write_text(json.dumps({"manifest": [{"key": k, "dependencyKeys": []} for k in found]}, indent=2) + "\n")
    print(f"bundles.json: {len(found)} bundle(s)")
elif manifest.exists():
    manifest.unlink()
    print("no bundles found, removed bundles.json")

data = json.loads(ARTIFACTS.read_text(encoding="utf-8"))
for artifact in data["artifacts"]:
    bundle = next((k for k in found if Path(k).name == f"item_sptstalker_{artifact['key']}.bundle"), "")
    if artifact.get("prefabPath", "") != bundle:
        print(f"  {artifact['key']:12s} -> {bundle or '(placeholder model)'}")
    artifact["prefabPath"] = bundle
ARTIFACTS.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

unused = [k for k in found if not any(Path(k).name == f"item_sptstalker_{a['key']}.bundle" for a in data["artifacts"])]
if unused:
    print("bundles not matching any artifact key (listed in bundles.json only):", ", ".join(unused))
