using System;
using UnityEngine;
using UnityEngine.UI;
using AshenWoods.GameManagement;

namespace AshenWoods.UI
{
    /// <summary>
    /// The settings screen: one row per option, each a "‹ value ›" stepper. A change is
    /// applied and saved at once. Rows are found by name under the panel ("Row_Quality",
    /// each with "Value", "Prev" and "Next" children), so the layout lives in the builder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsMenu : MonoBehaviour
    {
        private struct Row
        {
            public string Name;
            public Func<string> Read;
            public Action<int> Step;
        }

        private Row[] rows;
        private bool wired;

        /// <summary>The top row in navigation order: what a screen selects when it opens.</summary>
        public Selectable First { get; private set; }

        private void Awake()
        {
            rows = new[]
            {
                new Row
                {
                    Name = "Difficulty",
                    Read = () => AshenWoods.World.Difficulty.Names[GameSettings.Difficulty],
                    Step = d => { GameSettings.Difficulty = Wrap(GameSettings.Difficulty + d, AshenWoods.World.Difficulty.Names.Length); }
                },
                new Row
                {
                    // Each language is named in itself, so it can be found whichever one is showing.
                    Name = "Language",
                    Read = () => AshenWoods.World.Loc.English ? "English" : "Русский",
                    Step = d => { AshenWoods.World.Loc.Language = AshenWoods.World.Loc.English ? AshenWoods.World.GameLanguage.Russian : AshenWoods.World.GameLanguage.English; }
                },
                new Row
                {
                    Name = "Quality",
                    Read = () => GameSettings.QualityNames[GameSettings.Quality],
                    Step = d => { GameSettings.Quality = Wrap(GameSettings.Quality + d, GameSettings.QualityNames.Length); GameSettings.ApplyQuality(); }
                },
                new Row
                {
                    Name = "FrameLimit",
                    Read = () => GameSettings.FrameLimitName(GameSettings.FrameLimit),
                    Step = d => { GameSettings.FrameLimit = Wrap(GameSettings.FrameLimit + d, GameSettings.FrameLimits.Length); GameSettings.ApplyFrameRate(); }
                },
                new Row
                {
                    Name = "VSync",
                    Read = () => GameSettings.VSync ? World.Loc.Pick("Вкл", "On") : World.Loc.Pick("Выкл", "Off"),
                    Step = d => { GameSettings.VSync = !GameSettings.VSync; GameSettings.ApplyFrameRate(); }
                },
                new Row
                {
                    Name = "Fov",
                    Read = () => GameSettings.FieldOfView + "°",
                    Step = d => { GameSettings.FieldOfView += d * 5; GameSettings.ApplyView(); }
                },
                new Row
                {
                    Name = "Brightness",
                    Read = () => (GameSettings.Brightness > 0 ? "+" : string.Empty) + GameSettings.Brightness,
                    Step = d => { GameSettings.Brightness += d; GameSettings.ApplyView(); }
                },
                new Row
                {
                    Name = "Sensitivity",
                    Read = () => GameSettings.Sensitivity.ToString(),
                    Step = d => { GameSettings.Sensitivity += d; GameSettings.ApplyInput(); }
                },
                new Row
                {
                    Name = "Volume",
                    Read = () => (GameSettings.Volume * 10) + "%",
                    Step = d => { GameSettings.Volume += d; AudioListener.volume = GameSettings.Volume / 10f; }
                }
            };

            Wire();
            BuildNavigation();
        }

        /// <summary>Steps the named row (keyboard left/right or a click on the row) and plays the step tick.</summary>
        public void StepRow(string rowName, int delta)
        {
            foreach (Row row in rows)
            {
                if (row.Name == rowName)
                {
                    Change(row, delta);
                    return;
                }
            }
        }

        /// <summary>
        /// Up/down walks the rows top to bottom and ends on Back; the arrow buttons are mouse-only. Rows the
        /// screen does not have (the pause screen has no difficulty row in older scenes) are skipped.
        /// </summary>
        private void BuildNavigation()
        {
            var order = new System.Collections.Generic.List<Selectable>();
            foreach (Row row in rows)
            {
                Transform rowTransform = transform.Find("Row_" + row.Name);
                Selectable selectable = rowTransform != null ? rowTransform.GetComponent<SettingsRowSelectable>() : null;
                if (selectable != null)
                {
                    order.Add(selectable);
                }

                foreach (string arrow in new[] { "Prev", "Next" })
                {
                    Transform button = rowTransform != null ? rowTransform.Find(arrow) : null;
                    Selectable arrowSelectable = button != null ? button.GetComponent<Selectable>() : null;
                    if (arrowSelectable != null)
                    {
                        arrowSelectable.navigation = new Navigation { mode = Navigation.Mode.None };
                    }
                }
            }

            Transform back = transform.Find("BackButton");
            Selectable backSelectable = back != null ? back.GetComponent<Selectable>() : null;
            if (backSelectable != null)
            {
                order.Add(backSelectable);
            }

            First = order.Count > 0 ? order[0] : null;
            for (int i = 0; i < order.Count; i++)
            {
                order[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = order[(i - 1 + order.Count) % order.Count],
                    selectOnDown = order[(i + 1) % order.Count]
                };
            }
        }

        private void Wire()
        {
            if (wired)
            {
                return;
            }

            wired = true;
            foreach (Row row in rows)
            {
                Transform rowTransform = transform.Find("Row_" + row.Name);
                if (rowTransform == null)
                {
                    continue;
                }

                Row captured = row;
                Hook(rowTransform.Find("Prev"), () => Change(captured, -1));
                Hook(rowTransform.Find("Next"), () => Change(captured, +1));
            }
        }

        private void Change(Row row, int delta)
        {
            row.Step(delta);
            PlayerPrefs.Save();
            UiSound.Step(delta);
            Refresh();
        }

        private void OnEnable()
        {
            AshenWoods.World.Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            AshenWoods.World.Loc.Changed -= Refresh;
        }

        /// <summary>Redraws every value.</summary>
        public void Refresh()
        {
            if (rows == null)
            {
                return;
            }

            foreach (Row row in rows)
            {
                Transform value = transform.Find("Row_" + row.Name + "/Value");
                Text text = value != null ? value.GetComponent<Text>() : null;
                if (text != null)
                {
                    text.text = row.Read();
                }
            }
        }

        private static void Hook(Transform target, UnityEngine.Events.UnityAction action)
        {
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }
    }
}
