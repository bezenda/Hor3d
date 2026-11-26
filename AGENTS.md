# Working notes for assistants

`README.md` holds the design brief and stage table (long - open it only when the task needs
it). This file is the practical side: layout, verification, and what has bitten before.

## Ground rules

- **Art: any lawfully usable licence (owner, 2026-11-11).** This is a personal project that will not be
  redistributed, so free assets under any licence the law allows are fine (CC0, CC-BY, CC-BY-NC, free Asset
  Store / Sketchfab downloads, ...); keep their terms (e.g. credit for CC-BY) and log every source with its
  licence next to the asset (`Assets/Audio/CC0_SOURCES.md` for sounds, a sources/licence file per art folder).
  No pirated or ripped content. Already in the project: Poly Haven models/textures in `Assets/Art/PolyHaven/` (licence file there), CC0 recordings
  (Kenney, Freesound, OpenGameArt) in `Assets/Audio/` (sources in `Assets/Audio/CC0_SOURCES.md`) and
  the Inter font in `Assets/Art/Fonts/` (OFL). The owner asked (2026-11-10) for no
  synthesised sounds: a sound without a recording is silent and warns once. The house dressing still
  falls back to textured boxes. The
  owner may drop their own models into `Assets/Models/` (the enemy model goes under the
  enemy prefab's `Visuals`); code must keep working with the placeholder capsule as well.
- **Packages and tools are allowed (owner, 2026-11-11).** Install whatever Unity packages, Blender add-ons
  or tools best reach the goal (e.g. Animation Rigging); when unsure whether something fits, ask first.
  Note each new package in the README/AGENTS so the next session knows it is there.
- **Never overwrite the enemy prefab's visuals.** `Stage3EnemyBuilder` returns an existing
  `Enemy_Patroller.prefab` untouched; keep it that way. `Ashen Woods > Enemy > Set Up Enemy
  Animations` is the only tool that edits it, and only adds Animator/EnemyAnimator wiring.
- **Never depend on a visual.** Gameplay components reference transforms and interfaces, not
  mesh types. The `Visuals` child is always disposable.
- **Hand edits win.** The owner rescales, moves and deletes decor by hand. `HandEditTracker`
  (editor) marks an edited item under `House/Dressing`, `House/Atmosphere`, `Menu_Set` (and the
  menu's camera/lights/canvas) with `HandEdited`, and records deleted items in the scene's
  `HandEdits` ledger (`HandEditLedger`). Any builder that re-fits, re-tunes, re-lays or recreates
  decor must check `HandEditTracker.Protected(t)` / `WasDeleted(parent, name)` first. Builders
  never write through Undo, so their own changes are not mistaken for hand edits. Gameplay pieces
  (doors, keys, player, enemy) are deliberately not tracked.
- **Builders are the only way objects get authored.** Do not ask the user to hand-place
  objects in the editor if a builder can do it. Every builder must stay additive and
  idempotent: look up by name, add only what is missing, re-point references every run.
- **Do not break an earlier stage.** Builders call forward to later stages; when adding a
  stage, chain it into the earlier menu items the same way.
- **Never claim testing that did not happen.** Say what was checked (compile, console, a
  builder run, Play mode through MCP) and hand the user a test procedure for the rest.

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
| - | Two-storey house (layout v2: 24 x 16 m grid plan), stairs, anchor alignment | `HouseBuilder` (+ `.Layout`) |
| 7 | Endings, restart, flashlight, batteries, house power, sound, threat cues | `Stage7FlowBuilder` |
| 8 | Throwable bottles and cans, throwing hand, throw HUD | `Stage8ThrowableBuilder` |
| 9 | Shadowed lamps, torch + spill, post volume, SSAO, mono clips, sound mix | `Stage9PolishBuilder` |
| 10 | Interface style: Inter font, palette, left-column screens, HUD layout, stamina/flash bars, room caption | `Stage10InterfaceBuilder` |
| 11 | Balance pass: stamina, flash (stun), throw arc, room atlas, batteries on tables, caught fall | `Stage11GameplayBuilder` |
| 12 | Atmosphere: dust, fog, grade, chase volume, windows, marks, cobwebs, camera feel, enemy eyes/presence light, flash white-out | `Stage12AtmosphereBuilder` |
| - | Title screen scene `AshWoods_Menu.unity` (primitive mansion, garden, moon), first in build settings | `MenuSceneBuilder` |
| - | Sci-fi facility `AshWoods_Facility.unity` from the SciFi Warehouse Kit (copy of the house scene, house stripped) | `FacilityBuilder` (+ `.Shell/.Catwalks/.Dressing/.Gameplay/.Navigation/.Lab/.Halls/.Storage/.Horror/.Furnish`) |

## Where the rest lives (read only what the task touches)

| File | Read it before touching |
| --- | --- |
| `docs/agents/facility.md` | `FacilityBuilder*`, facility props/Blender scripts, quests, checkpoints, side content, facility AI/sound tuning |
| `docs/agents/interface-text.md` | UI, HUD (eye meter, hiding HUD, tips), notes, Russian text, title screen, enemy model swap |
| `docs/agents/enemy.md` | enemy animation, clips, Mixamo, stun/get-up, voice, motion feel |
| `docs/agents/house-legacy.md` | house scene, legacy playground, Stage 1-12 builders' house-specific rules |

Always-on rules from those files:
- Build list = `AshWoods_Menu` + `AshWoods_Facility`. Stage builders never touch the facility (`IsLevelScene` false);
  `FacilityBuilder` owns it. One-time facility changes are guarded by marker objects (`Name_vN`); delete a marker to re-run.
- **All player-facing text is Russian** (natural, gender-neutral for the player, em dash "—"); title logo stays "Ashen Woods".
  `Editor/RussianText.cs` translates builder literals at the end of every chain; never bulk-replace short literals (ids).
- UI is restyled only in builders (`InterfaceRedesignBuilder`, runs on any scene by name), never by hand. Runtime UIs use `UiTheme`/`HudKit`.
- Controls: WASD, Shift run / hold breath, Ctrl hold crouch, E use, F torch, Space jump, LMB hands/throw, RMB flash, J journal,
  Tab satchel, Esc pause. Older notes naming Q/G/X/C describe the previous layout.
- UI screenshots in Play mode: `HudShot.Take(name)` -> `Temp/Shots/<name>.png`.
- Not in git (public repo): `Assets/Models`, `Assets/Animations` (owner art), `Assets/SciFi Warehouse Kit` (Asset Store EULA,
  re-import from Package Manager > My Assets), `Builds/`, owner-supplied audio of unknown licence (title music `Sounds/MenuMusic`,
  enemy voice takes `Staging/EnemyVoice` + their processed `caught_stalker`/`found_you` wavs, `Audio_Unused/`). `Enemy_Patroller.prefab` and `AshWoods_House.unity` stay uncommitted.

Each builder exposes `EnsureStageNPieces()` and a `MenuItem` that validates the open scene
first. `Stage1PlaygroundBuilder` also holds the shared helpers: `SetObjectReference`,
`SetLayerMask`, `EnsureFolder`, `ScenePath`, `PrefabFolder`.

The menu tree is grouped, and the group is part of the interface - keep it:

```
Ashen Woods / Level              Create Two-Storey House Scene, Build Or Update The House,
                                 Create Main Menu Scene, Bright Editing Light (toggle, Ctrl+Alt+L: unlit
                                 Scene view + hidden never-saved lights, removed for Play)
Ashen Woods / Stages             Stage 2..12 + Rewrite Objective Chain, Interface Redesign (Open Scene)
Ashen Woods / Legacy Playground  Create Playground Scene, Add Missing Pieces, Build The East Wing
Ashen Woods / Enemy              Set Up Enemy Animations (after dropping a model under Visuals), Fix Foot Bones In Avatar
Ashen Woods / Tuning             Apply Tuned Stealth Values
Ashen Woods / Build              Windows Build - Next Patch / Next Minor (versioned folder + zip)
Ashen Woods / Diagnostics        House layout, overlap check, nav graph, stealth, objectives, wing,
                                 Verbose Gameplay Logs (toggle)
```

Two level scenes share one set of stage builders. `HouseBuilder.IsLevelScene()` is the guard
every stage menu uses, and `HouseBuilder.EnsureLevelPieces()` is what they call instead of
`EnsurePlaygroundInActiveScene()`: it returns 0 in the house scene, because the old Stage 1
shell (room, corridor, wing) must never appear inside the new building.

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
- `GameFlowController` owns the run's start and endings. It listens to
  `PlayerEnemyTarget.Caught` and to the objective chain / `EscapeExit.Escaped`, never to the
  enemy. While it is in the scene it sets `PlayerEnemyTarget.RespawnOnCaught = false`; without
  it the old respawn placeholder still works. `IEnemyTarget.IsOutOfPlay` (caught or escaped)
  makes every enemy ignore the player.
- `EnemyBrain.Alertness` (0..1) and its state are all `ThreatFeedback` reads; it never sees
  the player. `IEnemyTarget.ApproachPoint` is where an enemy stands to drag a hidden player
  out (the spot in front of the wardrobe).
- `Acoustics` (World) is the one rule for sound through the house: clear / wall / floor (other
  storey, split at y 2.6). Enemy hearing (3D distance, `floorAttenuation`), the sounds the
  player hears (gain + low-pass) and `ThreatFeedback` proximity all use it. Characters and
  loose props are never obstacles.
- Every sound comes from `SoundBank.Get(GameSound)`: each file in `Resources/Sounds/<GameSound>/`
  is a variant, picked at random. Raw takes live in `Assets/Audio/Staging` and are cut/trimmed/normalised
  by `Ashen Woods > Audio > Prepare Recorded Sounds` (SoundPrep). New `GameSound` values go at the end. Gameplay noises become audible through
  `NoiseSoundPlayer`, which listens to the same `NoiseSystem` event as the enemy's ears.
- `GameMenuController` freezes with `Time.timeScale = 0` and disables input/interaction,
  restoring the *previous* values so pausing during the caught lock-out cannot hand control
  back early.

## Verification

With the editor open, use the Unity MCP tools: `refresh_unity` (compile: request), then
`read_console` filtered to errors; run builders with `execute_menu_item` or `execute_code`.
Read only what you need (`find_in_file`, `count`, filters) - console and scene dumps are big.
With the editor closed, `Stage7FlowBuilder.BuildHouseInBatchMode` compiles and rebuilds.

## Gotchas

- Runtime trace messages use `GameLog.Info`, not `Debug.Log`; they print only while
  `Diagnostics > Verbose Gameplay Logs` is on (stored in PlayerPrefs, read when Play starts).
  Use `Debug.LogWarning`/`LogError` for anything the user must always see.
- `HousePower` scales the lamps it was given; it remembers their authored intensity in
  `Awake`. Edit lamp brightness on the lights, not on `HousePower`. The Stage 7 builder
  re-collects every non-directional light that is not on the player or an enemy.
- Pausing and the end screens set `AudioListener.pause`. A sound that must play over a frozen
  game needs `ignoreListenerPause` (the death sting does).
- Unity *is* installed on the owner's machine (6000.4.4f1). When the editor is closed,
  `Stage7FlowBuilder.BuildHouseInBatchMode` compiles the project and rebuilds and saves the
  house scene from the command line - a real compile check, but still not a playtest.

- **Unity MCP transport.** `.mcp.json` runs `mcpforunityserver` over **stdio**, so the
  editor must be on stdio too (*Window > MCP for Unity*, Transport = Stdio, Start Session;
  bridge on port 6400). Then the `mcp__unityMCP__*` tools work directly. If they report
  "no Unity instances", the editor has been switched to HTTP (EditorPref
  `MCPForUnity.UseHttpTransport`) or the session is stopped. Fallback for an HTTP editor
  session: `Tools/UnityMcp/mcp.ps1` and `run_cs.ps1` talk to `http://127.0.0.1:8080/mcp`. Play mode only ticks while the editor
  is focused unless `Application.runInBackground = true` is set for the session.
- A Rigidbody carried as a child transform (a held throwable) keeps its old physics pose.
  Set `body.position`/`rotation` (and `Physics.SyncTransforms()`) when letting go, or it
  teleports back to where it was picked up.
- Menu items that end in `EditorUtility.DisplayDialog` block the editor until someone clicks
  OK; from automation call the underlying method (e.g. `EnemyModelInstaller.Run`) instead.
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
- `Mathf.SmoothStep(from, to, t)` is an interpolation, not GLSL smoothstep; texture code uses
  `AtmosphereAssets.Step`. Lit transparent materials need `_BlendModePreserveSpecular = 0`, or URP
  premultiplies and a dark stain glows.
- `CameraFeel` (order 900) offsets the camera's local pose; `CaughtCameraFall` (order 1000) owns
  the camera once the run is over. `FlashScreenEffect` dips `AudioListener.volume` and always
  restores it to `GameSettings.Volume`.
- Doors (2026-11-14): `Door` swings away from whoever opens it (`swingAwayFromUser`, the authored `openAngle` sign is
  only the fallback), unless that side's swing is blocked (`SwingBlocked`: leaf box half way and at the end, ignoring
  what it touches when closed, characters and loose rigidbodies). The enemy calls `Door.OpenFrom(position)`.
- Heavy items (2026-11-14): a heavy `KeyPickup` is switched off, not destroyed, and listed in `HeavyCarry` (added to
  the player at run time); X / d-pad down puts the last one down in front (key removed, `PickupLedger.Drop` records
  the pose; a checkpoint reload moves it there, carried ones are switched off and handed back to `HeavyCarry`).
- Title screen settings (2026-11-14): `MenuSceneBuilder.EnsureSettingsPanel` builds `Menu_Canvas/SettingsPanel` with
  the same `Row_*` names (Difficulty + `Stage4MenuBuilder.SettingsRows`) so `SettingsMenu` drives it;
  `MainMenuController` hides `titleOnly` while it is open, Esc closes it. `GameSettings.ApplyView` sets FOV only
  when a `PlayerLook` exists (the menu camera keeps its lens).
- Throws leave from the crosshair line (`PlayerThrower.GetLaunch`), not the carrying hand;
  `ThrowTrajectory` simulates the same launch to draw the arc.

## Player builds (owner, 2026-11-12)

Build only through `Ashen Woods > Build > Windows Build - Next Patch / Next Minor` (`VersionedBuild.Build(minor)`;
from automation call it directly in `execute_code`, ~80 s; `delayCall` never fires while the editor is unfocused). Every build gets a new
Semantic Versioning 2.0.0 number (0.y.z while in development: patch per build, minor when a feature lands),
written to `PlayerSettings.bundleVersion` (the title screen's corner shows it as Application.version), and its own
folder + zip: `Builds/AshenWoods_<version>/Ashen Woods.exe`, `Builds/AshenWoods_<version>.zip` (without `*_DoNotShip`).
Every build ships the change list in Russian AND English (owner, 2026-11-26): `CHANGELOG.txt` (`== Не выпущено ==`)
and `CHANGELOG_EN.txt` (`== Unreleased ==`) in the project root, shipped as `CHANGELOG_RU.txt` / `CHANGELOG_EN.txt`.
They are for players only: short, plain bullets, no technical words. The build refuses if either section is empty,
then renames both to `== <version> - <date> ==` and copies them into the folder and zip.
Windows graphics APIs are **Direct3D11 first, Direct3D12 second** (2026-11-13): a friend's GTX 1650 (driver 516.56)
crashed every run in Unity 6.4's DX12 backend (`D3D12ScratchAllocator::DestroyScratch` <- `GfxDeviceD3D12::QueueExecute`,
null read), in the menu or right after the level loaded. Do not put DX12 back first. To read a player crash: the
friend's `Player.log` + `crash.dmp`; symbolize with dbghelp against
`<Unity>/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_nondevelopment_mono/UnityPlayer_Win64_player_mono_x64.pdb`
(load UnityPlayer.dll at the base address from the log).
Never build into an existing folder: an earlier build
into `AshenWoods_Test` (exe `AshenWoods.exe`) left two exes side by side. The pre-versioning builds were
removed on 2026-11-12; `Builds` holds only versioned folders and their zips.

## Git

Work is pushed by the assistant to `main` (the owner pulls) - but only when the next MINOR or MAJOR version is
ready (owner, 2026-11-26): no commits or pushes for patch builds or work in progress. Commit messages state why the
change was made. Keep the stage numbering in the README table accurate when a stage lands.
