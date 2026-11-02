using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.Enemy;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 5 authoring: the sound system, the player's noise bridge, and the enemy's ears.
    ///
    /// Additive and idempotent, like every other stage builder: an existing NoiseSystem is
    /// reused, and components are only added when they are missing. Wiring is refreshed on
    /// every run, so pulling the latest scripts and re-running the menu item repairs
    /// references without touching anything you placed by hand.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 5 - Add Sound And Stealth To Open Scene
    /// </summary>
    public static class Stage5NoiseBuilder
    {
        private const string NoiseSystemName = "NoiseSystem";

        [MenuItem("Ashen Woods/Stage 5 - Add Sound And Stealth To Open Scene", false, 6)]
        private static void AddStage5Noise()
        {
            if (SceneManager.GetActiveScene().path != Stage1PlaygroundBuilder.ScenePath)
            {
                EditorUtility.DisplayDialog(
                    "Open the playground scene first",
                    "This tool only edits:\n" + Stage1PlaygroundBuilder.ScenePath + "\n\n" +
                    "Nothing was changed. Open that scene from Assets/Scenes, then run the menu item again.",
                    "OK");
                return;
            }

            int added = EnsureStage5Pieces();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Added {added} Stage 5 piece(s). Save the scene with Ctrl+S, then press Play. " +
                          "Sprinting should now pull the enemy towards you; crouching should not.");
            }
            else
            {
                Debug.Log("Stage 5 is already complete in this scene - only references were refreshed.");
            }
        }

        /// <summary>
        /// Adds every missing Stage 5 piece and re-points the wiring. Safe to call repeatedly,
        /// from any of the Ashen Woods menu items.
        /// </summary>
        /// <returns>How many objects or components were added.</returns>
        public static int EnsureStage5Pieces()
        {
            int added = 0;
            added += EnsureNoiseSystem();
            added += EnsurePlayerNoise();
            added += EnsureEnemyHearing();
            return added;
        }

        /// <summary>The scene's single sound hub. Everything else only talks to this.</summary>
        private static int EnsureNoiseSystem()
        {
            GameObject existing = GameObject.Find(NoiseSystemName);

            if (existing != null)
            {
                if (existing.GetComponent<NoiseSystem>() != null)
                {
                    return 0;
                }

                existing.AddComponent<NoiseSystem>();
                return 1;
            }

            GameObject system = new GameObject(NoiseSystemName);
            system.AddComponent<NoiseSystem>();
            return 1;
        }

        /// <summary>Bridges PlayerMotor's footstep events into the sound system.</summary>
        private static int EnsurePlayerNoise()
        {
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogWarning("No PlayerMotor in this scene, so the player will make no noise. " +
                                 "Run Stage 1 first.");
                return 0;
            }

            PlayerNoiseEmitter emitter = motor.GetComponent<PlayerNoiseEmitter>();
            bool added = false;

            if (emitter == null)
            {
                emitter = motor.gameObject.AddComponent<PlayerNoiseEmitter>();
                added = true;
            }

            Stage1PlaygroundBuilder.SetObjectReference(emitter, "motor", motor);
            return added ? 1 : 0;
        }

        /// <summary>
        /// Puts ears on every enemy that has a brain. The brain still decides what to do; the
        /// ears only filter what reaches it.
        /// </summary>
        private static int EnsureEnemyHearing()
        {
            EnemyBrain[] brains = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);

            if (brains.Length == 0)
            {
                Debug.LogWarning("No EnemyBrain in this scene, so nothing can hear the player. " +
                                 "Run Stage 3 first.");
                return 0;
            }

            int added = 0;

            foreach (EnemyBrain brain in brains)
            {
                EnemyHearing hearing = brain.GetComponent<EnemyHearing>();
                if (hearing == null)
                {
                    // Adds an override on the enemy prefab instance. Use Prefab > Apply if you
                    // want every future enemy to carry ears by default.
                    hearing = brain.gameObject.AddComponent<EnemyHearing>();
                    added++;
                }

                Stage1PlaygroundBuilder.SetObjectReference(hearing, "brain", brain);

                Transform eye = brain.transform.Find("Eye");
                if (eye != null)
                {
                    Stage1PlaygroundBuilder.SetObjectReference(hearing, "ear", eye);
                }
            }

            return added;
        }
    }
}
