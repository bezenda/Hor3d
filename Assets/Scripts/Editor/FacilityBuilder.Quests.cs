using UnityEditor;
using UnityEngine;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The longer objective chain (2026-11-12): torch -> office code (three notes) -> keycard -> breaker room ->
    /// breakers (control power only) -> two fuel cans -> generator (main power) -> hydraulic valves in the order on
    /// the schematic -> loading bay (hold out while it lifts). Every piece is made once, by name, under
    /// Facility/Quests; references are re-pointed every run. The chain itself, the torch requirement and the
    /// janitor's guide are written once, guarded by the Quests_v1 marker (delete it to rewrite them).
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string QuestsMarker = "Quests_v1";
        private const string TorchId = "torch";
        private const string FuelOneId = "fuel_1";
        private const string FuelTwoId = "fuel_2";

        // East wall of the electrical hall, ground floor, clear between the pilasters (measured 2026-11-12).
        private const float ValveWallX = 35.85f;
        private static readonly float[] ValveZ = { -22.8f, -21.6f, -20.4f, -19.2f };
        private static readonly string[] ValveLabels = { "A", "B", "C", "D" };

        internal static PuzzleController BreakerPanel() => FindPuzzle("BreakerPanel");
        private static PuzzleController GeneratorPanel() => FindPuzzle("GeneratorPanel");
        private static PuzzleController ValvePanel() => FindPuzzle("ValvePanel");

        /// <summary>What powers the lamps and the bay door: the generator once it exists, else the breakers.</summary>
        private static PuzzleController PowerSource()
        {
            PuzzleController generator = GeneratorPanel();
            return generator != null ? generator : BreakerPanel();
        }

        private static PuzzleController FindPuzzle(string name)
        {
            GameObject found = GameObject.Find(name);
            PuzzleController puzzle = found != null ? found.GetComponent<PuzzleController>() : null;
            return puzzle != null ? puzzle : null; // never Unity's "fake null"

        }

        private static int SetUpQuests()
        {
            Transform quests = Group("Quests");
            int added = 0;
            added += UpgradeQuestPieces(quests);
            added += OpenValveRoom();
            added += QuestTorch(quests);
            added += QuestOfficeDoor(quests);
            added += QuestCodeNotes(quests);
            added += QuestFuelCans(quests);
            added += QuestGenerator(quests);
            added += QuestValves(quests);
            added += ValveBoard(quests);
            added += SetUpHidingAndLures();
            added += SetUpSideGoals();
            added += DifficultyRow();
            WireQuests();

            if (root.Find(QuestsMarker) == null)
            {
                WriteQuestChain();
                var marker = new GameObject(QuestsMarker);
                marker.SetActive(false);
                marker.transform.SetParent(root, false);
                added++;
            }

            return added;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>A pickup cloned from the keycard (same KeyPickup + collider), its look replaced.</summary>
        private static GameObject QuestPickup(Transform parent, string name, Vector3 position, float yaw, string keyId,
            string displayName, bool heavy, string takenMessage)
        {
            GameObject template = GameObject.Find("Keycard");
            if (template == null)
            {
                Debug.LogWarning("[Facility] No Keycard to copy for " + name);
                return null;
            }

            GameObject copy = Object.Instantiate(template, parent);
            copy.name = name;
            copy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Transform glint = copy.transform.Find("Glint");
            if (glint != null)
            {
                Object.DestroyImmediate(glint.gameObject);
            }

            Transform visual = copy.transform.Find("Visuals");
            if (visual != null)
            {
                Object.DestroyImmediate(visual.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(visual.GetComponent<MeshFilter>());
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }

            var so = new SerializedObject(copy.GetComponent<KeyPickup>());
            so.FindProperty("keyId").stringValue = keyId;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("heavy").boolValue = heavy;
            so.FindProperty("takenMessage").stringValue = takenMessage;
            if (heavy)
            {
                so.FindProperty("spinDegreesPerSecond").floatValue = 0f;
                so.FindProperty("bobHeight").floatValue = 0f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        /// <summary>Top of whatever stands at (x, z) below <paramref name="fromY"/>.</summary>
        private static Vector3 SurfaceAt(float x, float z, float fromY = 2.2f)
        {
            if (Physics.Raycast(new Vector3(x, fromY, z), Vector3.down, out RaycastHit hit, fromY + 1f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return new Vector3(x, 0f, z);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 localPosition,
            Vector3 localEuler, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static int QuestTorch(Transform parent)
        {
            if (parent.Find("Torch") != null)
            {
                return 0;
            }

            // On the janitor's desk, next to the spare battery.
            Vector3 top = SurfaceAt(8.9f, 29.5f); // desk spans x 7.6..9.5, z 28.9..29.7, top 0.78
            GameObject torch = QuestPickup(parent, "Torch", top + Vector3.up * 0.06f, 30f, TorchId, "torch", false,
                "Тяжёлый стальной фонарь. Работает — включается клавишей F.");
            if (torch == null)
            {
                return 0;
            }

            Transform visual = torch.transform.Find("Visuals");
            Part(visual, "Body", PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.042f, 0.1f, 0.042f), MetalDark);
            Part(visual, "Head", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.12f), new Vector3(90f, 0f, 0f), new Vector3(0.062f, 0.03f, 0.062f), MetalMid);
            Part(visual, "Lens", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.151f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.003f, 0.05f), ScreenOn);
            Part(visual, "Switch", PrimitiveType.Cube, new Vector3(0f, 0.022f, 0.02f), Vector3.zero, new Vector3(0.012f, 0.008f, 0.025f), PaintRed);

            BoxCollider box = torch.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, 0f, 0.04f);
                box.size = new Vector3(0.22f, 0.16f, 0.4f);
            }

            return 1;
        }

        private static int QuestOfficeDoor(Transform parent)
        {
            int added = 0;
            GameObject door = GameObject.Find("Door_Office");
            if (door == null)
            {
                GameObject source = GameObject.Find("Door_BreakerRoom");
                if (source == null)
                {
                    return 0;
                }

                // The office's west wall (x 84) has the same kit door frame as the breaker room's (x 24, 9 m up).
                door = Object.Instantiate(source, parent);
                door.name = "Door_Office";
                door.transform.SetPositionAndRotation(source.transform.position + new Vector3(60f, -BreakerFloor, 0f),
                    source.transform.rotation);

                var so = new SerializedObject(door.GetComponent<Door>());
                so.FindProperty("isLocked").boolValue = true;
                so.FindProperty("requiredKeyId").stringValue = string.Empty;
                so.FindProperty("openPrompt").stringValue = "Открыть кабинет";
                so.FindProperty("closePrompt").stringValue = "Закрыть дверь";
                so.FindProperty("lockedPrompt").stringValue = "Кабинет лаборатории (кодовый замок)";
                so.FindProperty("lockedMessage").stringValue = "Заперто. Кодовая панель у двери мигает красным.";
                so.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }

            if (parent.Find("Keypad_Office") == null)
            {
                // On the door frame's outer face (x 83.85), right of the door seen from the lab.
                GameObject pad = Box(parent, "Keypad_Office", new Vector3(83.82f, 1.35f, -18.85f),
                    new Vector3(0.05f, 0.22f, 0.15f), MetalDark, true);
                BoxCollider hit = pad.GetComponent<BoxCollider>();
                hit.size = new Vector3(4f, 1.6f, 1.8f);
                pad.isStatic = false;

                Part(pad.transform, "Screen", PrimitiveType.Cube, new Vector3(-0.55f, 0.28f, 0f), Vector3.zero,
                    new Vector3(0.1f, 0.22f, 0.7f), ScreenOn);
                Part(pad.transform, "Keys", PrimitiveType.Cube, new Vector3(-0.55f, -0.15f, 0f), Vector3.zero,
                    new Vector3(0.1f, 0.45f, 0.7f), MetalMid);
                Lamp(pad.transform, "Led", new Vector3(83.7f, 1.5f, -18.85f), new Color(1f, 0.15f, 0.1f), 0.25f, 1.2f, true);

                Keypad keypad = pad.AddComponent<Keypad>();
                var so = new SerializedObject(keypad);
                so.FindProperty("door").objectReferenceValue = door.GetComponent<Door>();
                so.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }

            return added;
        }

        private static int QuestCodeNotes(Transform parent)
        {
            int added = 0;
            added += CodeNote(parent, "Note_Code_1", StorageNoteSpots[0], "СКЛАД — НОЧНАЯ СМЕНА",
                "Hale changed the office lock AGAIN.\n\nI only caught the FIRST number before she shut the door:\n\n" +
                "        <b>{code1}</b>\n\nAsk Pete in assembly for the rest.\n\n- M.");
            added += CodeNote(parent, "Note_Code_2", AssemblyNoteSpots[0], "ASSEMBLY",
                "Pete -\n\nthe MIDDLE digit of the new office code is\n\n        <b>{code2}</b>\n\n" +
                "Do not write the whole thing down anywhere. She will have our heads.\n\n- R.");
            added += CodeNote(parent, "Note_Code_3", LabNoteSpots[0], "ЛАБОРАТОРИЯ — КРЫЛО B",
                "Office keypad, LAST digit: <b>{code3}</b>\n(first two - ask stores and assembly)\n\n" +
                "B-7 was scratching at the glass again tonight. Third time this week.\n" +
                "It goes quiet when the lights are on.");
            return added;
        }

        /// <summary>
        /// A code note pinned to a hall wall at eye height (owner, 2026-11-13: notes on tables were hard to find and
        /// some ended up inside props). Built at the first spot of its list; RandomPlacement picks the run's spot.
        /// </summary>
        private static int CodeNote(Transform parent, string name, WallSpot at, string title, string body)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            int made = WallNote(parent, name, at, "Прочитать записку", body);
            Transform note = parent.Find(name);
            if (note != null)
            {
                MakeSheet(note, title, body);
            }

            return made;
        }

        /// <summary>A point on a wall face (x, z) and the direction into the room.</summary>
        internal struct WallSpot
        {
            public float x, z;
            public Vector3 normal;
            public WallSpot(float x, float z, Vector3 normal) { this.x = x; this.z = z; this.normal = normal; }

            public Vector3 Position(float height, float off = 0.008f) => new Vector3(x, height, z) + normal * off;
            public float Yaw => Quaternion.LookRotation(normal).eulerAngles.y;
        }

        // Measured 2026-11-13: bare hall wall at 1.1-1.9 m, nothing within a metre in front (colliders and renderers).
        internal static readonly WallSpot[] StorageNoteSpots =
        {
            new WallSpot(0.15f, 13f, Vector3.right), new WallSpot(0.15f, 22f, Vector3.right), new WallSpot(35.85f, 8f, Vector3.left),
        };

        internal static readonly WallSpot[] AssemblyNoteSpots =
        {
            new WallSpot(61f, 29.85f, Vector3.back), new WallSpot(48.31f, 11f, Vector3.right), new WallSpot(91f, 29.69f, Vector3.back),
        };

        internal static readonly WallSpot[] LabNoteSpots =
        {
            new WallSpot(48.31f, -23f, Vector3.right), new WallSpot(58f, -41.69f, Vector3.forward), new WallSpot(80f, -41.69f, Vector3.forward),
        };

        private const float NoteHeight = 1.5f;

        /// <summary>A sheet of paper (0.3 x 0.4 m) with a red pin, flat on the wall, readable with E from 2 m.</summary>
        private static int WallNote(Transform parent, string name, WallSpot at, string prompt, string body)
        {
            var note = GameObject.CreatePrimitive(PrimitiveType.Cube);
            note.name = name;
            Object.DestroyImmediate(note.GetComponent<Collider>());
            note.transform.SetParent(parent, false);
            note.transform.SetPositionAndRotation(at.Position(NoteHeight), Quaternion.Euler(0f, at.Yaw, 0f));
            note.transform.localScale = new Vector3(0.3f, 0.4f, 0.008f);
            note.GetComponent<MeshRenderer>().sharedMaterial = Paper;

            Part(note.transform, "Pin", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 1.2f), Vector3.zero, new Vector3(0.06f, 0.045f, 2.5f), PaintRed);

            BoxCollider hit = note.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.size = new Vector3(1.4f, 1.3f, 40f); // 0.42 x 0.52 m, 0.32 m deep: easy to aim at

            InfoPoint info = note.AddComponent<InfoPoint>();
            var so = new SerializedObject(info);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>
        /// One-time (Notes_v2): table notes and the small schematic made way for wall notes and the lit valve board;
        /// the steam vents were restyled so they no longer look like the puzzle valves.
        /// </summary>
        private static int UpgradeQuestPieces(Transform quests)
        {
            const string marker = "Notes_v2";
            if (root.Find(marker) != null)
            {
                return 0;
            }

            foreach (string name in new[] { "Note_Code_1", "Note_Code_2", "Note_Code_3", "Note_Valves" })
            {
                Transform old = quests.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            Transform lures = quests.Find("Lures");
            if (lures != null)
            {
                Object.DestroyImmediate(lures.gameObject);
            }

            // The lines that pointed at the old places; the hints are cleared so WriteClues writes the new ones.
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                SerializedProperty steps = so.FindProperty("steps");
                for (int i = 0; i < steps.arraySize; i++)
                {
                    SerializedProperty step = steps.GetArrayElementAtIndex(i);
                    string id = step.FindPropertyRelative("id").stringValue;
                    if (id == "office_code")
                    {
                        step.FindPropertyRelative("description").stringValue =
                            "Open the lab office - its code is on three notes pinned to walls (storage, assembly, lab)";
                        step.FindPropertyRelative("hint").stringValue = string.Empty;
                    }
                    else if (id == "valves")
                    {
                        step.FindPropertyRelative("description").stringValue =
                            "Open the hydraulic valves in the order on the yellow VALVE ORDER board (electrical hall, east wall)";
                        step.FindPropertyRelative("hint").stringValue = string.Empty;
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var flag = new GameObject(marker);
            flag.SetActive(false);
            flag.transform.SetParent(root, false);
            return 1;
        }

        private static int QuestFuelCans(Transform parent)
        {
            int added = 0;
            added += FuelCan(parent, "FuelCan_1", new Vector3(1.9f, 0f, 12.5f), 80f, FuelOneId);
            added += FuelCan(parent, "FuelCan_2", new Vector3(34.1f, 0f, 15f), -100f, FuelTwoId);
            return added;
        }

        private static int FuelCan(Transform parent, string name, Vector3 floor, float yaw, string keyId)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            GameObject can = QuestPickup(parent, name, SurfaceAt(floor.x, floor.z, 1.2f), yaw, keyId, "fuel can", true,
                "Полная канистра. Тяжёлая — с ней не побегаешь.");
            if (can == null)
            {
                return 0;
            }

            Transform visual = can.transform.Find("Visuals");
            Part(visual, "Body", PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.17f, 0.44f, 0.34f), PaintRed);
            Part(visual, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.475f, -0.04f), Vector3.zero, new Vector3(0.04f, 0.05f, 0.2f), PaintRed);
            Part(visual, "Spout", PrimitiveType.Cylinder, new Vector3(0f, 0.47f, 0.12f), new Vector3(30f, 0f, 0f), new Vector3(0.05f, 0.04f, 0.05f), MetalDark);
            Part(visual, "Label", PrimitiveType.Cube, new Vector3(0.086f, 0.24f, 0f), Vector3.zero, new Vector3(0.004f, 0.12f, 0.16f), PaintYellow);

            BoxCollider box = can.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(0.3f, 0.5f, 0.42f);
            }

            return 1;
        }

        private static int QuestGenerator(Transform parent)
        {
            if (GameObject.Find("GeneratorPanel") != null)
            {
                return 0;
            }

            // A starter post on the generator's east end (the generator spans x 9.6 .. 16.1 at z -25).
            var panel = new GameObject("GeneratorPanel");
            panel.transform.SetParent(parent, false);
            panel.transform.SetPositionAndRotation(new Vector3(16.75f, 0f, -25f), Quaternion.Euler(0f, 90f, 0f));

            Part(panel.transform, "Post", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.12f, 1f, 0.12f), MetalDark);
            Part(panel.transform, "Housing", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), Vector3.zero, new Vector3(0.46f, 0.42f, 0.2f), PaintYellow);
            Part(panel.transform, "Face", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0.101f), Vector3.zero, new Vector3(0.4f, 0.36f, 0.004f), MetalDark);
            Part(panel.transform, "Fuel", PrimitiveType.Cylinder, new Vector3(-0.32f, 0.85f, 0f), Vector3.zero, new Vector3(0.12f, 0.08f, 0.12f), PaintRed);
            Lamp(panel.transform, "Led", new Vector3(16.75f, 1.45f, -24.9f) + Vector3.right * 0.12f, new Color(1f, 0.6f, 0.15f), 0.3f, 1.5f, true);

            var starter = new GameObject("Starter");
            starter.transform.SetParent(panel.transform, false);
            starter.transform.localPosition = new Vector3(0.08f, 1.12f, 0.13f);
            BoxCollider aim = starter.AddComponent<BoxCollider>();
            aim.size = new Vector3(0.5f, 0.5f, 0.3f);

            var lever = new GameObject("Lever");
            lever.transform.SetParent(starter.transform, false);
            Part(lever.transform, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.02f), Vector3.zero, new Vector3(0.03f, 0.16f, 0.03f), PaintRed);
            Part(lever.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0.02f), Vector3.zero, new Vector3(0.08f, 0.03f, 0.04f), Rubber);

            PuzzleController controller = panel.AddComponent<PuzzleController>();
            PuzzleSwitch puzzleSwitch = starter.AddComponent<PuzzleSwitch>();

            var sw = new SerializedObject(puzzleSwitch);
            sw.FindProperty("controller").objectReferenceValue = controller;
            sw.FindProperty("lever").objectReferenceValue = lever.transform;
            sw.FindProperty("switchOnPrompt").stringValue = "Залить топливо и дёрнуть стартер";
            sw.FindProperty("switchOffPrompt").stringValue = "Starter";
            sw.FindProperty("switchedOnMessage").stringValue = "Генератор кашляет и ревёт.";
            sw.FindProperty("flipNoiseLoudness").floatValue = 0.5f;
            sw.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(controller);
            SerializedProperty list = so.FindProperty("switches");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = puzzleSwitch;
            so.FindProperty("requiredItemId").stringValue = FuelOneId + "," + FuelTwoId;
            so.FindProperty("consumeItems").boolValue = true;
            so.FindProperty("lockedPrompt").stringValue = "Генератор (нет топлива)";
            so.FindProperty("lockedMessage").stringValue = "Бак сухой. Двух канистр должно хватить.";
            so.FindProperty("prerequisitePrompt").stringValue = "Генератор (нет питания управления)";
            so.FindProperty("prerequisiteMessage").stringValue = "Стартер не отвечает — нет питания управления. Видимо, рубильники на щите опущены.";
            so.FindProperty("solvedMessage").stringValue =
                "Топливо залито, стартер дёрнут. Генератор кашляет и ревёт — по всей станции, мигая, загорается свет. Это слышали все.";
            so.FindProperty("solveNoiseLoudness").floatValue = 0.9f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static int QuestValves(Transform parent)
        {
            if (GameObject.Find("ValvePanel") != null)
            {
                return 0;
            }

            var panel = new GameObject("ValvePanel");
            panel.transform.SetParent(parent, false);
            panel.transform.position = new Vector3(ValveWallX, 0f, -21f);
            PuzzleController controller = panel.AddComponent<PuzzleController>();

            // A header pipe along the wall that the four valves hang off.
            Cyl(panel.transform, "Header", new Vector3(ValveWallX - 0.09f, 1.75f, -21f), 0.09f, 4.6f, MetalMid, false, new Vector3(90f, 0f, 0f));
            Box(panel.transform, "Sign", new Vector3(ValveWallX - 0.01f, 2.15f, -21f), new Vector3(0.02f, 0.26f, 1.6f), PaintYellow, false);
            Label(panel.transform, "SignText", new Vector3(ValveWallX - 0.025f, 2.15f, -21f), "ГИДРАВЛИКА ВОРОТ", 0.03f, Color.black);

            var switches = new PuzzleSwitch[ValveZ.Length];
            for (int i = 0; i < ValveZ.Length; i++)
            {
                float z = ValveZ[i];
                var valve = new GameObject("Valve_" + ValveLabels[i]);
                valve.transform.SetParent(panel.transform, false);
                // Local +x points out of the wall into the hall: the wheel turns about it.
                valve.transform.SetPositionAndRotation(new Vector3(ValveWallX - 0.16f, 1.3f, z), Quaternion.Euler(0f, 180f, 0f));
                BoxCollider aim = valve.AddComponent<BoxCollider>();
                aim.center = new Vector3(0.02f, 0f, 0f);
                aim.size = new Vector3(0.25f, 0.5f, 0.5f);

                Cyl(valve.transform, "Pipe", new Vector3(ValveWallX - 0.08f, 1.3f, z), 0.07f, 0.16f, MetalMid, false, new Vector3(0f, 0f, 90f));
                Cyl(valve.transform, "Riser", new Vector3(ValveWallX - 0.09f, 1.52f, z), 0.06f, 0.46f, MetalMid, false);

                var lever = new GameObject("Lever");
                lever.transform.SetParent(valve.transform, false);
                Part(lever.transform, "Wheel", PrimitiveType.Cylinder, new Vector3(0.02f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.34f, 0.015f, 0.34f), PaintRed);
                Part(lever.transform, "Spoke1", PrimitiveType.Cube, new Vector3(0.03f, 0f, 0f), Vector3.zero, new Vector3(0.02f, 0.3f, 0.025f), MetalDark);
                Part(lever.transform, "Spoke2", PrimitiveType.Cube, new Vector3(0.03f, 0f, 0f), Vector3.zero, new Vector3(0.02f, 0.025f, 0.3f), MetalDark);
                Part(lever.transform, "Nub", PrimitiveType.Cube, new Vector3(0.04f, 0.13f, 0f), Vector3.zero, new Vector3(0.04f, 0.05f, 0.04f), PaintYellow);

                Box(valve.transform, "Plate", new Vector3(ValveWallX - 0.01f, 1.0f, z), new Vector3(0.02f, 0.16f, 0.16f), PaintYellow, false);
                Label(valve.transform, "Letter", new Vector3(ValveWallX - 0.025f, 1.0f, z), ValveLabels[i], 0.09f, Color.black);

                PuzzleSwitch puzzleSwitch = valve.AddComponent<PuzzleSwitch>();
                var sw = new SerializedObject(puzzleSwitch);
                sw.FindProperty("controller").objectReferenceValue = controller;
                sw.FindProperty("label").stringValue = ValveLabels[i];
                sw.FindProperty("lever").objectReferenceValue = lever.transform;
                sw.FindProperty("onAngle").floatValue = 120f;
                sw.FindProperty("offAngle").floatValue = 0f;
                sw.FindProperty("flipDegreesPerSecond").floatValue = 150f;
                sw.FindProperty("switchOnPrompt").stringValue = "Open valve " + ValveLabels[i];
                sw.FindProperty("switchOffPrompt").stringValue = "Valve " + ValveLabels[i];
                sw.FindProperty("switchedOnMessage").stringValue = "Valve " + ValveLabels[i] + " turns with a long hiss.";
                sw.FindProperty("flipNoiseLoudness").floatValue = 0.3f;
                sw.ApplyModifiedPropertiesWithoutUndo();
                switches[i] = puzzleSwitch;
            }

            var so = new SerializedObject(controller);
            SerializedProperty list = so.FindProperty("switches");
            list.arraySize = switches.Length;
            for (int i = 0; i < switches.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = switches[i];
            }

            so.FindProperty("requiredItemId").stringValue = string.Empty;
            so.FindProperty("ordered").boolValue = true;
            so.FindProperty("shuffleOrder").boolValue = true;
            so.FindProperty("orderToken").stringValue = "valves";
            so.FindProperty("prerequisitePrompt").stringValue = "Вентиль (у насосов нет питания)";
            so.FindProperty("prerequisiteMessage").stringValue = "Крутится вхолостую. У насосов ещё нет питания.";
            so.FindProperty("solvedMessage").stringValue =
                "Последний вентиль открыт. Трубы грохочут до самой погрузочной зоны — в гидравлике появилось давление.";
            so.FindProperty("solveNoiseLoudness").floatValue = 0.7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return 1;
        }

        /// <summary>
        /// The valve order on a big yellow board right beside the valves (same wall, 2 m north of valve D), lit by
        /// its own lamp and titled in large letters - the owner could not find the small sheet that hung here before.
        /// </summary>
        private static int ValveBoard(Transform parent)
        {
            var spot = new WallSpot(ValveWallX, -16.6f, Vector3.left);
            if (parent.Find("Note_Valves") != null)
            {
                // Every run: the title sits ON the yellow backing (on the dark wall above it, it could not be read).
                Transform backing = parent.Find("ValveBoard/Backing");
                if (backing != null)
                {
                    backing.localPosition = new Vector3(0f, 1.62f, 0.02f);
                    backing.localScale = new Vector3(0.8f, 1.15f, 0.04f);
                }

                Transform title = parent.Find("BoardTitle");
                if (title != null)
                {
                    title.position = spot.Position(2.06f, 0.045f);
                }

                return 0;
            }

            var board = new GameObject("ValveBoard");
            board.transform.SetParent(parent, false);
            board.transform.SetPositionAndRotation(spot.Position(0f, 0f), Quaternion.Euler(0f, spot.Yaw, 0f));
            Part(board.transform, "Backing", PrimitiveType.Cube, new Vector3(0f, 1.62f, 0.02f), Vector3.zero, new Vector3(0.8f, 1.15f, 0.04f), PaintYellow);
            Label(parent, "BoardTitle", spot.Position(2.06f, 0.045f), "ПОРЯДОК ВЕНТИЛЕЙ", 0.055f, Color.black);

            int made = WallNote(parent, "Note_Valves", new WallSpot(ValveWallX - 0.04f, -16.6f, Vector3.left), "Прочитать порядок вентилей", ValveSchematic);
            Transform note = parent.Find("Note_Valves");
            if (note != null)
            {
                note.position = spot.Position(1.5f, 0.045f);
                note.localScale = new Vector3(0.6f, 0.75f, 0.008f);
                BoxCollider hit = note.GetComponent<BoxCollider>();
                hit.size = new Vector3(1.3f, 1.2f, 40f);
                MakeSheet(note, "ГИДРАВЛИКА ВОРОТ — ПЕРЕЗАПУСК", ValveSchematic);
            }

            Light lamp = Lamp(parent, "ValveBoard_Lamp", spot.Position(2.35f, 0.5f), new Color(1f, 0.85f, 0.6f), 1.4f, 3.2f, true);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 70f;
            lamp.transform.rotation = Quaternion.LookRotation(spot.Position(1.5f, 0f) - lamp.transform.position);
            return made;
        }

        private const string ValveSchematic =
            "Pumps run on MAIN power only (generator).\n\n" +
            "Open the line valves in THIS order and no other:\n\n" +
            "              <b>{valves}</b>\n\n" +
            "Wrong order = blowback. Everything shuts and you start again - and the bang carries.\n\n" +
            "With the pressure up, the bay door in assembly can be lifted. It is slow. It is loud.";

        /// <summary>World-space letters (TextMesh), readable from the hall side of the east wall.</summary>
        private static void Label(Transform parent, string name, Vector3 position, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 90f, 0f));
            TextMesh mesh = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        /// <summary>The settings screen gets a Difficulty row under Volume (the Back button moves down a row).</summary>
        private static int DifficultyRow()
        {
            AshenWoods.UI.SettingsMenu menu = Object.FindAnyObjectByType<AshenWoods.UI.SettingsMenu>(FindObjectsInactive.Include);
            Transform volume = menu != null ? menu.transform.Find("Row_Volume") : null;
            if (volume == null || menu.transform.Find("Row_Difficulty") != null)
            {
                return 0;
            }

            const float step = 50f;
            GameObject row = Object.Instantiate(volume.gameObject, menu.transform);
            row.name = "Row_Difficulty";
            foreach (RectTransform child in row.transform)
            {
                child.anchoredPosition -= new Vector2(0f, step);
            }

            UnityEngine.UI.Text label = row.transform.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
            if (label != null)
            {
                label.text = "Сложность";
            }

            if (menu.transform.Find("BackButton") is RectTransform back)
            {
                back.anchoredPosition -= new Vector2(0f, step);
            }

            return 1;
        }

        // ------------------------------------------------------------------ wiring

        /// <summary>Every run: power, the bay door's two requirements and the breakers' new role.</summary>
        private static void WireQuests()
        {
            PuzzleController breakers = BreakerPanel();
            PuzzleController generator = GeneratorPanel();
            PuzzleController valves = ValvePanel();

            if (generator != null)
            {
                var so = new SerializedObject(generator);
                so.FindProperty("prerequisite").objectReferenceValue = breakers;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (valves != null)
            {
                var so = new SerializedObject(valves);
                so.FindProperty("prerequisite").objectReferenceValue = generator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (breakers != null && generator != null)
            {
                var so = new SerializedObject(breakers);
                if (string.IsNullOrEmpty(so.FindProperty("solvedMessage").stringValue))
                {
                    so.FindProperty("solvedMessage").stringValue =
                        "Щит ожил, но основной свет так и не загорелся. Похоже, генератор без топлива.";
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();
            if (exit != null)
            {
                var so = new SerializedObject(exit);
                so.FindProperty("powerSource").objectReferenceValue = PowerSource();
                so.FindProperty("extraRequirement").objectReferenceValue = valves;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            WirePower();
            WriteClues();

            Keypad keypad = GameObject.Find("Keypad_Office")?.GetComponent<Keypad>();
            Door office = GameObject.Find("Door_Office")?.GetComponent<Door>();
            if (keypad != null && office != null)
            {
                var so = new SerializedObject(keypad);
                so.FindProperty("door").objectReferenceValue = office;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Every run, only where still empty (hand-written text wins): the journal line of each clue note and the
        /// hint of each objective step.
        /// </summary>
        private static void WriteClues()
        {
            void Clue(string path, string line)
            {
                InfoPoint info = GameObject.Find(path)?.GetComponent<InfoPoint>();
                if (info == null)
                {
                    return;
                }

                var so = new SerializedObject(info);
                if (string.IsNullOrEmpty(so.FindProperty("journalLine").stringValue))
                {
                    so.FindProperty("journalLine").stringValue = line;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Clue("Facility/Quests/Note_Code_1", "Код кабинета, первая цифра: <b>{code1}</b> (склад)");
            Clue("Facility/Quests/Note_Code_2", "Код кабинета, средняя цифра: <b>{code2}</b> (сборочный цех)");
            Clue("Facility/Quests/Note_Code_3", "Код кабинета, последняя цифра: <b>{code3}</b> (лаборатория)");
            Clue("Facility/Quests/Note_Valves", "Порядок вентилей (комната под электрощитовой): <b>{valves}</b>");
            Clue("Facility/Notes/Note_Janitor", "F — фонарь, Q — вспышка, G (зажать) — бросок, E — действие, C — пригнуться. Оно охотится на звук.");

            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager == null)
            {
                return;
            }

            var hints = new System.Collections.Generic.Dictionary<string, string>
            {
                { "torch", "Стол уборщика — прямо здесь, в каморке, где всё началось." },
                { "office_code", "Three notes are pinned to the walls: one in storage, one in assembly, one in the lab. They glow faintly. J shows the digits you found." },
                { "keycard", "Ключ-карта лежит на столе в кабинете лаборатории, у северной стены." },
                { "breaker_room", "Мостки в электроцехе ведут к двери электрощитовой — она высоко у восточной стены." },
                { "breakers", "Все рубильники на щите должны стоять вверху." },
                { "fuel", "Канистры стоят на складе, на полу у стен." },
                { "generator", "Генератор — на нижнем этаже электроцеха, пусковой пульт у его восточного края." },
                { "valves", "The valve room is the ground floor under the breaker room - doors on its west and south sides. The yellow board inside gives the order. J recalls it." },
                { "escape", "The bay door is on the assembly hall's east wall. Once it starts lifting, hide, flash, throw - stay alive." },
            };

            var m = new SerializedObject(manager);
            SerializedProperty steps = m.FindProperty("steps");
            bool changed = false;
            for (int i = 0; i < steps.arraySize; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                SerializedProperty hint = step.FindPropertyRelative("hint");
                if (hint != null && string.IsNullOrEmpty(hint.stringValue)
                    && hints.TryGetValue(step.FindPropertyRelative("id").stringValue, out string text))
                {
                    hint.stringValue = text;
                    changed = true;
                }
            }

            if (changed)
            {
                m.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Once (Quests_v1): the new chain, the torch as an item, the janitor's guide.</summary>
        private static void WriteQuestChain()
        {
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager == null)
            {
                return;
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

            Door office = GameObject.Find("Door_Office")?.GetComponent<Door>();
            Door breakerDoor = GameObject.Find("Door_BreakerRoom")?.GetComponent<Door>();
            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();

            Step("torch", "Find a torch (janitor's desk)", ObjectiveCondition.CarryKey, TorchId, null, null);
            Step("office_code", "Open the lab office - its code is split across three notes (storage, assembly, lab)",
                ObjectiveCondition.DoorUnlocked, null, office, "door");
            Step("keycard", "Take the security keycard (lab office)", ObjectiveCondition.CarryKey, KeycardId, null, null);
            Step("breaker_room", "Climb the catwalks to the breaker room (electrical hall)", ObjectiveCondition.DoorUnlocked, null, breakerDoor, "door");
            Step("breakers", "Reset the breakers", ObjectiveCondition.PuzzleSolved, null, BreakerPanel(), "puzzle");
            Step("fuel", "Find fuel for the generator (storage hall)", ObjectiveCondition.CarryKeys, FuelOneId + "," + FuelTwoId, null, null);
            Step("generator", "Start the generator (electrical hall, ground floor)", ObjectiveCondition.PuzzleSolved, null, GeneratorPanel(), "puzzle");
            Step("valves", "Open the hydraulic valves in the order on the schematic (electrical hall, east wall)",
                ObjectiveCondition.PuzzleSolved, null, ValvePanel(), "puzzle");
            Step("escape", "Lift the loading bay door and hold out until it is open (assembly hall)", ObjectiveCondition.EscapeUsed, null, exit, "exit");
            so.ApplyModifiedPropertiesWithoutUndo();

            PlayerFlashlight torch = Object.FindAnyObjectByType<PlayerFlashlight>();
            if (torch != null)
            {
                var t = new SerializedObject(torch);
                t.FindProperty("requiredItemId").stringValue = TorchId;
                t.FindProperty("startOn").boolValue = false;
                t.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform guide = Group("Notes").Find("Note_Janitor");
            InfoPoint info = guide != null ? guide.GetComponent<InfoPoint>() : null;
            if (info != null)
            {
                var g = new SerializedObject(info);
                string text = g.FindProperty("message").stringValue;
                int cut = text.IndexOf("<b>Way out</b>", System.StringComparison.Ordinal);
                if (cut >= 0)
                {
                    g.FindProperty("message").stringValue = text.Substring(0, cut) + WayOut;
                    g.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private const string WayOut =
            "<b>Way out</b>: Dr. Hale's keycard is in the lab office. She locked it with a keypad and the night crew " +
            "only ever knew a digit each - their notes are around the halls. The keycard opens the breaker room " +
            "(electrical hall, up the catwalks). The breakers only bring control power: the generator is dry, the fuel " +
            "is kept in the storage hall. With main power, open the bay hydraulics (the order is on the schematic) " +
            "and lift the loading bay door in assembly. It is slow and loud. Hold out.";
    }
}
