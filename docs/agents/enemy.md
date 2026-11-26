# Enemy: animation, voice, stun, motion

Moved out of AGENTS.md on 2026-11-26. Read before touching EnemyAnimator/EnemyBrain/EnemyNavigation, clips or the setup tools.

- `EnemyEyeGlow` parents the glow to the head bone and places it on the rig's eye bones (Humanoid
  eyes, else face bones named "eye", split by side); spacing/forward/up are only the fallback.
- `EnemyVoice` (scene enemy, added by Stage 12) speaks from `EnemyBrain` events only: `EnemyFound`
  when a chase starts (8 s cooldown), `EnemyCaught` on the catch (random variant, ignores the
  listener pause), `EnemyStun` on `EnemyBrain.Stunned` - its own top-priority, mostly flat source
  that ignores the flash's listener-volume muffle (scaled by `GameSettings.Volume` instead).
  Recorded clips: `Assets/Audio/Resources/Sounds/Enemy{Caught,Found,Stun}/`. Drop more
  `EnemyCaught_N.wav` there for more variants. Stunned/Caught clips play at their own speed.
- The stun clip is a fall: the setup tool makes it a one-shot (no loop) with root rotation and Y
  baked into the pose (`PrepareOneShot`), the Stunned state at speed 0.7 without foot IK.
  Unbaked Y, the drop becomes root motion, which the AI-moved enemy discards: it lay in mid-air.
  The fall's XZ is deliberately *not* baked (the get-up's is): baked, the body ended 1.08 m behind
  the root while the get-up starts on it, and the Stunned -> GettingUp blend slid it forward.
- While stunned, `EnemyAnimator` grounds the model: it bakes the body meshes (accessories under
  10 % of the vertices, like the tail, are ignored) every 0.05 s and moves the model so the lowest
  vertex rests on the floor below the enemy. This replaced a head-bone lift that left it hovering.
- A stun (playtest 3) is `dazeSeconds` on the floor (10 s, `Balance_v3`) plus `GetUpSeconds`
  (the get-up clip's length, written to `EnemyAnimator.getUpSeconds` by the setup tool and handed
  to the brain in Awake; 0 for the capsule). Animator: Stunned -> GettingUp (param `GettingUp`)
  -> Locomotion; the old Stunned -> Locomotion exit also needs GettingUp off. While stunned the
  enemy's collider ignores the player's (`Physics.IgnoreCollision`), and after getting up it
  may chase but not catch for `postStunGraceSeconds` (4 s); collision returns after the grace.
- Enemy motion feel (2026-11-11, from a frame-by-frame Play-mode recording): `EnemyNavigation` now ramps
  speed (`acceleration` 7, `deceleration` 11 m/s², brakes for the route's end, coasts to a stop in
  `LateUpdate` when nobody moved it this frame), bends towards the next waypoint inside `cornerBlendDistance`,
  moves at most `maxSlipAngle` (35 deg) off its facing and slows on sharp turns. `TurnTowards` is a
  `SmoothDampAngle` capped by `turnRateLimit` (240 deg/s; the scene's `turnSpeed` 540 whipped it round);
  a second call in the same frame replaces the first. Chase faces the target only within
  `EnemyBrain.faceTargetDistance` (3 m) or with no route left - before, it ran sideways. Climb turns, no snap.
  The setup tool sets the Caught blend to 0.25 s and measures an in-place run clip off the planted feet
  (`InPlaceGroundSpeed`: Mixamo run 3.30 m/s, was guessed 3.6). `EnemyAnimationSetup.Run(out ok)` is the
  dialog-free entry for automation.
- **Mixamo MCP (`Assets/MixamoMcp`, owner, 2026-11-11).** Its `MixamoPostprocessor` re-imports every FBX
  under a path containing "Animation(s)"/"Mixamo" as Humanoid and sets Loop Time from the *clip* name -
  every Mixamo take is "mixamo.com", so idle/walk/run stopped looping and the enemy glided in its last
  frame. `Assets/Scripts/Editor/MixamoLoopFix.cs` (postprocess order 1000) puts back the importer's Loop
  Time, or judges by the file name. Do not edit the vendor folder. The released `mixamo-mcp.exe` crashes
  on start (PyInstaller relative-import bug), so `.mcp.json` runs the server from source with `uvx`
  (pinned commit 6170d5e, plus `--with "mcp<2"`: mcp 2.x dropped `Server.list_tools` and the server died on start; reads the login token from `~/.mixamo_mcp_token`, talks only to mixamo.com).
  Tools: `mixamo-search`, `mixamo-download`, `mixamo-batch`. If an animation "freezes", check
  `clip.isLooping` first.
- Extra enemy clips (Mixamo MCP, 2026-11-11; download with `characterId` 2dee24f8-3b49-48af-b735-c6377509eaac = X Bot,
  one request at a time): `notice.fbx` (scream) and `search_look.fbx` play on the `Upper` layer (mask
  `EnemyUpperBody.mask`, legs stay on locomotion), faded by `EnemyAnimator`: Notice trigger on chase start (8 s
  cooldown), look-around while `Search`. `turn_right.fbx` is the base `Turn` state (bool `Turning`, mirrored by
  `TurnLeft`) when standing and yawing > 60 deg/s. Its yaw is NOT baked into the pose (`PrepareTurnLoop`,
  2026-11-14): the clip turns the body ~123 deg per 0.93 s cycle, and baked in it swung round and snapped back every
  loop. As root motion it is discarded (applyRootMotion off); height/drift stay baked. `climb_ladder.fbx` is not wired.
- Look-around on the spot (patrol pause, investigate, search) is `EnemyBrain.LookAround`: turn 55-110 deg to one
  side, hold 0.8-1.8 s, turn to the other side. The old constant spin (search 65 deg/s > `turnStartRate` 60) kept
  the Turn state on forever.
- Diagnosing animation in Play mode through MCP: read bones in `Application.onBeforeRender`; a skin that
  is off camera does not animate (`CullUpdateTransforms`), so frame the enemy with the main camera.
- `EnemyAnimator` scales playback so feet keep pace (`walkClipSpeed`/`runClipSpeed`; the setup
  tool measures them, an in-place clip keeps the inspector value) and drives `Stunned`.

Catch v2 + tension director (2026-11-26, spec `docs/specs/2026-11-26-catch-and-director-design.md`):
- `CaughtCameraFall` is now the catch scene (2.5 s, timeline constants in code - the old serialized fall values are
  unused): grab to the face (whip with overshoot from behind), hold with struggle + red vignette + heart, cut with
  crunch (GameSound.Crunch + a low DeathHit), procedural blood splats on the lens, black, Ringing. Own overlay canvas at order 29
  (under the flow canvas 30). Warm `CatchFaceLight` under the lens. `CatchesThisRun` (reset in CheckpointSystem.Clear):
  from the 2nd catch any key skips. `GameFlowController` times the death screen from `catchScene.Duration/Elapsed`.
- `IEnemyTarget.NotifyCaught(position, catcher, eyes)`; `EnemyBrain.FacePoint()` = Humanoid head + offset, else
  FlashPoint. A run-ending catch keeps the enemy in `Caught` (it used to walk off after `caughtHoldSeconds` and drag
  the camera's anchor along).
- `EnemyGrab` (runtime, next to EnemyLookAt): IK hands to the player's neck/shoulders (under the view - at the head they
  filled the frame), look-at the camera. EnemyLookAt is off while Caught.
- `EnemyEyeGlow` fallback placement is clamped to the head size (the scene's spacing 0.19 / forward 0.17 were for the
  old model and left the Y Bot's eyes beside its face).
- `TensionDirector` (GameManagement, added by GameFlowController): Calm/BuildUp/Peak/Relief, events never emit a
  NoiseSystem noise. Hooks it uses: `HousePower.Disturb` (+ `Lamps`), `Door.DriftShut`, `EnemyBrain.ProwlTowards` /
  `RequestRetreat`, `AmbiencePlayer.Hush`, `LampSway`, drips (`GameSound.Drip`), `DirectorShade` (stripped copy of the enemy's `Visuals`). F8 in the
  editor forces an event. Checking a catch in Play mode through MCP: the intro card holds timeScale 0, so pause +
  `EditorApplication.Step()` and set `CaughtCameraFall.elapsed` by reflection, then `HudShot.Take`.
- Chase music (2026-11-26): `UI/ChaseMusic` (owned by ThreatFeedback, replaces its single ChaseLoop source) plays each
  `Sounds/ChaseLoop` variant as a whole stereo track (mp3, Streaming): new track per chase, resume within 6 s, crossfade
  1.6 s before `UsableLength` (silent tails cut), low-pass 2.4k -> 22k and gain 0.78 -> 1.12 by `Threat.Closeness(16)`,
  pitch sags to 0.86 while the chase fades. Per-track `TrackGain` evens RMS; a new track needs entries there.
