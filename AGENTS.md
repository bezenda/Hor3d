# Working notes for assistants

Read `README.md` first — it holds the original design brief and the stage table. This file
is the practical side: how the codebase is organised, how to verify a change, and what has
bitten previous contributors.

## Ground rules

- **Placeholders only.** Built-in primitives, flat colour materials, no imported assets, no
  new packages.
- **Never depend on a visual.** Gameplay components reference transforms and interfaces, not
  mesh types. The `Visuals` child is always disposable.
- **Builders are the only way objects get authored.** Do not ask the user to hand-place
  objects in the editor if a builder can do it. Every builder must stay additive and
  idempotent: look up by name, add only what is missing, re-point references every run.
- **Do not break an earlier stage.** Builders call forward to later stages; when adding a
  stage, chain it into the earlier menu items the same way.
- **Never claim testing that did not happen.** There is no Unity editor in the cloud VM.
  Report compilation-only verification and hand the user an explicit test procedure.

## Stage map

| Repo stage | What it owns | Builder |
| --- | --- | --- |
| 1 | Scene, player rig, movement, look, input, footsteps | `Stage1PlaygroundBuilder` |
| 1 | East wing rooms, corridor opening, boot-room player start | `Stage1WingBuilder` |
| 2 | Interaction raycast, doors, keys, key holder, prompt UI | `Stage2InteractionBuilder` |
| 3 | Enemy prefab, waypoint graph, vision, patrol/chase/search, hiding spots | `Stage3EnemyBuilder` |
| 4 | Pause menu, satchel, EventSystem, buttons | `Stage4MenuBuilder` |
| 5 | Noise bus, player noise, enemy hearing, Investigate state | `Stage5NoiseBuilder` |
| 6 | Objective chain, breaker puzzle, front-door exit, objective HUD | `Stage6ObjectiveBuilder` |
| 7 | (planned) death, restart, completed screen | — |

Each builder exposes `EnsureStageNPieces()` and a `MenuItem` that validates the open scene
first. `Stage1PlaygroundBuilder` also holds the shared helpers: `SetObjectReference`,
`SetLayerMask`, `EnsureFolder`, `ScenePath`, `PrefabFolder`.

## Architecture at a glance

- `PlayerInteractor` casts a ray and calls `IInteractable.Interact(actor)`. Interfaces only;
  doors, keys and hiding spots implement them.
- `PlayerEnemyTarget` is the *only* file that knows about both the player and the enemy. The
  enemy reads `IEnemyTarget`, never `PlayerMotor`.
- `EnemyBrain` is a state machine over `EnemyVision` (one frame of sight) and
  `EnemyHearing` (one noise). It never moves itself — `EnemyNavigation` does.
- `NoiseSystem` is a scene component with a static `Emit(...)`. Emitters (footsteps, doors,
  key pickups, hiding) call it; listeners subscribe to the instance event. No system means
  no crash: `Emit` returns false and warns once.
- `ObjectiveManager` owns the ordered chain and *polls* the current step (key held, door
  unlocked, puzzle solved, exit used), so pickups and doors know nothing about objectives.
  `Report(id)` exists only for steps nothing can poll, and it refuses an id that is not the
  current step: the chain cannot be finished out of order.
- `PuzzleController` owns the solve rule over a list of `PuzzleSwitch`; a switch only flips
  itself and reports. `EscapeExit` is a separate interactable (not a `Door`) because the
  corridor's end wall is solid; it gates on the puzzle being solved.
- `GameMenuController` freezes with `Time.timeScale = 0` and disables input/interaction,
  restoring the *previous* values so pausing during the caught lock-out cannot hand control
  back early.

## Verification without Unity

`/tmp/check` holds a throwaway .NET harness that compiles the whole `Assets/Scripts` tree
against hand-written Unity stubs. It catches typos, renamed fields and bad API calls — not
behaviour. It is wiped when the VM is restored; rebuild it if needed:

1. Install .NET 8 locally (`/tmp/dotnet`), create a `check.csproj` with `LangVersion 9`,
   `NoWarn CS0169;CS0649;CS0414;CS0067;CS0108;CS0114`, and glob the project's `.cs` files.
2. Add stub files under `/tmp/check` for anything the code uses: core `UnityEngine` types,
   editor APIs, uGUI, `EventSystems`, Input System, `UnityEngine.Rendering`.
3. Build and filter: `dotnet build check.csproj -v q 2>&1 | grep -E "error CS" | sort -u`.
4. When a stub is missing a member, make the stub type `partial` and extend it in a new stub
   file instead of rewriting the existing one.

`write_file` cannot write to `/tmp`; use a terminal heredoc. Commands are capped at 64 KiB.

## Gotchas

- The scene has **no baked NavMesh** and the enemy deliberately does not use one. Waypoints
  are `Enemy_Navigation/NavNodes` children plus `Node_10` on the stair landing.
- `Active Input Handling` is **Both**; buttons therefore rely on `StandaloneInputModule`.
  A switch to Input-System-only would need the module replaced.
- uGUI needs an `EventSystem`; Stage 4 creates one if the scene has none.
- Door hinges rotate only the `Hinge` child; the panel must stay parented to it.
- Hiding keeps the interact target pinned to the spot (`SetTargetOverride`) so E always gets
  the player out.
- Enemy layer checks exclude the player layer so the player's body never breaks the graph.
- Physics queries inside a `CharacterController` capsule ignore the capsule itself; that is
  why sight rays start at the eye, inside the enemy.
- The objective chain is written **once**, when the `ObjectiveManager` list is empty. Later
  builder runs re-point the manager's `keyHolder` but leave the steps alone, so hand-edited
  wording survives. `Stage 6 - Rewrite Default Objective Chain` is the deliberate way to
  throw those edits away.
- Stage 5 numbers are tuned to this level's 12.8 m diagonal (table in `README.md`). Changing
  the constants in code does **not** update existing scene components - serialized values
  win - so use `Stage 5 - Apply Tuned Stealth Values` after editing them, and read the
  result back with `Diagnostics/Log Stealth Ranges`.
- Private serialized fields are written from editor code through `SetFloat` /
  `SetObjectReference` / `SetLayerMask` (SerializedObject). Plain assignment is silently
  discarded by Unity.
- The east wing (`Stage1WingBuilder`) opens the corridor's dead end once, and only while
  `Corridor_Wall_End` still has the position and size the Stage 1 builder gave it. If that
  wall was edited by hand the tool leaves it alone and logs a warning; the wing is built
  either way.
- The front door (`Escape_FrontDoor`, Stage 6) used to sit on that dead end. Opening the
  corridor would put a locked door across the only way to the old room, so the wing moves it
  once to `Stage1WingBuilder.FrontDoorPosition` (the gallery's far wall). Stage 6 reads the
  same constant, so the two files cannot drift apart. A front door anywhere else is never
  moved.
- The player is only moved to the boot-room start while standing on the old corridor spawn
  (1.8, 0.05, 7.2). A hand-placed start point anywhere else is never overwritten.
- `EnemyBrain.wakeDelaySeconds` is the start grace: the enemy patrols, but sight and
  hearing stay off until it runs out. It is the second half of the anti-spawn-kill design -
  keep the boot room's walls, do not rely on the delay alone.
- Wing waypoints are appended after `Node_10` on purpose. Inserting one in the middle would
  leave every saved `Node_N` in an existing scene pointing at the wrong place, because the
  builder only creates missing names and never moves existing ones.

## Git

Work is pushed by the assistant to `main` (the owner pulls). Commit messages state why the
change was made. Keep the stage numbering in the README table accurate when a stage lands.
