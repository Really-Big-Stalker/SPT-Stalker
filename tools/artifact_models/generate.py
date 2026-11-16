#!/usr/bin/env python3
"""
Procedural 3D models for the SPT-Stalker artifacts.

Writes one binary glTF (.glb) per artifact plus a preview sheet. Needs only numpy + Pillow.

    python3 tools/artifact_models/generate.py            # -> art/artifacts/models/*.glb, art/artifacts/previews/*.png

Models are in metres (about 6-11 cm across), Y up, centred on the origin, with smooth normals,
a vertex-colour gradient and a PBR material (base colour, emissive glow, optional transparency).
Import them into Unity (glTFast / UnityGLTF) or Blender to build the game asset bundles; see
docs/ARTIFACT_MODELS.md.
"""
import json
import math
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT_MODELS = ROOT / "art" / "artifacts" / "models"
OUT_PREVIEWS = ROOT / "art" / "artifacts" / "previews"

rng = np.random.default_rng(1986)


# ----------------------------------------------------------------------------- mesh basics

class Mesh:
    def __init__(self, v, f, c=None):
        self.v = np.asarray(v, dtype=np.float64)
        self.f = np.asarray(f, dtype=np.int64)
        self.c = np.ones((len(self.v), 3)) if c is None else np.asarray(c, dtype=np.float64)

    def merge(self, other):
        return Mesh(np.vstack([self.v, other.v]), np.vstack([self.f, other.f + len(self.v)]), np.vstack([self.c, other.c]))

    def normals(self):
        n = np.zeros_like(self.v)
        tri = self.v[self.f]
        fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        for i in range(3):
            np.add.at(n, self.f[:, i], fn)
        length = np.linalg.norm(n, axis=1, keepdims=True)
        return n / np.maximum(length, 1e-12)


def icosphere(subdiv=3):
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    v = [np.array(p, float) / np.linalg.norm(p) for p in v]
    for _ in range(subdiv):
        cache, nf = {}, []

        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                m = v[a] + v[b]
                v.append(m / np.linalg.norm(m))
                cache[key] = len(v) - 1
            return cache[key]

        for a, b, c in f:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    return Mesh(np.array(v), np.array(f))


def lathe(profile, segments=48):
    """Revolve [(radius, y), ...] around the Y axis."""
    profile = np.asarray(profile, float)
    n = len(profile)
    ang = np.linspace(0, 2 * np.pi, segments, endpoint=False)
    v = np.array([(r * math.cos(a), y, r * math.sin(a)) for a in ang for r, y in profile])
    f = []
    for s in range(segments):
        s2 = (s + 1) % segments
        for i in range(n - 1):
            a, b, c, d = s * n + i, s * n + i + 1, s2 * n + i, s2 * n + i + 1
            f += [(a, c, b), (b, c, d)]
    return Mesh(v, f)


def cone(base_center, tip, radius, segments=10):
    axis = tip - base_center
    length = np.linalg.norm(axis)
    m = lathe([(radius, 0.0), (radius * 0.55, length * 0.5), (0.0, length)], segments)
    return place(m, base_center, axis / length)


def tube(points, radius, segments=8):
    """Simple swept tube along a polyline (used for tentacles / filaments / petals)."""
    points = np.asarray(points, float)
    rings = []
    for i, p in enumerate(points):
        d = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        d /= np.linalg.norm(d) + 1e-12
        a = np.cross(d, [0, 1, 0] if abs(d[1]) < 0.9 else [1, 0, 0])
        a /= np.linalg.norm(a)
        b = np.cross(d, a)
        r = radius(i / (len(points) - 1)) if callable(radius) else radius
        rings.append([p + r * (math.cos(t) * a + math.sin(t) * b) for t in np.linspace(0, 2 * np.pi, segments, endpoint=False)])
    v = np.array(rings).reshape(-1, 3)
    f = []
    for i in range(len(points) - 1):
        for s in range(segments):
            s2 = (s + 1) % segments
            a, b, c, d = i * segments + s, i * segments + s2, (i + 1) * segments + s, (i + 1) * segments + s2
            f += [(a, c, b), (b, c, d)]
    return Mesh(v, f)


def torus(R, r, seg=48, rseg=16):
    v, f = [], []
    for i in range(seg):
        u = 2 * np.pi * i / seg
        for j in range(rseg):
            w = 2 * np.pi * j / rseg
            v.append(((R + r * math.cos(w)) * math.cos(u), r * math.sin(w), (R + r * math.cos(w)) * math.sin(u)))
    for i in range(seg):
        for j in range(rseg):
            a, b = i * rseg + j, i * rseg + (j + 1) % rseg
            c, d = ((i + 1) % seg) * rseg + j, ((i + 1) % seg) * rseg + (j + 1) % rseg
            f += [(a, b, c), (b, d, c)]
    return Mesh(v, f)


def place(mesh, origin, direction):
    """Rotate a Y-up mesh so +Y points along `direction`, then move it to `origin`."""
    y = np.array([0.0, 1.0, 0.0])
    d = direction / np.linalg.norm(direction)
    axis = np.cross(y, d)
    s, c = np.linalg.norm(axis), float(np.dot(y, d))
    if s < 1e-9:
        rot = np.eye(3) if c > 0 else np.diag([1, -1, -1])
    else:
        k = axis / s
        K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
        rot = np.eye(3) + s * K + (1 - c) * K @ K
    return Mesh(mesh.v @ rot.T + origin, mesh.f, mesh.c)


def noise3(p, freq=3.0, octaves=3, seed=0):
    """Cheap smooth value noise from summed sines (deterministic, no extra libraries)."""
    r = np.random.default_rng(seed)
    out = np.zeros(len(p))
    amp = 1.0
    for _ in range(octaves):
        dirs = r.normal(size=(6, 3))
        phases = r.uniform(0, 2 * np.pi, 6)
        for d, ph in zip(dirs, phases):
            out += amp * np.sin(freq * p @ (d / np.linalg.norm(d)) + ph) / 6
        freq *= 2.1
        amp *= 0.5
    return out


def gradient(mesh, top, bottom, axis=1):
    y = mesh.v[:, axis]
    t = ((y - y.min()) / max(np.ptp(y), 1e-9))[:, None]
    mesh.c = np.asarray(bottom) * (1 - t) + np.asarray(top) * t
    return mesh


def scale_to(mesh, size):
    ext = np.ptp(mesh.v, axis=0).max()
    mesh.v = (mesh.v - (mesh.v.max(0) + mesh.v.min(0)) / 2) * (size / ext)
    return mesh


# ----------------------------------------------------------------------------- artifacts

def jellyfish():
    m = icosphere(4)
    v = m.v.copy()
    y = v[:, 1]
    # flattened bell with a rippled rim, hollowed underside
    v[:, 1] = np.where(y > -0.1, y * 0.75, -0.1 * 0.75 + (y + 0.1) * 0.25)
    ang = np.arctan2(v[:, 2], v[:, 0])
    rim = np.exp(-((y + 0.1) / 0.25) ** 2)
    v[:, [0, 2]] *= (1 + 0.08 * np.sin(8 * ang) * rim)[:, None]
    v += m.v * (0.04 * noise3(m.v, 4, seed=1))[:, None]
    bell = gradient(Mesh(v, m.f), (0.85, 0.75, 1.0), (0.45, 0.35, 0.85))
    for k in range(8):
        a = 2 * np.pi * k / 8 + 0.2
        pts = [(0.7 * math.cos(a) * (1 - 0.15 * t), -0.1 - 1.3 * t, 0.7 * math.sin(a) * (1 - 0.15 * t)) for t in np.linspace(0, 1, 14)]
        pts = [(x + 0.08 * math.sin(6 * t + k), yy, z + 0.08 * math.cos(5 * t + k)) for (x, yy, z), t in zip(pts, np.linspace(0, 1, 14))]
        tent = tube(pts, lambda t: 0.06 * (1 - t) + 0.01)
        tent.c = np.tile((0.55, 0.45, 0.95), (len(tent.v), 1))
        bell = bell.merge(tent)
    return scale_to(bell, 0.09), dict(color=(0.75, 0.65, 1.0, 0.55), emissive=(0.35, 0.25, 0.7), roughness=0.15, blend=True)


def stone_blood():
    m = icosphere(4)
    d = 1 + 0.28 * noise3(m.v, 2.2, 4, seed=2) + 0.06 * noise3(m.v, 9, 2, seed=3)
    m.v = m.v * d[:, None] * np.array([1.15, 0.8, 1.0])
    t = (d - d.min()) / np.ptp(d)
    m.c = np.array((0.35, 0.05, 0.04)) * (1 - t[:, None]) + np.array((0.65, 0.22, 0.12)) * t[:, None]
    return scale_to(m, 0.08), dict(color=(1, 1, 1, 1), emissive=(0.12, 0.0, 0.0), roughness=0.85)


def sparkler():
    core = icosphere(2)
    core.v *= 0.18
    mesh = gradient(core, (1, 0.95, 0.6), (1, 0.8, 0.3))
    dirs = rng.normal(size=(26, 3))
    for d in dirs:
        d /= np.linalg.norm(d)
        pts = [d * (0.15 + 0.85 * t) + 0.12 * np.sin(np.array([7, 5, 9]) * t + d * 4) * t for t in np.linspace(0, 1, 10)]
        fil = tube(pts, lambda t: 0.03 * (1 - t) + 0.006, 6)
        fil.c = np.tile((1.0, 0.85, 0.35), (len(fil.v), 1))
        mesh = mesh.merge(fil)
    return scale_to(mesh, 0.08), dict(color=(1, 1, 1, 1), emissive=(1.0, 0.8, 0.25), roughness=0.3, metallic=0.6)


def slime():
    m = icosphere(4)
    blobs = [(0, 0, 0, 1.0), (0.6, -0.15, 0.25, 0.55), (-0.5, -0.2, -0.35, 0.5), (0.15, 0.5, -0.25, 0.45), (-0.3, 0.1, 0.55, 0.4)]
    v = m.v.copy()
    r = np.zeros(len(v))
    for x, y, z, sz in blobs:
        r += sz * np.exp(-np.sum((v - np.array([x, y, z])) ** 2, 1) * 1.8)
    m.v = v * (0.55 + 0.55 * r + 0.05 * noise3(v, 6, seed=9))[:, None] * np.array([1.0, 0.75, 1.0])
    m.v[:, 1] = np.maximum(m.v[:, 1], -0.5)
    gradient(m, (0.65, 0.98, 0.35), (0.12, 0.42, 0.08))
    mesh = m
    for k in range(5):  # drips hanging off the sides
        a = 2 * np.pi * k / 5 + 0.4
        x, z = 0.75 * math.cos(a), 0.75 * math.sin(a)
        pts = [(x * (1 - 0.1 * t), -0.25 - 0.55 * t * (0.6 + 0.4 * (k % 2)), z * (1 - 0.1 * t)) for t in np.linspace(0, 1, 8)]
        drip = tube(pts, lambda t: 0.12 * (1 - t) ** 0.5 * (1 - 0.6 * t) + 0.05 * math.exp(-((t - 0.95) / 0.08) ** 2), 10)
        drip.c = np.tile((0.2, 0.6, 0.12), (len(drip.v), 1))
        mesh = mesh.merge(drip)
    return scale_to(mesh, 0.085), dict(color=(1, 1, 1, 0.8), emissive=(0.12, 0.3, 0.05), roughness=0.05, blend=True)


def moonlight():
    m = icosphere(4)
    lat = np.arcsin(np.clip(m.v[:, 1], -1, 1))
    m.v *= (1 - 0.035 * (np.cos(lat * 14) > 0.85))[:, None]  # engraved bands
    m.c = np.tile((0.8, 0.9, 1.0), (len(m.v), 1))
    ring = torus(1.12, 0.035)
    ring.v = ring.v @ np.array([[1, 0, 0], [0, math.cos(0.5), -math.sin(0.5)], [0, math.sin(0.5), math.cos(0.5)]]).T
    ring.c = np.tile((0.6, 0.75, 1.0), (len(ring.v), 1))
    return scale_to(m.merge(ring), 0.07), dict(color=(1, 1, 1, 1), emissive=(0.45, 0.6, 1.0), roughness=0.2)


def flash():
    mesh = None
    crystals = [(0, 1, 0, 1.15)] + [(math.cos(a), 1.3, math.sin(a), rng.uniform(0.6, 0.9)) for a in np.linspace(0, 2 * np.pi, 7, endpoint=False)]
    for x, y, z, length in crystals:
        d = np.array([x, y, z], float)
        d /= np.linalg.norm(d)
        c = lathe([(0.0, -0.05), (0.13, 0.0), (0.13, length * 0.78), (0.0, length)], 6)
        c = place(c, np.zeros(3), d)
        gradient(c, (0.98, 1.0, 0.8), (0.7, 0.85, 1.0))
        mesh = c if mesh is None else mesh.merge(c)
    base = icosphere(3)
    base.v *= (np.array([0.42, 0.18, 0.42]) * (1 + 0.15 * noise3(base.v, 5, seed=8))[:, None])
    base.c = np.tile((0.35, 0.38, 0.42), (len(base.v), 1))
    return scale_to(mesh.merge(base), 0.09), dict(color=(1, 1, 1, 0.8), emissive=(0.85, 0.9, 0.5), roughness=0.05, blend=True)


def thorn():
    core = icosphere(3)
    core.v *= (0.45 + 0.05 * noise3(core.v, 5, seed=4))[:, None]
    mesh = gradient(core, (0.45, 0.32, 0.18), (0.25, 0.16, 0.08))
    sphere_dirs = icosphere(1).v
    for d in sphere_dirs:
        d = d + rng.normal(scale=0.12, size=3)
        d /= np.linalg.norm(d)
        spine = cone(d * 0.4, d * rng.uniform(0.95, 1.25), 0.07, 8)
        gradient(spine, (0.85, 0.75, 0.5), (0.35, 0.22, 0.1))
        mesh = mesh.merge(spine)
    return scale_to(mesh, 0.08), dict(color=(1, 1, 1, 1), emissive=(0.05, 0.02, 0.0), roughness=0.7)


def fireball():
    m = icosphere(4)
    m.v[:, 1] *= np.where(m.v[:, 1] > 0, 1.25, 1.0)  # egg
    n = noise3(m.v, 3, 3, seed=5)
    m.v *= (1 + 0.07 * n)[:, None]
    t = ((n - n.min()) / np.ptp(n))[:, None]
    m.c = np.array((0.35, 0.08, 0.02)) * (1 - t) + np.array((1.0, 0.55, 0.1)) * t  # glowing cracks
    return scale_to(m, 0.075), dict(color=(1, 1, 1, 1), emissive=(1.0, 0.35, 0.05), roughness=0.6)


def night_star():
    m = icosphere(3)
    v = m.v
    # 8 star points along cube diagonals
    tips = np.array([(x, y, z) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]) / math.sqrt(3)
    spike = np.max(np.clip(v @ tips.T, 0, 1) ** 12, axis=1)
    m.v = v * (0.55 + 0.75 * spike)[:, None]
    m.c = np.array((0.25, 0.35, 0.8)) * (1 - spike[:, None]) + np.array((0.85, 0.95, 1.0)) * spike[:, None]
    return scale_to(m, 0.08), dict(color=(1, 1, 1, 1), emissive=(0.3, 0.45, 1.0), roughness=0.25, metallic=0.3)


def battery():
    profile = [(0.0, -1.0), (0.42, -1.0), (0.48, -0.94)]
    for i, y in enumerate(np.linspace(-0.9, 0.9, 9)):
        r = 0.5 if i % 2 == 0 else 0.44  # cell ridges
        profile += [(r, y - 0.07), (r, y + 0.07)]
    profile += [(0.48, 0.94), (0.42, 1.0), (0.18, 1.0), (0.16, 1.18), (0.0, 1.18)]
    m = lathe(profile, 40)
    n = noise3(m.v, 6, 2, seed=6)
    m.v[:, [0, 2]] *= (1 + 0.04 * n)[:, None]
    gradient(m, (0.85, 0.85, 0.35), (0.35, 0.4, 0.2))
    return scale_to(m, 0.085), dict(color=(1, 1, 1, 1), emissive=(0.4, 0.45, 0.05), roughness=0.35, metallic=0.5)


def soul():
    mesh = None
    for k in range(7):
        a = 2 * np.pi * k / 7
        pts = []
        for t in np.linspace(0, 1, 16):
            r = 0.15 + 0.75 * math.sin(t * math.pi * 0.85)
            tw = a + 1.6 * t
            pts.append((r * math.cos(tw), -0.4 + 1.1 * t, r * math.sin(tw)))
        petal = tube(pts, lambda t: 0.13 * math.sin(math.pi * min(t * 1.1, 1)) + 0.015, 10)
        gradient(petal, (1.0, 0.75, 0.95), (0.55, 0.15, 0.45))
        mesh = petal if mesh is None else mesh.merge(petal)
    heart = icosphere(3)
    heart.v *= 0.22
    heart.v[:, 1] += 0.05
    heart.c = np.tile((1.0, 0.85, 0.6), (len(heart.v), 1))
    return scale_to(mesh.merge(heart), 0.09), dict(color=(1, 1, 1, 0.85), emissive=(0.7, 0.3, 0.6), roughness=0.3, blend=True)


def compass():
    ring = torus(1.0, 0.12, 64, 16)
    ring.c = np.tile((0.75, 0.6, 0.3), (len(ring.v), 1))
    ring2 = torus(0.78, 0.05, 64, 10)
    ring2.v = ring2.v[:, [1, 0, 2]]
    ring2.c = np.tile((0.6, 0.5, 0.3), (len(ring2.v), 1))
    core = icosphere(3)
    core.v *= 0.28
    core.c = np.tile((0.4, 1.0, 0.85), (len(core.v), 1))
    needle_a = cone(np.array([0, 0, 0.0]), np.array([0.0, 0.0, 0.85]), 0.09, 6)
    needle_a.c = np.tile((0.9, 0.2, 0.15), (len(needle_a.v), 1))
    needle_b = cone(np.array([0, 0, 0.0]), np.array([0.0, 0.0, -0.85]), 0.09, 6)
    needle_b.c = np.tile((0.85, 0.85, 0.9), (len(needle_b.v), 1))
    mesh = ring.merge(ring2).merge(core).merge(needle_a).merge(needle_b)
    for k in range(4):
        a = 2 * np.pi * k / 4 + np.pi / 4
        d = np.array([math.cos(a), 0, math.sin(a)])
        s = cone(d * 1.08, d * 1.4, 0.07, 6)
        s.c = np.tile((0.75, 0.6, 0.3), (len(s.v), 1))
        mesh = mesh.merge(s)
    mesh.v = mesh.v @ np.array([[1, 0, 0], [0, math.cos(1.1), -math.sin(1.1)], [0, math.sin(1.1), math.cos(1.1)]]).T
    return scale_to(mesh, 0.10), dict(color=(1, 1, 1, 1), emissive=(0.15, 0.55, 0.45), roughness=0.25, metallic=0.75)


BUILDERS = {
    "jellyfish": jellyfish, "stone_blood": stone_blood, "sparkler": sparkler, "slime": slime,
    "moonlight": moonlight, "flash": flash, "thorn": thorn, "fireball": fireball,
    "night_star": night_star, "battery": battery, "soul": soul, "compass": compass,
}


# ----------------------------------------------------------------------------- GLB writer

def write_glb(path, mesh, mat, name):
    pos = mesh.v.astype(np.float32)
    nrm = mesh.normals().astype(np.float32)
    col = np.hstack([np.clip(mesh.c, 0, 1), np.ones((len(mesh.c), 1))]).astype(np.float32)
    idx = mesh.f.astype(np.uint32).ravel()

    blobs, views, accessors = [], [], []
    offset = 0

    def add(arr, target, comp_type, typ, minmax=False):
        nonlocal offset
        data = arr.tobytes()
        pad = (-len(data)) % 4
        blobs.append(data + b"\0" * pad)
        views.append({"buffer": 0, "byteOffset": offset, "byteLength": len(data), "target": target})
        acc = {"bufferView": len(views) - 1, "componentType": comp_type, "count": len(arr), "type": typ}
        if minmax:
            acc["min"], acc["max"] = arr.min(0).tolist(), arr.max(0).tolist()
        accessors.append(acc)
        offset += len(data) + pad
        return len(accessors) - 1

    a_pos = add(pos, 34962, 5126, "VEC3", True)
    a_nrm = add(nrm, 34962, 5126, "VEC3")
    a_col = add(col, 34962, 5126, "VEC4")
    a_idx = add(idx, 34963, 5125, "SCALAR")

    material = {
        "name": f"{name}_mat",
        "pbrMetallicRoughness": {
            "baseColorFactor": list(mat.get("color", (1, 1, 1, 1))),
            "metallicFactor": mat.get("metallic", 0.0),
            "roughnessFactor": mat.get("roughness", 0.5),
        },
        "emissiveFactor": list(mat.get("emissive", (0, 0, 0))),
        "doubleSided": True,
    }
    if mat.get("blend"):
        material["alphaMode"] = "BLEND"

    gltf = {
        "asset": {"version": "2.0", "generator": "SPT-Stalker artifact generator"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": name}],
        "meshes": [{"name": name, "primitives": [{"attributes": {"POSITION": a_pos, "NORMAL": a_nrm, "COLOR_0": a_col},
                                                   "indices": a_idx, "material": 0}]}],
        "materials": [material],
        "accessors": accessors,
        "bufferViews": views,
        "buffers": [{"byteLength": offset}],
    }
    js = json.dumps(gltf, separators=(",", ":")).encode()
    js += b" " * ((-len(js)) % 4)
    binary = b"".join(blobs)
    with open(path, "wb") as fh:
        fh.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binary)))
        fh.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        fh.write(struct.pack("<II", len(binary), 0x004E4942) + binary)


# ----------------------------------------------------------------------------- preview renderer

def render(mesh, mat, size=320, bg=(28, 30, 26)):
    """Tiny painter's-algorithm renderer: rotate, shade per face, draw back to front."""
    v = mesh.v - (mesh.v.max(0) + mesh.v.min(0)) / 2
    v = v / (2 * np.linalg.norm(v, axis=1).max())
    ry, rx = math.radians(35), math.radians(-22)
    rot = np.array([[math.cos(ry), 0, math.sin(ry)], [0, 1, 0], [-math.sin(ry), 0, math.cos(ry)]])
    rot = np.array([[1, 0, 0], [0, math.cos(rx), -math.sin(rx)], [0, math.sin(rx), math.cos(rx)]]) @ rot
    v = v @ rot.T
    n = mesh.normals() @ rot.T
    tri = v[mesh.f]
    fn = n[mesh.f].mean(1)
    fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
    light = np.array([-0.4, 0.7, 0.6])
    light /= np.linalg.norm(light)
    diffuse = np.clip(fn @ light, 0, 1)
    spec = np.clip(fn @ (light + np.array([0, 0, 1])) / np.linalg.norm(light + np.array([0, 0, 1])), 0, 1) ** 24
    base = mesh.c[mesh.f].mean(1) * np.array(mat.get("color", (1, 1, 1, 1))[:3])
    emissive = np.array(mat.get("emissive", (0, 0, 0)))
    shade = base * (0.25 + 0.75 * diffuse[:, None]) + emissive * 0.45 + spec[:, None] * (1 - mat.get("roughness", 0.5)) * 0.8
    alpha = mat.get("color", (1, 1, 1, 1))[3]

    img = Image.new("RGB", (size, size), bg)
    # soft glow behind emissive artifacts
    glow = Image.new("RGB", (size, size), (0, 0, 0))
    gd = ImageDraw.Draw(glow)
    e = np.clip(emissive * 160, 0, 255).astype(int)
    for r in range(size // 2, 0, -6):
        k = (1 - r / (size / 2)) ** 2
        gd.ellipse([size / 2 - r, size / 2 - r, size / 2 + r, size / 2 + r], fill=tuple((e * k).astype(int)))
    img = Image.blend(img, glow, 0.6)

    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    order = np.argsort(tri[:, :, 2].mean(1))
    scale = size * 0.92
    for i in order:
        if alpha >= 0.99 and fn[i, 2] < -0.05:
            continue
        pts = [(size / 2 + p[0] * scale, size / 2 - p[1] * scale) for p in tri[i]]
        col = tuple(int(x) for x in np.clip(shade[i] * 255, 0, 255))
        a = int(255 * (alpha if alpha < 0.99 else 1))
        draw.polygon(pts, fill=col + (a,))
    img.paste(layer, (0, 0), layer)
    return img


# ----------------------------------------------------------------------------- main

def main():
    OUT_MODELS.mkdir(parents=True, exist_ok=True)
    OUT_PREVIEWS.mkdir(parents=True, exist_ok=True)
    artifacts = json.loads((ROOT / "src/SPTStalker/data/artifacts/artifacts.json").read_text(encoding="utf-8"))["artifacts"]
    names = {a["key"]: a["locales"]["en"]["name"] for a in artifacts}

    tiles = []
    for key, build in BUILDERS.items():
        mesh, mat = build()
        write_glb(OUT_MODELS / f"{key}.glb", mesh, mat, key)
        img = render(mesh, mat)
        img.save(OUT_PREVIEWS / f"{key}.png")
        tiles.append((names.get(key, key), img))
        print(f"{key:12s} {len(mesh.v):6d} verts {len(mesh.f):6d} tris  size {np.ptp(mesh.v, axis=0).round(3)} m")

    cols, tile = 4, 320
    sheet = Image.new("RGB", (cols * tile, math.ceil(len(tiles) / cols) * (tile + 34)), (18, 19, 15))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 20)
    except OSError:
        font = ImageFont.load_default()
    for i, (name, img) in enumerate(tiles):
        x, y = (i % cols) * tile, (i // cols) * (tile + 34)
        sheet.paste(img, (x, y))
        draw.text((x + 12, y + tile + 6), name, fill=(214, 170, 40), font=font)
    sheet.save(OUT_PREVIEWS / "_sheet.png")
    missing = set(names) - set(BUILDERS)
    if missing:
        print("No model builder yet for:", ", ".join(sorted(missing)), file=sys.stderr)


if __name__ == "__main__":
    main()
