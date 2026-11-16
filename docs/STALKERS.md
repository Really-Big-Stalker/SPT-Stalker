# Loner stalkers

Roaming groups of neutral stalkers (2–4: a leader and followers) who wander the map hunting artifacts at this raid's
anomaly fields.

## Parts

| Part | Does | Needs |
|---|---|---|
| Server mod (`data/bots/stalkers.json`) | identity, gear, group spawns, hostility | nothing extra |
| `SPTStalker.Bots.dll` (BepInEx plugin) | the roaming behaviour (a BigBrain layer) | **[BigBrain](https://github.com/DrakiaXYZ/SPT-BigBrain) 1.5.0+**; **[Waypoints](https://github.com/DrakiaXYZ/SPT-Waypoints)** recommended |

Without the plugin (or BigBrain) the stalkers still spawn, dressed and named as stalkers, but behave like the game's
own Bloodhound AI. Waypoints adds extra navmesh, so they can reach much more of each map.

## Who they are

They use the game's **`arenaFighterEvent`** role (the Halloween "Bloodhounds"), which is unused outside events and built
for roaming groups. The server mod restyles it:

* **Names**: stalker nicknames (Wolf, Grach, Tolik, Sych, ...)
* **Gear**: ushankas, beanies, SSh-68, Kolpak; GP-5/GP-7 gas masks, respirators, balaclavas; 6B2/6B23/Module-3M armour;
  scav vests and chest rigs; duffle/VKBO/Pilgrim bags; AKS-74U, AK-74N, AKM, MP-133/153, sawn-off, TOZ, the odd SVDS;
  PM or PB sidearm. Attachments, plates and ammo come from the scav and raider bot data, so every item is built normally.
* **Hostility**: **neutral** to player PMCs and player scavs by default (they defend themselves if attacked); enemies of
  scavs (bandits), cultists, raiders, rogues, PMC bots and infected. PMC bots are already hostile to this role in SPT.

## Spawns

One `BossLocationSpawn` entry per possible group, spread over the map's open zones, at a random time:

| Map | Groups x chance |
|---|---|
| Woods | 2 x 60% |
| Customs, Shoreline | 2 x 55% |
| Lighthouse | 1 x 50% |
| Reserve | 1 x 45% |
| Interchange, Streets, Ground Zero | 1 x 40% |
| Factory, Labs, Labyrinth | none |

## Behaviour (plugin)

While a stalker has no enemy, isn't under fire or healing, and has been calm for 30 s:

* **Leader**: picks a destination. 70% of the time it's the edge of one of this raid's **anomaly fields** (they stop
  10–14 m short, they're not stupid); otherwise a random reachable spot 60–160 m away. It walks there, **searches** for
  25–60 s (crouching, looking around), then moves on. If it's stuck for 15 s, it picks somewhere else.
* **Followers**: keep a loose formation 3–6 m around the leader and look around when he stops.
* Anything combat-related goes back to the game's AI immediately.

F12 → **SPT-Stalker Bots**: roaming on/off, anomaly field preference, search time, walking speed, calm-down time, debug log.

## SAIN

Supported (SAIN 4.5.x for SPT 4.x, ArchangelWTF's build; soft dependency):

* SAIN already manages the Bloodhound role (brain `ArenaFighter`), so **stalkers fight with SAIN's AI** (its combat layers,
  priority 20–24) and get SAIN personalities.
* The roam layer sits at **19**, just below SAIN's combat, so a fight always wins.
* SAIN tracks enemies itself, so when SAIN controls a stalker the roam layer also asks SAIN, through its public
  `SAINExternal` API (the one QuestingBots uses), whether the bot is calm: no enemy sensed for the calm-down time
  (`TimeSinceSenseEnemy`) and not fighting or searching a last known position (`CanBotQuest`).
* Without SAIN everything works as before.

SAIN's own extract layer (24) can make stalkers leave the raid late on, like other SAIN-controlled bots.

## First test checklist

1. Server console at startup: `Loner stalkers ready (role arenaFighterEvent, up to N groups across all maps)`.
2. BepInEx log at game start: `SPT-Stalker Bots loaded (Waypoints found, SAIN found)`, then
   `SAIN detected, stalkers use SAIN's enemy awareness` once a stalker starts roaming.
3. In raid (Woods is best), when a group spawns, the log shows one line per stalker:
   `stalker Grach (leader), brain 'ArenaFighter', active layer 'SPTStalkerRoam', SAIN personality 'Normal'`.
   * If the **active layer** is never `SPTStalkerRoam`, send me the **brain** name from that line; the layer is registered
     for a list of brain names, and that tells me which one the role really uses.
4. Turn on **Debug log** to see `[Grach] heading to ... / arrived, searching`.

## Next steps

* More factions (Bandits, Duty, Freedom, Monolith) as separate roles/brains
* Stalkers that trade, talk and offer contracts
* Campfire camps and sleeping spots; stalkers reacting to anomalies and emissions
