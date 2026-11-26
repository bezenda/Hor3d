using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshenWoods.GameManagement
{
    /// <summary>Moving between the title screen and the level, with time and sound put back to normal.</summary>
    public static class SceneFlow
    {
        public const string MenuScene = "AshWoods_Menu";

        /// <summary>True when the title screen is in the build settings (it is the first scene of a build).</summary>
        public static bool HasMainMenu => Application.CanStreamedLevelBeLoaded(MenuScene);

        /// <summary>
        /// Back to the title screen. The checkpoint is kept, so the title screen can offer Continue - unless the
        /// run is over (escaped), then the next run starts fresh.
        /// </summary>
        public static void LoadMainMenu(bool keepCheckpoint)
        {
            if (!keepCheckpoint)
            {
                CheckpointSystem.Clear();
            }

            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(MenuScene);
        }
    }
}
