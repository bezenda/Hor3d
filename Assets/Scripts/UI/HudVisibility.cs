using UnityEngine;

namespace AshenWoods.UI
{
    /// <summary>
    /// Hides the in-play HUD canvases while a menu or end screen is up, so the objective and prompts do not sit on
    /// top of the pause / satchel / death / completed screens. Runtime-built HUD pieces read <see cref="Shown"/>.
    /// </summary>
    public static class HudVisibility
    {
        private static readonly string[] HudCanvases = { "ObjectiveCanvas", "InteractionCanvas", "AtmosphereCanvas" };

        /// <summary>False while a menu or end screen hides the HUD.</summary>
        public static bool Shown { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Shown = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Shown = true;
        }

        public static void Set(bool visible)
        {
            Shown = visible;
            GameObject ui = GameObject.Find("UI");
            if (ui == null)
            {
                return;
            }

            foreach (string name in HudCanvases)
            {
                Transform child = ui.transform.Find(name);
                Canvas canvas = child != null ? child.GetComponent<Canvas>() : null;
                if (canvas != null)
                {
                    canvas.enabled = visible;
                }
            }
        }
    }
}
