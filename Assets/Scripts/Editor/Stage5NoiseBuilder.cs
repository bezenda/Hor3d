using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.Enemy;
using AshenWoods.Interaction;
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
        private const string NavigationRootName = "Enemy_Navigation";
        private const string NavNodesName = "NavNodes";

        // -------------------------------------------------------------------- tuning
        //
        // Chosen against the level's real distances rather than round numbers:
        //   room interior 10 x 8 m            -> 12.8 m diagonal
        //   corridor mouth to the closet door -> 11.7 m
        //   enemy spawn to corridor mouth     -> 10.1 m
        // Of the 16 m maximum radius that means:
        //   crouch 2.6 m - heard only when the enemy is almost touching
        //   walk   9.0 m - across one room, not the whole house
        //   sprint 16 m  - anywhere indoors, walls permitting
        //   door  11.0 m - across the room, the classic give-away
        //   key    5.0 m - one room at most
        // The enemy hears 14 m in open air; a wall multiplies that by 0.75, so a sprint
        // carries 10.5 m and a walk 6.7 m through a closed partition.

        private const float MaxNoiseRadiusMetres = 16f;
        private const float CrouchLoudness = 0.16f;
        private const float WalkLoudness = 0.56f;
        private const float SprintLoudness = 1f;
        private const float LandingLoudness = 0.8f;
        private const float DoorLoudness = 0.69f;
        private const float KeyLoudness = 0.31f;
        private const float HidingEnterLoudness = 0.25f;
        private const float HidingLeaveLoudness = 0.19f;

        private const float HearingRangeMetres = 14f;
        private const float HearingMinimumLoudness = 0.1f;
        private const float HearingMinimumDistanceMetres = 1.5f;
        private const float WallAttenuation = 0.75f;

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

        [MenuItem("Ashen Woods/Stage 5 - Apply Tuned Stealth Values", false, 7)]
        private static void ApplyTunedStealthValues()
        {
            if (!IsPlaygroundOpen())
            {
                return;
            }

            int changed = ApplyTuning();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Wrote the tuned stealth values to {changed} component(s). " +
                      "Save the scene with Ctrl+S.\n\n" + DescribeStealth());
        }

        [MenuItem("Ashen Woods/Diagnostics/Log Stealth Ranges", false, 91)]
        private static void LogStealthRanges()
        {
            Debug.Log(DescribeStealth());
        }

        private static bool IsPlaygroundOpen()
        {
            if (SceneManager.GetActiveScene().path == Stage1PlaygroundBuilder.ScenePath)
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "Open the playground scene first",
                "This tool only edits:\n" + Stage1PlaygroundBuilder.ScenePath + "\n\n" +
                "Nothing was changed. Open that scene from Assets/Scenes, then run the menu item again.",
                "OK");
            return false;
        }

        /// <summary>Writes every tuned value into the scene. Returns how many components changed.</summary>
        private static int ApplyTuning()
        {
            int changed = 0;

            NoiseSystem noise = Object.FindAnyObjectByType<NoiseSystem>();
            if (noise != null)
            {
                Stage1PlaygroundBuilder.SetFloat(noise, "maxRadius", MaxNoiseRadiusMetres);
                changed++;
            }

            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                Stage1PlaygroundBuilder.SetFloat(motor, "crouchLoudness", CrouchLoudness);
                Stage1PlaygroundBuilder.SetFloat(motor, "walkLoudness", WalkLoudness);
                Stage1PlaygroundBuilder.SetFloat(motor, "sprintLoudness", SprintLoudness);
                Stage1PlaygroundBuilder.SetFloat(motor, "landingLoudness", LandingLoudness);
                changed++;
            }

            foreach (EnemyHearing ear in Object.FindObjectsByType<EnemyHearing>(FindObjectsSortMode.None))
            {
                Stage1PlaygroundBuilder.SetFloat(ear, "hearingRange", HearingRangeMetres);
                Stage1PlaygroundBuilder.SetFloat(ear, "minimumLoudness", HearingMinimumLoudness);
                Stage1PlaygroundBuilder.SetFloat(ear, "minimumDistance", HearingMinimumDistanceMetres);
                Stage1PlaygroundBuilder.SetFloat(ear, "wallAttenuation", WallAttenuation);
                changed++;
            }

            foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                Stage1PlaygroundBuilder.SetFloat(door, "openNoiseLoudness", DoorLoudness);
                changed++;
            }

            foreach (KeyPickup key in Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None))
            {
                Stage1PlaygroundBuilder.SetFloat(key, "pickupNoiseLoudness", KeyLoudness);
                changed++;
            }

            foreach (PlayerHidingController hiding in
                     Object.FindObjectsByType<PlayerHidingController>(FindObjectsSortMode.None))
            {
                Stage1PlaygroundBuilder.SetFloat(hiding, "enterNoiseLoudness", HidingEnterLoudness);
                Stage1PlaygroundBuilder.SetFloat(hiding, "leaveNoiseLoudness", HidingLeaveLoudness);
                changed++;
            }

            return changed;
        }

        /// <summary>
        /// Reads the values back out of the scene and prints the distance every source
        /// carries, so the tuning can be checked against the layout without opening an
        /// Inspector.
        /// </summary>
        private static string DescribeStealth()
        {
            NoiseSystem noise = Object.FindAnyObjectByType<NoiseSystem>();
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            EnemyHearing ears = Object.FindAnyObjectByType<EnemyHearing>();
            Door door = Object.FindAnyObjectByType<Door>();
            KeyPickup key = Object.FindAnyObjectByType<KeyPickup>();
            PlayerHidingController hiding = Object.FindAnyObjectByType<PlayerHidingController>();

            float maxRadius = Stage1PlaygroundBuilder.GetFloat(noise, "maxRadius", MaxNoiseRadiusMetres);
            float crouch = Stage1PlaygroundBuilder.GetFloat(motor, "crouchLoudness", CrouchLoudness);
            float walk = Stage1PlaygroundBuilder.GetFloat(motor, "walkLoudness", WalkLoudness);
            float sprint = Stage1PlaygroundBuilder.GetFloat(motor, "sprintLoudness", SprintLoudness);
            float landing = Stage1PlaygroundBuilder.GetFloat(motor, "landingLoudness", LandingLoudness);
            float doorLoudness = Stage1PlaygroundBuilder.GetFloat(door, "openNoiseLoudness", DoorLoudness);
            float keyLoudness = Stage1PlaygroundBuilder.GetFloat(key, "pickupNoiseLoudness", KeyLoudness);
            float hideIn = Stage1PlaygroundBuilder.GetFloat(hiding, "enterNoiseLoudness", HidingEnterLoudness);
            float hideOut = Stage1PlaygroundBuilder.GetFloat(hiding, "leaveNoiseLoudness", HidingLeaveLoudness);
            float hearingRange = Stage1PlaygroundBuilder.GetFloat(ears, "hearingRange", HearingRangeMetres);
            float attenuation = Stage1PlaygroundBuilder.GetFloat(ears, "wallAttenuation", WallAttenuation);

            StringBuilder text = new StringBuilder();
            text.AppendLine("Stealth ranges in this scene (radius = loudness x max radius, capped by hearing):");
            text.AppendLine(
                $"  max noise radius {maxRadius:0.0} m   enemy hearing {hearingRange:0.0} m   through a wall x{attenuation:0.00}");

            AppendRange(text, "crouch step", crouch, maxRadius, hearingRange, attenuation);
            AppendRange(text, "walk step", walk, maxRadius, hearingRange, attenuation);
            AppendRange(text, "sprint step", sprint, maxRadius, hearingRange, attenuation);
            AppendRange(text, "landing", landing, maxRadius, hearingRange, attenuation);
            AppendRange(text, "door", doorLoudness, maxRadius, hearingRange, attenuation);
            AppendRange(text, "key pickup", keyLoudness, maxRadius, hearingRange, attenuation);
            AppendRange(text, "hide (in)", hideIn, maxRadius, hearingRange, attenuation);
            AppendRange(text, "hide (out)", hideOut, maxRadius, hearingRange, attenuation);
            text.AppendLine($"  furthest two waypoints in the level: {LargestWaypointGap():0.0} m");

            return text.ToString();
        }

        private static void AppendRange(
            StringBuilder text, string label, float loudness, float maxRadius, float hearingRange, float attenuation)
        {
            float radius = Mathf.Clamp01(loudness) * maxRadius;
            float audible = Mathf.Min(radius, hearingRange);

            text.AppendLine(
                $"  {label,-12} loudness {loudness:0.00}   open {audible,5:0.0} m   behind a wall {audible * attenuation,5:0.0} m");
        }

        private static float LargestWaypointGap()
        {
            Transform nodes = GameObject.Find(NavigationRootName)?.transform.Find(NavNodesName);
            if (nodes == null)
            {
                return 0f;
            }

            float largest = 0f;

            for (int i = 0; i < nodes.childCount; i++)
            {
                for (int j = i + 1; j < nodes.childCount; j++)
                {
                    float distance = Vector3.Distance(nodes.GetChild(i).position, nodes.GetChild(j).position);
                    if (distance > largest)
                    {
                        largest = distance;
                    }
                }
            }

            return largest;
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
