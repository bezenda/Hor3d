using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using AshenWoods.Player;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The house level: a 24 x 16 m, two-storey house with sixteen rooms, two staircases and a
    /// long corridor on each floor. The plan itself lives in HouseBuilder.Layout.cs as two
    /// letter maps; this file turns it into a scene and puts the stage objects on their anchors.
    ///
    ///   GROUND (y 0)                               FIRST FLOOR (y 3.2)
    ///   kitchen + locked pantry (crowbar)          master bedroom (wardrobe)
    ///   back hall -> back stair                    linen room
    ///   dining room, utility room                  study (breaker panel)
    ///   corridor, 24 m east-west                   storeroom (enemy wakes here)
    ///   living room (crouch tunnel, iron key)      corridor, 24 m east-west
    ///   foyer (player start, front door, main stair) guest bedroom (wardrobe), playroom
    ///   library, toilet                            bathroom, small room
    ///
    /// Both stairs land on the upper corridor, so the two floors form loops: there is always a
    /// second way round, and the enemy has two ways up.
    ///
    /// Layout v1 (the first 12 x 8 m house) is replaced, not patched: when a scene still has the
    /// old House root it is deleted together with the builder-made batteries, throwables,
    /// navigation nodes and patrol points, and everything is built again from the plan. Stage
    /// objects (doors, key, breaker, enemy, wardrobes...) are kept and moved onto new anchors.
    ///
    /// Menu:
    ///   Ashen Woods > Level > Create Two-Storey House Scene
    ///   Ashen Woods > Level > Build Or Update The House
    ///   Ashen Woods > Diagnostics > Log House Layout
    ///   Ashen Woods > Diagnostics > Log House Overlap Check
    /// </summary>
    public static partial class HouseBuilder
    {
        internal const string HouseScenePath = "Assets/Scenes/AshWoods_House.unity";

        private const string EnvironmentName = "Environment";
        private const string HouseRootName = "House";
        private const string ClosetRootName = "Closet_Locked";

        private const float Wall = 0.2f;
        private const float GroundCeiling = 3f;
        private const float UpperFloor = 3.2f;
        private const float UpperCeiling = 6f;

        // Doorways are cut 2.1 m tall; the escape frame is 2.15 m tall at the wall hole.
        private const float DoorHead = 2.1f;
        private const float UpperDoorHead = 2.05f; // above the upper floor

        /// <summary>Where a fresh run starts: in the foyer, back to the front door.</summary>
        internal static readonly Vector3 PlayerStartPosition = new Vector3(0f, 0.05f, 6.4f);
        internal const float PlayerStartYaw = 180f;

        /// <summary>The enemy wakes upstairs in the storeroom, the far corner from the start.</summary>
        internal static readonly Vector3 EnemySpawnPosition = new Vector3(9f, UpperFloor, -5f);

        /// <summary>The escape exit, on the inside face of the foyer's south wall.</summary>
        internal static readonly Vector3 FrontDoorPosition = new Vector3(0f, 0f, 7.9f);

        // ------------------------------------------------------------------- menus

        [MenuItem("Ashen Woods/Level/Create Two-Storey House Scene", false, 1)]
        private static void CreateHouseScene()
        {
            if (System.IO.File.Exists(HouseScenePath))
            {
                bool openExisting = EditorUtility.DisplayDialog(
                    "House scene already exists",
                    HouseScenePath + " already exists, so nothing was overwritten.\n\n" +
                    "Open it now? If it is missing objects, use\n" +
                    "Ashen Woods > Level > Build Or Update The House",
                    "Open it",
                    "Cancel");

                if (openExisting && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);
                }

                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int added = EnsureHouseInActiveScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HouseScenePath);
            AddSceneToBuildSettings(HouseScenePath);

            Debug.Log($"[House] Created {HouseScenePath} with {added} piece(s).\n" +
                      "Press Play. The player starts in the foyer, the enemy wakes upstairs in the storeroom.\n" +
                      "Route: iron key under the low table (living room) -> locked pantry (kitchen) -> crowbar -> " +
                      "breaker panel (upstairs storeroom) -> front door (foyer).");
        }

        [MenuItem("Ashen Woods/Level/Build Or Update The House", false, 2)]
        private static void BuildOrUpdateHouse()
        {
            if (!IsHouseScene())
            {
                EditorUtility.DisplayDialog(
                    "Open the house scene first",
                    "This tool only edits:\n" + HouseScenePath + "\n\n" +
                    "Nothing was changed. Create it with Ashen Woods > Level > Create Two-Storey House Scene.",
                    "OK");
                return;
            }

            int added = EnsureHouseInActiveScene();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[House] Added or moved {added} piece(s). Save the scene with Ctrl+S, then press Play.");
            }
            else
            {
                Debug.Log("[House] The house is already complete in this scene - nothing was changed.");
            }
        }

        /// <summary>True when the open scene is one of the two level scenes.</summary>
        internal static bool IsLevelScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == HouseScenePath || path == Stage1PlaygroundBuilder.ScenePath;
        }

        internal static bool IsHouseScene()
        {
            return SceneManager.GetActiveScene().path == HouseScenePath;
        }

        /// <summary>
        /// The old Stage 1 shell belongs to the playground only. Every stage menu calls this
        /// instead of <c>EnsurePlaygroundInActiveScene</c>, so running a stage in the house can
        /// never drop the old room, corridor and wing into the new building.
        /// </summary>
        internal static int EnsureLevelPieces()
        {
            return IsHouseScene() ? 0 : Stage1PlaygroundBuilder.EnsurePlaygroundInActiveScene();
        }

        // ------------------------------------------------------------------- build

        /// <summary>
        /// Builds (or upgrades) the house and adopts the stage objects. Idempotent: boxes are
        /// looked up by name, stage objects are only moved when they are not on their anchor.
        /// </summary>
        /// <returns>How many pieces were added or moved.</returns>
        internal static int EnsureHouseInActiveScene()
        {
            Material floorMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Floor_House", new Color(0.18f, 0.16f, 0.14f));
            Material wallMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Wall", new Color(0.33f, 0.31f, 0.28f));
            Material ceilingMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Ceiling", new Color(0.14f, 0.13f, 0.12f));
            Material propMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Prop", new Color(0.35f, 0.25f, 0.16f));
            Material trimMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_HouseTrim", new Color(0.29f, 0.25f, 0.20f));

            GameObject environment = GameObject.Find(EnvironmentName);
            if (environment == null)
            {
                environment = new GameObject(EnvironmentName);
            }

            int added = UpgradeOldLayout(environment.transform);

            Transform house = environment.transform.Find(HouseRootName);
            if (house == null)
            {
                GameObject houseObject = new GameObject(HouseRootName);
                houseObject.transform.SetParent(environment.transform, false);
                house = houseObject.transform;
            }

            if (house.Find(LayoutMarkerName) == null)
            {
                new GameObject(LayoutMarkerName).transform.SetParent(house, false);
            }

            // Stage 2 builds its own little closet when an Environment exists. The pantry is
            // part of the house plan instead, so this empty root tells Stage 2 the closet exists.
            if (GameObject.Find(ClosetRootName) == null)
            {
                new GameObject(ClosetRootName).transform.SetParent(house, false);
                added++;
            }

            added += EnsureFloors(house, floorMaterial, ceilingMaterial);
            added += EnsureStoreyWalls(house, "House_G", GroundPlan, GroundOpenings, 0f, GroundCeiling, wallMaterial);
            added += EnsureStoreyWalls(house, "House_U", UpperPlan, UpperOpenings, UpperFloor, UpperCeiling, wallMaterial);
            added += EnsureStairRamps(house, trimMaterial);
            added += EnsureProps(house, propMaterial, trimMaterial);
            added += EnsureLighting(house);
            added += EnsurePlayer();

            // The stage builders place their objects using their original coordinates. They run
            // before the alignment step below, which moves each object onto its house anchor.
            added += Stage2InteractionBuilder.EnsureStage2Pieces();
            added += Stage3EnemyBuilder.EnsureStage3Pieces();
            added += Stage4MenuBuilder.EnsureStage4Pieces();
            added += Stage5NoiseBuilder.EnsureStage5Pieces();
            added += Stage6ObjectiveBuilder.EnsureStage6Pieces();
            added += Stage7FlowBuilder.EnsureStage7Pieces();
            added += Stage8ThrowableBuilder.EnsureStage8Pieces();
            added += Stage9PolishBuilder.EnsureStage9Pieces();
            added += Stage10InterfaceBuilder.EnsureStage10Pieces();

            added += EnsureHouseHidingSpots(house);
            added += EnsureDressing(house);
            added += EnsureNavigationPoints();
            added += AlignStageObjects();
            Stage6ObjectiveBuilder.RefreshHouseWording();

            AssetDatabase.SaveAssets();
            return added;
        }

        /// <summary>
        /// Removes everything the builders made for layout v1 so the plan can be built fresh:
        /// the House root, spare batteries, throwables, navigation nodes and patrol points.
        /// Stage objects (doors, key, breaker, crowbar, enemy, wardrobes, escape) are kept and
        /// re-anchored later; the player is moved to the new start.
        /// </summary>
        private static int UpgradeOldLayout(Transform environment)
        {
            Transform house = environment.Find(HouseRootName);
            if (house == null || house.Find(LayoutMarkerName) != null)
            {
                return 0;
            }

            int removed = 0;
            Object.DestroyImmediate(house.gameObject);
            removed++;

            GameObject items = GameObject.Find("Items");
            if (items != null)
            {
                for (int i = items.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = items.transform.GetChild(i);
                    if (child.name.StartsWith("Battery_") || child.name.StartsWith("Throwable_"))
                    {
                        Object.DestroyImmediate(child.gameObject);
                        removed++;
                    }
                }
            }

            GameObject navigationRoot = GameObject.Find("Enemy_Navigation");
            if (navigationRoot != null)
            {
                foreach (string group in new[] { "NavNodes", "PatrolRoute" })
                {
                    Transform root = navigationRoot.transform.Find(group);
                    if (root == null)
                    {
                        continue;
                    }

                    for (int i = root.childCount - 1; i >= 0; i--)
                    {
                        Object.DestroyImmediate(root.GetChild(i).gameObject);
                        removed++;
                    }
                }
            }

            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                bool wasEnabled = controller != null && controller.enabled;
                if (controller != null)
                {
                    controller.enabled = false;
                }

                player.transform.SetPositionAndRotation(PlayerStartPosition, Quaternion.Euler(0f, PlayerStartYaw, 0f));

                if (controller != null)
                {
                    controller.enabled = wasEnabled;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(player.gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                }
            }

            Debug.Log($"[House] Replaced the old 12 x 8 m layout with layout v{LayoutVersion} ({removed} old piece(s) removed).");
            return removed;
        }

        // ----------------------------------------------------------------- floors

        private static int EnsureFloors(Transform house, Material floor, Material ceiling)
        {
            float width = Columns * Cell + Wall;
            float depth = Rows * Cell + Wall;

            int added = 0;
            added += EnsureBox(house, "House_Floor_Ground",
                new Vector3(0f, -0.1f, 0f), new Vector3(width, 0.2f, depth), floor);
            added += EnsureUpperSlab(house, floor);
            added += EnsureBox(house, "House_Ceiling",
                new Vector3(0f, UpperCeiling + 0.1f, 0f), new Vector3(width, 0.2f, depth), ceiling);
            return added;
        }

        // ------------------------------------------------------------------ props

        /// <summary>
        /// Furniture. Everything stands against a wall and is at most 0.5 m deep, so the 1 m
        /// from a wall to a cell centre stays clear for the enemy's routes; free-standing pieces
        /// sit between cell centres, never on one.
        /// </summary>
        private static int EnsureProps(Transform house, Material prop, Material trim)
        {
            int added = 0;
            const float g = 0f;          // ground floor
            const float u = UpperFloor;  // upper floor

            // Living room: the crouch tunnel in the north-west corner. 1.1 m under a 1.25 m roof:
            // the crouching player (0.95 m) fits, the enemy (1.5 m head clearance) never does.
            added += EnsureBox(house, "House_Living_CrouchRoof", new Vector3(-10.7f, 1.25f, 0.8f), new Vector3(2.4f, 0.3f, 1.4f), prop);
            added += EnsureBox(house, "House_Living_CrouchSupport", new Vector3(-9.6f, 0.55f, 0.8f), new Vector3(0.2f, 1.1f, 1.4f), prop);
            // Furniture is in HouseBuilder.Dressing.cs (models). Only boxes without a model are here.
            added += EnsureBox(house, "House_Toilet_Sink", new Vector3(11.65f, g + 0.45f, 7f), new Vector3(0.5f, 0.9f, 0.6f), trim);
            added += EnsureBox(house, "House_Play_ToyChest", new Vector3(0f, u + 0.3f, 7.65f), new Vector3(1.2f, 0.6f, 0.5f), prop);
            added += EnsureBox(house, "House_Bath_Tub", new Vector3(11.6f, u + 0.3f, 2f), new Vector3(0.6f, 0.6f, 1.8f), trim);

            return added;
        }

        // ---------------------------------------------------------- hiding spots

        /// <summary>
        /// Stage 3 makes two wardrobes; a house this size needs a hiding place within a few
        /// seconds of every route. Six more wardrobes and three beds to hide under, all against
        /// walls and centred on a grid line, so they stand between navigation nodes.
        /// A wardrobe or bed opens towards its local +z.
        /// </summary>
        private static int EnsureHouseHidingSpots(Transform house)
        {
            GameObject wardrobe = Stage3EnemyBuilder.WardrobePrefab();
            GameObject bed = Stage3EnemyBuilder.BedPrefab();
            Quaternion facesSouth = Quaternion.identity;               // against a north wall
            Quaternion facesNorth = Quaternion.Euler(0f, 180f, 0f);     // against a south wall
            Quaternion facesWest = Quaternion.Euler(0f, -90f, 0f);      // against an east wall
            const float u = UpperFloor;

            int added = 0;

            // Ground floor.
            added += Stage3EnemyBuilder.PlaceHidingSpot(wardrobe, "Hiding_Wardrobe_Kitchen", new Vector3(-6f, 0f, -7.575f), facesSouth);
            added += Stage3EnemyBuilder.PlaceHidingSpot(wardrobe, "Hiding_Wardrobe_Utility", new Vector3(10f, 0f, -7.575f), facesSouth);
            added += Stage3EnemyBuilder.PlaceHidingSpot(wardrobe, "Hiding_Wardrobe_Foyer", new Vector3(2f, 0f, 7.575f), facesNorth);
            added += Stage3EnemyBuilder.PlaceHidingSpot(wardrobe, "Hiding_Wardrobe_Library", new Vector3(6f, 0f, 7.575f), facesNorth);

            // Upper floor.
            added += Stage3EnemyBuilder.PlaceHidingSpot(wardrobe, "Hiding_Wardrobe_Study", new Vector3(2f, u, -2.425f), facesNorth);
            added += Stage3EnemyBuilder.PlaceHidingSpot(wardrobe, "Hiding_Wardrobe_SmallRoom", new Vector3(11.575f, u, 6f), facesWest);
            added += Stage3EnemyBuilder.PlaceHidingSpot(bed, "Hiding_Bed_Master", new Vector3(-6f, u, -6.95f), facesSouth);
            added += Stage3EnemyBuilder.PlaceHidingSpot(bed, "Hiding_Bed_Guest", new Vector3(-8f, u, 6.95f), facesNorth);
            added += Stage3EnemyBuilder.PlaceHidingSpot(bed, "Hiding_Bed_Playroom", new Vector3(-2f, u, 6.95f), facesNorth);

            // The plain box beds they replace, and the items that lay on those.
            foreach (string oldBed in new[] { "House_Master_Bed", "House_Guest_Bed" })
            {
                Transform box = house.Find(oldBed);
                if (box != null)
                {
                    Object.DestroyImmediate(box.gameObject);
                    added++;
                }
            }

            added += RelocateIfAt("Battery_2", new Vector3(-8f, 3.76f, 7.1f), new Vector3(-8f, u + 0.62f, 6.95f));
            added += RelocateIfAt("Throwable_Bottle_6", new Vector3(-6f, 3.855f, -7.3f), new Vector3(-6f, u + 0.75f, -6.6f));
            return added;
        }

        /// <summary>Moves an object only while it still stands at the old builder position.</summary>
        private static int RelocateIfAt(string name, Vector3 oldPosition, Vector3 newPosition)
        {
            GameObject target = GameObject.Find(name);
            if (target == null || Vector3.Distance(target.transform.position, oldPosition) > 0.02f)
            {
                return 0;
            }

            target.transform.position = newPosition;
            EditorUtility.SetDirty(target.transform);
            return 1;
        }

        // --------------------------------------------------------------- lighting

        private static int EnsureLighting(Transform house)
        {
            int added = 0;

            Transform lighting = house.Find("Lighting");
            if (lighting == null)
            {
                lighting = new GameObject("Lighting").transform;
                lighting.SetParent(house, false);
                added++;
            }

            Color warm = new Color(1f, 0.86f, 0.68f);
            Color cold = new Color(0.86f, 0.9f, 0.86f);
            const float g = 2.6f;
            const float u = 5.5f;

            // Ground floor.
            added += EnsurePointLight(lighting, "House_Light_Foyer", new Vector3(1f, g, 5f), 2.6f, 9f, warm);
            added += EnsurePointLight(lighting, "House_Light_Living", new Vector3(-7f, g, 4f), 2.6f, 10f, warm);
            added += EnsurePointLight(lighting, "House_Light_Kitchen", new Vector3(-8f, g, -5f), 2.4f, 9f, cold);
            added += EnsurePointLight(lighting, "House_Light_Pantry", new Vector3(-10f, 2.5f, -1f), 1.2f, 4f, cold);
            added += EnsurePointLight(lighting, "House_Light_Dining", new Vector3(4f, g, -5f), 2.4f, 9f, warm);
            added += EnsurePointLight(lighting, "House_Light_Utility", new Vector3(10f, g, -5f), 1.6f, 6f, cold);
            added += EnsurePointLight(lighting, "House_Light_BackHall", new Vector3(-3f, g, -5f), 1.4f, 5f, warm);
            added += EnsurePointLight(lighting, "House_Light_Library", new Vector3(8f, g, 3f), 2.4f, 9f, warm);
            added += EnsurePointLight(lighting, "House_Light_Toilet", new Vector3(10f, g, 6f), 1.4f, 5f, cold);
            added += EnsurePointLight(lighting, "House_Light_Corridor_W", new Vector3(-7f, g, -1f), 1.8f, 7f, warm);
            added += EnsurePointLight(lighting, "House_Light_Corridor_E", new Vector3(5f, g, -1f), 1.8f, 7f, warm);

            // Upper floor.
            added += EnsurePointLight(lighting, "House_Light_Master", new Vector3(-8f, u, -5f), 2.4f, 9f, warm);
            added += EnsurePointLight(lighting, "House_Light_Linen", new Vector3(-3f, u, -5f), 1.2f, 5f, warm);
            added += EnsurePointLight(lighting, "House_Light_Study", new Vector3(4f, u, -5f), 2.2f, 9f, cold);
            added += EnsurePointLight(lighting, "House_Light_Store", new Vector3(10f, u, -5f), 1.4f, 6f, warm);
            added += EnsurePointLight(lighting, "House_Light_Guest", new Vector3(-8f, u, 4f), 2.4f, 9f, warm);
            added += EnsurePointLight(lighting, "House_Light_Playroom", new Vector3(-1f, u, 5f), 2f, 8f, warm);
            added += EnsurePointLight(lighting, "House_Light_Bathroom", new Vector3(8f, u, 3f), 1.8f, 8f, cold);
            added += EnsurePointLight(lighting, "House_Light_SmallRoom", new Vector3(10f, u, 6f), 1.4f, 5f, warm);
            added += EnsurePointLight(lighting, "House_Light_UpperCorridor_W", new Vector3(-7f, u, -1f), 1.8f, 7f, warm);
            added += EnsurePointLight(lighting, "House_Light_UpperCorridor_E", new Vector3(5f, u, -1f), 1.8f, 7f, warm);

            // Dark on purpose: the flashlight is the main light (HousePower dims it further
            // until the breaker is fixed). Matches Stage9PolishBuilder.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.045f, 0.055f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.skybox = null;

            return added;
        }

        /// <summary>
        /// Room lamps get soft, low-resolution shadows: without them light leaked through
        /// floors and walls and the house never got dark. Forward+ plus a 4096 shadow atlas
        /// (Stage 9) keeps the cost reasonable.
        /// </summary>
        private static int EnsurePointLight(
            Transform parent, string name, Vector3 position, float intensity, float range, Color color)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft; // low shadow tier: Stage9PolishBuilder.EnsureShadowTiers

            return 1;
        }

        // ----------------------------------------------------------------- player

        private static int EnsurePlayer()
        {
            if (Object.FindAnyObjectByType<PlayerMotor>() != null)
            {
                return 0;
            }

            Material playerMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Player", new Color(0.55f, 0.72f, 0.85f));

            GameObject prefab = Stage1PlaygroundBuilder.EnsurePlayerPrefab(playerMaterial);
            if (prefab == null)
            {
                Debug.LogError("[House] The player prefab could not be created, so no player was placed.");
                return 0;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = PlayerStartPosition;
            instance.transform.rotation = Quaternion.Euler(0f, PlayerStartYaw, 0f);

            Selection.activeGameObject = instance;
            return 1;
        }

        // ------------------------------------------------------------- navigation

        /// <summary>
        /// One node per walkable cell plus the ramp points, and the patrol points in loop order.
        /// Missing children are created; existing ones are moved onto the plan; spare patrol
        /// points beyond the loop are removed (the brain walks every child of PatrolRoute).
        /// </summary>
        private static int EnsureNavigationPoints()
        {
            GameObject navigationRoot = GameObject.Find("Enemy_Navigation");
            if (navigationRoot == null)
            {
                return 0;
            }

            int changed = 0;
            changed += SyncPoints(navigationRoot.transform.Find("NavNodes"), "Node_", NavNodes);
            changed += SyncPoints(navigationRoot.transform.Find("PatrolRoute"), "Patrol_", PatrolPoints);
            return changed;
        }

        private static int SyncPoints(Transform root, string prefix, List<Vector3> positions)
        {
            if (root == null)
            {
                return 0;
            }

            int changed = 0;

            for (int i = 0; i < positions.Count; i++)
            {
                Transform point = root.Find(prefix + i);
                if (point == null)
                {
                    point = new GameObject(prefix + i).transform;
                    point.SetParent(root, false);
                    changed++;
                }

                point.SetSiblingIndex(i);

                if (Vector3.Distance(point.localPosition, positions[i]) > 0.001f)
                {
                    point.localPosition = positions[i];
                    EditorUtility.SetDirty(point);
                    changed++;
                }
            }

            for (int i = root.childCount - 1; i >= positions.Count; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
                changed++;
            }

            return changed;
        }

        // -------------------------------------------------------------- alignment

        /// <summary>
        /// Moves each stage object onto its house anchor. Objects already at their anchor are
        /// left alone, so this is safe to run again.
        /// </summary>
        private static int AlignStageObjects()
        {
            int moved = 0;

            // Main door: living room <-> foyer, in the wall at x = -2. Hinge at the south end
            // of the 1.6 m opening (z 2.2), leaf along +z.
            moved += MoveObject("Door_Main", new Vector3(-2f, 0f, 2.25f), Quaternion.Euler(0f, -90f, 0f));

            // Locked pantry door: kitchen <-> pantry, in the wall at z = -2 (opening x -9.8..-8.2).
            moved += MoveObject("Door_Closet_Locked", new Vector3(-9.75f, 0f, -2f), Quaternion.identity);

            // Iron key: on the floor under the crouch tunnel's roof.
            moved += MoveObject("Key_Iron", new Vector3(-10.7f, 0.15f, 0.8f), null);

            // Crowbar: in the pantry, in front of the shelf.
            moved += MoveObject("Crowbar_Closet", new Vector3(-10.6f, 0.16f, -1.2f), null);

            // Breaker panel: the upstairs storeroom (a small service room), north wall.
            moved += MoveObject("BreakerPanel", new Vector3(10f, UpperFloor + 1.35f, -7.83f), Quaternion.identity);

            // Escape exit: the foyer's south wall.
            moved += MoveObject("Escape_FrontDoor", FrontDoorPosition, Quaternion.identity);

            // Enemy: the upstairs storeroom.
            moved += MoveObject("Enemy_Patroller", EnemySpawnPosition, Quaternion.identity);

            // Wardrobes: master bedroom (north wall, facing south) and guest bedroom (west wall).
            moved += MoveObject("Hiding_Wardrobe_West", new Vector3(-9f, UpperFloor, -7.575f), Quaternion.identity);
            moved += MoveObject("Hiding_Wardrobe_East", new Vector3(-11.575f, UpperFloor, 4f), Quaternion.Euler(0f, 90f, 0f));

            return moved;
        }

        /// <summary>
        /// Moves one object and records the change on a prefab instance. Returns 1 when it
        /// actually moved, so the menus can report a real number.
        /// </summary>
        private static int MoveObject(string name, Vector3 position, Quaternion? rotation)
        {
            GameObject target = GameObject.Find(name);
            if (target == null)
            {
                return 0;
            }

            bool positionMatches = Vector3.Distance(target.transform.position, position) < 0.01f;
            bool rotationMatches = rotation == null
                                   || Quaternion.Angle(target.transform.rotation, rotation.Value) < 0.1f;

            if (positionMatches && rotationMatches)
            {
                return 0;
            }

            CharacterController controller = target.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            target.transform.position = position;
            if (rotation != null)
            {
                target.transform.rotation = rotation.Value;
            }

            if (controller != null)
            {
                controller.enabled = wasEnabled;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform);
            }
            else
            {
                EditorUtility.SetDirty(target.transform);
            }

            return 1;
        }

        // ------------------------------------------------------------ diagnostics

        [MenuItem("Ashen Woods/Diagnostics/Log House Layout", false, 93)]
        private static void LogHouseLayout()
        {
            GameObject house = GameObject.Find(HouseRootName);
            if (house == null)
            {
                Debug.Log("[House] Not in this scene. Run Ashen Woods > Level > Create Two-Storey House Scene.");
                return;
            }

            var report = new System.Text.StringBuilder($"[House] Layout v{LayoutVersion}, 24 x 16 m, 2 m cells (north at the top)");
            report.Append("\n  GROUND");
            foreach (string row in GroundPlan)
            {
                report.Append("\n    ").Append(row);
            }

            report.Append("\n    K kitchen  P pantry  H back hall  + back stair  D dining  U utility  C corridor" +
                          "\n    L living   F foyer   * main stair  B library  T toilet");
            report.Append("\n  FIRST FLOOR");
            foreach (string row in UpperPlan)
            {
                report.Append("\n    ").Append(row);
            }

            report.Append("\n    M master  N linen  R study  A storeroom  E corridor  G guest  O playroom  Q bathroom  W small room  h stairwell");
            report.Append($"\n  Navigation: {NavNodes.Count} nodes, patrol of {PatrolPoints.Count} points.");

            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            GameObject enemy = GameObject.Find("Enemy_Patroller");

            if (player != null)
            {
                report.Append($"\n  Player at {player.transform.position}");
            }

            if (enemy != null)
            {
                report.Append($"\n  Enemy at {enemy.transform.position}");
            }

            Debug.Log(report.ToString(), house);
        }

        [MenuItem("Ashen Woods/Diagnostics/Log House Overlap Check", false, 94)]
        private static void LogHouseOverlaps()
        {
            GameObject house = GameObject.Find(HouseRootName);
            if (house == null)
            {
                Debug.Log("[House] Not in this scene, so there is nothing to check.");
                return;
            }

            Debug.Log(BuildOverlapReport(house), house);
        }

        internal static string BuildOverlapReport(GameObject house)
        {
            Renderer[] renderers = house.GetComponentsInChildren<Renderer>();
            var boxes = new List<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                // Ramps deliberately sink their low end into the floor; treads are decoration.
                // Dressing (models, overlays) is decoration made of overlapping parts by design.
                string name = renderers[i].name;
                bool dressing = renderers[i].transform.parent != null
                                && renderers[i].GetComponentInParent<Transform>().root == house.transform.root
                                && IsUnderDressing(renderers[i].transform, house.transform);
                if (!dressing && !name.Contains("Stair_Ramp") && !name.Contains("_Tread_"))
                {
                    boxes.Add(renderers[i]);
                }
            }

            var report = new System.Text.StringBuilder("[House] Overlap check");
            int overlaps = 0;

            for (int i = 0; i < boxes.Count; i++)
            {
                Bounds a = boxes[i].bounds;

                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Bounds b = boxes[j].bounds;
                    Vector3 shared = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);

                    // 2 cm on every axis: touching surfaces are not an overlap.
                    if (shared.x <= 0.02f || shared.y <= 0.02f || shared.z <= 0.02f)
                    {
                        continue;
                    }

                    overlaps++;
                    if (overlaps <= 12)
                    {
                        report.Append($"\n  {boxes[i].name} overlaps {boxes[j].name} by " +
                                      $"({shared.x:0.00}, {shared.y:0.00}, {shared.z:0.00}) m");
                    }
                }
            }

            report.Append(overlaps == 0
                ? $": {boxes.Count} boxes, none overlapping."
                : $"\n  {overlaps} overlapping pair(s) among {boxes.Count} boxes.");
            return report.ToString();
        }

        private static bool IsUnderDressing(Transform transform, Transform house)
        {
            Transform dressing = house.Find(DressingRootName);
            return dressing != null && transform.IsChildOf(dressing);
        }

        // ----------------------------------------------------------------- helpers

        private static int EnsureBox(
            Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject existing = parent.Find(name)?.gameObject;
            if (existing != null)
            {
                return 0;
            }

            Stage1PlaygroundBuilder.CreateBox(name, parent, center, size, material);
            return 1;
        }

        private static GameObject EnsureBoxRotated(
            Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation,
            Material material)
        {
            GameObject existing = parent.Find(name)?.gameObject;
            if (existing != null)
            {
                return null;
            }

            GameObject box = Stage1PlaygroundBuilder.CreateBox(name, parent, center, size, material);
            box.transform.localRotation = rotation;
            return box;
        }

        private static int EnsureWallX(
            Transform parent, string name, float z, float fromX, float toX,
            float bottom, float top, Material material)
        {
            return EnsureBox(parent, name, new Vector3((fromX + toX) * 0.5f, (bottom + top) * 0.5f, z),
                new Vector3(toX - fromX, top - bottom, Wall), material);
        }

        private static int EnsureWallZ(
            Transform parent, string name, float x, float fromZ, float toZ,
            float bottom, float top, Material material)
        {
            return EnsureBox(parent, name, new Vector3(x, (bottom + top) * 0.5f, (fromZ + toZ) * 0.5f),
                new Vector3(Wall, top - bottom, toZ - fromZ), material);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == path)
                {
                    existing[i].enabled = true;
                    EditorBuildSettings.scenes = existing;
                    return;
                }
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            System.Array.Copy(existing, updated, existing.Length);
            updated[existing.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
