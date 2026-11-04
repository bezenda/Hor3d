using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.Player;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The east wing: a boot room the player starts in, a hall, a kitchen, a gallery and a
    /// narrow back passage, all built onto the corridor's dead end.
    ///
    /// Why the level grew: the original house put the player alone in a 1.8 m corridor that
    /// looked straight into the enemy's patrol room, so a fresh run could end in a chase
    /// before the player had taken a step. The boot room is closed on every side but one,
    /// the enemy starts in the far room, and EnemyBrain keeps its senses off for the first
    /// few seconds of a run.
    ///
    /// Interior rectangles (1 unit = 1 m, floor at y = 0):
    ///
    ///   gallery  x -2.4 .. 4.4   z 13.2 .. 16.6   long room, the loop closes behind it
    ///   passage  x  4.6 .. 6.6   z 12.6 .. 16.6   narrow back route
    ///   kitchen  x  3.0 .. 7.0   z  8.4 .. 12.4
    ///   hall     x -1.6 .. 2.8   z  8.4 .. 13.0   junction, the corridor enters from the south
    ///   boot     x -5.4 .. -1.8  z  8.4 .. 13.0   player start, deliberately never patrolled
    ///
    /// Additive and idempotent like every other builder: boxes are looked up by name and only
    /// added when missing, so anything moved by hand survives a re-run.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 1 - Build The East Wing
    ///   Ashen Woods > Diagnostics/Log Wing Layout
    /// </summary>
    public static class Stage1WingBuilder
    {
        internal const string WingRootName = "Wing";
        private const string EnvironmentName = "Environment";

        /// <summary>Where a fresh run begins: behind two walls, facing the boot room's exit.</summary>
        internal static readonly Vector3 PlayerStartPosition = new Vector3(-4.4f, 0.05f, 10.2f);
        internal const float PlayerStartYaw = 90f;

        /// <summary>
        /// The house's front door: centred in the gallery's far wall, which is the wing's only
        /// exterior wall. Stage 6 builds its escape exit here so both files agree on one spot.
        /// </summary>
        internal static readonly Vector3 FrontDoorPosition = new Vector3(0.65f, 0f, 16.55f);

        /// <summary>The corridor dead end, where earlier builds mounted the front door.</summary>
        private static readonly Vector3 LegacyFrontDoorPosition = new Vector3(1.8f, 0f, 8f);

        /// <summary>The spawn every earlier build used. Only a player standing here is moved.</summary>
        private static readonly Vector3 LegacyPlayerSpawn = new Vector3(1.8f, 0.05f, 7.2f);

        private const float Wall = 0.2f;
        private const float DoorHeight = 2.05f;

        // The player crouches to 0.95 m, so 1.05 m leaves room for the controller skin while
        // still being too low for the enemy's 1.5 m head-clearance ray.
        private const float CrawlHeight = 1.05f;

        [MenuItem("Ashen Woods/Stage 1 - Build The East Wing", false, 8)]
        private static void BuildWingMenu()
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
                        + Stage3EnemyBuilder.EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Wing] Added {added} piece(s). Save the scene with Ctrl+S, then press Play. " +
                          "The player now starts in the boot room, west of the hall.");
            }
            else
            {
                Debug.Log("[Wing] The wing and every stage are already built - nothing was changed.");
            }
        }

        /// <summary>
        /// Adds every missing wing piece and re-points what it owns. Safe to call repeatedly,
        /// from any of the Ashen Woods menu items.
        /// </summary>
        /// <returns>How many pieces were added.</returns>
        internal static int EnsureWingPieces()
        {
            int added = 0;

            GameObject environment = GameObject.Find(EnvironmentName);
            if (environment == null)
            {
                environment = new GameObject(EnvironmentName);
                added++;
            }

            Transform wing = environment.transform.Find(WingRootName);
            if (wing == null)
            {
                GameObject wingObject = new GameObject(WingRootName);
                wingObject.transform.SetParent(environment.transform, false);
                wing = wingObject.transform;
                added++;
            }

            Material floorMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Floor_Wing", new Color(0.19f, 0.17f, 0.14f));
            Material wallMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Wall", new Color(0.33f, 0.31f, 0.28f));
            Material ceilingMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Ceiling", new Color(0.14f, 0.13f, 0.12f));
            Material propMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Prop", new Color(0.35f, 0.25f, 0.16f));
            Material trimMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_WingTrim", new Color(0.27f, 0.29f, 0.26f));

            added += EnsureCorridorOpening(wing, wallMaterial);
            added += EnsureFrontDoorMoved();
            added += EnsureFloorsAndCeilings(wing, floorMaterial, ceilingMaterial);
            added += EnsureWalls(wing, wallMaterial);
            added += EnsureLights(wing);
            added += EnsureProps(wing, propMaterial, trimMaterial);
            added += EnsurePlayerAtWingStart();

            return added;
        }

        // ---------------------------------------------------------------- corridor

        /// <summary>
        /// Opens the corridor's dead end. This is the one place the wing changes an object an
        /// earlier stage created, so it only does it while that wall still has the exact
        /// position and size the Stage 1 builder gave it.
        /// </summary>
        private static int EnsureCorridorOpening(Transform wing, Material wallMaterial)
        {
            int added = 0;

            GameObject endWall = GameObject.Find("Corridor_Wall_End");
            if (endWall != null)
            {
                Vector3 expectedScale = new Vector3(1.8f, 2.4f, 0.2f);
                Vector3 expectedPosition = new Vector3(1.8f, 1.2f, 8.1f);

                bool untouched = Vector3.Distance(endWall.transform.position, expectedPosition) < 0.05f
                                 && Vector3.Distance(endWall.transform.localScale, expectedScale) < 0.05f;

                if (untouched)
                {
                    Object.DestroyImmediate(endWall);
                    added++;
                }
                else
                {
                    Debug.LogWarning(
                        "Corridor_Wall_End has been moved or resized by hand, so it was left alone. " +
                        "The wing is built, but the corridor stays sealed: delete that wall yourself, or " +
                        "move it back to the Stage 1 position and run the menu item again.", endWall);
                }
            }

            // The corridor ceiling sits at 2.4 m, so the opening keeps a 0.35 m lintel.
            added += EnsureWallX(wing, "Corridor_End_Lintel", 8.1f, 1f, 2.6f, DoorHeight, 2.4f, wallMaterial);
            return added;
        }

        // ---------------------------------------------------------------- front door

        /// <summary>
        /// Earlier builds mounted the front door on the corridor's dead end. The wing turns
        /// that dead end into the way into the house, and a locked front door there would cut
        /// the old room off completely, so the door is moved to the wing's far wall once.
        /// </summary>
        private static int EnsureFrontDoorMoved()
        {
            GameObject frontDoor = GameObject.Find("Escape_FrontDoor");
            if (frontDoor == null)
            {
                return 0;
            }

            if (Vector3.Distance(frontDoor.transform.position, LegacyFrontDoorPosition) > 1f)
            {
                return 0; // Already moved, or placed by hand somewhere else.
            }

            frontDoor.transform.position = FrontDoorPosition;
            frontDoor.transform.rotation = Quaternion.identity;

            if (PrefabUtility.IsPartOfPrefabInstance(frontDoor))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(frontDoor.transform);
            }
            else
            {
                EditorUtility.SetDirty(frontDoor.transform);
            }

            Debug.Log("[Wing] Moved the front door from the corridor to the gallery's far wall. " +
                      "It is still the escape exit and its puzzle wiring is untouched.", frontDoor);
            return 1;
        }

        // ------------------------------------------------------------------- shell

        private static int EnsureFloorsAndCeilings(
            Transform wing, Material floorMaterial, Material ceilingMaterial)
        {
            int added = 0;

            // Floors meet the corridor floor at z = 8.1 and never overlap each other, which
            // keeps the flat placeholder surfaces from z-fighting.
            added += EnsureFloor(wing, "Wing_Floor_Boot", -5.6f, -1.6f, 8.1f, 13.2f, floorMaterial);
            added += EnsureFloor(wing, "Wing_Floor_Hall", -1.6f, 3.0f, 8.1f, 13.2f, floorMaterial);
            added += EnsureFloor(wing, "Wing_Floor_Kitchen", 3.0f, 7.2f, 8.1f, 12.6f, floorMaterial);
            added += EnsureFloor(wing, "Wing_Floor_Gallery", -2.6f, 4.4f, 13.2f, 16.8f, floorMaterial);
            added += EnsureFloor(wing, "Wing_Floor_Passage", 4.4f, 6.8f, 12.6f, 16.8f, floorMaterial);

            // Ceilings differ per room on purpose: it reads as a real house and gives each
            // space its own silhouette from the inside.
            added += EnsureCeiling(wing, "Wing_Ceiling_Boot", -5.6f, -1.6f, 8.1f, 13.2f, 2.8f, ceilingMaterial);
            added += EnsureCeiling(wing, "Wing_Ceiling_Hall", -1.6f, 3.0f, 8.1f, 13.2f, 3.0f, ceilingMaterial);
            added += EnsureCeiling(wing, "Wing_Ceiling_Kitchen", 3.0f, 7.2f, 8.1f, 12.6f, 2.6f, ceilingMaterial);
            added += EnsureCeiling(wing, "Wing_Ceiling_Gallery", -2.6f, 4.4f, 13.2f, 16.8f, 3.2f, ceilingMaterial);
            added += EnsureCeiling(wing, "Wing_Ceiling_Passage", 4.4f, 6.8f, 12.6f, 16.8f, 2.3f, ceilingMaterial);

            return added;
        }

        private static int EnsureWalls(Transform wing, Material wallMaterial)
        {
            int added = 0;

            // Boot room: closed on three sides, one doorway east into the hall.
            added += EnsureWallX(wing, "Wing_Boot_Wall_South", 8.3f, -5.4f, -1.8f, 0f, 2.8f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Boot_Wall_West", -5.5f, 8.2f, 13.2f, 0f, 2.8f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Boot_Wall_North", 13.1f, -5.4f, -2.6f, 0f, 2.8f, wallMaterial);

            // Hall west wall, shared with the boot room: doorway at z 9.4 .. 11.0.
            added += EnsureWallZ(wing, "Wing_Hall_Wall_West_South", -1.7f, 8.4f, 9.4f, 0f, 3f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Hall_Wall_West_North", -1.7f, 11f, 13f, 0f, 3f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Hall_Wall_West_Lintel", -1.7f, 9.4f, 11f, DoorHeight, 3f, wallMaterial);

            // Hall south wall: the corridor doorway is x 1.0 .. 2.6.
            added += EnsureWallX(wing, "Wing_Hall_Wall_South_West", 8.3f, -1.8f, 1f, 0f, 3f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Hall_Wall_South_East", 8.3f, 2.6f, 3f, 0f, 3f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Hall_Wall_South_Lintel", 8.3f, 1f, 2.6f, DoorHeight, 3f, wallMaterial);

            // Hall east wall: a doorway into the kitchen at z 9.4 .. 11.0, then a crawl hole
            // at z 11.4 .. 12.4 that only a crouching player fits through.
            added += EnsureWallZ(wing, "Wing_Hall_Wall_East_South", 2.9f, 8.4f, 9.4f, 0f, 3f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Hall_Wall_East_DoorLintel", 2.9f, 9.4f, 11f, DoorHeight, 3f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Hall_Wall_East_Middle", 2.9f, 11f, 11.4f, 0f, 3f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Hall_Wall_East_CrawlLintel", 2.9f, 11.4f, 12.4f, CrawlHeight, 3f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Hall_Wall_East_North", 2.9f, 12.4f, 13f, 0f, 3f, wallMaterial);

            // Kitchen. Its north doorway spans the passage, x 4.6 .. 6.6.
            added += EnsureWallX(wing, "Wing_Kitchen_Wall_South", 8.3f, 3f, 7f, 0f, 2.6f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Kitchen_Wall_East", 7.1f, 8.2f, 12.6f, 0f, 2.6f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Kitchen_Wall_North_West", 12.5f, 3f, 4.4f, 0f, 2.6f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Kitchen_Wall_North_Lintel", 12.5f, 4.6f, 6.6f, DoorHeight, 2.6f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Kitchen_Wall_North_East", 12.5f, 6.8f, 7f, 0f, 2.6f, wallMaterial);

            // Gallery, the long room at the back. Its south doorway is x 0.2 .. 1.8.
            added += EnsureWallZ(wing, "Wing_Gallery_Wall_West", -2.5f, 13f, 16.8f, 0f, 3.2f, wallMaterial);

            // The gallery's far wall carries the house's front door, so it has a 1.5 m opening
            // in the middle for the escape exit to sit in.
            added += EnsureWallX(wing, "Wing_Gallery_Wall_North_West", 16.7f, -2.4f, -0.1f, 0f, 3.2f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Gallery_Wall_North_East", 16.7f, 1.4f, 4.4f, 0f, 3.2f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Gallery_Wall_North_Lintel", 16.7f, -0.1f, 1.4f, DoorHeight, 3.2f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Gallery_Wall_South_West", 13.1f, -2.4f, 0.2f, 0f, 3.2f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Gallery_Wall_South_Lintel", 13.1f, 0.2f, 1.8f, DoorHeight, 3.2f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Gallery_Wall_South_East", 13.1f, 1.8f, 4.4f, 0f, 3.2f, wallMaterial);

            // Passage. Its west wall is the gallery's east wall, with a doorway at z 14.4 .. 16.0.
            added += EnsureWallZ(wing, "Wing_Passage_Wall_West_South", 4.5f, 12.4f, 14.4f, 0f, 3.2f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Passage_Wall_West_Lintel", 4.5f, 14.4f, 16f, DoorHeight, 3.2f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Passage_Wall_West_North", 4.5f, 16f, 16.8f, 0f, 3.2f, wallMaterial);
            added += EnsureWallZ(wing, "Wing_Passage_Wall_East", 6.7f, 12.4f, 16.8f, 0f, 2.3f, wallMaterial);
            added += EnsureWallX(wing, "Wing_Passage_Wall_North", 16.7f, 4.6f, 6.6f, 0f, 2.3f, wallMaterial);

            return added;
        }

        // ------------------------------------------------------------------ lights

        private static int EnsureLights(Transform wing)
        {
            int added = 0;

            Transform lighting = wing.Find("Lighting");
            if (lighting == null)
            {
                lighting = new GameObject("Lighting").transform;
                lighting.SetParent(wing, false);
                added++;
            }

            added += EnsurePointLight(lighting, "Wing_Light_Boot",
                new Vector3(-3.6f, 2.45f, 10.2f), 2.6f, 8f, new Color(1f, 0.86f, 0.68f));
            added += EnsurePointLight(lighting, "Wing_Light_Hall",
                new Vector3(0.6f, 2.65f, 10.6f), 3.2f, 9f, new Color(1f, 0.87f, 0.7f));
            added += EnsurePointLight(lighting, "Wing_Light_Kitchen",
                new Vector3(5f, 2.35f, 10.2f), 2.2f, 7f, new Color(0.86f, 0.9f, 0.84f));
            added += EnsurePointLight(lighting, "Wing_Light_Gallery",
                new Vector3(1f, 2.85f, 14.8f), 2.8f, 10f, new Color(1f, 0.85f, 0.66f));
            added += EnsurePointLight(lighting, "Wing_Light_Passage",
                new Vector3(5.6f, 1.95f, 14.4f), 1.5f, 5f, new Color(0.9f, 0.86f, 0.72f));

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

        // ------------------------------------------------------------------- props

        private static int EnsureProps(Transform wing, Material propMaterial, Material trimMaterial)
        {
            int added = 0;

            // Boot room: a bench and a crate, both clear of the walk to the doorway.
            added += EnsureBox(wing, "Wing_Boot_Bench",
                new Vector3(-5.05f, 0.225f, 9.8f), new Vector3(0.5f, 0.45f, 1.4f), propMaterial);
            added += EnsureBox(wing, "Wing_Boot_Crate",
                new Vector3(-2.4f, 0.3f, 12.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);

            // Hall: a sideboard on the south-east side, off every walk line.
            added += EnsureBox(wing, "Wing_Hall_Sideboard",
                new Vector3(2.45f, 0.4f, 8.9f), new Vector3(0.5f, 0.8f, 0.8f), propMaterial);

            // Kitchen: counters, an island that breaks the sightline from the hall doorway,
            // and a crate to crouch behind.
            added += EnsureBox(wing, "Wing_Kitchen_Counter",
                new Vector3(6.75f, 0.45f, 10.4f), new Vector3(0.5f, 0.9f, 2.4f), propMaterial);
            added += EnsureBox(wing, "Wing_Kitchen_Island",
                new Vector3(4f, 0.45f, 11.6f), new Vector3(1.2f, 0.9f, 0.6f), trimMaterial);
            added += EnsureBox(wing, "Wing_Kitchen_Crate",
                new Vector3(3.5f, 0.3f, 9f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);

            // Gallery: two tall shelves at opposite ends, leaving a clear run down the middle.
            added += EnsureBox(wing, "Wing_Gallery_Shelf_East",
                new Vector3(3f, 1f, 13.6f), new Vector3(2f, 2f, 0.35f), trimMaterial);
            added += EnsureBox(wing, "Wing_Gallery_Shelf_West",
                new Vector3(-1.5f, 1f, 16.35f), new Vector3(1.6f, 2f, 0.35f), trimMaterial);
            added += EnsureBox(wing, "Wing_Gallery_Crate",
                new Vector3(-1.9f, 0.3f, 13.6f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);

            // Passage: one crate, so the back route still reads as narrow.
            added += EnsureBox(wing, "Wing_Passage_Crate",
                new Vector3(6.3f, 0.3f, 13.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);

            return added;
        }

        // ------------------------------------------------------------------ player

        /// <summary>
        /// Moves the player out of the old corridor spawn. A player anywhere else is left
        /// alone, so a hand-placed start point is never overwritten.
        /// </summary>
        private static int EnsurePlayerAtWingStart()
        {
            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player == null)
            {
                return 0;
            }

            if (Vector3.Distance(player.transform.position, LegacyPlayerSpawn) > 1f)
            {
                return 0;
            }

            player.transform.position = PlayerStartPosition;
            player.transform.rotation = Quaternion.Euler(0f, PlayerStartYaw, 0f);

            if (PrefabUtility.IsPartOfPrefabInstance(player))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            }
            else
            {
                EditorUtility.SetDirty(player.transform);
            }

            Debug.Log("[Wing] Moved the player from the corridor to the boot room: " +
                      "closed room, one doorway, facing it.", player);
            return 1;
        }

        // --------------------------------------------------------------- diagnostics

        [MenuItem("Ashen Woods/Diagnostics/Log Wing Layout", false, 92)]
        private static void LogWingLayout()
        {
            GameObject wingObject = GameObject.Find(WingRootName);

            if (wingObject == null)
            {
                Debug.Log("[Wing] Not in this scene yet. Run Ashen Woods > Stage 1 - Build The East Wing.");
                return;
            }

            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            GameObject enemy = GameObject.Find("Enemy_Patroller");
            GameObject corridorEnd = GameObject.Find("Corridor_Wall_End");

            var report = new System.Text.StringBuilder("[Wing] Layout");
            report.Append("\n  Boot room  x -5.4..-1.8  z 8.4..13.0   player start, never on the patrol route");
            report.Append("\n  Hall       x -1.6..2.8   z 8.4..13.0   corridor, boot room, gallery, kitchen");
            report.Append("\n  Kitchen    x  3.0..7.0   z 8.4..12.4   doorway + 1.05 m crawl hole from the hall");
            report.Append("\n  Gallery    x -2.4..4.4   z 13.2..16.6  the loop closes through the passage");
            report.Append("\n  Passage    x  4.6..6.6   z 12.6..16.6");
            report.Append("\n  Corridor dead end: ");
            report.Append(corridorEnd == null
                ? "opened"
                : "still sealed (the wall was edited by hand, see the Console warning)");

            if (player != null)
            {
                report.Append($"\n  Player at ({player.transform.position.x:0.0}, {player.transform.position.z:0.0})");
            }

            if (player != null && enemy != null)
            {
                report.Append($", enemy at ({enemy.transform.position.x:0.0}, {enemy.transform.position.z:0.0})");
                report.Append(
                    $" - {Vector3.Distance(player.transform.position, enemy.transform.position):0.0} m apart at start");
            }

            Debug.Log(report.ToString(), wingObject);
        }

        // ----------------------------------------------------------------- helpers

        private static int EnsureBox(
            Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            Stage1PlaygroundBuilder.CreateBox(name, parent, center, size, material);
            return 1;
        }

        private static int EnsureFloor(
            Transform parent, string name, float minX, float maxX, float minZ, float maxZ, Material material)
        {
            return EnsureBox(parent, name,
                new Vector3((minX + maxX) * 0.5f, -0.1f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.2f, maxZ - minZ), material);
        }

        private static int EnsureCeiling(
            Transform parent, string name,
            float minX, float maxX, float minZ, float maxZ, float height, Material material)
        {
            return EnsureBox(parent, name,
                new Vector3((minX + maxX) * 0.5f, height + 0.1f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.2f, maxZ - minZ), material);
        }

        private static int EnsureWallX(
            Transform parent, string name, float z, float fromX, float toX,
            float bottomY, float topY, Material material)
        {
            return EnsureBox(parent, name,
                new Vector3((fromX + toX) * 0.5f, (bottomY + topY) * 0.5f, z),
                new Vector3(toX - fromX, topY - bottomY, Wall), material);
        }

        private static int EnsureWallZ(
            Transform parent, string name, float x, float fromZ, float toZ,
            float bottomY, float topY, Material material)
        {
            return EnsureBox(parent, name,
                new Vector3(x, (bottomY + topY) * 0.5f, (fromZ + toZ) * 0.5f),
                new Vector3(Wall, topY - bottomY, toZ - fromZ), material);
        }
    }
}
