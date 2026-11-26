# House scene and legacy playground

Moved out of AGENTS.md on 2026-11-26. The house (AshWoods_House.unity) and playground are out of the build list; read this only when a stage builder or HouseBuilder must change.

- Legacy playground only (`Stage1WingBuilder`): the wing opens `Corridor_Wall_End`, moves
  `Escape_FrontDoor` to `FrontDoorPosition` and the player to the boot room - each once, and
  only while the object is still where Stage 1/6 put it. Hand-edited positions are kept.
- `EnemyBrain.wakeDelaySeconds` is the start grace: the enemy patrols, but sight and
  hearing stay off until it runs out. It is the second half of the anti-spawn-kill design -
  keep the boot room's walls, do not rely on the delay alone.
- Wing waypoints are appended after `Node_10` on purpose. Inserting one in the middle would
  leave every saved `Node_N` in an existing scene pointing at the wrong place, because the
  builder only creates missing names and never moves existing ones.
- The house scene (`AshWoods_House.unity`) is authored by `HouseBuilder`. It never runs
  `Stage1PlaygroundBuilder.EnsurePlaygroundInActiveScene()`, so the old room, corridor and
  wing stay out of it. Stage objects are created by the stage builders at their original
  coordinates and then moved onto house anchors; `MoveObject` only moves an object that is
  not already at its anchor, so re-running is safe.
- **House layout v2** (`HouseBuilder.Layout.cs`): the plan is two 12 x 8 letter maps on a
  2 m grid plus a list of openings. Walls, slabs, nav nodes (one per walkable cell + 4 per
  ramp) and patrol points (`PatrolCells`) are all derived from it, so edit the maps, not
  boxes. Changing a map changes generated box names; delete `House_G_Wall*`/`House_U_Wall*`
  (or the whole House root) and rebuild, because `EnsureBox` never moves an existing box.
- `Layout_v2` under `House` marks the new layout. Without it, `UpgradeOldLayout` deletes
  the House root, `Battery_*`/`Throwable_*` items and every nav/patrol point, then rebuilds.
- Wall runs share corners without overlapping: at each grid vertex exactly one run owns the
  0.2 m square (a straight-through z run, else a straight x run, else the x run of an L).
- Openings are cut out of one run (pieces + `_Lintel`), never added as frames. Stair feet
  and tops are cut to the ceiling: with a 2.1 m lintel the enemy's head hit it on the way
  down the ramp and it stood there for good.
- Stairs are ramp colliders (38.7 degrees) with collider-less treads, walled in on the
  ground floor so nothing can walk under the high end. The top ramp node sits at 95 % of the
  climb: lower, its 0.3 m clearance ray clipped the upper slab's lip and cut the stair out of
  the graph.
- Navigation links need floor under the whole line (`HasFloorAlong`), not only clear air;
  single-ray "hairline" legs are only trusted within 1.2 m.
- `Diagnostics > Log House Overlap Check` compares every renderer bound under `House` and
  ignores stair ramps and treads.
- Stage 9 writes tuned light/sound values once, guarded by an inactive root `Polish_v1`
  (find it via scene roots - `GameObject.Find` misses inactive objects). URP shadow size per
  light is the additional-light data's tier, not `Light.shadowResolution` (Built-in only).
  Stage 10 re-applies the UI style on every run; restyle in its code, not by hand.
- Furniture fits height first and may stretch width/depth up to 1.4x (holder `Visuals` takes
  the scale, `Model` inside keeps the FBX rotation). Skirting lives in `House/Dressing/Skirting`
  and skips walls along the stair strips.
- **Furnishing v3+** (`HouseBuilder.Furnishing.cs`): a modern house. `FurnitureList` mixes Poly
  Haven models and box *kits* (counter, uppers, fridge, drawers, book shelf, TV unit, washer,
  toilet, vanity, table). Bump `DressingVersionName` to make every scene re-lay its dressing
  once. Keep solids against walls or centred between cell centres with the short side under
  1 m, then confirm no `Node_*` sits inside a `Furn_*` collider (existing nodes are never
  deleted by the builder). Everything under `Dressing` is batching-static.
- Performance: shadowed point lights cost six shadow passes each; `LampCuller` (next to
  HousePower) gives shadows to the nearest lamps only and switches off lamps hidden on the other
  storey. `GameSettings` owns quality/FPS/FOV/brightness/sensitivity/volume (PlayerPrefs) and
  restores the pipeline asset when play mode ends. Never leave extra enabled cameras in the
  scene (two stray `EyeCam`s once tripled the render cost).
- Enemy vs furniture: a target standing on a `Furn_*` collider (or the crouch roof) cannot be
  caught; the brain besieges it (stare, back off, wait) and tracks the real position for
  `besiegeMemorySeconds` after it climbs down.
- **House dressing** (`HouseBuilder.Dressing.cs`) is visual only. Shell boxes get meshes with
  UVs in metres (`Assets/Art/Generated/BoxMeshes.asset`, sub-mesh 0 = top face) so textures
  tile evenly. Furniture is a data list (`FurnitureList`): each Poly Haven FBX is scaled to
  fit its box, keeps its own root rotation (Blender FBX roots are turned -90 about x), and
  gets a box collider; any cell centre under a footprint (+0.5 m) is not a nav node. To redo
  furniture, delete `House/Dressing/Furn_*` and rebuild. Picture-frame glass renderers are
  switched off (they import as opaque white). Decor settles onto the surface below.
- **Stage 11** is called at the end of `Stage10InterfaceBuilder.EnsureStage10Pieces()`, so every
  chain reaches it. One-time values (chase 4 m/s, flashlight drain 0.0133/s, battery +30 %,
  batteries onto `TableBatterySpots`) are guarded by the inactive root `Balance_v1`.
- Sprint is limited by `PlayerMotor.Stamina` (5 s, refills in 7 s, locked out until 35 % after
  running dry). The enemy's 4 m/s chase only works because of this: sprint is still 4.3.
- `FlashlightFlash`: Q, or the throw button with an empty hand (with an item it throws). Finds
  `ILightSensitive` (the enemy) with an overlap query - rooms hold 100+ colliders, keep the
  buffer large. `EnemyBrain.Daze` is a stun (playtest 2: 30 % battery, 3.5 s, speed 0): while
  `IsStunned` the enemy cannot see, hear or catch, and the last `dazeRecoverySeconds` ramp its
  speed back up. Values are written once by Stage 11's `Balance_v2` marker.
- A run-ending catch is shown by `CaughtCameraFall` (camera jerks to the enemy, drops to the
  floor on its side) and a black fade that waits `deathFadeHoldSeconds`; no red screen.
- **Stage 12 atmosphere** is called at the end of Stage 11. Generated art (textures, materials,
  cobweb/menu meshes) lives in `Assets/Art/Generated/Atmosphere`; textures carry a version in
  their importer `userData` (`AtmosphereAssets.TextureVersion`) and are redrawn in place when it
  changes. Marks and cobwebs are scattered only into an empty group (delete `House/Atmosphere/Marks`
  or `Cobwebs` to re-scatter). Moon windows and the enemy's presence light carry `IndependentLight`,
  which Stage 7/9 and `LampCuller` skip - house power must never dim the moon.
- Stage 12's `Atmosphere_v2` marker applied the owner's second look once: house fog 0.03, corridor
  fog alpha 0.06, calmer `CameraFeel` (bob 0.016/0.028, fear shake 0.35 deg), chase lens -0.06.
- `RoomAtlas` holds the layout letter maps and room names (`HouseBuilder.FillRoomAtlas`);
  `RoomCaptionUI` shows each room's name on first entry. Corridors and stairs have no name.
- `UpgradeRoomIdentity` (marker `Dressing/Rooms_v1`) rebuilt only the pantry and linen shelves;
  new room props are added by name, so hand-scaled furniture is kept. The breaker is in the
  upstairs storeroom (x 10, north wall).
- Hiding beds (`HidingSpot_Bed.prefab`, built by `Stage3EnemyBuilder.BedPrefab`) keep the
  mattress slab down to 0.22 m: the navigation's lowest probe is 0.3 m, and a higher gap would
  let routes run straight through a bed. House hiding spots stand against walls, centred on
  a grid line, so they sit between nav nodes; `HidingSpot.eyeHeight` lowers the camera while
  hidden and `PlayerHidingController` restores it on leaving.


Title screen v3 (2026-11-26, `MenuSceneBuilder.Winter.cs`, chained from `BuildStation`): marker `Station_Winter_v1` ran the
one-time fixes (door moved to x 7.5 off the pilaster, window frames/sills + unlit interiors `Tex_WindowDark/Blinds`,
`Station_Ash` off, primitive barrels/pallets/crate off). Parts built once: `Station_Winter` (snow caps measured from
the parts, drift meshes saved to `Assets/Art/Generated/Menu/Drift_*.asset`, tyre-track decals), `Station_Clutter`
(Poly Haven CC0 models at real scale, grounded by bounds), `Station_Details` (signs, cameras, ladder, downpipes, cables),
`Station_Snow` (two calm layers: owner found turbulent flakes "like midges" - keep noise ~0.1 at low frequency, slow scroll), `Station_VFX` (steam in its own unlit `Station_Steam` material - the lit fog puff read as dark blots at night; wall-box sparks). Delete a part to rebuild it. Surfaces:
hall `precast_concrete_wall`, yard `asphalt_snow`, land `snow_02`.
