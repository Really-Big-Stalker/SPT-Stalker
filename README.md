# SPT-Stalker

A S.T.A.L.K.E.R.-inspired overhaul for **Single Player Tarkov 4.1.x** (server mod, C# / .NET 10).

## Roadmap

| Stage | Content | Status |
|---|---|---|
| 1 | **Zone trader** "Bunker": a Cordon bunker trader with 4 loyalty levels | ✅ this release |
| 2 | Custom quest line for the trader | planned |
| 3 | Random dynamic quests with new condition types | planned |
| 4 | Artifact items (based on the artifacts from the S.T.A.L.K.E.R. games) | planned |
| 5 | Anomalies | planned (needs a client/BepInEx plugin) |
| 6 | New AI enemies | planned |

## Stage 1: the Zone trader

Kuzma "Bunker" runs a trade bunker on the edge of the Zone. He buys Zone loot (barter items, food, drink,
meds, weapons, ammo, intel, gas masks), repairs gear, and sells a kit that grows with loyalty:

* **LL1 Rookie:** PM, sawn-off MP-43, buckshot, bandages, AI-2, tushonka, sausage, vodka, grenades, PACA, flashlight
* **LL2 Experienced:** AKS-74U, MP-133, 5.45 PS, IFAK, morphine, 6B2, SSh-68, GP-5; scav pack for vodka + cigarettes
* **LL3 Veteran:** AK-74N, AKM, BT/PS rounds, Salewa, Propital, 6B23-1, GP-7; SVDS for Geiger counters + gunpowder
* **LL4 Master:** VSS Vintorez, SP-5/SP-6, Grizzly, eTG; 6B13 and Ratnik helmet for gold chains / bitcoin

Weapons and armour are sold as the game's default presets (full attachments and plates).

## Install

1. Download the `SPTStalker` artifact from the latest successful **Build** run under the repo's Actions tab (or build it yourself, below).
2. Copy `SPT/user/mods/SPTStalker` into your SPT install so you get `SPT/user/mods/SPTStalker/SPTStalker.dll`
   and `SPT/user/mods/SPTStalker/data/...`.
3. Start the server. You should see: `[SPT-Stalker] Trader 'Bunker' is open for business (47 offers)`.

## Build

Requires the .NET 10 SDK.

```bash
python3 tools/validate_data.py
dotnet build src/SPTStalker/SPTStalker.csproj -c Release
# output: src/SPTStalker/bin/Release/SPTStalker/  -> copy to SPT/user/mods/SPTStalker
```

The project references the SPT 4.1.2 NuGet packages, which are the newest published for 4.1. The mod declares
`~4.1.0`, so it loads on any 4.1.x server, including 4.1.6.

## Editing the trader

Everything lives in `src/SPTStalker/data/trader/`. Edit it there, or directly in the installed mod folder.

| File | What it controls |
|---|---|
| `settings.json` | Restock time, flea-market listing, name / nickname / location / description per language (`en`, `ru`, ...; the `defaultLocale` text is used for every other language) |
| `base.json` | Trader id, loyalty level requirements, what he buys, repairs |
| `assort.json` | What he sells |
| `avatar.jpg` | Trader portrait (placeholder art; swap in your own 256×256 jpg) |

An `assort.json` offer looks like this:

```json
{
  "key": "rookie_pm",              // unique and permanent; the offer's in-game id is derived from it
  "name": "PM pistol",             // just a label for you
  "tpl": "5448bd6b4bdc2dfc2f8b4569",
  "loyaltyLevel": 1,
  "stock": 6,                      // or "unlimited": true
  "buyLimit": 2,                   // per restock, optional
  "price": { "currency": "RUB", "amount": 11000 }
  // or: "barter": [ { "name": "Vodka", "tpl": "5d40407c86f774318526545a", "count": 1 } ]
}
```

Item ids can be found in SPT's `ItemTpl` list or the item database (`SPT_Data/database/templates/items.json`).
Bad entries are logged and skipped instead of stopping the server.

## Project layout

```
src/SPTStalker/
  ModMetadata.cs                 mod name/version, SPT version range
  Common/StableId.cs             ids that stay the same between server restarts
  Trader/StalkerTraderLoader.cs  registers the trader (avatar, restock, flea, locales, stock)
  Trader/TraderAssortBuilder.cs  assort.json -> server assort (presets, prices, barters)
  Trader/TraderSettings.cs       JSON models for settings.json / assort.json
  data/trader/                   trader data (see above)
tools/validate_data.py           data checks, also run in CI
```
