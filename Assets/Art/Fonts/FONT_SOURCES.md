# Fonts: sources and licences

All fonts are under the SIL Open Font License 1.1 (licence texts next to this file). Runtime UI code loads
them with `Resources.Load<Font>("Fonts/<name>")`, so they live in `Resources/Fonts`.

| File | Role in the game | Source | Licence |
| --- | --- | --- | --- |
| Inter-Regular/Medium/SemiBold.ttf | body text, buttons, HUD (full Inter 4.1 with Cyrillic, from `extras/ttf` of Inter-4.1.zip, 2026-11-25) | https://github.com/rsms/inter/releases/tag/v4.1 | OFL 1.1 (`LICENSE_Inter_OFL.txt`) |
| AlumniSans.ttf | display titles (variable font; Unity draws its default Thin instance) | https://github.com/googlefonts/alumni (copy shipped with github.com/pbakaus/impeccable, 2026-11-19) | OFL 1.1 (`LICENSE_AlumniSans_OFL.txt`) |
| JetBrainsMono.ttf | digits only: keypad display, codes | https://github.com/JetBrains/JetBrainsMono (copy shipped with github.com/pbakaus/impeccable, 2026-11-19) | OFL 1.1 (`LICENSE_JetBrainsMono_OFL.txt`) |
| Caveat.ttf | handwritten notes (variable font; Unity draws its Regular default) | https://github.com/google/fonts/tree/main/ofl/caveat (2026-11-25) | OFL 1.1 (`LICENSE_Caveat_OFL.txt`) |
| PTSerif-Regular/Bold.ttf | printed memos, manifests, instructions on notes | https://github.com/google/fonts/tree/main/ofl/ptserif (2026-11-25) | OFL 1.1 (`LICENSE_PTSerif_OFL.txt`) |
| PTMono.ttf | typewritten research logs on notes | https://github.com/google/fonts/tree/main/ofl/ptmono (2026-11-25) | OFL 1.1 (`LICENSE_PTMono_OFL.txt`) |
