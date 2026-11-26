# Interface, title screen, Russian text, HUD

Moved out of AGENTS.md on 2026-11-26. Read before touching UI, HUD, notes, text, the title scene or the enemy model swap.

Interface v2 (2026-11-19), `InterfaceRedesignBuilder` (menu `Stages > Interface Redesign (Open Scene)`; also run at the end of
Stage 10, `MenuSceneBuilder.CreateOrUpdate` and `FacilityBuilder`):
- The facility's UI was a stale copy (Stage 4/7/10 never run there, `IsLevelScene` is false). The redesign works on any scene
  by name: `Menu_Canvas` (title) or the `UI` root (pause, satchel, settings, death, completed, HUD). Restyle there, not by hand.
- Shared runtime look: `UiTheme` (palette, fonts from `Assets/Art/Fonts/Resources/Fonts`: Inter body, Alumni Sans titles - variable
  font, Unity draws its Thin default, use only at 40 px+ - JetBrains Mono digits; sources in `Assets/Art/Fonts/FONT_SOURCES.md`).
  Runtime-built UIs (NoteReader, KeypadUI, HuntedIndicator, FinalChase) use it instead of searching the scene for a font.
- `UiButtonFeel` on every option: hover selects (mouse and keys share one focus), label brightens/steps right, amber leader line,
  sounds. `UiSound` plays `GameSound.UiFocus/UiConfirm/UiBack/UiStep/UiStart/UiPaper` (copies of existing CC0 clips, pitch/volume
  only) over a paused game. Screens pick their first option with `UiButtonFeel.SelectSilently`.
- Settings rows are `SettingsRowSelectable` (left/right steps, up/down moves); `SettingsMenu.First` is the row to select first.
- `SceneFlow.LoadMainMenu(keepCheckpoint)`; the title's Continue appears when `CheckpointSystem.HasCheckpointIn(gameScene)`.
- `HudVisibility` hides Objective/Interaction/Atmosphere canvases while a menu or end screen is up.
- Copy: objectives start with a verb (pause says "You were trying to ..."), `FacilityBuilder.Copy` swaps builder texts only while
  they still equal the old builder text. Death screen advice comes from `GameFlowController.DeathAdvice` (hidden/exhausted/torch).
- Impeccable design skill (github.com/pbakaus/impeccable, v4.10) is in `.claude/skills/impeccable` (+ agents). Its engine binary
  (web-only checks, downloads an exe) was not run; its playbooks were applied by hand. No hook installed.
- Enemy avatar: `EnemyAvatarFix` (Enemy > Fix Foot Bones In Avatar, also run by Set Up Enemy Animations) maps Toes to `ball_l/_r`;
  the auto-mapping had picked `L_SmallToe1`. The model wears high heels and the Mixamo clips are flat-footed - expect some foot
  tilt anyway; a Mixamo-native character avoids retargeting entirely.

Session 2026-11-19 (part 2):
- Build list = Menu + Facility only (house and playground removed from Build Settings; files kept).
- Title screen v2 `MenuSceneBuilder.Station` (marker `Station_v1` switches the mansion parts off, never deletes):
  station + yard + burnt forest + ash + embers + horizon haze. Far sheets use Sprites/Default (fog hides URP Unlit).
  URP punctual lights fall off with distance squared: floodlights need ~380 intensity. `SceneryLight` (World) =
  stutter/beacon/smoulder; the SciFi kit already has a global `LightFlicker`, do not reuse that name.
- Enemy: owner's Sugar model moved to `Assets/Models/Enemy_Sugar`, its prefab instance parked in inactive
  `Visuals_Retired`. Installed `Assets/Models/Enemy/Mixamo_YBot.fbx` (copy of notice.fbx, the Mixamo Y Bot the clips
  were made on) with `EnemyModelInstaller` + `EnemyAnimationSetup`. `EnemyLook` (Enemy > Apply Charred Skin) draws a
  charred skin + ember-crack emission; keep `globalIlluminationFlags = RealtimeEmissive` or URP drops `_EMISSION`.
  A Mixamo "Mutant" swap needs a fresh token (`mixamo-auth`) and the character selected on mixamo.com.
- First minute: `FacilityBuilder.Opening` (marker `Opening_v1`) turns the player to the janitor's desk and adds a
  battery desk lamp (IndependentLight); `IntroCard` shows once per run (`IntroCard.ShownThisRun`, reset by
  `CheckpointSystem.Clear`).
- Russian: done 2026-11-25 as Russian-only text (see the readability pass below the session notes).

Readability pass + Russian only (2026-11-25, design in `docs/superpowers/specs/2026-11-25-readability-russian-design.md`):
- **All player-facing text is Russian; English is gone (owner).** Write every new prompt, note, objective, HUD line in
  Russian. Title logo stays "Ashen Woods"; in-world place name is «Пепельный лес». `Editor/RussianText.cs` holds the
  English -> Russian table for texts builders wrote once into scenes/prefabs; `InterfaceRedesignBuilder.Apply()` runs
  it at the end (every scene chain ends there), so a builder that still writes an English literal is translated
  afterwards. It skips id fields (`*Id`, `id`, `group`, `rowName`, `*Prefix`, `*Token` ...): "torch" is both a key id
  and a display name - never bulk-replace short literals in builder code (object names and ids broke that way once).
  Builder sources: multi-word literals are Russian, short ones still English (the pass covers them).
  The Loc/language-switch plan is dropped.
- Cyrillic: the three Inter faces are the full Inter 4.1 files (Cyrillic included, same GUIDs as the old Latin subset).
  Alumni Sans, JetBrains Mono, Caveat, PT Serif, PT Mono have Cyrillic. Sources in `Assets/Art/Fonts/FONT_SOURCES.md`.
- Runtime HUD pieces build themselves (RuntimeInitializeOnLoad, `HudKit` for canvas/text/procedural sprites; hidden while
  `HudVisibility.Shown` is false or the game is paused):
  `DetectionMeter` (replaces HuntedIndicator): small eye under the crosshair, below the interaction prompt (cluster at
  y -170 from centre), shown only while an enemy is within `showDistance` (18 m) or alert; no direction arrow (it read
  wrongly, removed on the owner's request) - closed grey / yellow "?" filling (seen a bit,
  Investigate) / orange "?" (Search, checking hiding places) / red "!" (Chase) / red + ring + red edge (knows where you
  are) / blue closed + countdown (stunned); caption + urgent line, arrow at the crosshair to that enemy, player noise bar
  (`NoiseSystem` events whose source is on the player; thrown impacts ignored).
  `HidingHud`: while hidden, a panel with status line (near / checking / it hears you / no air / it knows), Shift / E keys,
  air bar; heavy breathing loop (`Panting`), silent + heartbeat + dark edges while the breath is held, gasp when it runs out.
  `HidingHud` panel sits at y 150 from the bottom (the eye cluster is above it); an "Оно слышит" bar shows while an enemy
  listens at the door. Wrapped HUD text goes through `HudKit.Typeset` (no-break space after 1-2 letter words) and tip
  cards size themselves to their text (`preferredHeight`), top edge fixed.
  `GameTips`: first-meeting cards once per run (hiding spot, eye, loud noise, torch, heavy item; reset by
  `CheckpointSystem.Clear`) and a persistent "heavy: no running, X" line.
- Watched hide is no longer a certain catch: `EnemyBrain.seenHideStareSeconds` (2.2) - it screams (`SawTargetHide` ->
  EnemyAnimator notice, EnemyVoice found) and stares before coming; climbing out and running is the counter.
  `EnemyBrain.LastCatchCause` (Open / WatchedHide / HeardBreath / CheckedHide) feeds `GameFlowController.DeathAdvice`.
- Listening at the door (owner: the catch came before the warning could be read): a hunt from heard breathing or a
  random check no longer grabs on arrival. `EnemyBrain.UpdateListening` stops at the door; audible breath fills
  `ListenProgress` over `listenSeconds` (3), held breath lets it ebb and `quietToLeaveSeconds` (2) of silence send it away
  (retreat); full, it reaches for the door (`ReachedForHidingPlace`: creak + heartbeat in HidingHud) and catches after
  `reachSeconds` (1). Watched hides skip this (their chance is the stare).
- Bursting out (owner, 2026-11-25: stepping out used to land the player in its arms): `PlayerHidingController.Leaving`
  (raised before the teleport) -> `PlayerEnemyTarget.OnLeavingHide`: with an enemy `IsAtHidingPlace` and the slam off
  cooldown (`doorBashCooldown` 20 s) it calls `EnemyBrain.Shove` (stands `shoveRecoilSeconds` 1.4, then chases unable to
  catch for `shoveGraceSeconds` 2; body ignores the player), the player exits beside the door (`OverrideExit`, side away
  from it, capsule-checked) facing away, and a loud noise goes out. HidingHud says "E — выбить дверцу и бежать".
- Text pass 2 (owner: "sounds machine-translated", "YOU GOT OUT" left in English): all text rewritten in natural Russian,
  gender-neutral for the player (no "залез/очнулся/прочитал"), em dashes "—". `RussianText.Revisions` maps the first
  Russian wording to the second so saved scenes update on the same pass. Screen titles come from builder code
  (`ScreenTitle`, `UiTheme.Tracked` spaces the letters) - a table of scene values never sees them; translate them in
  the builder. Item prompts are "<Name> — взять" (no Russian cases needed). Pause subtitle = the objective verbatim.
- First minute v2 (`FacilityBuilder.Briefing`, marker `Opening_v2`, delete it to re-run): the janitor's `Laptop` runs on
  its battery (`needsPower` off, screen lit) with an in-world security briefing (how it hunts, how to survive, the three
  things the bay door needs); `InfoPoint.showControls` puts the key legend (`NoteReader.ControlsLegend`) as a game hint
  under the sheet, never in the world text; `InfoPoint.poweredMessage` is added on top once the house is powered. The
  clipboard `Note_Janitor` is the janitor's own note ("ДЛЯ СМЕНЩИКА"). Keypads: a wrong code plays `GameSound.KeypadAlarm`
  (two OpenGameArt CC0 recordings) at the panel, emits a 0.9 noise and locks the panel for `lockoutSeconds` (5);
  `NoiseSoundPlayer` skips keypad noises. The LED is a `LedLens` cube on the face between screen and keys plus the
  `Led` light 1.5 cm in front (range 0.3, intensity 0.05); `Keypad.Update` drives both (red / blinking in the alarm /
  green when open).
- Honest chase (owner, 2026-11-25: players were run down while the HUD said it was only checking a noise):
  `EnemyBrain.HearNoise` treats a noise within `ownNoiseRadius` (1.8 m) of the visible target and within
  `hearChaseDistance` (12 m x difficulty hearing) as the target's own and starts a real Chase (music, red eye); out of
  sight in a chase such noises keep `lastKnownPoint`/`unseenTimer` fresh. Impacts, hiding noises, hidden targets and
  quiet steps while retreating are excluded. `HeardTargetAt` lets `DetectionMeter` say "Слышит тебя — идёт прямо на
  тебя!"; while it can see the player outside a chase the eye says "Видит тебя…" (yellow -> orange with the fill).
- Heavy items drop with physics: `HeavyCarry.Drop` hands the item to `DroppedProp` (added at run time: a solid box
  fitted to the visible meshes next to the trigger, Rigidbody 12 kg, clang noise on the first hard hit, frozen
  kinematic once still for 0.4 s, then `PickupLedger.Drop` records the pose).
- Props pass 2 (`FacilityBuilder.Props2`, marker `Props2_v1`; models `Prop2_*` from `Tools/Blender/facility_props2.py`
  `build_props2()`): fuel cans, sample vials, valve wheels, steam vents, security terminal, laptop, keypad (+ LED moved
  under the key grid), keycard, desk lamp, push pins, generator control box (+ roof status lamp holding its `Led`) and
  starter lever, clipboard. Each model is a `Model2` (pins: `PinModel`) child in the host's frame; primitive renderers
  are switched off, their colliders kept; scaled hosts get an un-scaling child. Prop2 models import without colliders.
  The script works in Unity coordinates (`UB/UC/UT`, `_umatrix`); note the kit's `box()` applies location too
  (transform_apply defaults), so rotations pivot on the geometry centre. Not done: catwalk rails (square bars) and the
  locker linings stay boxes; the kit's exit-sign texture was not checked for English.
- World labels (TextMesh) are translated by `RussianText.WorldLabels` (letters shrink to keep the plate width).
- Props fixes (marker `Props2_v2`, `SetUpProps2Fix`): desk lamp height = laptop pivot + 0.425 (the desk has no
  collider under the lamp's foot - a raycast hit the floor), steam-vent `Tag`/`TagText` moved to the riser's front
  (z 0.028/0.036), their `Steam` particle to the new outlet, every TextMesh set in Inter SemiBold (font + its material).
  `Furn_MetalShelf`/`_B` are rebuilt by `facility_props2.build_shelves2()` (detailed cartons, jugs, tins, binders,
  toolbox; overwrites the FBX from facility_horror.py - do not re-run the old `metal_shelf`).
- `PaperWriting` (UI, self-booting): every sheet note (`InfoPoint.ShowsSheet`, not a screen) gets a world-space canvas
  with its title and first lines in its NoteStyle font and ink, cut at the paper's edge, tokens filled at run time.
- **Controls (owner, 2026-11-25, fewer keys):** WASD, Shift (run / hold breath), Ctrl held = crouch (`CrouchMode.Hold`,
  scene + Player.prefab), E use, F torch, Space jump, LMB = hands (hold/release throws a bottle; press drops a heavy
  can - `HeavyCarry` ignores it while a throwable is held), RMB = torch flash, J journal, Tab satchel, Esc pause.
  G, X, Q, C and LMB-as-interact are gone; the throw button with empty hands no longer flashes. Older notes in this file
  that name Q / G / X / C describe the previous layout. Gamepad bindings are unchanged.
- `FearMeter` bar sits with the status bars (bottom-left, y 252) with the label "Страх — не стой рядом с ним долго".
- Checking screens with the UI in Play mode through MCP: `HudShot.Take(name)` (Editor) renders the overlay canvases via
  the camera into `Temp/Shots/<name>.png` (the MCP screenshot drops overlay UI while the editor is unfocused). Colours
  come out slightly washed (post-processing applies) - judge layout and text, not tone.
- Notes (`NoteReader` + `NotePaper`): `NoteStyle` Handwritten (Caveat, blue ink, yellowed paper, tape, red margin) /
  Memo (PT Serif, letter folds, document header) / Typed (PT Mono, research logs) / Screen (JetBrains Mono green on dark
  glass, scan lines, cursor). `NoteReader.StyleFor(prompt, title)` picks it for an `InfoPoint`; the journal is
  handwritten; the security map uses Memo at a fixed 720 x 880. The sheet is sized to its text (body shrinks a size at a
  time to fit 940 px); the close hint sits under the sheet. Paper textures are procedural, cached per style.

- The menu scene (`MenuSceneBuilder`) builds each `Menu_Set` part once; delete a part to rebuild it.
  Its moon and stars use `Sprites/Default` so URP fog never hides them. Exterior textures are the
  Poly Haven CC0 sets castle_brick_07, roof_slates_02, leafy_grass, gravel_floor, rustic_stone_wall.


English (owner, 2026-11-26; Russian stays the original and the default):
- `World/Loc.cs`: language (PlayerPref `AshenWoods.Language`, 0 RU / 1 EN), `Loc.Changed`, `Loc.Pick(ru, en)` for code
  literals, `Loc.T(ru)` for text that lives in scenes/prefabs (lookup in `Resources/Localization/en.json`, RU -> EN pairs;
  a missing entry shows the Russian).
- Translation happens at the sinks: `TextTokens.Apply` (notes, journal lines, hints - before tokens are filled),
  `PlayerInteractor` (prompts and every feedback message), `ObjectiveManager.CurrentDescription`, `ObjectiveUI` heading,
  `RoomAtlas` names, `NoteReader.Show`, `InventoryUI`/`KeyPickup.TryDisplayName`, intro card. `UI/LocalizedTexts` (auto,
  DontDestroyOnLoad) swaps every static uGUI Text / TextMesh between the two languages by dictionary match.
- New scene/prefab text needs a pair in `en.json`; new code text is written as `Loc.Pick`. Keep tokens/tags identical.
- UI: `LanguageBuilder` (from `InterfaceRedesignBuilder.Apply`) adds `Row_Language` to every SettingsMenu and the
  `LanguageSwitch` (RU / EN frame, top right) to the title screen. World signs on the title screen are world-space
  uGUI Text (TextMesh broke apart when the shared dynamic font atlas was rebuilt).
