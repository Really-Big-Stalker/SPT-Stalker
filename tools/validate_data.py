#!/usr/bin/env python3
"""Sanity-check the mod's JSON data before building. Usage: python3 tools/validate_data.py"""
import json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "src" / "SPTStalker" / "data"
HEX24 = re.compile(r"^[0-9a-f]{24}$")
errors = []

def load(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as e:
        errors.append(f"{path}: invalid JSON ({e})")
        return None

trader = ROOT / "trader"
base = load(trader / "base.json")
settings = load(trader / "settings.json")
assort = load(trader / "assort.json")

if base:
    if not HEX24.match(base.get("_id", "")):
        errors.append("base.json: _id must be a 24 character lowercase hex id")
    avatar = base.get("avatar", "")
    if not (avatar.startswith("/files/trader/avatar/") and avatar.endswith(".jpg")):
        errors.append("base.json: avatar must look like /files/trader/avatar/<name>.jpg")
    if len(base.get("loyaltyLevels", [])) != 4:
        errors.append("base.json: expected 4 loyaltyLevels")

if not (trader / "avatar.jpg").is_file():
    errors.append("trader/avatar.jpg is missing")

if settings:
    if settings.get("defaultLocale") not in settings.get("locales", {}):
        errors.append("settings.json: defaultLocale has no entry in locales")
    if settings.get("restockMinutesMin", 0) <= 0:
        errors.append("settings.json: restockMinutesMin must be > 0")

if assort:
    keys = set()
    for i, o in enumerate(assort.get("offers", [])):
        where = f"assort.json offer #{i} ({o.get('key')})"
        if not o.get("key"):
            errors.append(f"{where}: missing key")
        elif o["key"] in keys:
            errors.append(f"{where}: duplicate key")
        keys.add(o.get("key"))
        if not HEX24.match(o.get("tpl", "")):
            errors.append(f"{where}: tpl is not a 24 character hex id")
        if not 1 <= o.get("loyaltyLevel", 1) <= 4:
            errors.append(f"{where}: loyaltyLevel must be 1-4")
        has_price, has_barter = "price" in o, bool(o.get("barter"))
        if has_price == has_barter:
            errors.append(f"{where}: needs exactly one of 'price' or 'barter'")
        if has_price and o["price"].get("currency") not in ("RUB", "USD", "EUR"):
            errors.append(f"{where}: currency must be RUB, USD or EUR")
        for b in o.get("barter") or []:
            if not HEX24.match(b.get("tpl", "")) or b.get("count", 0) <= 0:
                errors.append(f"{where}: bad barter entry {b}")

# ---------- artifacts ----------
arts = load(ROOT / "artifacts" / "artifacts.json")
if arts:
    seen_keys, seen_ids = set(), set()
    for a in arts.get("artifacts", []):
        where = f"artifacts.json ({a.get('key')})"
        if not a.get("key") or a["key"] in seen_keys:
            errors.append(f"{where}: missing or duplicate key")
        seen_keys.add(a.get("key"))
        if not HEX24.match(a.get("id", "")) or a["id"] in seen_ids:
            errors.append(f"{where}: id must be a unique 24 character hex id")
        seen_ids.add(a.get("id"))
        if not HEX24.match(a.get("placeholderTpl", "")):
            errors.append(f"{where}: placeholderTpl must be a 24 character hex id")
        if a.get("rarity") not in arts.get("rarityWeights", {}):
            errors.append(f"{where}: rarity '{a.get('rarity')}' has no entry in rarityWeights")
        if not 1 <= a.get("tier", 0) <= 4:
            errors.append(f"{where}: tier must be 1-4")
        if not a.get("locales"):
            errors.append(f"{where}: needs at least one locale")
    for c, pct in arts.get("spawns", {}).get("containers", {}).items():
        if not HEX24.match(c) or not 0 <= pct < 90:
            errors.append(f"artifacts.json spawns: bad container entry {c}: {pct}")

# ---------- contracts ----------
con = load(ROOT / "contracts" / "contracts.json")
if con:
    mod_types = {"daytime", "bodyPart", "distance", "caliber", "weaponClass", "wearing", "survive"}
    mod_keys = {m.get("key") for m in con.get("modifiers", [])}
    for m in con.get("modifiers", []):
        if m.get("type") not in mod_types:
            errors.append(f"contracts.json modifier {m.get('key')}: unknown type {m.get('type')}")
        for x in m.get("excludes", []):
            if x not in mod_keys:
                errors.append(f"contracts.json modifier {m.get('key')}: excludes unknown modifier {x}")
    for f in con.get("factions", []):
        if not f.get("texts"):
            errors.append(f"contracts.json faction {f.get('key')}: needs at least one text")
        for x in f.get("blockedModifiers", []):
            if x not in mod_keys:
                errors.append(f"contracts.json faction {f.get('key')}: blocks unknown modifier {x}")
    for group, rng in {**con.get("hunts", {}).get("kills", {}), **con.get("artifactHunts", {}).get("count", {})}.items():
        if len(rng) != 2 or rng[0] < 1 or rng[0] > rng[1]:
            errors.append(f"contracts.json: bad [min, max] range for {group}: {rng}")

if errors:
    print("\n".join(errors))
    sys.exit(1)
print(f"OK: {len(assort['offers'])} offers, {len(arts['artifacts'])} artifacts, {len(con['factions'])} factions, {len(con['modifiers'])} modifiers")
