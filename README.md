# Ashen Woods

An original first-person survival horror prototype in Unity, built in small stages.
Placeholder primitives only: no external models, textures or paid packages until the
gameplay is complete.

- **Unity:** 6000.4.4f1
- **Input:** Unity Input System, project set to *Active Input Handling = Both*
- **Navigation:** no NavMesh — a waypoint graph with A* lives in `EnemyNavigation`
- **Scene:** `Assets/Scenes/AshWoods_Stage1_Playground.unity`

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
| 7 | Death screen, restart, game-completed state, full UI | Not started |
| 8 | Integration pass, playtest and fixes | Not started |

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

Ashen Woods > Tuning > Apply Tuned Stealth Values
Ashen Woods > Diagnostics > Log House Layout / Log House Overlap Check
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

## Controls

| Key | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look |
| Shift | Sprint (loud) |
| C | Crouch (quiet) |
| Space | Jump |
| E | Use / pick up / hide / leave hiding place |
| Esc | Pause menu |
| Tab | Satchel |

## Level layout

### The rebuilt house - `Assets/Scenes/AshWoods_House.unity`

Two storeys, eight rooms, one staircase. Ground floor top at y = 0, first floor top at
y = 3.2, ceiling at y = 6.0.

| Floor | Room | Interior | Entered from | Notes |
| --- | --- | --- | --- | --- |
| Ground | Living room | x -6.0..0.2, z -2.0..1.8 | Foyer (door), kitchen | Crouch tunnel with the **iron key**, wardrobe-free cover, breaker is upstairs |
| Ground | Kitchen | x 0.4..6.0, z -2.0..1.8 | Living room, hall | Pantry in the north-east corner (x 4.3..6.0, z -2.0..-0.2) behind the **locked door** and the **crowbar** |
| Ground | Foyer | x -6.0..0.2, z 2.0..6.0 | Front door, living room, hall | **Player start**, the **front door / escape exit** in the south wall |
| Ground | Hall | x 0.4..6.0, z 2.0..6.0 | Foyer, kitchen | Stairs rise east along the south wall |
| First | Study | x -6.0..0.2, z -2.0..1.8 | Bedroom, bathroom | **Enemy start**, **breaker panel** on the north wall, wardrobe |
| First | Bathroom | x 0.4..6.0, z -2.0..1.8 | Study, landing | Empty room, a dead end for the player |
| First | Bedroom | x -6.0..0.2, z 2.0..6.0 | Study, landing | Bed and a wardrobe |
| First | Landing | x 0.4..6.0, z 2.0..6.0 | Bedroom, bathroom, stairs | Stairwell hole x 1.4..5.0, z 5.0..6.0, rails on two sides |

Door openings are 1.6 m wide (1.5 m leaf) and 2.1 m tall, cut out of one wall; the first
floor uses 2.0 m open doorways. The stair is a 41.6 degree ramp collider with its treads
only drawn, so the enemy's height probes pass over it. Both floors form a loop, and the
patrol route crosses the stairs, so hiding upstairs is not automatically safe.

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
