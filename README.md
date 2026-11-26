# Ashen Woods

An original first-person survival horror prototype in Unity, built in small stages.
Art: free assets under any lawful licence (Poly Haven, SciFi Warehouse Kit, Mixamo, CC0 sounds), Blender-made
props, and the owner's own enemy model (kept out of git). Sources are logged next to each asset folder.

- **Unity:** 6000.4.4f1 (URP)
- **Input:** Unity Input System, project set to *Active Input Handling = Both*
- **Navigation:** no NavMesh — a waypoint graph with A* lives in `EnemyNavigation`
- **Scenes (build order):** `AshWoods_Menu.unity` (title) -> `AshWoods_Facility.unity` (the level).
  `AshWoods_House.unity` is the earlier level, frozen and out of the build.
- **Builds:** `Ashen Woods > Build` only - versioned folders `Builds/AshenWoods_<SemVer>` with `CHANGELOG.txt`
  (see AGENTS.md, "Player builds").

The full design brief this project is built against is at the bottom of this file.

## Progress

| Stage | Scope | Status |
| --- | --- | --- |
| 1 | Project inspection, playground scene, first-person controller, camera, collisions, footstep events | Done |
| 2 | Interaction system (raycast + prompt), doors, locks, keys, key inventory | Done |
| 3 | Enemy: waypoint navigation, vision, patrol, chase, search, caught placeholder, hiding spot | Done |
| 4 | Pause menu (freeze + release cursor), satchel screen, clickable buttons | Done |
| 5 | Sound events, enemy hearing, investigate state, stealth noise, hiding noise | Done |
| 6 | Breaker puzzle, objective chain (key → closet → crowbar → breaker → exit), front door, objective HUD | Done |
| 7 | Death screen, restart, completed screen, flashlight + batteries, dark house until the breaker, procedural sound, threat cues, enemy drags you out of a hiding place it saw you enter | Done |
| 8 | Integration pass, playtest and fixes; throwable distractions (bottles, tin cans) | In progress - see below |
| 9 | Sound and light polish: floor-aware hearing and muffling, directional enemy steps, quiet player steps, shadowed lamps, real flashlight, post-processing, furniture at true scale, skirting | Built, awaiting playtest |
| 10 | Interface style: Inter font, one palette, minimal left-column screens and HUD | Built, awaiting playtest |
| 11 | Playtest balance: stamina, flashlight flash, throw arc, room names, batteries on tables, breaker in the storeroom; playtest 2: flash stun (30 %, 3.5 s), fall-to-floor catch, no red screen | Built, awaiting playtest |
| 12 | Atmosphere: torch dust, corridor fog, cold grade + grain, chase aberration, faulty lamps, moonlit windows, stains/cracks/mould, cobwebs, camera bob/lean/fear shake, enemy eyes + presence light, flash white-out and ringing; main menu scene (mansion, garden, fountain, gate, moon) | Built, awaiting playtest |
| - | Facility level (`FacilityBuilder`): sci-fi kit shell, catwalks, Blender props, horror pass, playtest fixes | Played by the owner |
| 13 | Fixes + checkpoints: sounds prewarmed (catch hitch), enemy climb state (needs a `climb_over` clip), lockers back with costs (holding breath drains stamina, searching enemy may check a locker), checkpoint after every objective (Try Again resumes there) | Built, awaiting playtest |
| 14 | Longer game loop (facility): torch item, keypad office (code on 3 notes), breakers -> fuel -> generator -> ordered valves -> bay; per-run random code/order/item spots (`RunRandom`, `RandomPlacement`); staff logs + B-7 samples -> 4 endings; security terminal; steam lures; desk hiding; J journal + idle hints; difficulty; enemy look-at + spring bones | Built, logic run through in Play mode by script; not played by hand |

Note: the brief's stages 2 and 3 are merged here (interaction + items + locks are all
Stage 2 in this repo), so this project's numbering is one behind the brief after Stage 3.
Use the table above, not the brief numbers, when resuming work..

## How to run a stage

Everything is authored from the editor menu, never by hand-placing objects:

```
Ashen Woods > Level > Create Two-Storey House Scene      <- the rebuilt level
Ashen Woods > Level > Build Or Update The House

Ashen Woods > Stages > Stage 2 - Interaction And Doors
Ashen Woods > Stages > Stage 3 - Enemy And Hiding Spots
Ashen Woods > Stages > Stage 4 - Pause Menu And Inventory
Ashen Woods > Stages > Stage 5 - Sound And Stealth
Ashen Woods > Stages > Stage 6 - Objectives And Escape
Ashen Woods > Stages > Stage 7 - Endings, Flashlight And Atmosphere
Ashen Woods > Stages > Stage 8 - Throwable Distractions
Ashen Woods > Stages > Stage 9 - Sound And Light Polish
Ashen Woods > Stages > Stage 10 - Interface Style
Ashen Woods > Stages > Stage 11 - Gameplay Balance

Ashen Woods > Tuning > Apply Tuned Stealth Values
Ashen Woods > Diagnostics > Log House Layout / Log House Overlap Check
Ashen Woods > Diagnostics > Verbose Gameplay Logs      <- toggle; off by default
```

The house scene can also be rebuilt and saved without opening the editor:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod AshenWoods.EditorTools.Stage7FlowBuilder.BuildHouseInBatchMode
```

Each item is **additive and idempotent**: existing objects are reused, only missing pieces
are added, and references are re-pointed on every run. Hand-placed objects survive.
Later stages also call the earlier ones, so running the newest item is enough on a scene
built from Stage 1.

Two level scenes exist:

- `Assets/Scenes/AshWoods_House.unity` - the rebuilt two-storey house. Open it and press
  Play. `Build Or Update The House` builds it if needed, then runs Stages 2-6 and moves
  their doors, key, breaker, crowbar, escape frame, enemy, wardrobes and waypoints onto the
  house anchors. The menu tree is `Level / Stages / Legacy Playground / Tuning /
  Diagnostics`.
- `Assets/Scenes/AshWoods_Stage1_Playground.unity` - the original single-storey playground
  plus the east wing, kept working for comparison. Its items live under `Legacy Playground`:
  `Create Playground Scene`, `Add Missing Pieces To Open Scene`, `Build The East Wing`.

Running a `Stages` item in the house scene only adds that stage's objects and re-points
them; it never drops the old room, corridor or wing into the new building.

**Save the scene (Ctrl+S) after running a menu item.** The tools mark the scene dirty but
never save it for you.

## The escape chain

Stage 6 adds the prototype objective chain from the brief, in order:

1. **Find the iron key** - under the crouch tunnel in the west corner (Stage 2).
2. **Unlock the closet** - the locked door in the north-east corner (Stage 2).
3. **Take the crowbar** - inside the closet, a second `KeyPickup` with id `crowbar`.
4. **Restore the breaker** - three switches on the panel on the north wall. The panel is
   screwed shut until the crowbar is carried, and flipping switches is loud enough to pull
   the enemy in.
5. **Pry open the front door** - the panel at the far end of the corridor, behind the spawn
   point. Its latch is dead until the breaker is live, so the exit cannot be used first.

The chain lives in one list on `ObjectiveManager`; steps are watched by the manager itself
(key held, door unlocked, puzzle solved, exit used) so no pickup or door knows about
objectives. The HUD line is `ObjectiveUI`, and the objective text is the only place the
wording is written.

```
Ashen Woods > Stage 6 - Rewrite Default Objective Chain
Ashen Woods > Diagnostics/Log Objective Chain
```

The first replaces the list with the five default steps (destructive - it discards your
wording). The second prints the chain with each step's state and its wired target, which
is the quickest way to spot a step that can never complete.

The breaker panel is a `PuzzleController` over a group of `PuzzleSwitch` components, so a
second puzzle (a valve wheel, an ordered set of levers) can reuse the switches and only
need a new controller. `EscapeExit` is a dedicated interactable rather than a `Door`
because the corridor's end wall is solid: the panel is the placeholder for a doorway that
will be cut into a real level later.

## Stage 7 - endings and atmosphere

- **Endings.** `GameFlowController` (on `UI/FlowCanvas`) fades in at the start. Being caught
  turns the screen red and shows *You were caught* (Try Again reloads the scene); forcing
  the front door fades to black and shows *You escaped* with the time taken. The pause menu
  is locked while an end screen is up.
- **Darkness.** `HousePower` keeps every lamp at a faint, flickering glow and the ambient
  light near black until the breaker puzzle is solved, then stutters the lamps back on.
- **Flashlight.** F toggles a spot light on the camera (`PlayerFlashlight`). It drains in
  about three minutes, flickers below 15 % and dies at 0. Three `BatteryPickup`s in the
  house (kitchen counter, bedroom bed, bathroom floor) add 45 % each. Battery bar bottom-left.
- **Sound.** Recorded CC0 clips by Kenney (Impact Sounds, RPG Audio; licence in
  `Assets/Audio/KENNEY_LICENSE.txt`) live in `Assets/Audio/Resources/Sounds/<SoundName>/`:
  wooden footsteps, enemy steps, door creaks, cloth, metal, glass and tin. `ProceduralAudio.Get`
  picks a random variant from that folder and falls back to the synthesised placeholder for
  anything without recordings (heartbeat, sting, notice, power-on, death hit). Drop more
  clips into a folder to add variants. The whole mix was turned down (heartbeat 0.9 -> 0.3,
  softer and only above 30 % danger); Stage 7 moves existing scene values that still hold
  the old defaults.
- **Sound (original note).** All sounds were synthesised in code (`ProceduralAudio`), no audio files.
  `NoiseSoundPlayer` plays every NoiseSystem event as a 3D sound, so the player hears exactly
  what the enemy hears. `EnemyFootstepAudio` gives the enemy heavy steps audible through walls.
- **Threat cues.** `ThreatFeedback` darkens the screen edges as an enemy gets close or starts
  noticing you, turns them red in a chase, fades in a heartbeat that speeds up with danger,
  plays a rising breath the moment an enemy starts noticing you and a sting when a chase starts.
- **No safe wardrobe in plain sight.** Hiding while being chased, or while an enemy is
  looking straight at you, no longer works: it walks to the wardrobe and drags you out.
- **Quiet Console.** Gameplay trace logs go through `GameLog` and are off by default; turn
  them on with *Diagnostics > Verbose Gameplay Logs*. Warnings and errors always show.

## Stage 8 - integration pass (in progress)

Found by driving Play mode in the house scene from the assistant (Unity MCP), with a probe
logging the enemy's position and patrol index every second:

- **Spawn catch in 15 s.** The patrol loop ran study -> living room -> foyer centre, which is
  0.2 m from the player start. The loop now runs upstairs first (study, bathroom, landing,
  bedroom) and reaches the foyer last; an idle player is found after about 45 s instead.
- **Bed in the bedroom doorway.** The bed filled the landing doorway down to a 0.7 m gap, so
  the enemy jammed there and never patrolled the bedroom. It now stands against the north
  wall (moved once, only if it is still where the builder first put it).
- **Shortcuts across the stairwell.** Route shortening accepted straight lines that crossed
  open air (top of the stairs straight to the pantry), and the enemy jammed on the ramp edge.
  Shortcuts and last legs now need floor under the whole line (`HasFloorAlong`).
- **Doors on patrol.** The enemy opens closed, unlocked doors on patrol too, not only while
  chasing/searching, so a door shut across its loop no longer stalls it.
- The full objective chain (key -> closet -> crowbar -> three switches -> front door) was
  played through by calling each interactable in order and ends in the escape screen.
- **Noises on the wrong floor.** A destination is now snapped to the floor under it, and the
  nearest-node fallback weighs height four times, so a noise in the foyer no longer sends
  the enemy to the bedroom right above it.

### Throwable distractions

`Stage 8 - Throwable Distractions` (also run by every earlier stage item and by *Build Or
Update The House*) adds:

- **Five glass bottles** (foyer sideboard next to the start, living-room crate, kitchen
  counter, bathroom floor, study by the breaker) and **two tin cans** (hall, bedroom).
- **E** picks one up into the left hand (one at a time). **Hold RMB or G** to wind up -
  a tap is a gentle lob (5 m/s), 0.7 s is a full throw (14 m/s) - and release to throw.
  Not possible from inside a hiding place.
- A bottle that lands hard (>= 4 m/s) **shatters**: loudness 0.75, about 12 m in the open,
  plus glass flecks. A tin can **clatters** (0.55, about 9 m) and can be picked up again.
  A soft landing is only 0.18.
- The enemy hears the landing like any other noise and walks over to **investigate** that
  spot. It cannot be distracted mid-chase.
- HUD line above the battery bar: `RMB / G  THROW BOTTLE`, with a charge bar while winding up.

Runtime pieces: `ThrowableItem` (+ `ShardBurst`) in Interaction, `IThrowableHolder`, `PlayerThrower`
on the player, `ThrowableUI`, `NoiseCategory.Impact`, and the `Shatter` / `Clatter` sounds.

Checked in Play mode by script: pickup, "hands full" prompt, HUD text, a full-charge bottle
shattering 0.2 s after release, the enemy investigating from the floor above, and a can
landing 6.5 m away and being picked up again. Not checked by hand with mouse and keyboard.

## Using your own enemy model

1. Put the model (and its animation files, e.g. Mixamo `.fbx` clips) in one folder, for
   example `Assets/Models/Enemy/`.
2. Open `Assets/Prefabs/Enemies/Enemy_Patroller.prefab`, delete `Visuals/Body` and
   `Visuals/Face`, and drag the model under `Visuals`. Feet at y = 0, about 1.8 m tall,
   facing +Z (blue arrow). Move the `Eye` child to head height (~1.6 m).
3. Run **Ashen Woods > Enemy > Set Up Enemy Animations**. It adds an Animator (with the
   model's Avatar), removes colliders from the model, matches clips by name (*idle*,
   *walk*, *run*, *attack/grab/catch/scream*), ticks Loop Time on the movement clips,
   builds `Assets/Animations/Enemy/Enemy.controller` (Speed blend tree 0 / 1.5 / 3.3 plus a
   Caught state), adds `EnemyAnimator` and turns off the placeholder colour tint. A dialog
   lists what was matched and warns if the model's height or feet are off.
4. Any clip it did not match: drag it into the controller's Locomotion blend tree or the
   Caught state by hand. Running the menu again only fills empty slots.

`EnemyAnimator` writes `Speed` (m/s), `State` (0 Patrol, 1 Investigate, 2 Chase, 3 Search,
4 Caught), `Chasing` and the `Caught` trigger; parameters the controller lacks are skipped,
so a hand-made controller works too. Root motion is always off - the AI moves the body.

## Controls

| Key | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look |
| Shift | Sprint (loud) |
| C | Crouch (quiet) |
| Space | Jump |
| E | Use / pick up / hide / leave hiding place |
| F | Flashlight on / off |
| RMB / G (hold, release) | Throw the bottle or can in your hand |
| R | Restart (on the death and completed screens) |
| Esc | Pause menu |
| Tab | Satchel |

## Level layout

### The rebuilt house - `Assets/Scenes/AshWoods_House.unity`

Layout v2: an original 24 x 16 m, two-storey house with sixteen rooms and two staircases
(ground floor top y = 0, first floor top y = 3.2, ceiling y = 6.0). The plan is two letter
maps on a 2 m grid in `HouseBuilder.Layout.cs` - north is the top row, the front door is in
the south wall:

```
GROUND             FIRST FLOOR
KKKKHHDDDDUU       MMMMNNRRRRAA     K kitchen  P pantry (locked: crowbar)  H back hall
KKKKH+DDDDUU       MMMMNhRRRRAA     + back stair  D dining  U utility  C corridor
KKKKH+DDDDUU       MMMMNhRRRRAA     L living (crouch tunnel: iron key)  F foyer (start, exit)
PPCCCCCCCCCC       EEEEEEEEEEEE     * main stair  B library  T toilet
LLLLLFF*BBBB       GGGGOOOhQQQQ     M master bedroom (wardrobe)  N linen  R study (breaker)
LLLLLFF*BBBB       GGGGOOOhQQQQ     A storeroom (enemy wakes)  E upper corridor  h stairwell
LLLLLFFFBBTT       GGGGOOOOQQWW     G guest bedroom (wardrobe)  O playroom  Q bathroom
LLLLLFFFBBTT       GGGGOOOOQQWW     W small room
```

- Walls are generated on every boundary between two rooms; doorways (1.6 m, 2.1 m tall),
  the pantry door, the living-room door and the 1.5 m front-door hole are cut out of them.
- Both stairs land on the upper corridor, so each floor is a loop with two ways between
  floors. A stair is a 38.7 degree ramp collider with drawn treads, walled in on the ground
  floor so nothing can walk under it, and its openings run to the ceiling.
- Navigation: one node per walkable cell (191 with the ramp points), and an 18-point patrol
  that sweeps the upper floor first, comes down the main stair into the foyer about 80 s in,
  circles the ground floor and climbs the back stair (a lap is about three minutes).
- **Dressing:** worn wallpaper, old plank floors, plaster ceilings, tiled kitchen, toilet
  and bathroom, rugs, and ~55 pieces of CC0 Poly Haven furniture and decor (sofa, rocking
  chair, grandfather clock, ornate mirror, gothic cabinets, dining set, desks, shelves,
  crates, pictures, oil lamps). Wardrobes have open doors and a cornice; hiding beds have
  posts, a footboard, a pillow and a blanket. All of it is built by `HouseBuilder.Dressing.cs`.
- **Hiding places (11):** wardrobes in the master and guest bedrooms (Stage 3) plus the
  kitchen, utility room, foyer (next to the exit), library, study and small room, and three
  beds to hide under (master, guest, playroom). Under a bed the camera drops to 0.12 m and
  you look out between the legs; *E* crawls back out. A bed is a `HidingSpot` with an
  `eyeHeight`, so any new hiding prop only needs that field. Hiding while the enemy watches
  still gets you dragged out - from under a bed too.
- A scene that still has the first 12 x 8 m house is upgraded by *Build Or Update The House*:
  the old House geometry, spare batteries, throwables, nodes and patrol points are replaced;
  doors, key, crowbar, breaker, wardrobes, enemy, exit and player move to the new anchors.

### The original playground (legacy)

The playground scene is the original stage room plus an east wing of five spaces:

| Space | Interior | Entered from | Notes |
| --- | --- | --- | --- |
| Original room | 10 x 8 m | Corridor | Enemy start, locked closet, crouch tunnel, iron key |
| Corridor | 1.6 m wide | Room doorway | Its dead end now opens into the wing |
| Boot room | 3.6 x 4.6 m | Hall, west | **Player start**; no patrol point, so it stays the quiet pocket |
| Hall | 4.4 x 4.6 m | Corridor, boot room, gallery, kitchen | Junction, 3.0 m ceiling |
| Kitchen | 4 x 4 m | Hall doorway + 1.05 m crawl hole | Counters and an island break the sightline |
| Gallery | 6.8 x 3.4 m | Hall + back passage | Long room, two shelves, a wardrobe |
| Passage | 2 x 4 m | Gallery + kitchen | Narrow back route that closes the loop |

The hall -> kitchen -> passage -> gallery -> hall loop is deliberate: the enemy can circle
instead of queueing in a corridor, and there is always a second way out. The crawl hole
between the hall and the kitchen is 1.05 m high, so a crouching player fits through it and
the enemy does not.

The front door of the house is in the gallery's far wall, so the corridor is now an inner
hall rather than the entrance. Stage 6's escape exit is built there; a front door left over
from the old layout is moved there once, which is why the corridor dead end can be opened
into the wing without walling the old room off.

### Why a run cannot end at the start

- The player spawns in the boot room, behind two walls, facing its only doorway, instead of
  standing in the corridor with a clear sightline to the enemy.
- The enemy starts in the original room, about 13.5 m away through three walls.
- Nothing on the enemy's patrol route enters the boot room; it only comes in while chasing
  or while investigating a noise made there.
- `EnemyBrain > Start > Wake Delay Seconds` (default 8) keeps sight and hearing off at the
  beginning of a run while the enemy carries on patrolling. Set it to 0 in the Inspector
  for a merciless start.

## Sound tuning

The Stage 5 numbers are chosen against this level's real distances: the room interior is
10 x 8 m (12.8 m diagonal), the corridor mouth is 11.7 m from the closet door and the enemy
spawn is 10.1 m from that mouth. The wing's longest open run is the 6.8 m gallery, so the
same radii cover it without retuning. With a 16 m maximum noise radius and 14 m of hearing:

| Source | Loudness | Open air | Behind one wall |
| --- | --- | --- | --- |
| Crouch step | 0.16 | 2.6 m | 1.9 m |
| Walk step | 0.56 | 9.0 m | 6.7 m |
| Sprint step | 1.00 | 14 m (capped) | 10.5 m |
| Landing | 0.80 | 12.8 m | 9.6 m |
| Door | 0.69 | 11.0 m | 8.3 m |
| Key pickup | 0.31 | 5.0 m | 3.7 m |
| Hiding in / out | 0.25 / 0.19 | 4.0 / 3.0 m | 3.0 / 2.3 m |

Applying them to a scene is explicit, so re-running a stage builder never overwrites your
own tweaks:

```
Ashen Woods > Stage 5 - Apply Tuned Stealth Values
Ashen Woods > Diagnostics/Log Stealth Ranges
```

The second item prints the table above from the scene's live values and the largest gap
between two waypoints, so a mismatch between the numbers and the layout is visible at once.

## Repository layout

```
Assets/Scripts/
  Editor/           Stage builders (one per stage) + HouseBuilder (two-storey level)
                    + shared helpers in Stage1PlaygroundBuilder
  Player/           Input, movement, look, key holder, hiding, noise bridge, enemy target
  Enemy/            Vision, hearing, brain (state machine), navigation, state colours
  Interaction/      IInteractable, PlayerInteractor, Door, KeyPickup, HidingSpot,
                    PuzzleController + PuzzleSwitch, EscapeExit
  World/            NoiseSystem + NoiseEvent (the gameplay sound bus)
  UI/               Interaction prompt, satchel list, objective line
  GameManagement/   Pause/menu controller, objective chain
Assets/Prefabs/     Player, Enemy, Doors, HidingSpots (all built by the stage tools)
Assets/Materials/   Flat placeholder colours
```

See `AGENTS.md` for the conventions future contributors (human or AI) should follow.

## Verification status

The gameplay has been compiled and reasoned about, the house plan was checked offline
(every box against every other for overlaps, and every waypoint pair against the real link
rule: 3 heights x 3 lateral offsets, max 4.2 m), and the waypoint graph was simulated
offline, but **it has not been played in the Unity editor by the author of the scripts**.
That offline check reports no overlapping boxes except the stair ramp's low end, which is
deliberately sunk into the floor slab.
Every stage is expected to be play-tested by the project owner; the Editor steps in each
stage's write-up say exactly what to look for.

---

## Design brief (original request)

You are an experienced Unity gameplay programmer specialising in first-person survival
horror. Build **Ashen Woods**, a complete, playable 3D survival horror prototype inspired
by the *gameplay mechanics* of Granny: first-person exploration, stealth, enemy AI, hiding,
sound detection, locked doors, collectible items, puzzles, inventory and an escape
objective. This is an original project — no copying of characters, story, maps, names,
assets or exact level design. Systems must be original and modular.

### 1. Core philosophy

Build a functional game first, improve the visuals later. Use only built-in Unity
primitives and simple materials. No external assets, textures, paid packages or complex
shaders. The prototype must be playable from beginning to end with simple geometry:

- Capsule for the player; capsule or cube for the enemy.
- Cubes for walls, floors, ceilings, furniture, shelves, cabinets.
- Cubes or planes for doors; small cubes/spheres/cylinders for items.
- Basic solid-colour materials so object types are distinguishable.

Every placeholder must be easy to replace with a real model later.

### 2. Project requirements

Target the existing Unity project and version; inspect it before changing anything. Verify
that packages and input systems exist instead of assuming. Do not delete files or overwrite
working systems without a clear reason. Prefer readable code over abstractions. Implement
in small, testable stages.

### 3. Player controller

Capsule body, WASD, mouse look, walk/sprint/crouch, gravity and jumping where appropriate,
collision with walls and furniture, adjustable speed and sensitivity, crouch head height,
footstep sound-event hooks, camera-raycast interaction, on-screen prompt, pause menu with
resume and quit. Separate camera at head height; the camera must not rotate independently
of movement in unintended ways. The player must not pass through walls, doors or furniture.

### 4. Interaction system

Reusable architecture: look at an object, press E. Every interactable exposes a consistent
interface. Support doors (open/close/lock/unlock), item pickup, containers, hiding in
wardrobes or under beds, switches, puzzle objects, notes. Show contextual prompts
("Press E to open"). Object behaviour lives in the object's component, never hardcoded in
the player controller.

### 5. Enemy AI

States: **Patrol** between waypoints, **Investigate** a suspicious sound, **Search** where
the player was last detected, **Chase** on visual detection, **Attack** on contact, **Return**
to patrol. Perception: configurable detection distance, field of view, line-of-sight
raycasts blocked by walls and closed doors, hearing events from player actions, different
radii for walking, sprinting, crouching and dropped objects. The enemy must not be
omniscient and must not know the player's position through walls. Speeds, hearing range,
vision range, attack distance and search duration are inspector-configurable. Use NavMesh
if the project supports it, otherwise implement and document the alternative.

### 6. Sound and stealth

A centralised sound-event system. Events carry world position, category, configurable
radius/loudness and an optional source. Walking is quiet, sprinting loud, dropped objects
noticeable, noisy doors may attract, puzzle objects may emit. The enemy investigates only
sounds inside its hearing range. Provide Scene-view debug visualisation of sound radii and
detection ranges. Real audio clips are not required; gameplay sound events must work
independently of audio.

### 7. Doors, locks and keys

Reusable door component: open/close through interaction, smooth rotation or another
reliable animation, locked/unlocked state, optional key or special item, optional noise,
configurable direction and interaction distance. A locked door must not open until its
conditions are met. Requirements are configured by identifier or reference, never by
hardcoded names or positions.

### 8. Inventory and items

Basic inventory that supports keys, tools, puzzle components, quest items and carryable
objects. Start with text UI rather than a complex interface. Every item has a stable
identifier, display name, description and gameplay properties; reusable definitions
(ScriptableObjects) where appropriate. Requirements use configurable identifiers, not
fragile name comparisons.

### 9. Hiding

The player can hide in wardrobes, lockers or under beds. While hidden: no normal movement,
camera moves to a defined position, a designated key exits, visual detection fails when the
spot conceals them, and enemy search still behaves naturally around the hiding place. Hiding
spots are reusable components attachable to placeholder objects.

### 10. Puzzles and escape

A simple but complete objective chain: explore, find a key or tool, unlock a room or
container, obtain another item, solve a simple puzzle or mechanism, unlock the final exit,
reach the escape trigger. Implement it as a configurable prototype objective, not a fixed
story, with an objective manager that tracks progression and shows the current objective.
The escape must be achievable in an actual playthrough.

### 11. Placeholder environment

A small, complete test house: several connected rooms, corridors, walls/floors/ceilings,
doors and locked doors, cube furniture, a wardrobe or closet hiding spot, a bed or second
hiding place, shelves and containers, item spawn points, enemy patrol waypoints, a start
point and an escape point. 1 unit = 1 metre, consistent scale, basic colours per category.
Compact, not a large map.

### 12. UI

Minimal functional UI: crosshair, interaction prompt, inventory, current objective, pause
menu, caught/death screen, game-completed screen. All of it must reflect real gameplay
state, never decorative text.

### 13. Death, restart and game state

A central game-state manager supporting Playing, Paused, Player caught, Game completed.
Being caught stops normal gameplay and shows the failure screen. Restart must reset player,
enemy, doors, item placements, inventory and objective progress without leaving duplicated
managers or persistent objects behind.

### 14. Modular architecture

Gameplay logic must never depend on placeholder visuals. The door script must not require a
cube, the AI must not require a capsule, the inventory must not know how items are
rendered, the hiding system must not require a specific wardrobe model, the escape system
must not require a specific door mesh. Use clear hierarchies (Door → InteractionPoint,
Visuals, Collider, DoorLogic; Enemy → Visuals, DetectionOrigin, NavigationAgent, EnemyAI;
Player → CameraRoot, MainCamera, InteractionOrigin, PlayerController, Inventory),
serialized references, interfaces and configurable parameters. Replacing a placeholder
should mean swapping the Visuals child only.

### 15. Code quality

Folders: Player, Enemy, Interaction, Inventory, Items, World, UI, GameManagement.
Descriptive names. No giant god scripts, unnecessary singletons, hidden dependencies,
duplicated interaction code, scattered magic numbers, empty methods presented as finished,
fake implementations, or unverified package assumptions. Git-friendly, minimal renames and
moves. Explain dependencies between scripts.

### 16. Workflow

Incremental stages, with after each stage: what was implemented, files and scene objects
changed, exact editor setup steps, inspector references to assign, how to test, and compile
errors resolved before moving on. Never claim a feature works without checking the
implementation and giving a practical test procedure.

### 17. Definition of done

Start the game, explore in first person, interact with doors and objects, collect and use
items, unlock doors, hide from the enemy, attract the enemy with noise, be detected by
sight or hearing, be chased and caught, restart after being caught, complete the objective
chain and escape — all in one playable scene.
