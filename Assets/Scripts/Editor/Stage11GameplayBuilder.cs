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
    /// Stage 11 authoring: the first playtest's balance pass.
    ///
    ///  - Flashlight flash (FlashlightFlash on the player): Q, or the throw button with an
    ///    empty hand. Costs 30 % battery and stuns the enemy (blind, deaf, rooted) for 3.5 s,
    ///    15 s cooldown.
    ///  - Caught fall (CaughtCameraFall): the view drops to the floor when the enemy catches you.
    ///  - Throw arc (ThrowTrajectory): the predicted flight and landing ring while charging.
    ///  - Room names: a RoomAtlas filled from the house layout (the HUD caption reads it).
    ///  - Once per scene (marker 'Balance_v1'): the enemy chases at 4 m/s (sprint is 4.3 but
    ///    now limited by stamina), the flashlight lasts ~75 s, a battery gives 30 %, and the
    ///    six batteries lie on tables only.
    ///  - Once per scene (marker 'Balance_v2', second playtest): the flash stun values above,
    ///    a black death fade (no red) that waits for the fall, and a softer threat vignette.
    ///
    /// Additive and idempotent like every builder; the one-time values are written once so
    /// later hand tuning survives. Called at the end of Stage 10, so every chain reaches it.
    ///
    /// Menu: Ashen Woods > Stages > Stage 11 - Gameplay Balance
    /// </summary>
    public static class Stage11GameplayBuilder
    {
        private const string MarkerName = "Balance_v1";
        private const string MarkerV2Name = "Balance_v2";
        private const string MarkerV3Name = "Balance_v3";
        private const string ArcMaterialPath = "Assets/Materials/ThrowArc.mat";
        private const string AtlasName = "RoomAtlas";

        private const float ChaseSpeed = 4f;
        private const float FlashlightDrain = 0.0133f;
        private const float BatteryCharge = 0.3f;

        /// <summary>Battery spots on table tops (x, storey floor, z); the height is found by a ray.</summary>
        internal static readonly Vector3[] TableBatterySpots =
        {
            new Vector3(4.5f, 0f, -4.15f),    // dining table
            new Vector3(7.55f, 0f, 1.95f),    // library desk
            new Vector3(-6.05f, 0f, 3.7f),    // living-room coffee table
            new Vector3(2.4f, 3.2f, -7.5f),   // study desk
            new Vector3(-4.45f, 3.2f, 2.3f),  // guest-room desk
            new Vector3(0.2f, 3.2f, 3.05f)    // playroom table
        };

        [MenuItem("Ashen Woods/Stages/Stage 11 - Gameplay Balance", false, 29)]
        private static void AddStage11()
        {
            if (!HouseBuilder.IsLevelScene())
            {
                EditorUtility.DisplayDialog("Open a level scene first",
                    "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                    Stage1PlaygroundBuilder.ScenePath + "\n\nNothing was changed.", "OK");
                return;
            }

            // Through Stage 10, so the HUD bars exist before they are wired.
            int changed = Stage10InterfaceBuilder.EnsureStage10Pieces();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Stage 11: {changed} change(s). Save the scene with Ctrl+S.");
        }

        public static int EnsureStage11Pieces()
        {
            int changed = 0;
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                changed += EnsurePlayerPieces(motor.gameObject);
            }

            if (HouseBuilder.IsHouseScene())
            {
                changed += EnsureRoomAtlas();
            }

            if (!HasMarker(MarkerName))
            {
                changed += ApplyBalance();
                AddMarker(MarkerName);
                changed++;
                Debug.Log("Stage 11: applied the balance pass (marker 'Balance_v1' added).");
            }

            if (!HasMarker(MarkerV2Name))
            {
                changed += ApplyPlaytestTwo();
                AddMarker(MarkerV2Name);
                changed++;
                Debug.Log("Stage 11: applied the second playtest pass (marker 'Balance_v2' added).");
            }

            if (!HasMarker(MarkerV3Name))
            {
                // Third playtest: the flash puts the enemy on the floor for 10 s (it then gets
                // up and gives a 4 s head start - see EnemyBrain).
                foreach (FlashlightFlash flash in Object.FindObjectsByType<FlashlightFlash>(FindObjectsInactive.Include))
                {
                    Stage1PlaygroundBuilder.SetFloat(flash, "dazeSeconds", 10f);
                    changed++;
                }

                AddMarker(MarkerV3Name);
                changed++;
                Debug.Log("Stage 11: flash stun set to 10 s (marker 'Balance_v3' added).");
            }

            // Stage 12 (atmosphere) rides on the end of every chain, like Stage 11 on Stage 10.
            changed += Stage12AtmosphereBuilder.EnsureStage12Pieces();
            return changed;
        }

        // ------------------------------------------------------------------ player

        private static int EnsurePlayerPieces(GameObject player)
        {
            int changed = 0;
            Camera eye = player.GetComponentInChildren<Camera>(true);

            FlashlightFlash flash = player.GetComponent<FlashlightFlash>();
            if (flash == null)
            {
                flash = player.AddComponent<FlashlightFlash>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(flash, "flashlight", player.GetComponent<PlayerFlashlight>());
            Stage1PlaygroundBuilder.SetObjectReference(flash, "input", player.GetComponent<PlayerInputReader>());
            Stage1PlaygroundBuilder.SetObjectReference(flash, "thrower", player.GetComponent<PlayerThrower>());
            Stage1PlaygroundBuilder.SetObjectReference(flash, "hiding", player.GetComponent<PlayerHidingController>());
            Stage1PlaygroundBuilder.SetObjectReference(flash, "interactor", player.GetComponent<PlayerInteractor>());
            if (eye != null)
            {
                Stage1PlaygroundBuilder.SetObjectReference(flash, "aim", eye.transform);
            }

            CaughtCameraFall fall = player.GetComponent<CaughtCameraFall>();
            if (fall == null)
            {
                fall = player.AddComponent<CaughtCameraFall>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(fall, "target", player.GetComponent<PlayerEnemyTarget>());
            if (eye != null)
            {
                Stage1PlaygroundBuilder.SetObjectReference(fall, "view", eye.transform);
            }

            ThrowTrajectory arc = player.GetComponent<ThrowTrajectory>();
            if (arc == null)
            {
                arc = player.AddComponent<ThrowTrajectory>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(arc, "thrower", player.GetComponent<PlayerThrower>());
            Stage1PlaygroundBuilder.SetObjectReference(arc, "lineMaterial", ArcMaterial());
            return changed;
        }

        /// <summary>Vertex-coloured, transparent, unlit: the arc fades in from the hand.</summary>
        private static Material ArcMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArcMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "ThrowArc" };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Stage1PlaygroundBuilder.EnsureFolder("Assets", "Materials");
            AssetDatabase.CreateAsset(material, ArcMaterialPath);
            return material;
        }

        // ------------------------------------------------------------------- rooms

        private static int EnsureRoomAtlas()
        {
            int changed = 0;
            GameObject host = GameObject.Find(AtlasName);
            if (host == null)
            {
                host = new GameObject(AtlasName);
                changed++;
            }

            RoomAtlas atlas = host.GetComponent<RoomAtlas>();
            if (atlas == null)
            {
                atlas = host.AddComponent<RoomAtlas>();
                changed++;
            }

            HouseBuilder.FillRoomAtlas(atlas);
            return changed;
        }

        // ----------------------------------------------------------------- balance

        private static int ApplyBalance()
        {
            int changed = 0;

            foreach (EnemyBrain brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(brain, "chaseSpeed", ChaseSpeed);
                changed++;
            }

            foreach (PlayerFlashlight light in Object.FindObjectsByType<PlayerFlashlight>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(light, "drainPerSecond", FlashlightDrain);
                changed++;
            }

            foreach (BatteryPickup battery in Object.FindObjectsByType<BatteryPickup>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(battery, "charge", BatteryCharge);
                changed++;
            }

            if (HouseBuilder.IsHouseScene())
            {
                changed += PutBatteriesOnTables();
            }

            return changed;
        }

        /// <summary>Battery_1..6 onto the table tops; a ray finds each top's height.</summary>
        private static int PutBatteriesOnTables()
        {
            GameObject items = GameObject.Find("Items");
            if (items == null)
            {
                return 0;
            }

            Physics.SyncTransforms();
            int moved = 0;
            for (int i = 0; i < TableBatterySpots.Length; i++)
            {
                Transform battery = items.transform.Find("Battery_" + (i + 1));
                if (battery == null)
                {
                    continue;
                }

                Vector3 spot = TableBatterySpots[i];
                Vector3 origin = new Vector3(spot.x, spot.y + 2.2f, spot.z);
                float top = spot.y;
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(items.transform) || hit.distance >= nearest)
                    {
                        continue;
                    }

                    nearest = hit.distance;
                    top = hit.point.y;
                }

                battery.position = new Vector3(spot.x, top + 0.035f, spot.z);
                battery.rotation = Quaternion.Euler(0f, 25f * i, 0f);
                EditorUtility.SetDirty(battery);
                moved++;
            }

            return moved;
        }

        /// <summary>
        /// Second playtest: the flash must buy a real escape, and a catch should read as a
        /// fall to the floor rather than a red screen.
        /// </summary>
        private static int ApplyPlaytestTwo()
        {
            int changed = 0;

            foreach (FlashlightFlash flash in Object.FindObjectsByType<FlashlightFlash>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(flash, "batteryCost", 0.3f);
                Stage1PlaygroundBuilder.SetFloat(flash, "dazeSeconds", 3.5f);
                Stage1PlaygroundBuilder.SetFloat(flash, "dazeSpeedMultiplier", 0f);
                SetString(flash, "hitMessage", "Stunned! Run!");
                changed++;
            }

            foreach (AshenWoods.GameManagement.GameFlowController flow in
                     Object.FindObjectsByType<AshenWoods.GameManagement.GameFlowController>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(flow, "deathDelaySeconds", 3.2f);
                Stage1PlaygroundBuilder.SetFloat(flow, "deathFadeHoldSeconds", 1.4f);
                SetColor(flow, "deathFadeColor", new Color(0f, 0f, 0f, 1f));
                changed++;
            }

            foreach (AshenWoods.UI.ThreatFeedback threat in
                     Object.FindObjectsByType<AshenWoods.UI.ThreatFeedback>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(threat, "maxVignetteAlpha", 0.5f);
                SetColor(threat, "chaseColor", new Color(0.12f, 0.01f, 0.01f, 1f));
                changed++;
            }

            return changed;
        }

        private static void SetColor(Object target, string field, Color value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                return;
            }

            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddMarker(string name)
        {
            var marker = new GameObject(name) { tag = "EditorOnly" };
            marker.SetActive(false);
        }

        private static bool HasMarker(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
