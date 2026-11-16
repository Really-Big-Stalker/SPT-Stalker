"""
Make a 3D model ready for the EFT SDK (Unity), following the WTT "Intro to Static Objects" guide's
Blender steps, plus texture baking so the model works with Tarkov's "Bumped Specular Smap" shader.

Works on any model: the generated artifact .glb files, or models you make/buy (.glb/.gltf/.fbx/.obj/.blend).

    blender -b -P tools/blender/prepare_for_tarkov.py -- --input art/artifacts/models/jellyfish.glb --name jellyfish --size 0.09
    # or with the bpy Python module:  python prepare_for_tarkov.py --input ... --name ...

What it does:
  1. Imports the model into an empty scene, drops cameras/lights/armatures, joins all meshes into one object.
  2. Scales it so its longest side is --size metres (skip with --size 0 to keep the model's own scale),
     sets origin to geometry, applies rotation & scale, and centres it at 0,0,0.
  3. Reduces it to --max-tris triangles if it is heavier (guide: try to stay under ~10k).
  4. Unwraps UVs (Smart UV Project) and bakes ONE diffuse texture (base colour + vertex colour + emission glow)
     and, if the source has normal maps, ONE normal map. Tarkov's specular shader expects the look baked into
     the diffuse texture, and one material per item keeps it simple.
  5. Writes <out>/<name>.fbx, <out>/<name>.blend, <out>/<name>_diffuse.png, [<name>_normal.png], <name>_preview.png.

Output goes to --out (default: art/artifacts/unity/<name>/). Drag the .fbx (or .blend) into the SDK's Assets
folder and carry on from the guide's "Import into Unity" step, or use tools/unity/SptStalkerItemPrefabs.cs.
"""
import argparse
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--input", required=True)
    p.add_argument("--name", required=True, help="base name for the output files, e.g. jellyfish")
    p.add_argument("--size", type=float, default=0.0, help="longest side in metres (0 = keep)")
    p.add_argument("--max-tris", type=int, default=10000)
    p.add_argument("--texture-size", type=int, default=1024)
    p.add_argument("--out", default=None)
    p.add_argument("--samples", type=int, default=16, help="Cycles bake samples")
    p.add_argument("--solidify", type=float, default=0.0,
                   help="give thin open sheets this thickness in metres (e.g. 0.0015) so they render from both "
                        "sides with Tarkov's single-sided shaders; also points all faces outwards")
    return p.parse_args(argv)


def log(msg):
    print(f"[prepare_for_tarkov] {msg}", flush=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def repair_glb(path):
    """
    Fix common glTF export mistakes that make Blender's importer crash (seen in AI/trimesh exports):
    textures pointing at samplers that don't exist. Returns the path to import (a repaired temp copy if needed).
    """
    import json
    import struct
    import tempfile

    with open(path, "rb") as fh:
        data = fh.read()
    if data[:4] != b"glTF":
        return path
    json_len = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(data[20:20 + json_len])
    rest = data[20 + json_len:]

    samplers = gltf.get("samplers") or []
    fixed = []
    for i, tex in enumerate(gltf.get("textures", [])):
        if "sampler" in tex and tex["sampler"] >= len(samplers):
            del tex["sampler"]
            fixed.append(i)
    if not fixed:
        return path

    log(f"repaired glb: texture(s) {fixed} referenced missing samplers (now use the default sampler)")
    js = json.dumps(gltf, separators=(",", ":")).encode()
    js += b" " * ((-len(js)) % 4)
    out = os.path.join(tempfile.mkdtemp(), os.path.basename(path))
    with open(out, "wb") as fh:
        fh.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + len(rest)))
        fh.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        fh.write(rest)
    return out


def import_model(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".glb":
        bpy.ops.import_scene.gltf(filepath=repair_glb(path))
    elif ext == ".gltf":
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    elif ext == ".blend":
        with bpy.data.libraries.load(path, link=False) as (src, dst):
            dst.objects = src.objects
        for obj in dst.objects:
            if obj is not None:
                bpy.context.scene.collection.objects.link(obj)
    else:
        raise SystemExit(f"Unsupported file type: {ext}")


def single_mesh(name):
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            # keep the world transform of meshes parented to empties/armatures
            for child in obj.children:
                mw = child.matrix_world.copy()
                child.parent = None
                child.matrix_world = mw
            bpy.data.objects.remove(obj, do_unlink=True)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        raise SystemExit("No mesh found in the input file")

    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.modifiers.clear()
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()

    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = f"{name}_mesh"
    return obj


def normalise_transform(obj, size):
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if size > 0:
        longest = max(obj.dimensions)
        if longest > 0:
            obj.scale = (size / longest,) * 3
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Guide: Set Origin > Origin to Geometry, apply rotation & scale, Alt+G to centre
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    obj.location = (0, 0, 0)


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def solidify(obj, thickness):
    """Thicken open surfaces (one-sided sheets look see-through or black from one side in game)."""
    mod = obj.modifiers.new("solidify", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 0.0
    mod.use_even_offset = True
    mod.use_quality_normals = True
    mod.use_rim = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Every part is now closed: make all faces point outwards
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def reduce(obj, max_tris):
    tris = triangle_count(obj)
    if tris <= max_tris:
        return tris
    mod = obj.modifiers.new("decimate", "DECIMATE")
    mod.ratio = max_tris / tris
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return triangle_count(obj)


def has_image_textures(obj):
    return any(n.type == "TEX_IMAGE" and n.image for m in obj.data.materials if m and m.use_nodes for n in m.node_tree.nodes)


def has_normal_maps(obj):
    return any(n.type == "NORMAL_MAP" for m in obj.data.materials if m and m.use_nodes for n in m.node_tree.nodes)


def unwrap(obj):
    me = obj.data
    if len(me.uv_layers) == 0 or not has_image_textures(obj):
        # Fresh, non-overlapping layout for baking (keep the source UVs if the model is already textured)
        uv = me.uv_layers.new(name="bake")
        me.uv_layers.active = uv
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
        bpy.ops.object.mode_set(mode="OBJECT")
    else:
        # Bake into a separate clean layout, sampling from the original UVs
        src = me.uv_layers.active
        uv = me.uv_layers.new(name="bake")
        me.uv_layers.active = uv
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
        bpy.ops.object.mode_set(mode="OBJECT")
        for layer in me.uv_layers:
            layer.active_render = layer == src
    return uv


def principled_inputs(mat):
    """Base colour / emission of a material's Principled BSDF (glTF imports use one)."""
    base, emit, strength = (1, 1, 1, 1), (0, 0, 0, 1), 0.0
    if mat and mat.use_nodes:
        for n in mat.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                base = tuple(n.inputs["Base Color"].default_value)
                emit = tuple(n.inputs["Emission Color"].default_value)
                strength = n.inputs["Emission Strength"].default_value
    return base, emit, strength


def bake(obj, name, out_dir, size, samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.render.bake.margin = 8

    textured = has_image_textures(obj)
    want_normal = has_normal_maps(obj)
    color_attr = obj.data.color_attributes.active_color.name if obj.data.color_attributes else None

    diffuse = bpy.data.images.new(f"{name}_diffuse", size, size, alpha=False)
    normal = bpy.data.images.new(f"{name}_normal", size, size, alpha=False, is_data=True) if want_normal else None

    if textured:
        # Bake the existing materials' colour straight into the new atlas
        for mat in obj.data.materials:
            if not mat:
                continue
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = diffuse
            mat.node_tree.nodes.active = node
        scene.render.bake.use_pass_direct = False
        scene.render.bake.use_pass_indirect = False
        scene.render.bake.use_pass_color = True
        bpy.ops.object.bake(type="DIFFUSE")
        log("baked diffuse from source textures")
    else:
        # Untextured (e.g. generated artifacts): colour = base colour x vertex colour + a share of the glow,
        # baked through an emission-only material so lighting doesn't end up in the texture.
        for i, mat in enumerate(obj.data.materials or [None]):
            base, emit, strength = principled_inputs(mat)
            bake_mat = bpy.data.materials.new(f"{name}_bake_{i}")
            bake_mat.use_nodes = True
            nt = bake_mat.node_tree
            nt.nodes.clear()
            out = nt.nodes.new("ShaderNodeOutputMaterial")
            emission = nt.nodes.new("ShaderNodeEmission")
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs["Factor"].default_value = 1.0
            mix.inputs[6].default_value = base
            if color_attr:
                attr = nt.nodes.new("ShaderNodeVertexColor")
                attr.layer_name = color_attr
                nt.links.new(attr.outputs["Color"], mix.inputs[7])
            else:
                mix.inputs[7].default_value = (1, 1, 1, 1)
            glow = nt.nodes.new("ShaderNodeMix")
            glow.data_type = "RGBA"
            glow.blend_type = "ADD"
            glow.inputs["Factor"].default_value = min(1.0, 0.35 * max(strength, 1.0))
            nt.links.new(mix.outputs[2], glow.inputs[6])
            glow.inputs[7].default_value = emit
            nt.links.new(glow.outputs[2], emission.inputs["Color"])
            nt.links.new(emission.outputs["Emission"], out.inputs["Surface"])
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = diffuse
            nt.nodes.active = tex
            if mat is None:
                obj.data.materials.append(bake_mat)
            else:
                obj.data.materials[i] = bake_mat
        bpy.ops.object.bake(type="EMIT")
        log("baked diffuse from base/vertex colours")

    if normal is not None:
        for mat in obj.data.materials:
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = normal
            mat.node_tree.nodes.active = node
        bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
        log("baked normal map")

    paths = {}
    for img, suffix in ((diffuse, "diffuse"), (normal, "normal")):
        if img is None:
            continue
        path = os.path.join(out_dir, f"{name}_{suffix}.png")
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        paths[suffix] = path
    return paths


def final_material(obj, name, textures):
    """One simple material using the baked textures (what Unity will pick up from the FBX)."""
    mat = bpy.data.materials.new(f"{name}_mat")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.6
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(textures["diffuse"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "normal" in textures:
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = bpy.data.images.load(textures["normal"])
        ntex.image.colorspace_settings.name = "Non-Color"
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

    obj.data.materials.clear()
    obj.data.materials.append(mat)

    # Keep only the bake UVs so Unity uses them
    me = obj.data
    # Remove by name, one at a time (references go stale after each removal in Blender 4+/5)
    for layer_name in [l.name for l in me.uv_layers if l.name != "bake"]:
        layer = me.uv_layers.get(layer_name)
        if layer is not None:
            me.uv_layers.remove(layer)
    me.uv_layers["bake"].name = "UVMap"
    for attr in list(me.color_attributes):
        me.color_attributes.remove(attr)


def preview(obj, path):
    """Small Cycles render (CPU) so you can check the bake without opening Blender."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.film_transparent = True
    world = bpy.data.worlds.new("preview_world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.35, 0.38, 1)
    scene.world = world
    sun = bpy.data.objects.new("preview_sun", bpy.data.lights.new("preview_sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(sun)
    radius = max(obj.dimensions) * 0.5
    cam = bpy.data.objects.new("preview_cam", bpy.data.cameras.new("preview_cam"))
    scene.collection.objects.link(cam)
    direction = Vector((0.8, -1.0, 0.6)).normalized()
    cam.location = direction * radius * 4.2
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o in (cam, sun):
        bpy.data.objects.remove(o, do_unlink=True)


def main():
    args = parse_args()
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out_dir = os.path.abspath(args.out or os.path.join(root, "art", "artifacts", "unity", args.name))
    os.makedirs(out_dir, exist_ok=True)

    reset_scene()
    import_model(os.path.abspath(args.input))
    obj = single_mesh(args.name)
    normalise_transform(obj, args.size)
    if args.solidify > 0:
        tris = triangle_count(obj)
        solidify(obj, args.solidify)
        log(f"solidified {args.solidify * 1000:.1f} mm: {tris} -> {triangle_count(obj)} triangles")
    before = triangle_count(obj)
    after = reduce(obj, args.max_tris)
    log(f"{args.name}: {before} -> {after} triangles, size {tuple(round(d, 3) for d in obj.dimensions)} m")

    unwrap(obj)
    textures = bake(obj, args.name, out_dir, args.texture_size, args.samples)
    final_material(obj, args.name, textures)

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    fbx = os.path.join(out_dir, f"{args.name}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", use_mesh_modifiers=True,
        path_mode="RELATIVE", embed_textures=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, f"{args.name}.blend"), compress=True)
    preview(obj, os.path.join(out_dir, f"{args.name}_preview.png"))
    log(f"wrote {out_dir}")


if __name__ == "__main__":
    main()
