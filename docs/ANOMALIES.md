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

## Client settings (BepInEx config / F1 menu)

`BepInEx/config/com.sptstalker.client.cfg`: enable/disable, **damage multiplier**, hum volume, **detector**
(on/off, range, volume), and **Show markers**, which draws a sphere for every anomaly. That's handy for checking a
layout the first time.

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
