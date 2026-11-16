# Artifact 3D models

Artifacts work in game right away using **placeholder models** borrowed from vanilla items (`placeholderTpl` in
`src/SPTStalker/data/artifacts/artifacts.json`). Swapping in real models follows the SPT-Tushonka wiki guide
**"WTT - Item Creation Guides Vol. 1: Intro to Static Objects"** (Blender → EFT SDK in Unity → asset bundle),
with tools in this repo that do most of the clicking for you.

```
model (.glb/.fbx/.obj/.blend)                    any source: generated here, ChatGPT, Sketchfab...
   │  tools/blender/prepare_for_tarkov.py         guide §3 (scale, origin, apply transforms) + texture bake
   ▼
art/artifacts/unity/<key>/  <key>.fbx, <key>.blend, <key>_diffuse.png, [<key>_normal.png], <key>_preview.png
   │  EFT SDK + tools/unity/Editor/SptStalkerItemPrefabs.cs   guide §4–6 (shader, hierarchy, collider, pivot, label)
   ▼
item_sptstalker_<key>.bundle
   │  copy to src/SPTStalker/bundles/  +  python3 tools/link_bundles.py
   ▼
in game (same item id, so artifacts players already have switch to the new model)
```

## What you need

* **Blender** (3.0+; the scripts are tested with Blender 5.0)
* **Unity Hub** + the **Escape From Tarkov SDK** project, opened with the Unity version the SDK asks for
  (the guide's screenshots show **Unity 2019.4.39f1**)

## 1. Get a model

**Generated (already done):** `python3 tools/artifact_models/generate.py` writes `art/artifacts/models/<key>.glb`.

**Your own / ChatGPT models:** any `.glb`, `.gltf`, `.fbx`, `.obj` or `.blend`. Guide tips:
* Keep it under **~10k triangles** (the prep script reduces heavier models automatically, but a clean low-poly model looks better).
* Textures are welcome (base colour / albedo, normal map). The prep script bakes them into one set.
* Check the licence of anything you download.
* Name the file after the artifact **key** (`jellyfish`, `stone_blood`, `night_star`...), so it replaces that artifact.

## 2. Prepare it in Blender (guide §3)

```bash
# one model (your own: pick a size in metres; artifacts are ~0.06-0.10)
blender -b -P tools/blender/prepare_for_tarkov.py -- --input path/to/model.fbx --name jellyfish --size 0.09

# all generated models
tools/blender/prepare_all_artifacts.sh
```

The script:
* imports the model into an empty scene and joins it into one mesh,
* scales it so its longest side is `--size` metres (`--size 0` keeps the model's own scale), sets
  **Origin to Geometry**, **applies rotation & scale** and centres it at **0,0,0**,
* reduces it to `--max-tris` (default 10000) if needed,
* with `--solidify 0.0015`, gives thin open sheets (hoods, petals, membranes) 1.5 mm of thickness and points every face
  outwards. Use it when a model is made of single-sided surfaces: Tarkov's shaders draw only the front of a face, so
  such parts look black or see-through from one side. It roughly doubles those triangles, so raise `--max-tris`
  (e.g. 16000) if needed,
* unwraps UVs and **bakes one diffuse texture** (plus a normal map if the source had one). Tarkov's specular shader
  expects the look baked into the diffuse texture. Untextured models get their colours and glow baked in.
* writes `<key>.fbx`, `<key>.blend`, `<key>_diffuse.png`, `[<key>_normal.png]` and `<key>_preview.png` (check this one!)
  to `art/artifacts/unity/<key>/`.

The 12 generated artifacts are already prepared and committed there.

## 3. Build prefabs in the EFT SDK (guide §4–5)

1. Copy `tools/unity/Editor/SptStalkerItemPrefabs.cs` into the SDK project as `Assets/Editor/SptStalkerItemPrefabs.cs`.
2. Copy each `art/artifacts/unity/<key>/` folder to `Assets/SPTStalker/Models/<key>/`.
   (If you use `.blend` files instead of `.fbx`, Blender must be installed for Unity to read them.)
3. Run **SPT-Stalker → Build Item Prefabs**. For every model it:
   * makes a material with the **Bumped Specular SMap** shader, assigns the baked textures and the guide's baseline values
     (main colour white, specular 0.35, glossiness 1, reflection colour almost black); the Console lists the shader's
     properties in case you want to fine-tune per material,
   * builds `item_sptstalker_<key>`: an empty root at 0,0,0 with the model as its child,
   * adds a **convex Mesh Collider** to the child mesh and the **PreviewPivot** script to the root,
   * saves the prefab to `Assets/SPTStalker/Prefabs/` and sets its **AssetBundle label** to `item_sptstalker_<key>.bundle`.
4. **Window → Item Preview**: drag each prefab root in, rotate it until it looks good, then
   **Save current rotation to PreviewPivot** and **Render Icon** (guide §5.3).

## 4. Build the bundles (guide §6)

**Window → Asset Bundle Browser → Build tab → Build.** Wait until Unity is fully responsive again, or shaders may be
missing in game.

## 5. Put them in the mod

1. Copy the built `item_sptstalker_<key>.bundle` files to `src/SPTStalker/bundles/`
   (or straight into an installed mod: `SPT/user/mods/SPTStalker/bundles/`).
2. Run `python3 tools/link_bundles.py`. It writes `bundles.json` and sets each artifact's `prefabPath` to its bundle.
   Artifacts without a bundle keep their placeholder.
3. Build the mod (or copy `bundles/`, `bundles.json` and `data/artifacts/artifacts.json` into the installed mod) and restart the server.

## Troubleshooting (from the guide)

| Problem | Fix |
|---|---|
| Purple model | The shader is missing: re-assign Bumped Specular SMap and its textures, rebuild |
| Preview/icon off-centre | Re-run the prep script (it centres the origin) or move the PreviewPivot position by hand |
| Item falls through the ground | The Mesh Collider is missing or on the wrong object (it belongs on the child mesh) |
| Wrong size | Re-run the prep script with a different `--size`, then rebuild |
| Parts black or see-through from one side | Thin single-sided surfaces: re-run the prep script with `--solidify 0.0015` |
| Placeholder still shows | Check `bundles.json` lists the bundle and `prefabPath` matches its file name exactly |
