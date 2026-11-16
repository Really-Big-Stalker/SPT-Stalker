# Anomalies

Every raid rolls a new set of anomaly fields. They're never in the same place twice, and some hold an artifact.

## How it works

```
raid starts ─► server (SPTStalker.dll)                          client (BepInEx plugin SPTStalker.Client.dll)
               builds the raid's location + loot                  waits for the raid's GameWorld + player
               └─ rolls anomaly fields for this map               └─ GET /sptstalker/anomalies
                  · positions from the map's loot points             · snaps each anomaly to the ground
                  · ≥ 40 m from player spawns, ≥ 60 m apart          · light + hum + damage zone
                  · maybe an artifact of the field's family          · detector beeps as you get close
                    placed at the centre as real loot
```

* **Server** (`src/SPTStalker/Anomalies/`): rolls the layout and places the artifacts. Without the client plugin, nothing
  is visible or harmful, but the artifacts still spawn at the field centres.
* **Client** (`client/SPTStalker.Client/`): makes the anomalies exist in the raid. It affects only **your** player for
  now (bots walk through them).

## Anomaly types (`data/anomalies/anomalies.json`)

| Type | Family (artifacts) | Effect while inside | Look / sound |
|---|---|---|---|
| **Burner** | thermal | burning damage over time (legs, stomach, chest) | orange flickering light, crackle |
| **Electro** | electro | discharge every ~1.8 s: damage + contusion | blue flashing light, sparking buzz |
| **Springboard** | gravitational | slam every ~2.5 s: leg damage, contusion, 25% fracture | faint violet glow, low thrum |
| **Vortex** | gravitational | constant damage everywhere + slams with stun, contusion, bleeding | deep purple, very low hum |
| **Fruit Punch** | chemical | acid damage to legs, 35% intoxication on entry | green glow |
| **Radiation** | (none) | radiation exposure over a large area | invisible: only the detector warns you |

Every number is editable: damage, intervals, body parts, effect chances, radius, light colour/intensity/flicker, hum
pitch. The family links a type to artifacts with the same `"anomaly"` in `artifacts.json` (rarer tiers are less likely).

## Radiation and gas masks

You now carry a **radiation dose** through each raid, shown in the health screen's **radiation meter** (`dose / 100`).
The meter is in the game's UI, but EFT never feeds it a value; the plugin does.

**Where it comes from**

| Source | Dose / second | Gas mask helps? |
|---|---|---|
| Radiation hotspot (inside / around it) | 2.5 / up to 0.6 | partly (`maskProtection` 0.6) |
| Vortex (inside / around) | 0.8 / up to 0.15 | no |
| Fruit Punch (inside / around) | 0.5 / up to 0.1 | **mostly** (0.85), and it also cuts the acid damage and intoxication |
| Burner, springboard, electro (inside) | 0.2–0.3 | burner a little (smoke) |
| **Carried artifacts** | tier 1→4: 0.02 / 0.04 / 0.08 / 0.15 each | no. Keep them in your **secure container** or the **pocket special slots**, which count as shielded |

"Around it" fades out to 3x the anomaly's radius (2x for hotspots), so you pick up dose just by walking through a field.

**Gas masks** (worn in the face-cover slot): **GP-7 90%**, **GP-5 80%**, Pestily plague mask 50%, respirator 35%.
A mask's protection is multiplied by each anomaly type's `maskProtection`. For example, a GP-5 in a Fruit Punch field blocks
80% x 85% = 68% of the acid damage over time, the radiation and the chance of intoxication. Physical hits (springboard
slams, electro discharges) are never blocked.

**Getting rid of it**: the body clears 0.01 / s by itself. **Vodka -30**, **moonshine -40**, whiskey -20, bad vodka -15,
Pevko Light beer -10.

**Special slots**: artifacts are allowed in the three pocket special slots (the server adds them to those slots' item
filter at startup), and anything there is shielded like the secure container. Set `artifactsFitSpecialSlots` to false to
turn this off; `shieldedSubSlots` lists the slot names.

## Armour vs physical anomalies

Bursts are physical hits: springboard slams, electro discharges, vortex slams. Armour softens them:

* **Body**: the highest armour class in your **armour vest or armoured rig** (plates and soft inserts count) protects every
  body part except the head, like a stalker suit.
* **Head**: the highest class in your **headwear** slot.
* Reduction is **10% per class, up to 60%** (`armor.reductionPerClass`, `armor.maxReduction`), multiplied by each type's
  `armorProtection`: **springboard 0.9**, **vortex 0.7**, **electro 0.4** (current gets through metal), burner / fruit
  punch / radiation 0 (no physical hit).
* It reduces the burst's damage, the length and strength of contusion and stun, and the chance of fractures and bleeding.

Example: a class 5 vest (50%) vs a springboard slam (x 0.9) absorbs 45%: 25 leg damage becomes ~14, and the fracture
chance drops from 25% to ~14%.

**What it does** (`stages`)

| Dose | Stage | Effect |
|---|---|---|
| 25 | Irradiated | 0.05 radiation damage / s |
| 50 | Radiation sickness | 0.15 / s + dizzy spells (contusion) every ~90 s |
| 75 | Severe | 0.4 / s + dizzy spells every ~40 s |
| 95 | Lethal dose | 1.5 / s + dizzy spells every ~20 s |

Each stage can also start the game's own "radiation exposure" status effect (`gameRadiationEffect`). It's off by
default because how long that effect lasts hasn't been tested yet.

A **Geiger counter** clicks faster the more radiation is around you, including from what you carry. Its volume is in
the F12 menu (4. Detector & sounds).

The dose resets at the start of every raid (for now).

## Per-map settings

`maps` holds, per map id (`bigmap`, `woods`, `shoreline`, `lighthouse`, `interchange`, `rezervbase`, `tarkovstreets`,
`sandbox`, `factory4_day`, `laboratory`, ...; `default` covers the rest):

| Setting | Meaning |
|---|---|
| `fields` | `[min, max]` anomaly fields per raid |
| `anomaliesPerField` | `[min, max]` anomalies in a field |
| `fieldRadius` | how far (m) a field's anomalies spread from its centre |
| `artifactChancePercent` | chance a field has an artifact in the middle |
| `typeWeights` | type key → relative chance on this map (e.g. Woods favours springboards, Reserve radiation) |

## In-game settings (F12)

Press **F12** in game (BepInEx Configuration Manager) and open **SPT-Stalker**. Changes apply immediately unless noted.

| Section | Settings |
|---|---|
| **0. Status** | live line: `Radiation 34/100 (Irradiated) · GP-5 gas mask (80%) · armour body 4 / head 3 · 14 anomalies` |
| **1. Anomalies** | on/off, anomaly damage multiplier |
| **2. Radiation** | on/off, radiation from anomalies, from carried artifacts, radiation sickness damage, natural recovery speed, alcohol strength |
| **3. Protection** | gas mask effectiveness, armour effectiveness |
| **4. Detector & sounds** | anomaly detector on/off, range, volume; Geiger counter volume; anomaly hum volume* |
| **5. Raid generation** | spawn anomalies, number of fields, anomalies per field, artifact chance in fields. **Sent to the server, apply from the next raid** |
| **6. Debug** | show anomaly markers* |

\* applies to anomalies built from the next raid on.

The same settings live in `BepInEx/config/com.sptstalker.client.cfg`. Everything else (per-map counts, per-type
damage, gas mask and cure lists, radiation stages) stays in the server's `data/anomalies/anomalies.json`.

## Install

The CI package contains both parts:

* `SPT/user/mods/SPTStalker/`: server mod (as before)
* `SPT/BepInEx/plugins/SPTStalker/SPTStalker.Client.dll`: client plugin

## Status / testing notes

The plugin reaches EFT through reflection, using names verified in SPT 4.1.2's assembly (`EFT.GameWorld.MainPlayer`,
`ActiveHealthController.ApplyDamage/DoContusion/DoStun/DoFracture/DoBleed/DoIntoxication`, `DamageInfo`). If a name
doesn't match your game version, the BepInEx log (`BepInEx/LogOutput.log`) says exactly which one, and nothing else breaks.

When you first test, check:
1. Server console at raid start: `[SPT-Stalker] <map>: N anomaly fields, M with artifacts`
2. BepInEx log: `SPT-Stalker: N anomaly fields (K anomalies) on <map>`
3. Turn on **Show markers**, walk to one, and listen for the detector.

## Ideas for later

* Bots react to / avoid anomalies (or die in them)
* Throwable bolts that trigger anomalies, like in the games
* Custom visual effects (distortion, particles) once there are asset bundles for them
* Emissions (blowouts) that reshuffle anomalies mid-raid
* Keep the radiation dose between raids (saved on the server)
* Gas mask filters that wear out, and a lead container item for artifacts
