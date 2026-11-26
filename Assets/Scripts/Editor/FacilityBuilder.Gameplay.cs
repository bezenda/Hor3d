using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AshenWoods.Enemy;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>Lights and power, gameplay anchors, items, hiding lockers, objectives and navigation.</summary>
    public static partial class FacilityBuilder
    {
        internal static readonly Vector3 PlayerStart = new Vector3(3f, 0.05f, 27.6f);
        private const float PlayerStartYaw = 180f; // facing the closet door (south)
        internal static readonly Vector3 EnemyStart = new Vector3(75f, 0.05f, -27f);
        private const string KeycardId = "keycard";
        private const string LockerPrefabPath = Stage1PlaygroundBuilder.PrefabFolder + "/World/HidingSpot_Locker.prefab";

        private static readonly Color HallLight = new Color(0.86f, 0.93f, 1f);
        private static readonly Color LabLight = new Color(0.72f, 0.9f, 1f);
        private static readonly Color Emergency = new Color(1f, 0.12f, 0.06f);

        // ----------------------------------------------------------------- lights

        /// <summary>
        /// Powered lamps (listed on HousePower: almost dark until the breaker is reset), red
        /// emergency lights and the containment glow (IndependentLight: always on), and the
        /// janitor's lamp (always on, dim): the only real light at the start.
        /// </summary>
        private static int BuildLights()
        {
            Transform t = Group("Lights");
            int before = t.childCount;

            // Hall lamps under kit fixtures.
            foreach (float x in new[] { 6f, 18f, 30f })
                foreach (float z in new[] { 6f, 19f })
                    HallLamp(t, $"Storage_{x:0}_{z:0}", new Vector3(x, HallHeight, z), HallLight, 14f, 18f);

            foreach (float x in new[] { 54f, 66f, 78f, 90f })
                foreach (float z in new[] { 8f, 22f })
                    HallLamp(t, $"Assembly_{x:0}_{z:0}", new Vector3(x, HallHeight, z), HallLight, 14f, 18f);

            foreach (float x in new[] { 54f, 66f, 78f })
                foreach (float z in new[] { -18f, -36f })
                    HallLamp(t, $"Lab_{x:0}_{z:0}", new Vector3(x, HallHeight, z + 0.01f), LabLight, 12f, 16f);
            HallLamp(t, "Lab_Office", new Vector3(90f, HallHeight, -18f), LabLight, 9f, 12f);
            HallLamp(t, "Lab_East", new Vector3(90f, HallHeight, -33f), LabLight, 9f, 14f);

            foreach (Vector3 p in new[] { new Vector3(6f, 0f, -18f), new Vector3(18f, 0f, -18f), new Vector3(6f, 0f, -36f),
                                          new Vector3(18f, 0f, -36f), new Vector3(30f, 0f, -33f) })
                HallLamp(t, $"Electric_{p.x:0}_{p.z:0}", new Vector3(p.x, ElectricHeight, p.z), HallLight, 18f, 24f);
            HallLamp(t, "Breaker_Room", new Vector3(30f, BreakerCeiling, -18f), HallLight, 6f, 10f);

            // Airlocks: one powered lamp in each, plus the red emergency glow.
            AirlockLights(t, "A_SA", new Vector3(42f, 0f, 21f));
            AirlockLights(t, "A_SE", new Vector3(15f, 0f, -6f));
            AirlockLights(t, "A_EL", new Vector3(42f, 0f, -27f));
            AirlockLights(t, "A_BR", new Vector3(21f, BreakerFloor, -21f));
            AirlockLights(t, "A_AL", new Vector3(57f, 0f, -6f));

            // Emergency lights at the doors and the stairs.
            Vector3[] emergency =
            {
                new Vector3(3f, 3.2f, 23.4f), new Vector3(15f, 3.4f, 0.6f), new Vector3(35.4f, 3.4f, 21f),
                new Vector3(48.6f, 3.4f, 21f), new Vector3(95.2f, 5.2f, 15f), new Vector3(15f, 3.4f, -12.6f),
                new Vector3(35.4f, 3.4f, -27f), new Vector3(48.6f, 3.4f, -27f), new Vector3(5f, 2.6f, -16.2f),
                new Vector3(17.5f, 11.2f, -28.5f), new Vector3(35.4f, BreakerFloor + 3f, -18f), new Vector3(84.6f, 3f, -21f),
                new Vector3(24f, 2.5f, -40f), new Vector3(72f, 6.5f, 29.4f),
                new Vector3(57f, 3.4f, -0.6f), new Vector3(57f, 3.4f, -11.4f),
            };
            for (int k = 0; k < emergency.Length; k++)
            {
                Lamp(t, "Emergency_" + k, emergency[k], Emergency, 2.6f, 9f, true);
                Box(t, "Emergency_Lens_" + k, emergency[k], new Vector3(0.18f, 0.1f, 0.18f), WarningRed, false);
            }

            // Containment glow: the lab's backup power keeps the capsules lit.
            foreach (float x in CapsuleXs)
            {
                Lamp(t, $"Capsule_N_{x:0}", new Vector3(x, 1.8f, CapsuleRowNorth), new Color(0.2f, 1f, 0.6f), 1.4f, 5f, true);
                if (!Mathf.Approximately(x, 60f)) // the broken capsule is dark
                    Lamp(t, $"Capsule_S_{x:0}", new Vector3(x, 1.8f, CapsuleRowSouth), new Color(0.2f, 1f, 0.6f), 1.4f, 5f, true);
            }

            Lamp(t, "Containment", ContainmentCenter + new Vector3(0f, 0.8f, 0f), new Color(0.1f, 0.8f, 0.4f), 1.6f, 6f, true);

            // The janitor's closet: a weak, warm lamp that never went out.
            Light janitor = Lamp(t, "Janitor", new Vector3(6f, JanitorCeiling - 1.1f, 27f), new Color(1f, 0.78f, 0.52f), 2.2f, 8f, true, true);
            if (janitor != null)
            {
                janitor.shadows = LightShadows.Soft;
            }

            return t.childCount - before;
        }

        private static void HallLamp(Transform parent, string name, Vector3 ceilingPoint, Color color, float intensity, float range)
        {
            Put(parent, name + "_Fixture", "Hanging Light", ceilingPoint, 0f);
            Lamp(parent, name, ceilingPoint + Vector3.down * 1.1f, color, intensity, range, false);
        }

        private static void AirlockLights(Transform parent, string name, Vector3 floorCenter)
        {
            Lamp(parent, name + "_Lamp", floorCenter + new Vector3(0f, 3.4f, 0f), HallLight, 4f, 9f, false);
            Lamp(parent, name + "_Emergency", floorCenter + new Vector3(0f, 3.5f, 0f), Emergency, 1.6f, 8f, true);
        }

        // --------------------------------------------------------------- gameplay

        private static int SetUpGameplay()
        {
            int added = 0;
            bool firstRun = FindRoot(GameplayMarkerName) == null;

            if (firstRun)
            {
                added += MovePlayer();
                added += MoveEnemy();
                added += SetUpBreakerDoor();
                added += SetUpKeycard();
                added += SetUpBreakerPanel();
                added += SetUpExit();
                added += RedistributeItems();
                added += WriteObjectives();

                var marker = new GameObject(GameplayMarkerName);
                marker.SetActive(false);
                added++;
            }

            // Lockers are back (2026-11-12) with costs: holding breath drains stamina, a searching enemy may check one.
            if (LockersEnabled)
            {
                added += PlaceLockers();
            }

            added += PlaceNotes();
            added += FillRoomAtlas();
            added += WirePower();
            return added;
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g.name == name)
                {
                    return g;
                }
            }

            return null;
        }

        private static int MovePlayer()
        {
            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player == null)
            {
                return 0;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.Euler(0f, PlayerStartYaw, 0f));
            if (controller != null) controller.enabled = true;
            return 1;
        }

        private static int MoveEnemy()
        {
            EnemyBrain enemy = Object.FindAnyObjectByType<EnemyBrain>();
            if (enemy == null)
            {
                return 0;
            }

            CharacterController controller = enemy.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            enemy.transform.SetPositionAndRotation(EnemyStart, Quaternion.Euler(0f, -90f, 0f));
            if (controller != null) controller.enabled = true;

            EnemyNavigation navigation = Object.FindAnyObjectByType<EnemyNavigation>();
            if (navigation != null)
            {
                // Nodes are on a 2 m grid: link the diagonals (2.83 m) and the stair steps, nothing further.
                Stage1PlaygroundBuilder.SetFloat(navigation, "maxLinkDistance", 3.4f);
            }

            return 1;
        }

        /// <summary>The house's main door becomes the breaker room's steel door, locked by the keycard.</summary>
        private static int SetUpBreakerDoor()
        {
            GameObject doorObject = GameObject.Find("Door_Main");
            if (doorObject == null)
            {
                return 0;
            }

            doorObject.name = "Door_BreakerRoom";
            // The wall gap at x 24 runs z -20.26 .. -21.71, 9 .. 12.1 m up. Hinge at its north edge,
            // the panel reaching south (yaw 90 turns the panel's +x towards -z).
            doorObject.transform.SetPositionAndRotation(new Vector3(23.82f, BreakerFloor, -20.28f), Quaternion.Euler(0f, 90f, 0f));

            Transform hinge = doorObject.transform.Find("Hinge");
            Transform panel = hinge != null ? hinge.Find("Panel") : null;
            Transform handle = hinge != null ? hinge.Find("Handle") : null;
            if (panel != null)
            {
                panel.localPosition = new Vector3(0.71f, 1.3f, 0f);
                panel.localScale = new Vector3(1.42f, 2.6f, 0.1f);
                panel.GetComponent<MeshRenderer>().sharedMaterial = MetalMid;
            }

            if (handle != null)
            {
                handle.localPosition = new Vector3(1.25f, 1.1f, 0.1f);
                handle.GetComponent<MeshRenderer>().sharedMaterial = WarningRed;
            }

            Door door = doorObject.GetComponent<Door>();
            var so = new SerializedObject(door);
            so.FindProperty("isLocked").boolValue = true;
            so.FindProperty("requiredKeyId").stringValue = KeycardId;
            so.FindProperty("openPrompt").stringValue = "Открыть электрощитовую";
            so.FindProperty("closePrompt").stringValue = "Закрыть дверь";
            so.FindProperty("lockedPrompt").stringValue = "Электрощитовая (нужна ключ-карта)";
            so.FindProperty("lockedMessage").stringValue = "Считыватель мигает красным. Нужна ключ-карта охраны.";
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static int SetUpKeycard()
        {
            GameObject key = GameObject.Find("Key_Iron");
            if (key == null)
            {
                return 0;
            }

            key.name = "Keycard";
            key.transform.position = new Vector3(89.4f, 0.86f, -13.15f);
            Transform visual = key.transform.Find("Visuals");
            if (visual != null)
            {
                visual.localScale = new Vector3(0.09f, 0.006f, 0.058f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = ScreenOn;
            }

            var so = new SerializedObject(key.GetComponent<KeyPickup>());
            so.FindProperty("keyId").stringValue = KeycardId;
            so.FindProperty("displayName").stringValue = "security keycard";
            so.ApplyModifiedPropertiesWithoutUndo();

            // A faint blue glint so the card can be spotted on the desk.
            Lamp(key.transform, "Glint", key.transform.position + Vector3.up * 0.25f, new Color(0.3f, 0.6f, 1f), 0.25f, 1.2f, true);
            return 1;
        }

        private static int SetUpBreakerPanel()
        {
            PuzzleController panel = BreakerPanel();
            if (panel == null)
            {
                return 0;
            }

            // On the breaker room's east wall, front (+z) facing west.
            panel.transform.SetPositionAndRotation(new Vector3(35.78f, BreakerFloor + 1.45f, -18f), Quaternion.Euler(0f, -90f, 0f));
            var so = new SerializedObject(panel);
            so.FindProperty("requiredItemId").stringValue = string.Empty; // the door already took the keycard
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>The house's front door becomes the loading bay: invisible frame, a big interaction box.</summary>
        private static int SetUpExit()
        {
            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();
            if (exit == null)
            {
                return 0;
            }

            exit.gameObject.name = "Escape_LoadingBay";
            exit.transform.SetPositionAndRotation(new Vector3(94.9f, 0f, 15f), Quaternion.Euler(0f, -90f, 0f));
            foreach (Renderer r in exit.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }

            foreach (Collider c in exit.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            BoxCollider box = exit.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = exit.gameObject.AddComponent<BoxCollider>();
            }

            box.center = new Vector3(0f, 2.4f, 0f);
            box.size = new Vector3(4.6f, 4.8f, 0.3f);

            var so = new SerializedObject(exit);
            so.FindProperty("lockedPrompt").stringValue = "Ворота погрузочной зоны";
            so.FindProperty("lockedMessage").stringValue = "Пульт мёртв — воротам нужно питание.";
            so.FindProperty("openPrompt").stringValue = "Открыть ворота погрузочной зоны";
            so.FindProperty("escapedMessage").stringValue = "Ворота со скрежетом уходят вверх, в темноту. Холодный воздух. Ты бежишь.";

            // The finale: the bay takes 50 s to open and everything comes for the player (FinalChase).
            if (so.FindProperty("openDelaySeconds").floatValue <= 0f)
            {
                so.FindProperty("openDelaySeconds").floatValue = 50f;
            }

            GameObject bayDoor = GameObject.Find("LP_Bay_Door_snaps");
            if (bayDoor != null)
            {
                GameObjectUtility.SetStaticEditorFlags(bayDoor, 0); // it moves: no static batching
                so.FindProperty("rollingDoor").objectReferenceValue = bayDoor.transform;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            if (exit.GetComponent<FinalChase>() == null)
            {
                exit.gameObject.AddComponent<FinalChase>();
            }

            return 1;
        }

        /// <summary>Batteries and throwables: clones of the house's items on facility spots; the old ones go.</summary>
        private static int RedistributeItems()
        {
            GameObject items = GameObject.Find("Items");
            if (items == null)
            {
                return 0;
            }

            GameObject battery = null, bottle = null, can = null;
            float bestOffset = float.MaxValue;
            var old = new List<GameObject>();
            foreach (Transform child in items.transform)
            {
                if (child.name.StartsWith("Battery_"))
                {
                    Transform visual = child.Find("Visuals");
                    float offset = visual != null ? visual.localPosition.magnitude : 99f;
                    if (offset < bestOffset)
                    {
                        bestOffset = offset;
                        battery = child.gameObject;
                    }

                    old.Add(child.gameObject);
                }
                else if (child.name.StartsWith("Throwable_Bottle"))
                {
                    bottle = bottle ?? child.gameObject;
                    old.Add(child.gameObject);
                }
                else if (child.name.StartsWith("Throwable_Can"))
                {
                    can = can ?? child.gameObject;
                    old.Add(child.gameObject);
                }
            }

            int added = 0;
            Vector3[] batteries =
            {
                new Vector3(9.4f, 0.79f, 29.1f),                  // janitor's desk
                new Vector3(17.8f, 0.23f, 15f),                   // storage, on the aisle pallet
                new Vector3(10.08f, LevelOne + 0.65f, -31.85f),   // electrical: end of the dead-end spur, on a crate
                new Vector3(61.2f, 0.95f, 29.1f),                 // assembly: workbench
                new Vector3(74.5f, 0.97f, -40.9f),                // lab: south bench
            };
            for (int k = 0; k < batteries.Length && battery != null; k++)
            {
                added += Clone(battery, items.transform, "Battery_" + (k + 1), batteries[k]);
            }

            Vector3[] bottles =
            {
                new Vector3(1.6f, 0.85f, 29.5f), new Vector3(26f, 0.15f, 7.1f), new Vector3(9f, 0.15f, 22.9f),
                new Vector3(5f, LevelOne + 0.15f, -38.5f), new Vector3(9.5f, LevelTwo + 0.15f, -25.5f),
                new Vector3(61.5f, 1.0f, -13.1f), new Vector3(90.8f, 0.86f, -13.2f),
            };
            for (int k = 0; k < bottles.Length && bottle != null; k++)
            {
                added += Clone(bottle, items.transform, "Throwable_Bottle_" + (k + 1), bottles[k]);
            }

            Vector3[] cans =
            {
                new Vector3(4f, 0.1f, 25.2f), new Vector3(33f, 0.1f, 3f), new Vector3(70f, 0.1f, 12f),
                new Vector3(86f, 0.1f, 20f), new Vector3(20f, 0.1f, -20f),
            };
            for (int k = 0; k < cans.Length && can != null; k++)
            {
                added += Clone(can, items.transform, "Throwable_Can_" + (k + 1), cans[k]);
            }

            foreach (GameObject g in old)
            {
                Object.DestroyImmediate(g);
            }

            // The _new suffix kept the clones apart from the originals until those were deleted.
            foreach (Transform child in items.transform)
            {
                if (child.name.EndsWith("_new"))
                {
                    child.name = child.name.Substring(0, child.name.Length - 4);
                }
            }

            return added;
        }

        private static int Clone(GameObject template, Transform parent, string name, Vector3 position)
        {
            GameObject copy = Object.Instantiate(template, parent);
            copy.name = name + "_new";
            copy.transform.position = position;
            copy.transform.rotation = Quaternion.Euler(0f, Hash(position.x, position.z) * 360f, 0f);
            Transform visual = copy.transform.Find("Visuals");
            if (visual != null && name.StartsWith("Battery"))
            {
                visual.localPosition = Vector3.zero;
            }

            return 1;
        }

        private static int WriteObjectives()
        {
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            Door door = GameObject.Find("Door_BreakerRoom")?.GetComponent<Door>();
            PuzzleController panel = BreakerPanel();
            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();
            if (manager == null)
            {
                return 0;
            }

            var so = new SerializedObject(manager);
            SerializedProperty steps = so.FindProperty("steps");
            steps.ClearArray();

            void Step(string id, string text, ObjectiveCondition condition, string key, Object target, string field)
            {
                int i = steps.arraySize;
                steps.InsertArrayElementAtIndex(i);
                SerializedProperty s = steps.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("id").stringValue = id;
                s.FindPropertyRelative("description").stringValue = text;
                s.FindPropertyRelative("condition").enumValueIndex = (int)condition;
                s.FindPropertyRelative("keyId").stringValue = key ?? string.Empty;
                s.FindPropertyRelative("door").objectReferenceValue = field == "door" ? target : null;
                s.FindPropertyRelative("puzzle").objectReferenceValue = field == "puzzle" ? target : null;
                s.FindPropertyRelative("exit").objectReferenceValue = field == "exit" ? target : null;
            }

            Step("keycard", "Find a security keycard (research lab office)", ObjectiveCondition.CarryKey, KeycardId, null, null);
            Step("breaker_room", "Climb the catwalks to the breaker room", ObjectiveCondition.DoorUnlocked, null, door, "door");
            Step("power", "Reset the breakers", ObjectiveCondition.PuzzleSolved, null, panel, "puzzle");
            Step("escape", "Get out through the loading bay (assembly hall)", ObjectiveCondition.EscapeUsed, null, exit, "exit");
            so.FindProperty("allCompleteDescription").stringValue = "Ты на свободе.";
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        // ---------------------------------------------------------------- lockers

        private static readonly bool LockersEnabled = true;

        private static GameObject LockerPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LockerPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject wardrobe = Stage3EnemyBuilder.WardrobePrefab();
            string source = AssetDatabase.GetAssetPath(wardrobe);
            if (!AssetDatabase.CopyAsset(source, LockerPrefabPath))
            {
                return wardrobe;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(LockerPrefabPath);
            foreach (Renderer r in contents.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = r.name.ToLowerInvariant().Contains("handle") ? MetalMid : Mat("M_Locker", new Color(0.2f, 0.27f, 0.3f), 0.45f, 0.5f);
            }

            HidingSpot spot = contents.GetComponent<HidingSpot>();
            if (spot != null)
            {
                var so = new SerializedObject(spot);
                SerializedProperty enter = so.FindProperty("enterPrompt");
                SerializedProperty exit = so.FindProperty("exitPrompt");
                if (enter != null) enter.stringValue = "Спрятаться в шкафчик";
                if (exit != null) exit.stringValue = "Вылезти";
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, LockerPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return AssetDatabase.LoadAssetAtPath<GameObject>(LockerPrefabPath);
        }

        /// <summary>Lockers stand with their back to a wall and open towards local +z.</summary>
        private static int PlaceLockers()
        {
            GameObject locker = LockerPrefab();
            Quaternion intoPlusX = Quaternion.Euler(0f, 90f, 0f);
            Quaternion intoMinusX = Quaternion.Euler(0f, -90f, 0f);
            Quaternion intoMinusZ = Quaternion.Euler(0f, 180f, 0f);
            Quaternion intoPlusZ = Quaternion.identity;

            int added = 0;
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Janitor", new Vector3(11.42f, 0f, 28.2f), intoMinusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Storage_W", new Vector3(0.58f, 0f, 20.5f), intoPlusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Storage_E", new Vector3(35.42f, 0f, 9f), intoMinusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Electric_W", new Vector3(0.58f, 0f, -40.5f), intoPlusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Electric_E", new Vector3(35.42f, 0f, -28.5f), intoMinusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Assembly_W", new Vector3(48.58f, 0f, 4.5f), intoPlusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Assembly_N", new Vector3(80f, 0f, 29.42f), intoMinusZ);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Lab_W", new Vector3(49.1f, 0f, -39f), intoPlusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Lab_Mid", new Vector3(49.1f, 0f, -21f), intoPlusX);
            added += Stage3EnemyBuilder.PlaceHidingSpot(locker, "Locker_Lab_S", new Vector3(68f, 0f, -40.9f), intoPlusZ);
            return added;
        }

        // ------------------------------------------------------------------ notes

        private static int PlaceNotes()
        {
            Transform t = Group("Notes");
            int added = 0;

            added += Note(t, "Note_Janitor", new Vector3(8.15f, 0.8f, 29.15f), new Vector3(0.22f, 0.012f, 0.3f), Paper,
                "Прочитать записи на планшете", JanitorGuide, false);
            added += MakeSheet(t.Find("Note_Janitor"), "НОЧНОЙ СМЕНЕ — ПРОЧЕСТЬ", JanitorGuide);

            Note(t, "Laptop_Janitor", new Vector3(8.4f, 0.78f, 29.3f), Vector3.zero, null,
                "Посмотреть ноутбук", SiteLog, true);
            GameObject laptop = GameObject.Find("Laptop");
            added += MakeSheet(laptop != null ? laptop.transform : null, "SITE LOG", SiteLog);

            added += Note(t, "Note_Breaker", new Vector3(26.2f, BreakerFloor + 0.79f, -16.2f), new Vector3(0.3f, 0.012f, 0.22f), Paper,
                "Прочитать инструкцию",
                "СБРОС ГЛАВНОГО ЩИТА: все три рубильника — ВВЕРХ. Если свет в цехе не загорелся, переключите их ещё раз.", false);

            return added;
        }

        private const string JanitorGuide =
            "If you are reading this, you are still alive. Keep it that way.\n\n" +
            "<b>Move</b>   WASD.  Shift - sprint (short).  C - crouch.  Space - jump.\n" +
            "<b>Use</b>   E - doors, lockers, notes, items.\n" +
            "<b>Torch</b>   F - on / off. Spare batteries lie on tables.\n" +
            "<b>Flash</b>   Q - a burst of light. Knocks it down for a few seconds. Costs battery.\n" +
            "<b>Throw</b>   hold G, aim, let go. Bottles and cans - the crash draws it away.\n\n" +
            "It hunts by sound. Walk, crouch, run only when you must. If it sees you, break the line " +
            "of sight and get into a locker. Do not stay close to it for long - it gets into your head.\n\n" +
            "<b>Way out</b>: Dr. Hale's keycard is in the lab office. It opens the breaker room " +
            "(electrical hall, up the catwalks): all three switches UP. With the power back the " +
            "loading bay in the assembly hall opens.";

        private const string SiteLog =
            "Power restored.\n\nContainment B-7 breached. Specimen mobile.\n" +
            "It hunts by sound and avoids bright light.\n\nEvacuate through the loading bay.\nDo not wait for us.";

        /// <summary>Turns an InfoPoint into a paper sheet once (later runs keep hand-edited text).</summary>
        private static int MakeSheet(Transform target, string title, string message)
        {
            InfoPoint info = target != null ? target.GetComponent<InfoPoint>() : null;
            if (info == null)
            {
                return 0;
            }

            var so = new SerializedObject(info);
            if (so.FindProperty("showAsSheet").boolValue)
            {
                return 0;
            }

            so.FindProperty("showAsSheet").boolValue = true;
            so.FindProperty("sheetTitle").stringValue = title;
            so.FindProperty("message").stringValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>An InfoPoint with a little visual; the laptop reuses the laptop model built in the closet.</summary>
        private static int Note(Transform parent, string name, Vector3 at, Vector3 size, Material material, string prompt,
            string message, bool needsPower)
        {
            GameObject target;
            if (needsPower)
            {
                target = GameObject.Find("Laptop");
                if (target == null || target.GetComponent<InfoPoint>() != null)
                {
                    return 0;
                }
            }
            else
            {
                if (parent.Find(name) != null)
                {
                    return 0;
                }

                target = Box(parent, name, at, size, material, false);
                BoxCollider hit = target.AddComponent<BoxCollider>();
                hit.isTrigger = true;
                hit.size = new Vector3(1.6f, 6f, 1.6f); // the card itself is thin; give the ray room
            }

            InfoPoint info = target.AddComponent<InfoPoint>();
            var so = new SerializedObject(info);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = message;
            so.FindProperty("needsPower").boolValue = needsPower;
            if (needsPower)
            {
                so.FindProperty("screen").objectReferenceValue = target.transform.Find("Screen")?.GetComponent<Renderer>();
                so.FindProperty("screenOff").objectReferenceValue = ScreenOff;
                so.FindProperty("screenOn").objectReferenceValue = ScreenOn;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        // ------------------------------------------------------------- atlas, power

        /// <summary>Room names for the caption UI, on the kit's 6 m grid (x 0..96, z -42..30).</summary>
        private static int FillRoomAtlas()
        {
            RoomAtlas atlas = Object.FindAnyObjectByType<RoomAtlas>();
            if (atlas == null)
            {
                return 0;
            }

            var ground = new string[12];
            var upper = new string[12];
            for (int row = 0; row < 12; row++)
            {
                float z = -42f + row * 6f + 3f;
                char[] g = new char[16];
                char[] u = new char[16];
                for (int col = 0; col < 16; col++)
                {
                    float x = col * 6f + 3f;
                    char c = '.';
                    if (JanitorRect.Contains(new Vector2(x, z))) c = 'J';
                    else if (StorageRect.Contains(new Vector2(x, z))) c = 'S';
                    else if (AssemblyRect.Contains(new Vector2(x, z))) c = 'A';
                    else if (OfficeRect.Contains(new Vector2(x, z))) c = 'O';
                    else if (LabRect.Contains(new Vector2(x, z))) c = 'L';
                    else if (ElectricRect.Contains(new Vector2(x, z))) c = 'E';
                    g[col] = TowerRect.Contains(new Vector2(x, z)) ? 'V' : c;
                    u[col] = TowerRect.Contains(new Vector2(x, z)) ? 'B' : c;
                }

                ground[row] = new string(g);
                upper[row] = new string(u);
            }

            var so = new SerializedObject(atlas);
            so.FindProperty("minX").floatValue = 0f;
            so.FindProperty("minZ").floatValue = -42f;
            so.FindProperty("cell").floatValue = 6f;
            so.FindProperty("storeySplit").floatValue = 8f;
            SetStrings(so.FindProperty("groundRows"), ground);
            SetStrings(so.FindProperty("upperRows"), upper);
            so.FindProperty("letters").stringValue = "JSAOLEBV";
            SetStrings(so.FindProperty("names"), new[]
            {
                "Janitor's Closet", "Storage Hall", "Assembly Hall", "Lab Office", "Research Lab", "Electrical Hall", "Breaker Room",
                "Valve Room",
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            return 0;
        }

        private static void SetStrings(SerializedProperty array, string[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        /// <summary>HousePower gets every facility lamp that is not an IndependentLight.</summary>
        private static int WirePower()
        {
            HousePower power = Object.FindAnyObjectByType<HousePower>();
            if (power == null)
            {
                return 0;
            }

            var lamps = new List<Light>();
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional && !IndependentLight.Covers(light))
                {
                    lamps.Add(light);
                }
            }

            var so = new SerializedObject(power);
            SerializedProperty list = so.FindProperty("lamps");
            list.arraySize = lamps.Count;
            for (int i = 0; i < lamps.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            }

            so.FindProperty("breaker").objectReferenceValue = PowerSource();
            so.ApplyModifiedPropertiesWithoutUndo();
            return 0;
        }
    }
}
