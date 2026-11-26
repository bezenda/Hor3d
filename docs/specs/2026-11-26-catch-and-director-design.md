# Catch v2 + tension director (2026-11-26)

## Why
The catch is the strongest horror beat and currently the weakest: the camera turns and falls, the enemy is barely
seen. Between encounters nothing happens except far ambience events, so long quiet stretches feel empty.

## Decisions (owner, 2026-11-26)
- Catch: **with a hint** of violence (screen blood + crunch at the cut, nothing shown), **~2.5 s**, skippable from the
  second death of a run.
- Director: **environment and the enemy** (waves: calm -> build-up -> peak -> relief).
- Events: **lights/power, sounds nearby, silhouette, world physics** - all four.

## Catch v2
Timeline (real seconds, `CaughtCameraFall`):
1. 0-0.2 **Grab**: camera snaps to the enemy's eyes and is pulled to ~0.55 m in front of them, strong shake, FOV
   punch narrower. From behind (enemy outside ~100 deg of view) the turn is a forced whip with overshoot.
2. 0.2-1.6 **Face to face**: held close, struggle shake, slow push to ~0.35 m, red-black vignette closing,
   heartbeat racing. The enemy reaches for the camera with both hands and stares into it (`EnemyGrab`, Humanoid IK;
   nothing for the capsule).
3. ~1.65 **Cut**: violent sideways jerk + roll, crunch (DeathHit + low Shatter), blood splats on the "lens",
   hard cut to black; ringing in the ears.
4. ~2.5 death screen. From the second catch of a run any key/click jumps straight to it.
`GameFlowController` takes the death delay from the sequence at run time (no scene value to rewrite). Works from
a hiding spot too (the start pose is inside it: being pulled out reads as being dragged out).

## Tension director (`TensionDirector`, added by GameFlowController)
- Intensity 0..1: rises with chase / being seen / enemy within 10 m / own scares, decays slowly.
- Phases: Calm 70-120 s (no events) -> Build-up 60-100 s (an event every 18-30 s, escalating) -> Peak (one enemy
  is sent prowling to the player's area - stays in Patrol, no "?" on the eye; the room goes quiet as it nears) ->
  Relief 50-80 s (no events; after a hard chase the enemy is asked to back off).
- Silent: first 45 s, during the finale, while chased or an enemy is near and alert, while paused.
- Events (each with a cooldown; silhouette at most 2 per run, never while hidden):
  - Lights: lamps around the player stutter, or go out one by one towards the player and come back stuttering.
  - Sounds: footsteps on the catwalk above / behind, scraping in the duct overhead, something falling behind,
    whispering behind a wall. Plain 3D sounds - never a `NoiseSystem` noise (the enemy must not react to them).
  - Silhouette: a copy of the enemy's look standing still 14-28 m away at the edge of view in the dark; gone when
    looked at, approached, lit by the torch or after ~2.5 s.
  - Physics: an open door drifts shut with a creak, a hanging lamp starts to sway, a bottle/can rolls.
- F8 (editor and development builds) forces the next event, for testing.

## Not covered
Grab clip (the attack clip + IK). Crunch and drip recordings were added the same day (GameSound.Crunch, Drip).
