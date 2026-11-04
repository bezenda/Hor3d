using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.Enemy;
using AshenWoods.Interaction;
using AshenWoods.Player;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 3 authoring: a patrolling enemy, the waypoint graph it walks, and two hiding
    /// wardrobes.
    ///
    /// Same rules as Stage 1 and 2: additive only. Existing objects are never deleted or
    /// rebuilt, so anything you move by hand (waypoints, patrol points, wardrobe placement)
    /// survives every run. The prefabs are created once and then left alone, so edits you
    /// make to them in the Prefab Mode are kept.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 3 - Add Enemy And Hiding Spots To Open Scene
    /// </summary>
    public static class Stage3EnemyBuilder
    {
        // Prefab assets.
        private const string EnemyPrefabPath = Stage1PlaygroundBuilder.PrefabFolder + "/Enemies/Enemy_Patroller.prefab";
        private const string HidingSpotPrefabPath =
            Stage1PlaygroundBuilder.PrefabFolder + "/World/HidingSpot_Wardrobe.prefab";

        // Scene roots.
        private const string EnemiesRootName = "Enemies";
        private const string HidingSpotsRootName = "HidingSpots";
        private const string NavigationRootName = "Enemy_Navigation";
        private const string NavNodesName = "NavNodes";
        private const string PatrolRouteName = "PatrolRoute";

        // Object names used as the "does this already exist?" test.
        private const string EnemyInstanceName = "Enemy_Patroller";
        private const string WardrobeWestName = "Hiding_Wardrobe_West";
        private const string WardrobeEastName = "Hiding_Wardrobe_East";

        // Wardrobe: open towards local +Z, sized around a 1.8 m standing player.
        private const float WardrobeWidth = 0.95f;
        private const float WardrobeHeight = 2f;
        private const float WardrobeDepth = 0.7f;
        private const float WardrobePanel = 0.06f;

        private static readonly Vector3 EnemySpawn = new Vector3(-1.2f, 0f, -2.9f);

        /// <summary>
        /// Walkable points in the room and corridor. Links are worked out at runtime by
        /// EnemyNavigation, so adding a node here is enough to extend the route network.
        /// </summary>
        private static readonly Vector3[] NavNodePositions =
        {
            new Vector3(0.0f, 0f, 2.9f),    // 0: south, in front of the main door
            new Vector3(-2.0f, 0f, 1.6f),   // 1: south-west
            new Vector3(-3.6f, 0f, 0.3f),   // 2: west pocket, south of the crouch tunnel
            new Vector3(-0.9f, 0f, -0.2f),  // 3: room centre
            new Vector3(-1.2f, 0f, -2.9f),  // 4: north-west (enemy spawn)
            new Vector3(1.3f, 0f, -3.0f),   // 5: north-centre
            new Vector3(2.9f, 0f, -2.6f),   // 6: north-east, outside the closet
            new Vector3(2.4f, 0f, 0.9f),    // 7: east, in front of the ramp
            new Vector3(1.9f, 0f, 4.7f),      // 8: corridor mouth
            new Vector3(1.8f, 0f, 6.8f),      // 9: corridor end

            // Standing on the top step is height, not a hiding place: without a node up here
            // the enemy can only reach the foot of the stairs and a target on them is safe.
            // The y is the top of Step_4, so the walkability rays start above the treads.
            new Vector3(-3.4f, 0.72f, 3.05f), // 10: on top of the west steps

            // The east wing. Appending keeps every earlier index pointing at the node it was
            // built for, so a scene that already has Node_0 .. Node_10 is only extended.
            // Doorways are 1.6 m wide and each node below sits in the middle of one, which is
            // what lets the 0.3 m body clearance fit through at every height check.
            new Vector3(1.8f, 0f, 8.8f),    // 11: hall, just inside the corridor doorway
            new Vector3(0.6f, 0f, 10.6f),   // 12: hall centre
            new Vector3(0.6f, 0f, 12.6f),   // 13: hall north, in front of the gallery doorway
            new Vector3(1.0f, 0f, 14.4f),   // 14: gallery, south
            new Vector3(0.4f, 0f, 16.0f),   // 15: gallery, north-west
            new Vector3(3.4f, 0f, 15.2f),   // 16: gallery, in front of the passage doorway
            new Vector3(5.6f, 0f, 15.2f),   // 17: passage, north
            new Vector3(5.6f, 0f, 13.0f),   // 18: passage, south
            new Vector3(5.6f, 0f, 10.4f),   // 19: kitchen, north
            new Vector3(3.9f, 0f, 10.2f),   // 20: kitchen, in front of the hall doorway
            new Vector3(-3.4f, 0f, 10.6f),  // 21: boot room centre (player start)
            new Vector3(-1.0f, 0f, 10.2f)   // 22: boot room doorway
        };

        /// <summary>Indices into <see cref="NavNodePositions"/> used for the patrol loop, in order.</summary>
        private static readonly int[] PatrolNodeIndices = { 0, 2, 4, 6, 7, 13, 14, 16, 18, 19 };

        // The boot room (nodes 21 and 22) is deliberately absent: it is the player's start and
        // the one room the enemy never wanders into on its own.

        [MenuItem("Ashen Woods/Diagnostics/Log Enemy Navigation Graph", false, 90)]
        private static void LogNavigationGraph()
        {
            EnemyNavigation navigation = Object.FindAnyObjectByType<EnemyNavigation>();

            if (navigation == null)
            {
                EditorUtility.DisplayDialog(
                    "No enemy navigation in this scene",
                    "Open AshWoods_Stage1_Playground, add the Stage 3 pieces, then run this again.",
                    "OK");
                return;
            }

            navigation.RebuildGraph();
            Debug.Log(navigation.DescribeGraph(), navigation);
        }

        [MenuItem("Ashen Woods/Stage 3 - Add Enemy And Hiding Spots To Open Scene", false, 4)]
        private static void AddStage3Menu()
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

            int added = Stage1PlaygroundBuilder.EnsurePlaygroundInActiveScene()
                        + Stage2InteractionBuilder.EnsureStage2Pieces()
                        + EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Added {added} Stage 3 piece(s). Save the scene with Ctrl+S, then press Play.");
            }
            else
            {
                Debug.Log("Stage 3 is already complete in this scene - nothing was changed.");
            }
        }

        /// <summary>
        /// Adds every missing Stage 3 piece and re-points the wiring. Safe to call repeatedly,
        /// from any of the Ashen Woods menu items.
        /// </summary>
        /// <returns>How many pieces were added.</returns>
        public static int EnsureStage3Pieces()
        {
            Material enemyMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Enemy", new Color(0.34f, 0.36f, 0.32f));
            Material faceMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_EnemyFace", new Color(0.78f, 0.74f, 0.62f));
            Material hidingMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_HidingSpot", new Color(0.30f, 0.22f, 0.15f));

            int added = 0;

            added += EnsurePlayerHidingAndTarget();

            GameObject enemyPrefab = EnsureEnemyPrefab(enemyMaterial, faceMaterial);
            GameObject hidingPrefab = EnsureHidingSpotPrefab(hidingMaterial);

            added += EnsureNavigationAndEnemy(enemyPrefab);
            added += EnsureHidingSpots(hidingPrefab);

            AssetDatabase.SaveAssets();
            return added;
        }

        // ----------------------------------------------------------------- player

        /// <summary>
        /// Puts PlayerHidingController and PlayerEnemyTarget on the player prefab so every
        /// instance has them, mirroring how Stage 2 added the interaction components.
        /// </summary>
        private static int EnsurePlayerHidingAndTarget()
        {
            int added = 0;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(Stage1PlaygroundBuilder.PlayerPrefabPath) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(Stage1PlaygroundBuilder.PlayerPrefabPath);

                if (ConfigureHidingComponents(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, Stage1PlaygroundBuilder.PlayerPrefabPath);
                    added++;
                }

                PrefabUtility.UnloadPrefabContents(contents);
            }

            PlayerMotor[] players = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                GameObject playerObject = players[i].gameObject;

                if (PrefabUtility.GetCorrespondingObjectFromSource(playerObject) != null)
                {
                    continue; // Prefab instances inherit the components from the prefab above.
                }

                if (ConfigureHidingComponents(playerObject))
                {
                    added++;
                }
            }

            return added;
        }

        /// <summary>Adds the two components if missing and re-points their references.</summary>
        /// <returns>True when a component was added.</returns>
        private static bool ConfigureHidingComponents(GameObject playerRoot)
        {
            bool changed = false;

            PlayerHidingController hiding = playerRoot.GetComponent<PlayerHidingController>();
            if (hiding == null)
            {
                hiding = playerRoot.AddComponent<PlayerHidingController>();
                changed = true;
            }

            PlayerEnemyTarget target = playerRoot.GetComponent<PlayerEnemyTarget>();
            if (target == null)
            {
                target = playerRoot.AddComponent<PlayerEnemyTarget>();
                changed = true;
            }

            PlayerMotor motor = playerRoot.GetComponent<PlayerMotor>();
            PlayerLook look = playerRoot.GetComponent<PlayerLook>();
            PlayerInputReader input = playerRoot.GetComponent<PlayerInputReader>();
            PlayerInteractor interactor = playerRoot.GetComponent<PlayerInteractor>();
            CharacterController controller = playerRoot.GetComponent<CharacterController>();

            Transform cameraRoot = playerRoot.transform.Find("CameraRoot");
            Camera camera = playerRoot.GetComponentInChildren<Camera>(true);
            Transform aimPoint = cameraRoot != null ? cameraRoot : (camera != null ? camera.transform : null);

            Stage1PlaygroundBuilder.SetObjectReference(hiding, "motor", motor);
            Stage1PlaygroundBuilder.SetObjectReference(hiding, "look", look);
            Stage1PlaygroundBuilder.SetObjectReference(hiding, "controller", controller);
            Stage1PlaygroundBuilder.SetObjectReference(hiding, "interactor", interactor);

            Stage1PlaygroundBuilder.SetObjectReference(target, "aimPoint", aimPoint);
            Stage1PlaygroundBuilder.SetObjectReference(target, "hiding", hiding);
            Stage1PlaygroundBuilder.SetObjectReference(target, "motor", motor);
            Stage1PlaygroundBuilder.SetObjectReference(target, "look", look);
            Stage1PlaygroundBuilder.SetObjectReference(target, "input", input);
            Stage1PlaygroundBuilder.SetObjectReference(target, "interactor", interactor);

            if (interactor == null)
            {
                Debug.LogWarning("The player has no PlayerInteractor, so hiding spots cannot be used. " +
                                 "Run the Stage 2 menu item first.", playerRoot);
            }

            return changed;
        }

        // ---------------------------------------------------------------- prefabs

        private static GameObject EnsureEnemyPrefab(Material bodyMaterial, Material faceMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Stage1PlaygroundBuilder.EnsureFolder(Stage1PlaygroundBuilder.PrefabFolder, "Enemies");

            GameObject root = new GameObject(EnemyInstanceName);

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.05f;
            controller.minMoveDistance = 0f;

            // Sight starts inside the capsule so the enemy can never block its own view.
            GameObject eyeObject = new GameObject("Eye");
            eyeObject.transform.SetParent(root.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            EnemyVision vision = root.AddComponent<EnemyVision>();
            Stage1PlaygroundBuilder.SetObjectReference(vision, "eye", eyeObject.transform);

            CreateEnemyVisuals(root.transform, bodyMaterial, faceMaterial, out Renderer bodyRenderer);

            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            Stage1PlaygroundBuilder.SetObjectReference(brain, "vision", vision);

            // The colour cue lives on the body so it can be swapped for an animator later.
            EnemyStateVisuals visuals = root.AddComponent<EnemyStateVisuals>();
            Stage1PlaygroundBuilder.SetObjectReference(visuals, "brain", brain);
            Stage1PlaygroundBuilder.SetObjectReference(visuals, "body", bodyRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateEnemyVisuals(
            Transform parent, Material bodyMaterial, Material faceMaterial, out Renderer bodyRenderer)
        {
            Transform visualsRoot = new GameObject("Visuals").transform;
            visualsRoot.SetParent(parent, false);

            // Placeholder body: swap this child for the real model later. No collider, so it
            // can never fight with the CharacterController.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visualsRoot, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.85f, 0.9f, 0.85f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // A pale block on the face tells you which way the enemy is looking.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Face";
            face.transform.SetParent(visualsRoot, false);
            face.transform.localPosition = new Vector3(0f, 1.55f, 0.3f);
            face.transform.localScale = new Vector3(0.22f, 0.14f, 0.08f);
            face.GetComponent<MeshRenderer>().sharedMaterial = faceMaterial;
            Object.DestroyImmediate(face.GetComponent<Collider>());

            bodyRenderer = body.GetComponent<MeshRenderer>();
        }

        /// <summary>
        /// The wardrobe: three walls and a top, open towards local +Z so the player can see
        /// out while hidden. Panel colliders stay, because they are what the player bumps
        /// into and what the interaction ray hits.
        /// </summary>
        private static GameObject EnsureHidingSpotPrefab(Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HidingSpotPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Stage1PlaygroundBuilder.EnsureFolder(Stage1PlaygroundBuilder.PrefabFolder, "World");

            GameObject root = new GameObject("HidingSpot_Wardrobe");
            HidingSpot spot = root.AddComponent<HidingSpot>();

            HidingSpot spotComponent = spot;
            Transform visualsRoot = new GameObject("Visuals").transform;
            visualsRoot.SetParent(root.transform, false);

            float halfWidth = WardrobeWidth * 0.5f;
            float halfDepth = WardrobeDepth * 0.5f;
            float totalWidth = WardrobeWidth + WardrobePanel * 2f;
            float totalHeight = WardrobeHeight + WardrobePanel;
            float sideX = halfWidth + WardrobePanel * 0.5f;

            Stage1PlaygroundBuilder.CreateBox("Back", visualsRoot,
                new Vector3(0f, WardrobeHeight * 0.5f, -halfDepth + WardrobePanel * 0.5f),
                new Vector3(totalWidth, totalHeight, WardrobePanel), material);

            Stage1PlaygroundBuilder.CreateBox("Side_Left", visualsRoot,
                new Vector3(-sideX, WardrobeHeight * 0.5f, 0f),
                new Vector3(WardrobePanel, totalHeight, WardrobeDepth), material);

            Stage1PlaygroundBuilder.CreateBox("Side_Right", visualsRoot,
                new Vector3(sideX, WardrobeHeight * 0.5f, 0f),
                new Vector3(WardrobePanel, totalHeight, WardrobeDepth), material);

            Stage1PlaygroundBuilder.CreateBox("Top", visualsRoot,
                new Vector3(0f, WardrobeHeight + WardrobePanel * 0.5f, 0f),
                new Vector3(totalWidth, WardrobePanel, WardrobeDepth), material);

            GameObject hidePoint = new GameObject("HidePoint");
            hidePoint.transform.SetParent(root.transform, false);
            hidePoint.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            Stage1PlaygroundBuilder.SetObjectReference(spotComponent, "hidePoint", hidePoint.transform);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HidingSpotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------- world

        private static int EnsureNavigationAndEnemy(GameObject enemyPrefab)
        {
            int added = 0;

            GameObject navigationRoot = GameObject.Find(NavigationRootName);
            if (navigationRoot == null)
            {
                navigationRoot = new GameObject(NavigationRootName);
                added++;
            }

            EnemyNavigation navigation = navigationRoot.GetComponent<EnemyNavigation>();
            if (navigation == null)
            {
                navigation = navigationRoot.AddComponent<EnemyNavigation>();
                added++;
            }

            Transform navNodes = EnsureChild(navigationRoot.transform, NavNodesName, ref added);
            Transform patrolRoute = EnsureChild(navigationRoot.transform, PatrolRouteName, ref added);

            added += EnsurePoints(navNodes, "Node", NavNodePositions);

            var patrolPositions = new Vector3[PatrolNodeIndices.Length];
            for (int i = 0; i < PatrolNodeIndices.Length; i++)
            {
                patrolPositions[i] = NavNodePositions[PatrolNodeIndices[i]];
            }

            // Patrol points are separate objects on purpose: reorder or move them to change
            // the route without touching the navigation graph.
            added += EnsurePoints(patrolRoute, "Patrol", patrolPositions);

            GameObject enemyInstance = GameObject.Find(EnemyInstanceName);
            if (enemyInstance == null)
            {
                if (enemyPrefab == null)
                {
                    Debug.LogError("Enemy prefab could not be created, so no enemy was placed.");
                    return added;
                }

                enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemyInstance.name = EnemyInstanceName;
                enemyInstance.transform.SetParent(GetOrCreateRoot(EnemiesRootName).transform, false);
                enemyInstance.transform.localPosition = EnemySpawn;
                enemyInstance.transform.localRotation = Quaternion.identity;
                added++;
            }

            WireNavigation(navigation, navNodes, enemyInstance);
            WireBrain(enemyInstance, navigation, patrolRoute);

            return added;
        }

        private static void WireNavigation(EnemyNavigation navigation, Transform navNodes, GameObject enemyInstance)
        {
            if (navigation == null)
            {
                return;
            }

            int playerLayer = LayerMask.NameToLayer("Player");

            Stage1PlaygroundBuilder.SetObjectReference(navigation, "nodesRoot", navNodes);
            Stage1PlaygroundBuilder.SetLayerMask(
                navigation, "obstructionMask", playerLayer >= 0 ? ~(1 << playerLayer) : ~0);

            if (enemyInstance == null)
            {
                return;
            }

            Stage1PlaygroundBuilder.SetObjectReference(navigation, "agent", enemyInstance.transform);
            Stage1PlaygroundBuilder.SetObjectReference(
                navigation, "controller", enemyInstance.GetComponent<CharacterController>());
        }

        private static void WireBrain(GameObject enemyInstance, EnemyNavigation navigation, Transform patrolRoute)
        {
            if (enemyInstance == null)
            {
                return;
            }

            EnemyBrain brain = enemyInstance.GetComponent<EnemyBrain>();
            if (brain == null)
            {
                Debug.LogWarning($"{EnemyInstanceName} has no EnemyBrain component, so it will not think. " +
                                 "Delete the instance and run this menu item again.", enemyInstance);
                return;
            }

            PlayerEnemyTarget playerTarget = Object.FindAnyObjectByType<PlayerEnemyTarget>();

            SetInstanceReference(brain, "navigation", navigation);
            SetInstanceReference(brain, "patrolRoute", patrolRoute);
            SetInstanceReference(brain, "targetBehaviour", playerTarget);

            if (playerTarget == null)
            {
                Debug.LogWarning("No PlayerEnemyTarget in the scene, so the enemy will patrol but never chase. " +
                                 "Add a Player (or run the Stage 1 menu item) and run this again.");
            }
        }

        private static int EnsureHidingSpots(GameObject hidingPrefab)
        {
            if (hidingPrefab == null)
            {
                Debug.LogError("Hiding spot prefab could not be created, so no hiding spots were placed.");
                return 0;
            }

            int added = 0;
            Transform root = GetOrCreateRoot(HidingSpotsRootName).transform;

            // West: against the room's west wall, opening east towards the room.
            added += EnsureHidingSpotInstance(
                hidingPrefab, root, WardrobeWestName,
                new Vector3(-4.675f, 0f, 3.1f), Quaternion.Euler(0f, 90f, 0f));

            // East: against the east wall, opening west, south of the ramp.
            added += EnsureHidingSpotInstance(
                hidingPrefab, root, WardrobeEastName,
                new Vector3(4.675f, 0f, 0.2f), Quaternion.Euler(0f, -90f, 0f));

            // The wing adds two more, but only once its rooms exist.
            if (GameObject.Find(Stage1WingBuilder.WingRootName) != null)
            {
                // Boot room: on the west wall, opening east, behind the player's start point.
                added += EnsureHidingSpotInstance(
                    hidingPrefab, root, "Hiding_Wardrobe_Boot",
                    new Vector3(-5.05f, 0f, 12.2f), Quaternion.Euler(0f, 90f, 0f));

                // Gallery: under the north wall, opening south into the long room.
                added += EnsureHidingSpotInstance(
                    hidingPrefab, root, "Hiding_Wardrobe_Gallery",
                    new Vector3(2.6f, 0f, 16.15f), Quaternion.Euler(0f, 180f, 0f));
            }

            return added;
        }

        private static int EnsureHidingSpotInstance(
            GameObject prefab, Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            if (GameObject.Find(name) != null)
            {
                return 0;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;

            return 1;
        }

        // ---------------------------------------------------------------- helpers

        private static Transform EnsureChild(Transform parent, string name, ref int added)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            added++;
            return child;
        }

        /// <summary>Creates one empty transform per position, named prefix_0, prefix_1, ...</summary>
        private static int EnsurePoints(Transform parent, string prefix, Vector3[] positions)
        {
            int added = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                string pointName = prefix + "_" + i;
                if (parent.Find(pointName) != null)
                {
                    continue;
                }

                GameObject point = new GameObject(pointName);
                point.transform.SetParent(parent, false);
                point.transform.localPosition = positions[i];
                added++;
            }

            return added;
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing;
            }

            return new GameObject(name);
        }

        /// <summary>
        /// Writes a reference on a component that lives inside a prefab instance (or a plain
        /// scene object) and records it as an override, so the prefab itself stays clean.
        /// </summary>
        private static void SetInstanceReference(Component component, string fieldName, Object value)
        {
            if (component == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {component.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            else
            {
                EditorUtility.SetDirty(component);
            }
        }
    }
}
