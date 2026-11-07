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
    /// The rebuilt house: two storeys, eight rooms, one staircase, built from a plan that cannot
    /// overlap itself.
    ///
    /// Why a new scene instead of patching the playground: the playground grew room by room
    /// (Stage 1 shell, corridor, then the east wing), so its walls, lintels and door frames were
    /// authored at different times and started sharing the same space. This builder lays the
    /// whole house out at once from one set of coordinates:
    ///
    ///  - Every room is a rectangle, and walls sit on the rectangle boundaries, so no two wall
    ///    boxes ever occupy the same space.
    ///  - A doorway is cut out of one wall (opening + lintel + optional sill), never added as a
    ///    second frame on top of a wall, which is what made the old door frames overlap.
    ///  - Door openings are 1.6 m for a 1.5 m door leaf, 2.1 m tall, with a 2.15 m wall hole
    ///    around the escape frame so the frame never z-fights with the wall.
    ///
    /// Interior rectangles (1 unit = 1 m, ground floor top at y = 0,
    /// first floor top at y = 3.2, ceiling at y = 6.0):
    ///
    ///   GROUND                              FIRST FLOOR
    ///   living   x -6.0 .. 0.2  z -2 .. 1.8  study    (above the living room)
    ///   kitchen  x  0.4 .. 6.0  z -2 .. 1.8  bathroom (above the kitchen)
    ///   foyer    x -6.0 .. 0.2  z  2 .. 6.0  bedroom  (above the foyer, player-side start below)
    ///   hall     x  0.4 .. 6.0  z  2 .. 6.0  landing  (above the hall, stairwell hole cut out)
    ///
    /// The pantry is the kitchen's north-east corner (x 4.3 .. 6.0, z -2 .. -0.2) and holds the
    /// crowbar behind the locked door. The stair runs east along the hall's south wall, so the
    /// enemy can walk up it: a ramp collider carries the body, the treads are only drawn.
    ///
    /// The stage builders still create the doors, key, breaker, crowbar, escape frame, enemy,
    /// wardrobes and navigation points; this builder then moves each one onto its house anchor.
    /// Moving (rather than rewriting every stage constant) keeps the older scenes working.
    ///
    /// Menu:
    ///   Ashen Woods > Level > Create Two-Storey House Scene
    ///   Ashen Woods > Level > Build Or Update The House
    ///   Ashen Woods > Diagnostics > Log House Layout
    ///   Ashen Woods > Diagnostics > Log House Overlap Check
    /// </summary>
    public static class HouseBuilder
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
        private const float UpperDoorHead = 5.25f;

        /// <summary>Where a fresh run starts: in the foyer, facing into the house.</summary>
        internal static readonly Vector3 PlayerStartPosition = new Vector3(-2.9f, 0.05f, 4.6f);
        internal const float PlayerStartYaw = 180f;

        /// <summary>The enemy starts upstairs in the study, so the first minute is quiet.</summary>
        internal static readonly Vector3 EnemySpawnPosition = new Vector3(-2.4f, 3.2f, -0.4f);

        /// <summary>The escape exit, in the foyer's south wall, centred on its 1.5 m hole.</summary>
        internal static readonly Vector3 FrontDoorPosition = new Vector3(-2.9f, 0f, 6f);

        /// <summary>
        /// Walkable points. Ground nodes are at y = 0, first-floor nodes at y = 3.2, and the ramp
        /// nodes sit on the ramp surface. Doorways get a node on each side, centred on the
        /// opening, which is what lets the enemy's 0.3 m clearance fit through every link.
        /// </summary>
        internal static readonly Vector3[] NavNodes =
        {
            new Vector3(-3f, 0f, 0f),      // 0  living, centre
            new Vector3(-3f, 0f, 1.2f),    // 1  living, south of the main door
            new Vector3(-3f, 0f, 2.8f),    // 2  foyer, north of the main door
            new Vector3(-3f, 0f, 4.4f),    // 3  foyer, centre (player starts just south of here)
            new Vector3(-1f, 0f, 4.4f),    // 4  foyer, at the hall doorway
            new Vector3(1.4f, 0f, 4.4f),   // 5  hall, at the foyer doorway
            new Vector3(2.8f, 0f, 4.4f),   // 6  hall, centre
            new Vector3(0.8f, 0f, 5.5f),   // 7  hall, foot of the stairs
            new Vector3(3.4f, 0f, 2.8f),   // 8  hall, at the kitchen doorway
            new Vector3(2.4f, 0f, 0.6f),   // 9  kitchen, centre
            new Vector3(3.4f, 0f, -1.1f),  // 10 kitchen, at the pantry door
            new Vector3(5.2f, 0f, -1.1f),  // 11 pantry
            new Vector3(-4.6f, 0f, 0.6f),  // 12 living west, by the crouch tunnel
            new Vector3(-3f, 3.2f, 0f),    // 13 study, centre (enemy start)
            new Vector3(-3f, 3.2f, 1.2f),  // 14 study, south of the bedroom door
            new Vector3(-3f, 3.2f, 2.8f),  // 15 bedroom, north of the study door
            new Vector3(-3f, 3.2f, 4.4f),  // 16 bedroom, centre
            new Vector3(-0.5f, 3.2f, 3.6f),// 17 bedroom, at the landing doorway
            new Vector3(1.4f, 3.2f, 4.4f), // 18 landing, at the bedroom doorway
            new Vector3(2.8f, 3.2f, 4.4f), // 19 landing, centre
            new Vector3(3.4f, 3.2f, 2.8f), // 20 landing, at the bathroom doorway
            new Vector3(2.4f, 3.2f, 0.6f), // 21 bathroom, centre
            new Vector3(5.5f, 3.2f, 5.5f), // 22 top of the stairs
            new Vector3(5.3f, 3.2f, 4.4f), // 23 landing, east
            new Vector3(-4.6f, 3.2f, 0.4f),// 24 study west
            new Vector3(-4.6f, 3.2f, 4.4f),// 25 bedroom west
            new Vector3(0.9f, 3.2f, 0.4f), // 26 bathroom, at the study doorway
            new Vector3(1.9f, 0.44f, 5.5f),// 27 stairs, first ramp step
            new Vector3(2.8f, 1.24f, 5.5f),// 28 stairs, second ramp step
            new Vector3(3.7f, 2.04f, 5.5f),// 29 stairs, third ramp step
            new Vector3(4.6f, 2.84f, 5.5f),// 30 stairs, fourth ramp step
            new Vector3(0.9f, 0f, 0.4f)    // 31 kitchen, at the living-room doorway
        };

        /// <summary>
        /// The loop the enemy walks when it has no reason to chase. It uses both floors, so the
        /// stairs are exercised on every patrol and a player hiding upstairs is never safe.
        /// </summary>
        private static readonly int[] PatrolNodeIndices = { 0, 3, 5, 8, 10, 15, 16, 19, 21, 13 };

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
                      "Press Play. The player starts in the foyer, the enemy starts upstairs in the study.\n" +
                      "Route: iron key under the crouch tunnel (living room) -> locked pantry (kitchen) -> " +
                      "crowbar -> breaker panel (upstairs study) -> front door (foyer).");
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
                Debug.Log($"[House] Added {added} missing piece(s). Save the scene with Ctrl+S, then press Play.");
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
        /// Adds every missing house piece and adopts the stage objects. Additive and idempotent:
        /// boxes are looked up by name, and the stage objects are only moved while they still sit
        /// where their builder put them, so anything moved by hand survives.
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

            Transform house = environment.transform.Find(HouseRootName);
            if (house == null)
            {
                GameObject houseObject = new GameObject(HouseRootName);
                houseObject.transform.SetParent(environment.transform, false);
                house = houseObject.transform;
            }

            int added = 0;

            // Stage 2 builds its own little closet when an Environment exists. The pantry wall is
            // part of the house plan instead, so this empty root tells Stage 2 the closet exists.
            if (GameObject.Find(ClosetRootName) == null)
            {
                GameObject closet = new GameObject(ClosetRootName);
                closet.transform.SetParent(house, false);
                added++;
            }

            added += EnsureFloors(house, floorMaterial, ceilingMaterial);
            added += EnsureWalls(house, wallMaterial);
            added += EnsureStairs(house, trimMaterial);
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

            added += EnsureExtraNavNodes();
            added += AlignStageObjects();

            AssetDatabase.SaveAssets();
            return added;
        }

        // ----------------------------------------------------------------- floors

        private static int EnsureFloors(Transform house, Material floor, Material ceiling)
        {
            int added = 0;

            // Ground slab, then the four first-floor slabs. The landing is three pieces because
            // the stairwell is a hole: the stairs land on the east piece.
            added += EnsureBox(house, "House_Floor_Ground",
                new Vector3(0f, -0.1f, 2f), new Vector3(12f, 0.2f, 8f), floor);
            added += EnsureBox(house, "House_Floor_Study",
                new Vector3(-2.9f, 3.1f, -0.1f), new Vector3(6.2f, 0.2f, 3.8f), floor);
            added += EnsureBox(house, "House_Floor_Bathroom",
                new Vector3(3.2f, 3.1f, -0.1f), new Vector3(5.6f, 0.2f, 3.8f), floor);
            added += EnsureBox(house, "House_Floor_Bedroom",
                new Vector3(-2.9f, 3.1f, 4f), new Vector3(6.2f, 0.2f, 4f), floor);
            added += EnsureBox(house, "House_Floor_Landing_North",
                new Vector3(3.2f, 3.1f, 3.5f), new Vector3(5.6f, 0.2f, 3f), floor);
            added += EnsureBox(house, "House_Floor_Landing_West",
                new Vector3(0.9f, 3.1f, 5.5f), new Vector3(1f, 0.2f, 1f), floor);
            added += EnsureBox(house, "House_Floor_Landing_East",
                new Vector3(5.5f, 3.1f, 5.5f), new Vector3(1f, 0.2f, 1f), floor);

            added += EnsureBox(house, "House_Ceiling",
                new Vector3(0f, UpperCeiling + 0.1f, 2f), new Vector3(12f, 0.2f, 8f), ceiling);

            return added;
        }

        // ------------------------------------------------------------------ walls

        private static int EnsureWalls(Transform house, Material wall)
        {
            int added = 0;

            // Exterior shell. The south wall carries the escape exit, so its 1.5 m hole is cut
            // 2.15 m tall: the frame's own lintel fits inside the hole instead of inside the wall.
            added += EnsureWallX(house, "House_Wall_North", -2.1f, -6.1f, 6.1f, 0f, UpperCeiling, wall);
            added += EnsureWallXOpening(house, "House_Wall_South", 6.1f, -6.1f, 6.1f, 0f, UpperCeiling,
                -3.65f, -2.15f, 0f, 2.15f, wall);
            added += EnsureWallZ(house, "House_Wall_West", -6.1f, -2f, 6f, 0f, UpperCeiling, wall);
            added += EnsureWallZ(house, "House_Wall_East", 6.1f, -2f, 6f, 0f, UpperCeiling, wall);

            // Ground floor partitions. The centre wall spans to z = 2 so the junction with the
            // mid wall is filled instead of leaving a 0.2 m square hole at the crossing.
            added += EnsureWallZOpening(house, "House_Wall_Centre_Ground_North", 0.3f, -2f, 2f, 0f,
                GroundCeiling, -0.6f, 1f, 0f, DoorHead, wall);
            added += EnsureWallZOpening(house, "House_Wall_Centre_Ground_South", 0.3f, 2f, 6f, 0f,
                GroundCeiling, 3.6f, 5.2f, 0f, DoorHead, wall);
            added += EnsureWallXOpening(house, "House_Wall_Mid_Ground_West", 1.9f, -6f, 0.2f, 0f,
                GroundCeiling, -3.8f, -2.2f, 0f, DoorHead, wall);
            added += EnsureWallXOpening(house, "House_Wall_Mid_Ground_East", 1.9f, 0.4f, 6f, 0f,
                GroundCeiling, 2.6f, 4.2f, 0f, DoorHead, wall);

            // Pantry: partition at x = 4.2 with the locked doorway, and its south wall.
            added += EnsureWallZOpening(house, "House_Wall_Pantry_West", 4.2f, -2f, 0f, 0f,
                GroundCeiling, -1.9f, -0.3f, 0f, DoorHead, wall);
            added += EnsureWallX(house, "House_Wall_Pantry_South", -0.1f, 4.3f, 6f, 0f,
                GroundCeiling, wall);

            // First floor. These doorways are open (no doors) and 2.0 m wide, which gives the
            // navigation graph room to pass a 0.3 m clearance ray on each side.
            added += EnsureWallZOpening(house, "House_Wall_Centre_Upper_North", 0.3f, -2f, 2f,
                UpperFloor, UpperCeiling, -0.6f, 1.4f, UpperFloor, UpperDoorHead, wall);
            added += EnsureWallZOpening(house, "House_Wall_Centre_Upper_South", 0.3f, 2f, 6f,
                UpperFloor, UpperCeiling, 3.4f, 5.4f, UpperFloor, UpperDoorHead, wall);
            added += EnsureWallXOpening(house, "House_Wall_Mid_Upper_West", 1.9f, -6f, 0.2f,
                UpperFloor, UpperCeiling, -4f, -2f, UpperFloor, UpperDoorHead, wall);
            added += EnsureWallXOpening(house, "House_Wall_Mid_Upper_East", 1.9f, 0.4f, 6f,
                UpperFloor, UpperCeiling, 2.4f, 4.4f, UpperFloor, UpperDoorHead, wall);

            return added;
        }

        // ----------------------------------------------------------------- stairs

        private static int EnsureStairs(Transform house, Material trim)
        {
            int added = 0;

            // rise 3.2 over a 3.6 run: 41.6 degrees, inside the player's 45 degree slope limit,
            // and the collider is one smooth ramp so the enemy's height probes never clip a tread.
            if (EnsureBoxRotated(house, "House_Stair_Ramp",
                    new Vector3(3.2f, 1.525f, 5.5f), new Vector3(4.82f, 0.2f, 1f),
                    Quaternion.Euler(0f, 0f, 41.63f), trim) != null)
            {
                added++;
            }

            // Drawn treads sit on the ramp surface; they are decorative, so they carry no collider.
            for (int i = 0; i < 18; i++)
            {
                float along = -2.2f + (i + 0.5f) * 0.244f;
                GameObject tread = EnsureBoxRotated(house, "House_Stair_Tread_" + (i + 1),
                    new Vector3(3.2f + along * 0.747f, 1.525f + along * 0.664f + 0.12f, 5.5f),
                    new Vector3(0.22f, 0.05f, 1f), Quaternion.Euler(0f, 0f, 41.63f), trim);

                if (tread == null)
                {
                    continue;
                }

                added++;

                if (tread.GetComponent<Collider>() != null)
                {
                    Object.DestroyImmediate(tread.GetComponent<Collider>());
                }
            }

            // Rails guard the first-floor hole except where the stairs arrive (x 5 .. 6).
            added += EnsureBox(house, "House_Stair_Rail_North",
                new Vector3(3.2f, 3.65f, 5f), new Vector3(3.6f, 0.9f, 0.08f), trim);
            // Starts at z 5.04 so it butts against the north rail instead of overlapping it.
            added += EnsureBox(house, "House_Stair_Rail_West",
                new Vector3(1.4f, 3.65f, 5.52f), new Vector3(0.08f, 0.9f, 0.96f), trim);

            return added;
        }

        // ------------------------------------------------------------------ props

        private static int EnsureProps(Transform house, Material prop, Material trim)
        {
            int added = 0;

            // Living room: the crouch tunnel. 1.1 m of clearance under a 1.25 m roof: the player
            // (0.95 m crouched) fits, the enemy's 1.5 m head clearance never does.
            added += EnsureBox(house, "House_Living_CrouchRoof",
                new Vector3(-4.8f, 1.25f, -1.3f), new Vector3(2.4f, 0.3f, 1.4f), prop);
            added += EnsureBox(house, "House_Living_CrouchSupport",
                new Vector3(-3.7f, 0.55f, -1.3f), new Vector3(0.2f, 1.1f, 1.4f), prop);
            added += EnsureBox(house, "House_Living_Crate",
                new Vector3(-1.4f, 0.3f, 0.8f), new Vector3(0.6f, 0.6f, 0.6f), prop);

            // Foyer and kitchen cover.
            added += EnsureBox(house, "House_Foyer_Sideboard",
                new Vector3(-5.65f, 0.4f, 5.5f), new Vector3(0.7f, 0.8f, 1f), prop);
            added += EnsureBox(house, "House_Kitchen_Counter",
                new Vector3(5.75f, 0.45f, 0.8f), new Vector3(0.5f, 0.9f, 1.6f), prop);
            added += EnsureBox(house, "House_Kitchen_Island",
                new Vector3(1.4f, 0.45f, -0.6f), new Vector3(1f, 0.9f, 0.6f), trim);
            added += EnsureBox(house, "House_Pantry_Shelf",
                new Vector3(5.1f, 0.9f, -1.8f), new Vector3(1.4f, 1.8f, 0.35f), trim);

            // Bedroom bed, clear of every walk link.
            added += EnsureBox(house, "House_Bedroom_Bed",
                new Vector3(-0.6f, 3.45f, 5f), new Vector3(1.6f, 0.5f, 1.8f), prop);

            return added;
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

            added += EnsurePointLight(lighting, "House_Light_Foyer",
                new Vector3(-2.9f, 2.6f, 4.4f), 2.8f, 9f, new Color(1f, 0.86f, 0.68f));
            added += EnsurePointLight(lighting, "House_Light_Living",
                new Vector3(-3f, 2.6f, 0f), 2.6f, 9f, new Color(1f, 0.85f, 0.65f));
            added += EnsurePointLight(lighting, "House_Light_Kitchen",
                new Vector3(3f, 2.55f, 0.6f), 2.4f, 8f, new Color(0.86f, 0.9f, 0.84f));
            added += EnsurePointLight(lighting, "House_Light_Hall",
                new Vector3(4.4f, 2.6f, 3.2f), 2.6f, 8f, new Color(1f, 0.87f, 0.7f));
            added += EnsurePointLight(lighting, "House_Light_Pantry",
                new Vector3(5.1f, 2.5f, -1.2f), 1.2f, 4f, new Color(0.95f, 0.9f, 0.8f));
            added += EnsurePointLight(lighting, "House_Light_Bedroom",
                new Vector3(-3f, 5.5f, 4.4f), 2.4f, 9f, new Color(1f, 0.84f, 0.66f));
            added += EnsurePointLight(lighting, "House_Light_Study",
                new Vector3(-3f, 5.5f, 0f), 2.2f, 8f, new Color(0.9f, 0.88f, 0.78f));
            added += EnsurePointLight(lighting, "House_Light_Bathroom",
                new Vector3(3.2f, 5.5f, 0.6f), 1.8f, 7f, new Color(0.84f, 0.9f, 0.9f));
            added += EnsurePointLight(lighting, "House_Light_Landing",
                new Vector3(2.6f, 5.5f, 4.4f), 2f, 8f, new Color(1f, 0.86f, 0.7f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.skybox = null;

            return added;
        }

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
            light.shadows = LightShadows.Soft;

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
        /// Stage 3 creates one node per entry in its own list (23). The house needs 32, so the
        /// extra ones are created here before the alignment step positions every node.
        /// </summary>
        private static int EnsureExtraNavNodes()
        {
            GameObject navigationRoot = GameObject.Find("Enemy_Navigation");
            if (navigationRoot == null)
            {
                return 0;
            }

            Transform nodes = navigationRoot.transform.Find("NavNodes");
            if (nodes == null)
            {
                return 0;
            }

            int added = 0;

            for (int i = 0; i < NavNodes.Length; i++)
            {
                string pointName = "Node_" + i;
                if (nodes.Find(pointName) != null)
                {
                    continue;
                }

                GameObject point = new GameObject(pointName);
                point.transform.SetParent(nodes, false);
                point.transform.localPosition = NavNodes[i];
                added++;
            }

            return added;
        }

        // -------------------------------------------------------------- alignment

        /// <summary>
        /// Moves each stage object onto its house anchor. Objects already at their anchor are
        /// left alone, so this is safe to run again; objects a person moved by hand are listed
        /// in the log instead of being snapped back.
        /// </summary>
        private static int AlignStageObjects()
        {
            int moved = 0;

            // Main door: the foyer/living-room doorway. Hinge at the west side, leaf swings east.
            moved += MoveObject("Door_Main", new Vector3(-3.75f, 0f, 1.9f), Quaternion.identity);

            // Locked pantry door, hinge on the north side, leaf swings south along the wall.
            moved += MoveObject("Door_Closet_Locked", new Vector3(4.2f, 0f, -1.9f),
                Quaternion.Euler(0f, -90f, 0f));

            // Iron key: on the floor under the crouch tunnel's roof.
            moved += MoveObject("Key_Iron", new Vector3(-4.8f, 0.15f, -1.3f), null);

            // Breaker panel: upstairs study, on the north wall, switches facing into the room.
            // y = first floor (3.2) + 1.35. It used to be 1.35, which put the panel on the ground
            // floor inside the roof of the living-room crouch tunnel.
            moved += MoveObject("BreakerPanel", new Vector3(-4f, UpperFloor + 1.35f, -1.93f), Quaternion.identity);

            // Crowbar: inside the pantry, in front of the shelf.
            moved += MoveObject("Crowbar_Closet", new Vector3(5.2f, 0.16f, -1.1f), null);

            // Escape exit: foyer's south wall, opening into the room.
            moved += MoveObject("Escape_FrontDoor", FrontDoorPosition, Quaternion.identity);

            // Enemy: study, upstairs.
            moved += MoveObject("Enemy_Patroller", EnemySpawnPosition, Quaternion.identity);

            // Wardrobes: one in the bedroom, one in the study.
            moved += MoveObject("Hiding_Wardrobe_West", new Vector3(-4f, 3.2f, 5.675f),
                Quaternion.Euler(0f, 180f, 0f));
            moved += MoveObject("Hiding_Wardrobe_East", new Vector3(-5.675f, 3.2f, 0.4f),
                Quaternion.Euler(0f, 90f, 0f));

            moved += AlignNavigation();

            return moved;
        }

        private static int AlignNavigation()
        {
            GameObject navigationRoot = GameObject.Find("Enemy_Navigation");
            if (navigationRoot == null)
            {
                return 0;
            }

            Transform nodes = navigationRoot.transform.Find("NavNodes");
            Transform patrol = navigationRoot.transform.Find("PatrolRoute");

            int moved = 0;

            if (nodes != null)
            {
                for (int i = 0; i < NavNodes.Length; i++)
                {
                    Transform node = nodes.Find("Node_" + i);
                    if (node != null && Vector3.Distance(node.localPosition, NavNodes[i]) > 0.001f)
                    {
                        node.localPosition = NavNodes[i];
                        EditorUtility.SetDirty(node);
                        moved++;
                    }
                }
            }

            if (patrol != null)
            {
                for (int i = 0; i < PatrolNodeIndices.Length; i++)
                {
                    Transform point = patrol.Find("Patrol_" + i);
                    Vector3 expected = NavNodes[PatrolNodeIndices[i]];

                    if (point != null && Vector3.Distance(point.localPosition, expected) > 0.001f)
                    {
                        point.localPosition = expected;
                        EditorUtility.SetDirty(point);
                        moved++;
                    }
                }
            }

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

            target.transform.position = position;
            if (rotation != null)
            {
                target.transform.rotation = rotation.Value;
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

            var report = new System.Text.StringBuilder("[House] Layout");
            report.Append("\n  GROUND  living   x -6.0..0.2  z -2.0..1.8   crouch tunnel + iron key");
            report.Append("\n          kitchen  x  0.4..6.0  z -2.0..1.8   pantry x 4.3..6.0 z -2.0..-0.2");
            report.Append("\n          foyer    x -6.0..0.2  z  2.0..6.0   player start, front door");
            report.Append("\n          hall     x  0.4..6.0  z  2.0..6.0   stairs along the south wall");
            report.Append("\n  FIRST   study    x -6.0..0.2  z -2.0..1.8   enemy start, breaker panel");
            report.Append("\n          bathroom x  0.4..6.0  z -2.0..1.8");
            report.Append("\n          bedroom  x -6.0..0.2  z  2.0..6.0   wardrobe on the north wall");
            report.Append("\n          landing  x  0.4..6.0  z  2.0..6.0   stairwell hole x 1.4..5.0 z 5.0..6.0");
            report.Append("\n  Storey heights: ground floor top y 0.0, first floor top y 3.2, ceiling y 6.0");

            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            GameObject enemy = GameObject.Find("Enemy_Patroller");

            if (player != null)
            {
                report.Append($"\n  Player at ({player.transform.position.x:0.0}, {player.transform.position.y:0.0}, " +
                              $"{player.transform.position.z:0.0})");
            }

            if (enemy != null)
            {
                report.Append($"\n  Enemy at ({enemy.transform.position.x:0.0}, {enemy.transform.position.y:0.0}, " +
                              $"{enemy.transform.position.z:0.0})");
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

            Renderer[] renderers = house.GetComponentsInChildren<Renderer>();
            var boxes = new List<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                // The ramp deliberately sinks its low end into the floor slab.
                if (!renderers[i].name.Contains("Stair_Ramp"))
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

            if (overlaps == 0)
            {
                report.Append(": no overlapping boxes. Door frames, walls, floors and props all sit " +
                              "in their own space.");
            }
            else
            {
                report.Append($"\n  {overlaps} overlapping pair(s).");
            }

            Debug.Log(report.ToString(), house);
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

        private static int EnsureWallXOpening(
            Transform parent, string name, float z, float fromX, float toX,
            float bottom, float top, float openFrom, float openTo, float openBottom, float openTop,
            Material material)
        {
            int added = 0;

            if (openFrom > fromX + 0.001f)
            {
                added += EnsureBox(parent, name + "_West",
                    new Vector3((fromX + openFrom) * 0.5f, (bottom + top) * 0.5f, z),
                    new Vector3(openFrom - fromX, top - bottom, Wall), material);
            }

            if (toX > openTo + 0.001f)
            {
                added += EnsureBox(parent, name + "_East",
                    new Vector3((openTo + toX) * 0.5f, (bottom + top) * 0.5f, z),
                    new Vector3(toX - openTo, top - bottom, Wall), material);
            }

            if (top > openTop + 0.001f)
            {
                added += EnsureBox(parent, name + "_Lintel",
                    new Vector3((openFrom + openTo) * 0.5f, (openTop + top) * 0.5f, z),
                    new Vector3(openTo - openFrom, top - openTop, Wall), material);
            }

            if (openBottom > bottom + 0.001f)
            {
                added += EnsureBox(parent, name + "_Sill",
                    new Vector3((openFrom + openTo) * 0.5f, (bottom + openBottom) * 0.5f, z),
                    new Vector3(openTo - openFrom, openBottom - bottom, Wall), material);
            }

            return added;
        }

        private static int EnsureWallZOpening(
            Transform parent, string name, float x, float fromZ, float toZ,
            float bottom, float top, float openFrom, float openTo, float openBottom, float openTop,
            Material material)
        {
            int added = 0;

            if (openFrom > fromZ + 0.001f)
            {
                added += EnsureBox(parent, name + "_South",
                    new Vector3(x, (bottom + top) * 0.5f, (fromZ + openFrom) * 0.5f),
                    new Vector3(Wall, top - bottom, openFrom - fromZ), material);
            }

            if (toZ > openTo + 0.001f)
            {
                added += EnsureBox(parent, name + "_North",
                    new Vector3(x, (bottom + top) * 0.5f, (openTo + toZ) * 0.5f),
                    new Vector3(Wall, top - bottom, toZ - openTo), material);
            }

            if (top > openTop + 0.001f)
            {
                added += EnsureBox(parent, name + "_Lintel",
                    new Vector3(x, (openTop + top) * 0.5f, (openFrom + openTo) * 0.5f),
                    new Vector3(Wall, top - openTop, openTo - openFrom), material);
            }

            if (openBottom > bottom + 0.001f)
            {
                added += EnsureBox(parent, name + "_Sill",
                    new Vector3(x, (bottom + openBottom) * 0.5f, (openFrom + openTo) * 0.5f),
                    new Vector3(Wall, openBottom - bottom, openTo - openFrom), material);
            }

            return added;
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
