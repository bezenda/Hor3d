# Readability pass + Russian-only text (2026-11-25)

## Why
Playtesters do not understand why they die, mostly in lockers: the "hold your breath" hint shows once for a few
seconds, and an enemy that watched the hide catches with no counterplay. The owner wants every existing mechanic to
read at a glance (visual + audio), and all player-facing text in Russian (English removed, not switchable).

## Decisions (owner, 2026-11-25)
- Watched hide: **chance to escape**. Big "Оно видело, где ты!" warning; the enemy stares/screams for a moment before
  coming, so leaving the locker and running is a real option. Staying = dragged out.
- Detection meter: **big eye top-centre + direction arrow near the crosshair** towards the noticing enemy.
- Title stays **Ashen Woods** (Latin logo); everything else Russian.
- Extras: noise meter, first-meeting cards, persistent contextual hints, precise death reasons.

## Design
- **Lockers**: while hidden a panel "Shift — задержать дыхание" + breath bar stays on screen, pulses when an enemy is
  near. Heavy breathing loop (existing Panting recordings) inside; holding breath = silence, louder heartbeat, darker
  vignette; low air = bar turns red; running out = loud gasp (noise, as now).
- **Eye meter** (replaces "It knows where you are"): states from the most alert enemy -
  closed grey (unaware) / half-open yellow "?" filling with sight time or after a noise (Investigate) /
  open orange "?" (Search) / open red "!" pulsing + red edge + sting (Chase) / red with crosshair (knows where you
  are, hiding is useless) / blue crossed with timer (stunned). Caption under it in Russian. Arrow at the crosshair
  points to that enemy while it is suspicious or worse.
- **Noise meter**: small bar by stamina showing the player's last noise loudness (walk/run/crouch/throw).
- **First-meeting cards**: short card once per run on first locker, first enemy sighting, first heavy item, first
  torch flash use.
- **Contextual hints**: persistent while true (hidden, carrying heavy, enemy near while hidden).
- **Death screen**: cause recorded at catch time (watched hide / heard breathing / caught in the open / exhausted / torch).
- **Russian**: full Inter (OFL, with Cyrillic) replaces the Latin subset; all player text in code and builders
  becomes Russian; a one-time scene pass swaps existing scene strings. No localisation layer. AGENTS.md: new text in
  Russian only.

## Non-goals
Language switch, new synthesized sounds, enemy AI rebalancing beyond the watched-hide stare.

## Left to the builder
Exact sizes/colours (UiTheme palette), timings, wording.
