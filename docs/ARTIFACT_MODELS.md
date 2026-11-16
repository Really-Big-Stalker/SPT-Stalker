# Artifact 3D models

The artifacts work in game right away, using **placeholder models** borrowed from vanilla items
(Christmas ornaments, a light bulb, a spark plug, etc. — set by `placeholderTpl` in
`src/SPTStalker/data/artifacts/artifacts.json`). Their own models are made in two steps:

1. **Meshes (done here):** `tools/artifact_models/generate.py` builds a model for each artifact procedurally.
2. **Game asset bundles (needs Unity on your PC):** turn each mesh into an EFT item prefab and build an asset bundle.

## 1. Meshes

```bash
python3 tools/artifact_models/generate.py
```

Output (already committed):

| Path | Contents |
|---|---|
| `art/artifacts/models/<key>.glb` | Binary glTF 2.0, Y-up, metres (6–11 cm across), smooth normals, vertex colours, PBR material with emissive glow; jellyfish, slime, flash and soul are translucent |
| `art/artifacts/previews/<key>.png` | Quick render of each model |
| `art/artifacts/previews/_sheet.png` | All artifacts on one sheet |

Each shape is a short function in `generate.py` (`jellyfish()`, `thorn()`, ...). Tweak the numbers and re-run,
or use the `.glb` as a base mesh to sculpt and texture properly in Blender.

## 2. Asset bundles (Unity)

The EFT client only loads models from Unity asset bundles built with the **same Unity version as the game**.
Check the version on the `UnityPlayer.dll` in your game folder (right-click → Properties → Details).

1. Create a Unity project with that exact version. Install a glTF importer (Unity's **glTFast** package, or UnityGLTF).
2. Import the `.glb` files, then make a prefab per artifact. EFT item prefabs need the game's own item components and
   layer setup. The SPT community's Unity SDK projects for EFT item modding provide these; the SPT modding Discord/hub
   pins the current one for 4.x. A common shortcut is to extract a vanilla barter item's bundle, swap its mesh and
   material for yours and keep its components.
3. Set each prefab's AssetBundle name to `assets/content/items/sptstalker/<key>.bundle` and build the bundles.
4. Copy the built bundles into the mod: `SPT/user/mods/SPTStalker/bundles/assets/content/items/sptstalker/<key>.bundle`
   (in this repo: `src/SPTStalker/bundles/...`; the build already copies that folder).
5. Add a `bundles.json` next to `SPTStalker.dll` (repo: `src/SPTStalker/bundles.json`) listing every bundle:

   ```json
   {
     "manifest": [
       { "key": "assets/content/items/sptstalker/jellyfish.bundle", "dependencyKeys": [] }
     ]
   }
   ```

   If your prefab uses shaders or textures from vanilla bundles, list those bundle keys in `dependencyKeys`.
6. In `artifacts.json`, set the artifact's `"prefabPath"` to the path of the prefab **inside** the bundle, e.g.
   `"assets/content/items/sptstalker/jellyfish.bundle"` (the key you used in step 3). Leave it empty to keep the placeholder.

Restart the server. The artifact now uses the new model; nothing else changes (same item id, stashes keep working).
