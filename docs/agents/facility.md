# Facility notes (AshWoods_Facility.unity)

Moved out of AGENTS.md on 2026-11-26. Read before touching `FacilityBuilder*`, facility props, quests, AI tuning there.

Facility notes: stage builders never touch it (`IsLevelScene` is false there); `FacilityBuilder` owns its
gameplay anchors, lamps (`HousePower.lamps` = every Facility light without `IndependentLight`), lockers,
notes (`InfoPoint`), objectives, `RoomAtlas` (6 m cells) and nav (2 m grid, regenerated each run). Kit
stairs get invisible ramp colliders: the step nosings stand 0.3 m above the stair line and broke the graph.
Modelled props (`FacilityBuilder.Lab` / `.Halls` / `.Storage`) come from `Tools/Blender/facility_assets.py` (run in
Blender through the MCP Blender bridge: `build_all()`, `build_more()`, `build_storage()`), exported to
`Assets/Art/Generated/Facility/Models`. Imported axes: Blender x/y/z -> Unity -x/-z/y; pivots are the world origin.
Material slots are named by role and remapped on import (`ConfigureModels`, guarded by importer `userData`; a new
slot name needs an entry in `SlotMaterials`). `Lab_v2` / `Halls_v2` / `Halls_v3` markers ran the one-time swaps
(primitive machines, the primitive generator, props where the wall racks now stand); the dressing code no longer
makes those pieces. Storage racks are 7.2 m tall against the walls; roof trusses there run along z, clear of the
lamps (x 6/18/30) and the duct (x 10.5). Builder screenshots with fog off: set `RenderSettings.fog` back before saving.

Facility horror pass (2026-11-10, owner's playtest notes), all one-time blocks guarded by markers:
- Four halls form a ring: `Ring_v1` opened assembly S / lab N at x 54..60 (airlock `Airlock_AssemblyLab_*`).
- `Catwalks_v2` rebuilt the maze on the building grid (cell i,j = x 2i, z -42+2j) so deck two ends exactly in
  the breaker airlock's mouth (x 18, z -24..-18). Rails are generated (`Rails`/`RailRun`) along the outline of
  decks + landings, merged per straight edge, shared corner posts, cut where a stair's handrail takes over;
  the kit's 2 m rail pieces are no longer used. `FootstepSurface` on the Catwalks group = metal steps.
- `Horror_v1..v4` (FacilityBuilder.Horror): lamp/emission/fog/AI/player/noise values, overlap fixes, hanging
  lamps as downward spots (pools of light), and exposure. Kit surfaces are metal and read by reflections:
  the default sky washed everything blue, so reflections come from `Env_DarkRoom.cubemap` and their strength
  follows the power (`HousePower.unpowered/poweredReflection` 0.3/0.55). The shared house post profile (ACES)
  crushes a scene this dim: the facility `Grade` volume sits above it (priority 20) and only lifts exposure.
- `Furnish_v1/v2` (FacilityBuilder.Furnish): primitive furniture replaced by models from
  `Tools/Blender/facility_horror.py` (`build_horror()`, `build_fixes()`), horror dressing under `Facility/Horror`,
  bottle/can looks (`Item_Bottle`, `Item_Can` under each item's `Visuals/Model`), steel locker model in
  `HidingSpot_Locker.prefab` (old boxes stay as invisible colliders). Items are set down with `Settle`.
- `facility_kit.join` now applies the first part's rotation: a rotated root was dropped by `PutModel` and the
  cable drum sank 0.57 m into the floor.
- AI: `EnemyVision` 50 m, 130 deg, slower to make out far/dark targets (`sharpDistance`, `farClarity`,
  `IEnemyTarget.Visibility`); `EnemyBrain` prowls towards the player's area (`prowlBias`), searches further
  along the player's heading (`searchSweeps`), hurries to echoes; `EnemyNavigation.nodeSearchRadius` keeps
  route requests cheap on the 1100-node graph. Hard impacts (and the breakers) echo through the whole level
  (`NoiseSystem.impactsEcho`): every ear hears them, `NoiseSoundPlayer` plays the crash plus wall returns.
  `NoiseSystem.storeySplitHeight` is 1000 here (no storeys; the house keeps 2.6).
- Sound: `AmbiencePlayer` (room-tone beds + far-off events), reverb zones per hall/airlock, `PoweredSound` hums on
  machines, `ThreatFeedback` tension drone + chase loop. Player: walk 2.6, sprint 5.4 for 7 s (chase is 4).
- Overlap check used for this pass: per prop collider, `Physics.ComputePenetration` against everything nearby,
  ignoring floors/ceilings, pillars, ducts and panel-vs-wall-support touches (hidden in the panel pilasters).

Facility playtest fixes (2026-11-11), `FacilityBuilder.Signs` / `.Fix2`, one-time markers `Signs_v1/v2`, `Red_v3`,
`BreakerDoor_v1`, `Fix2_v1`, `Fill_v1`; models from `Tools/Blender/facility_fix2.py` (`build_fix2()`):
- Exit signs sit on the door frames; the kit's red `ExitLight` quad and every `Emergency_Lens_*` box are hidden;
  lamps that were on a wall got a modelled `Emergency_Beacon_*` (`Prop_EmergencyLamp`).
- Breaker room door: `Door_Breaker` model (1.62 x 2.45, chamfered to the kit frame) opens into the room
  (`openAngle` -100); the old box keeps its collider. Breaker panel/switches/levers and batteries keep their
  placeholder colliders with renderers off; the models carry no colliders (`ModelsWithoutCollider`).
- Stair landings without deck under a foot get `Catwalk_Post` posts (`StairPosts`, scaled to the drop).
- `Assembly/Dressing`, `Lab/Dressing`: spot lists placed by `Dress` (back to the wall face measured across the
  prop, skipped when it overlaps anything but the shell, covers a vent, stands within 2.6 m of a doorway or
  leaves under ~1 m in front). Delete a `Dress_*` child and the `Fill_v1` marker to re-run. Navigation is
  regenerated afterwards.
- `Zones_v1` (`FacilityBuilder.Zones`, models from `Tools/Blender/facility_zones.py` `build_zones()`) replaced
  that scatter with a planned layout: positions are footprint centres, turns are multiples of 90 deg. Assembly:
  rack pairs (`Storage_PalletRack`) across the north strip with 1.8 m aisles, receiving lanes and an engine bay
  on the south wall, control stands, yellow floor lines. Lab: back-to-back server rack rows, analysis islands,
  triage row. The old conveyors are merged scene meshes: their renderer/collider are off and a `Model`
  (`Factory_Conveyor`) child carries the look and collider; `Drive_*` ends sit on every open line end.
  Delete the marker to re-lay (it clears both `Dressing` groups first).
- `Rooms_v1` (`FacilityBuilder.Rooms`, models from `Tools/Blender/facility_bodies.py` `build_bodies()`):
  `Horror_CoveredBody2` is a cloth simulation (`drop_cloth`) dropped on a metaball mannequin, `Horror_BodyBag2` a
  metaball shell with a raycast zip; new slots `Skin`/`Bag`. The body-bag gurney moved off the north trench to
  (50.4, -30). Breaker door: the kit frame's clear opening is 2.05 x 2.45 (measured on the visual mesh, the wall
  collider is narrower); leaf 2.11 wide, hinge at z -19.97, `Panel` collider widened. Breaker room and lab office
  dressing under `Electrical/BreakerRoom/Dressing` and `Lab/Office/Dressing`.
- Debug mode F7 / panel toggle: full light (flat bright ambient, fog off, shadowless `DebugSun`), held every frame.

Facility game loop v2 (2026-11-12), `FacilityBuilder.Quests` / `.Lures` / `.Side`, all under `Facility/Quests` and
`Facility/Side`, made once by name; refs re-pointed each run; chain + torch requirement + janitor "Way out" written
once by the `Quests_v1` marker (delete it to rewrite them):
- Chain (9): torch (`KeyPickup` "torch", `PlayerFlashlight.requiredItemId`) -> office code (`Keypad` on
  `Door_Office`, a clone of the breaker door at +60 x; digits on `Note_Code_1..3` via `{code1..3}`) -> keycard ->
  breaker room -> breakers (`BreakerPanel`, now control power only) -> fuel (`ObjectiveCondition.CarryKeys`
  "fuel_1,fuel_2", heavy items: no sprint, x0.8 walk) -> `GeneratorPanel` (needs both cans, consumes them,
  prerequisite = breakers; it is `HousePower.breaker` and the exit's `powerSource`) -> `ValvePanel` (ordered
  `PuzzleController`, shuffled per run, `{valves}` on `Note_Valves`; it is `EscapeExit.extraRequirement`) -> bay.
  Find panels by name (`BreakerPanel()`, `PowerSource()`); never `FindAnyObjectByType<PuzzleController>` any more.
- Per run (`RunRandom` seed: new only from the menu or Play Again, kept through deaths/checkpoints): keypad code,
  valve order, `RandomPlacement` spots of fuel cans, code notes, samples. `TextTokens` fills `{tokens}` in notes.
- `CheckpointSystem` (added by GameFlowController): saves after a step completes once no enemy chases; Try Again
  reloads and replays steps quietly (`ObjectiveManager.RestoreProgress`, `PuzzleController.ForceSolve`) and
  `PickupLedger` removes taken pickups and hands their keys back. Static, session only.
- Side: 8 staff logs (`InfoPoint.loreId`), 3 samples (keys `sample_*`) -> 4 endings on the completed screen;
  `SecurityTerminal` (map + enemy dot, needs main power, beeps); `NoiseLure` steam valves (one per hall, 2 uses);
  lockers back (`LockersEnabled`) with `PlayerHidingController` breath drain + gasps and `EnemyBrain.checkHidingChance`;
  desk hiding spots (`Hide_Desk_*`, eye 0.5 m). `JournalKey` (J) + `ObjectiveStep.hint` after 150 s stuck.
- `World.Difficulty` (settings row `Row_Difficulty`, PlayerPrefs) scales enemy speed/sight/hearing, torch drain,
  locker checks. Enemy: `SpringBones` (tail/cape/backpack by bone name) and `EnemyLookAt` (needs IK Pass on the
  base layer) are added at run time by `EnemyAnimator`. A `Climb` state waits for a clip named `climb_over`/`vault`
  in `Assets/Animations/Enemy` (re-run Set Up Enemy Animations after downloading it).

Mechanics polish (2026-11-12, from research: Alien: Isolation AI write-up, Clea "Mr X's lessons" devlog, Outlast 2
chase article, Amnesia: The Bunker reviews):
- Hiding: `PlayerHidingController` hold breath = Sprint held (`holdSeconds` 7, `recoverSeconds` 14, gasp noise when
  it runs out); `IEnemyTarget.IsBreathAudible`. `EnemyBrain.ListenForBreathing` (radius 2.6) turns audible breathing
  into a pull-out; a *check* hunt (`huntIsCheck`, not a watched hide) finds nothing if the breath is held and backs off.
  Check chance grows per hide (`checkChancePerHide`).
- Breathers: `EnemyBrain` menace (`menaceLimitSeconds` 50 within `menaceRadius` 13) and a failed full search ->
  `BeginRetreat` (farthest waypoints, ignores non-echo noises under 0.6). Never during the finale.
- Telegraphs: `EnemyBrain.Active` list; `World.Threat.NearestEnemyDistance` (written by PlayerEnemyTarget) drives the
  torch's dread flicker; `HousePower.disturbRange` dips lamps near an enemy.
- Torch: `TorchSway` (order 1100) on the beam; flat torch shake (F); click falls back to the Clink recording.
- `PlayerInteractor.aimAssistRadius` sphere-cast fallback (line of sight checked); quest items carry a `Glint` light.

Owner bug pass (2026-11-13):
- Keypad digits: the `Key` enum has Digit1..Digit9 then Digit0 then Shift/Alt/Ctrl/Win - never do `Key.Digit0 + d`;
  `KeypadUI` lists the keys. Numpad keys are consecutive.
- `Notes_v2` marker: code notes are wall sheets (`WallNote`, 0.3 x 0.4 m at 1.5 m, glint in front) on measured
  `WallSpot`s (`Storage/Assembly/LabNoteSpots`, `RandomizeOnWalls`); every spot was screenshot-checked. The valve order
  is `ValveBoard` (yellow backing, title, spot lamp) at z -16.6 on the valve wall. Steam vents restyled (yellow box,
  lever, STEAM VENT label via `LabelFacing`). Sample spots are open bench tops (the capsule tops were inside glass).
  When adding a pickup/note spot: screenshot it from where a player stands - collider tops lie inside glass and racks.
- `SpringBones` tail sway (`walkWag`/`idleWag`/`wagRate`): a spring alone held the tail straight back.
- `HidingSpot_Locker.prefab` has `Visuals/Lining` (the locker model is single-sided; its left wall vanished inside).
- `InteractionPromptUI` shows a message 4-10 s depending on its length.
- Valve room (`FacilityBuilder.ValveRoom`, marker `TowerDoors_v1`): the tower's ground floor (x 24..36, z -24..-12)
  was a sealed box with the valves inside. `TowerW0_-24_0` and `TowerS0_30_0` are now kit "Wall Door" sections (the
  shell lambdas lay them as doors too) with unlocked breaker-door copies `Door_ValveRoom_W/S`; RoomAtlas letter `V`.
  Navigation no longer skips `TowerRect`. Kit door frames have a 2.45 m lintel that made `TryFloorNode` refuse every
  doorway - use `TryDoorwayNode` (office door too). Before placing a puzzle in a room, check a path into it exists
  (`EnemyNavigation.SetDestination` in Play mode) - the valve wall looked fine in screenshots taken from inside.
