# HullBreach 1.8.0: port to KSP 1.12.5 and BDArmory Plus 1.13

A port of HullBreach 1.7.0.5 by gomker (Derek Lane), with contributions from SpannerMonkey,
Gedas-S and zzhwudiaa (https://github.com/gomker/HullBreach), to KSP 1.12.5 and BDArmory Plus 1.13.
It is a modified work offered under CC BY-NC-SA 3.0, the same licence as the original (see `LICENSE`).

## Why 1.7 sank ships instantly, and what replaced it

| 1.7 behaviour | 1.8 |
|---|---|
| Flooding ran per frame (`Update`/`FixedUpdate`) with no `deltaTime` scaling, so the rate depended on frame rate | One `HullBreachVesselModule` per loaded vessel runs a fixed 0.1 s tick scaled by elapsed time |
| Damage came from the removed `BDArmory.Core` API. With the shipped thresholds (breachTemp 0.6, critBreachTemp 0.9) the "Normal" band was empty, so any part below 90 % HP counted as a **critical** breach | Severity comes from BDA hitpoint loss plus weighted armour loss (`FloodModel.Severity`), with separate breach and critical thresholds |
| `RequestResource("SeaWater", -rate)` filled the vessel-wide `ALL_VESSEL` pool (huge `maxAmount`s added by the patches) until it was full | Each hull part floods locally toward its own waterline equilibrium (see the model below) |
| `crushable` parts got `buoyancy = -1` as soon as they were fully submerged | Crushable parts lose buoyancy (to `founderBuoyancy`, default 0) only once their ship has *foundered*, and get it back if the ship surfaces |
| EC drain of 1000 units per frame per part, vessel-wide | Only a compartment flooded past 50 % loses the EC stored in that part |
| `hydroExplosive` parts gained `0.1 * depth` K per frame whenever fully submerged, which cooked submarines | Heat (K/s per metre of depth) only on a foundered ship or a flooded compartment |
| Submarine parts with `crushDepth` 20 to 100 m; submarines detected only by the root part name starting with "Sub" | Submarines come from `BDModuleSurfaceAI` Submarine mode or a `submarine = true` part. Crush depth is at least 350 m for them, inflow ×0.5, and they get damage-control pumping |
| Static `Instance` singletons, `force_activate`, `rigidAttachment`, `IgnoreGForces` | Removed |

## Flood model (FloodModel.cs, pure maths, unit-tested offline)

* **Severity** `s = 1 - (1 - hpLoss) * (1 - armorWeight * armorLoss)`. hpLoss comes from
  `HitpointTracker.Hitpoints` / max HP. armorLoss is measured against the largest armour value seen
  in flight, which is kept in the persistent field `armorBaseline` because BDA resets
  `StartingArmor` on reload.
* **Breach states.** `s < breachThreshold` (0.10) is watertight. Between that and `criticalThreshold`
  (0.60) the part is *Leaking*: only part of the compartment is open to the sea, `fill = (s-bt)/(ct-bt)`.
  At or above 0.60 it is *Critical*: the whole compartment is open, plus up to `criticalOverfill`
  (20 %) more, which models water over the deck and progressive flooding.
* **Target water.** `capacity * min(1, submergedPortion + overfill) * fill^subdivision`.
  `capacity` is the water mass that exactly cancels the part's buoyancy. It is computed the way KSP
  computes buoyancy: `PartBuoyancy.displacement * body.oceanDensity * PhysicsGlobals.BuoyancyScalar *
  part.buoyancy / SeaWater density`. It is recomputed every tick, so KerbalPowers Naval ballast,
  which changes `part.buoyancy`, stays consistent. A part config can override it with `floodCapacity`.
* **Rate.** `capacity * leakArea * depthFactor * multipliers / floodTime` (floodTime 60 s), where
  `depthFactor = sqrt((depth+1)/4)` is clamped to [0.5, 2.5]. Water drains back at half that rate when
  it is above the target, for example when the ship rises.
* **Damage control (submarines only).** Each non-critical compartment pumps out 0.2 % of its
  capacity per second, so a scratched submarine is not doomed.
* **Sealed.** If damage falls back below the breach threshold, the water stays until it is pumped
  out. The Large Sea Water Pump bails out all compartments.
* `subdivision = 2` is used for single-part ships and submarines (MNWS, SACD, CVX), so light damage
  does not flood the whole vessel.
* **Foundering.** A non-submarine with water aboard whose CoM is below -20 m has foundered: its
  crushable parts lose buoyancy, `hydroExplosive` parts heat up, and an "Abandon ship!" message
  appears once.
* **Crush depth.** Fully submerged parts below their crush depth take `0.1 * maxHP * (1 + over/25)`
  damage per second through `HitpointTracker.AddDamage`. If a part is still alive after 20 s, it
  explodes as a fail-safe. For submarines the effective crush depth is `max(crushDepth, 350 m)`.
  Stock overpressure kills parts at about 395 m.

`Tests/FloodModelSim` (net8.0, `dotnet run -c Release`) checks the defaults. The last run passed
every check:

| Scenario | Result |
|---|---|
| Destroyer, 5×400 t compartments, 40 % immersion: 1 or 2 compartments at 80 % damage | Stays afloat (53 % / 73 % immersion) |
| 3 compartments at 80 % | Sinks in about 105 s |
| All compartments at 30 % (spread gunfire) | Stays afloat, 67 % immersion |
| All compartments at 50 % | Sinks in about 110 s |
| 2 torpedo hits (2 compartments at 100 %) | Stays afloat at 80 % immersion |
| 2 torpedo hits + 3 compartments at 40 % | Sinks in about 82 s |
| Single-part ship (subdivision 2): 20 / 35 / 50 % damage | Stays afloat |
| Single-part ship: 60 % / 90 % damage | Sinks in 110 s / 69 s |
| Submarine at 75 m, one compartment at 20 % | Never reaches 5 % heavy; damage control holds it |
| Submarine at 75 m, one compartment at 40 % / 90 % | 5 % heavy after 51 s / 13 s |
| Submarine at 75 m, all compartments at 15 % | Holds at 0 % |

## Performance

* `ModuleHullBreach` has no `Update`, `FixedUpdate` or `LateUpdate`. All per-part work happens in the
  vessel module tick: 10 Hz, O(HullBreach parts on that vessel), with no allocations, no LINQ and no
  boxing. Status strings are constants.
* The module list, surface AI and submarine flag are rebuilt only on `onVesselWasModified`,
  `onVesselPartCountChanged` or a module start. A destroyed or decoupled module marks the list
  dirty. No code iterates over other vessels, so the cost across a fleet is linear in the number of parts.
* `HitpointTracker`, its HP `UI_ProgressBar` and the `SeaWater`/`ElectricCharge` `PartResource`s are
  cached per part. The submarine AI mode is re-read every 2 s by string comparison, because
  `BDModuleSurfaceAI.SurfaceType` calls `Enum.Parse`, which allocates.
* Severity is only computed for `hull` parts.

## API mapping (BDArmory 1.2 / KSP 1.4 → BDArmory Plus 1.13 / KSP 1.12)

| 1.7 | 1.8 |
|---|---|
| `BDArmory.Core.Extension.PartExtensions.Damage()/MaxDamage()` (through `Dependencies.Get<DamageService>()`) | Cached `BDArmory.Damage.HitpointTracker`: `Hitpoints`, max HP from `Fields["Hitpoints"].uiControlEditor` (`UI_ProgressBar.maxValue`, because `maxHitPoints` can be -1), `Ready`, `ArmorPanel` |
| `BDArmory.Core.Module.HitpointTracker.Armor` | `HitpointTracker.Armor` plus own persisted `armorBaseline` |
| Damage events | `DamageService.OnActionExecuted` still exists, but is deliberately not used: polling cached fields at 10 Hz is cheaper and survives save/load |
| Crush damage by `part.explode()` | `HitpointTracker.AddDamage(float)`, with `part.explode()` as a fail-safe |
| Submarine detection by vessel name | `BDArmory.Control.BDModuleSurfaceAI.SurfaceTypeName == "Submarine"` or a part with `submarine = true` |
| Per-part `OnUpdate`/`FixedUpdate` | `VesselModule` (`Activation.FlightScene \| LoadedVessels`) |
| Toolbar via static instances | `ApplicationLauncher` button registered on `onGUIApplicationLauncherReady`, removed in `OnDestroy` |
| `settings.cfg` written next to the DLL | Defaults in `Settings.cfg` (`HullBreachSettings`, patchable with MM). In-flight Save writes `PluginData/settings.cfg`, which MM ignores, so the cache is not invalidated |
| `SeaWater` RESOURCE in every patched part | Added at runtime with `Part.AddResource(ConfigNode)` on first breach, which fires `onPartResourceListChange` so pumps see it. It persists in saves |
| Build: .NET 3.5, old csproj | SDK-style csproj targeting .NET Framework 4.8 (same as BDArmory.csproj). References: KSP 1.12.5 `Assembly-CSharp`, `UnityEngine.*Module`, BDArmory 1.13.1.0 |

`[assembly: KSPAssemblyDependency("BDArmory", 1, 0)]` makes KSP skip the plugin cleanly if
BDArmory is missing.

Old craft: 1.7 saved `hull`, `crushable`, `hydroExplosive` and `DepthCharge` into craft files.
1.8 re-applies them from the part prefab in `OnStart`, so the new patches are authoritative.
`flowMultiplier` (clamped to 0.25 to 4, editor-only) and `crushDepth` are still per-part tweakables.

## Part config fields (ModuleHullBreach)

| Field | Default | Meaning |
|---|---|---|
| `hull` | false | Watertight compartment that floods |
| `crushable` | false | Loses buoyancy when its ship founders |
| `hydroExplosive` | false | Cooks off on a foundered ship |
| `submarine` | false | Marks the vessel as a submarine |
| `DepthCharge` | false | Explodes at `crushDepth` (legacy) |
| `crushDepth` | 200 | Persistent tweakable, 0 to 600 m |
| `flowMultiplier` | 1 | Persistent editor tweakable, 0.25 to 4 |
| `floodCapacity` | -1 | Units of SeaWater; -1 derives it from KSP buoyancy |
| `breachThreshold`, `criticalThreshold` | -1 | Per-part overrides; -1 uses the global settings |
| `subdivision` | 1 | Use 2 for single-part ships |

Removed keys (`flowRate`, `critFlowRate`, `breachTemp`, `critBreachTemp`, `partDebug`) are ignored by
KSP if an old third-party patch still sets them.

## Global settings (`GameData/HullBreach/Settings.cfg`)

`enabled, floodTime 60, breachThreshold 0.1, criticalThreshold 0.6, criticalOverfill 0.2,
armorWeight 0.35, globalFlowMultiplier 1, drainFactor 0.5, submarineDamageControlRate 0.002,
refDepth 4, minDepthFactor 0.5, maxDepthFactor 2.5, submarineFlowMultiplier 0.5,
submarineMinCrushDepth 350, crushDamageRate 0.1, crushFailsafeTime 20, founderAltitude -20,
founderBuoyancy 0, hydroHeatRate 5, ecDrain True, ecShortFraction 0.5, updateInterval 0.1,
messages True, debug False`

The toolbar window has toggles for enabled, EC drain, messages and debug, a global flood-speed
slider, and Save / Reload buttons. Debug mode shows severity and depth in the part action window,
plus a **HullBreach: Test Breach** button that forces 75 % severity.

## ModuleManager patches (`Patches/`)

Every patch has the form `@PART[a|b|...]:HAS[!MODULE[ModuleHullBreach]]:NEEDS[<pack folder>]:LAST[HullBreach]`.
Each one is inert without its pack, leaves alone parts that already carry their own HullBreach
config, and runs after other mods' patches. The patches were generated by a script and every part
name was validated against the part configs in each pack's release archive (and, for BDArmory and
stock, a KSP 1.12.5 install). 333 distinct parts are covered, compared with about 192 in 1.7.

| File | NEEDS | Parts | Roles |
|---|---|---|---|
| HullBreach_LargeBoatParts.cfg | LShipPartsModern, LShipPartsWW2, LShipPartsRequired | 133 | 59 hull sections, 6 submarine pressure hulls plus sail/platform, 66 superstructure/bridges/funnels (crushable) |
| HullBreach_MNWS.cfg | Johnny | 49 | 16 single-part ships (hull, subdivision 2), SSBN002 (submarine), 32 turrets/launchers/VLS/radars (crushable) |
| HullBreach_SACD.cfg | SACDCJ | 8 | Generic hull plus 7 decks |
| HullBreach_KerbalPowersNaval.cfg | KerbalPowers/Naval | 3 | Fugu, Fahaka and Mbu ballast hulls |
| HullBreach_BDArmory.cfg | BDArmory, BDArmoryExtended, BDArmoryRefurbished | 57 (+3 legacy names) | Naval guns, CIWS, radars, sonar, launchers (crushable); armour panels (crushable, as in 1.7) |
| HullBreach_Stock.cfg | Squad | 30 | Mk1/Mk2/Mk3 fuselages, cargo bays, cabins, adapters (hull) |
| HullBreach_NAS.cfg | NAS | 50 | Legacy coverage: turrets and magazine (crushable + hydroExplosive), AA, torpedo tubes, radars |
| HullBreach_CVX.cfg | Eskandare_Heavy_Industries/Carrier_Vessel_Expansion | 3 | Legacy coverage: Nimitz hull, island, radar |
| HullBreach_TweakScale.cfg | TweakScale | 1 | Pump scaling (1.7 added TweakScale unconditionally) |

Dropped 1.7 patches:
* `ACA.cfg` (Enterprise) and `BoomSticks_MM_Crushable.cfg`: the packs were not available to verify part names against.
* `SMM_MM.cfg` (SM Marine): the pack is not available, so its folder name can't be verified for `:NEEDS`.
* `Malfunc_Crushable_MM.cfg`: these parts now ship in Refurbished Armory and are in HullBreach_BDArmory.cfg. The RIM missiles are no longer patched.

Other distribution fixes:
* `Resources.cfg`: `SeaWater` stays. `BallastWater` is only defined when none of KerbalField,
  SM_Industries, Johnny or LShipPartsRequired already defines it.
* Pump part: explicit `MODEL` node (1.7 pointed at a non-existent `model.mu`), `GeneratesHeat` off
  (there is no core heat module).
* `Category.cfg`: icon paths fixed to `boat` / `boat_selected`.
* `HullBreach.version`: 1.8.0.0 for KSP 1.12.x. The broken JSON (unterminated `DOWNLOAD` string) is fixed.

## Build

```
dotnet build HullBreach.csproj -c Release -p:KSPRoot="<KSP folder>" [-p:BDArmoryDll="<path>\BDArmory.dll"]
```

`KSPRoot` can also come from the `KSPDIR` environment variable; `BDArmoryDll` defaults to
`<KSPRoot>/GameData/BDArmory/Plugins/BDArmory.dll`.

The result is 0 warnings and 0 errors. The output references only mscorlib, Assembly-CSharp,
UnityEngine.CoreModule, UnityEngine.IMGUIModule and BDArmory 1.13.1.0. The DLL in
`Distribution/` was built against BDArmory Plus 1.13.1.0. `Gui.cs` and
`partModuleReference.cs` are kept for history but are not compiled.

## Not verified (no in-game run was possible)

* Real buoyancy-derived capacities and flooding times on actual hulls: LBP, MNWS whole-ship
  parts and stock fuselages. Tune `floodTime` or `subdivision` if sinking feels too fast or too slow.
* `HitpointTracker.Ready` timing and HP bar `maxValue` on very large single-part hulls (MNWS
  carriers of 100k+ t).
* LBP, MNWS, SACD and KP Naval were not installed for testing. Their part names and `:NEEDS` folder
  names (`LShipPartsModern`, `LShipPartsWW2`, `LShipPartsRequired`, `Johnny`, `SACDCJ`,
  `KerbalPowers/Naval`) come from the release archives.
* How the BDA Surface AI reacts to a flooding ship (for example, the submarine AI fighting the extra weight).
* Stock overpressure versus the 350 m submarine crush minimum on deep-diving custom subs.
* Interaction with KSPCommunityFixes, PhysicsRangeExtender and VesselMover.

## In-game test checklist

1. Install `GameData/HullBreach` (with ModuleManager and BDArmory 1.13). Start the game and check
   `KSP.log` for `[HullBreach]: Settings loaded`, with no `[HullBreach]` exceptions or MM errors.
2. In the VAB/SPH, part info for a patched hull (for example a stock Mk3 fuselage) shows the HullBreach
   text. The PAW shows Crush Depth and Flood Rate.
3. Launch a small boat on water and enable Debug in the HullBreach toolbar window. On a hull part,
   press *Test Breach*: the status becomes "Critical breach", Flooded % rises over tens of seconds, the
   boat settles lower and stops at equilibrium unless several compartments are open. Toggle it off:
   "Flooded (sealed)". Add the Large Sea Water Pump and start it: the water drains.
4. BDA weapons test: shoot a destroyer below the waterline. Expect "Hull Breach" messages. One or two
   damaged compartments: it lists but floats. Heavy spread damage or torpedoes: it sinks in roughly
   1.5 to 3 minutes, with "Abandon ship!" once the CoM passes -20 m.
5. Submarine: set the BDA Surface AI to Submarine and dive to 100 m. Light hits should be held by
   damage control. A torpedo hit should make it heavy over 10 to 60 s, not instantly. Below 350 m
   (or the part's crushDepth if higher) expect "approaching crush depth" and then crush damage.
6. Fleet performance: run a 6+ ship BDA naval battle. Frame time should match the run without
   HullBreach (no per-part Update; one 10 Hz tick per vessel).
7. Save, reload mid-flood: water amounts, armour baseline and tweakables persist.
