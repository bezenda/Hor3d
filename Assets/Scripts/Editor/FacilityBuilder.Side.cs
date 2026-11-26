using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Phase 4 (2026-11-12): side goals and a level that changes between runs.
    ///  - Eight staff logs (InfoPoint lore ids): read them all for the WITNESS ending.
    ///  - Three B-7 tissue samples in the lab: carry them out for EVIDENCE (both = THE WHOLE TRUTH).
    ///  - A security terminal in the lab (needs main power): the map plus where the enemy is, at the price of a beep.
    ///  - RandomPlacement on the fuel cans, the three code notes and the samples: each run they lie elsewhere
    ///    (the same after a checkpoint retry).
    /// Made once by name under Facility/Side; spot lists are re-written every run.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private struct XZ
        {
            public float x, z, yaw;
            public XZ(float x, float z, float yaw = 0f) { this.x = x; this.z = z; this.yaw = yaw; }
        }

        private static int SetUpSideGoals()
        {
            Transform side = Group("Side");
            int added = 0;
            added += StaffLogs(side);
            added += Samples(side);
            added += SecurityTerminal(side);
            PlaceRandomly();
            AddGlints();
            return added;
        }

        // ------------------------------------------------------------------ staff logs

        private static readonly (string id, XZ at, float fromY, string title, string body)[] Logs =
        {
            ("log_stores", new XZ(18.1f, 7.1f, 10f), 2.2f, "СКЛАД — НАКЛАДНАЯ № 14",
                "Containment gel, 40 drums. Sedative (vet grade), 12 crates. Raw meat, 200 kg - weekly.\n\n" +
                "Signed for by Dr. E. Hale. Destination: B-7.\n\nWhat eats 200 kg a week?"),
            ("log_janitor", new XZ(1.1f, 7.2f, -15f), 2.2f, "COMPLAINT",
                "To whoever reads these:\n\nsomething is in the ducts over storage. It scratches all night and it " +
                "follows the sound of my cart. I am not imagining it.\n\nI asked for the vents to be sealed. Nobody answers."),
            ("log_foreman", new XZ(55.2f, 22.1f, 5f), 2.2f, "БРИГАДИР — НОЧНАЯ СМЕНА",
                "Hale came down in person. North vents to be welded tonight, no questions, double pay.\n\n" +
                "Ross asked what they were keeping in the lab. She told him to go home. He did not go home."),
            ("log_silence", new XZ(66.3f, 22.2f, -10f), 2.2f, "ПАМЯТКА ДЛЯ ВСЕГО ПЕРСОНАЛА",
                "Effective immediately: SILENCE PROTOCOL in all halls.\n\nNo running. No dropped tools. No radios.\n" +
                "If the lights fail, stay where you are and keep still.\n\nIt cannot see well in the dark. It does not need to.\n\n- E. Hale"),
            ("log_electric", new XZ(8.1f, -14.1f, 20f), 2.2f, "ELECTRICIAN'S LOG",
                "Hale told me to drain the generator and leave the cans in stores. \"If the power stays down, the bay " +
                "door stays down.\"\n\nShe is not trying to keep thieves out. She is trying to keep something IN."),
            ("log_research1", new XZ(59.4f, -40.9f, 0f), 2.2f, "ЖУРНАЛ ИССЛЕДОВАНИЙ — B-7, ДЕНЬ 31",
                "Recovered from the Ashen Woods, south of the old quarry.\n\nAversion to bright light is extreme - a " +
                "photoflash drops it for seconds. Hearing is far beyond anything we measured.\n\nIt has started to " +
                "imitate the voices of the night crew."),
            ("log_research2", new XZ(50.4f, -40.9f, 0f), 2.2f, "ЖУРНАЛ ИССЛЕДОВАНИЙ — B-7, ДЕНЬ 40",
                "It repeated something Ross said on his last night, in his voice, perfectly:\n\n" +
                "\"Ah, another victim.\"\n\nWe never found Ross. The glass of B-7 is cracked from the inside."),
            ("log_hale", new XZ(25.9f, -15.7f, 25f), 11.5f, "Э. ХЕЙЛ — ЛИЧНОЕ",
                "I cut the power myself. I hid the code, I drained the fuel, I locked the bay.\n\n" +
                "If it reaches the woods it reaches the town.\n\nWhoever reads this: if you open that door, " +
                "make sure you are the only thing that walks out."),
        };

        private static int StaffLogs(Transform parent)
        {
            int added = 0;
            foreach (var log in Logs)
            {
                string name = "Log_" + log.id;
                if (parent.Find(name) != null)
                {
                    continue;
                }

                Vector3 top = SurfaceAt(log.at.x, log.at.z, log.fromY);
                added += Note(parent, name, top + Vector3.up * 0.006f, new Vector3(0.21f, 0.01f, 0.29f), Paper,
                    "Прочитать журнал", log.body, false);
                Transform note = parent.Find(name);
                if (note == null)
                {
                    continue;
                }

                note.rotation = Quaternion.Euler(0f, log.at.yaw, 0f);
                MakeSheet(note, log.title, log.body);
                var so = new SerializedObject(note.GetComponent<InfoPoint>());
                so.FindProperty("loreId").stringValue = log.id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return added;
        }

        // ------------------------------------------------------------------ samples

        private static int Samples(Transform parent)
        {
            int added = 0;
            for (int i = 1; i <= SideProgress.SamplesTotal; i++)
            {
                string name = "Sample_" + i;
                if (parent.Find(name) != null)
                {
                    continue;
                }

                GameObject sample = QuestPickup(parent, name, new Vector3(54f, 1f, -37f), 0f, SideProgress.SamplePrefix + i,
                    "образец ткани B-7", false, "Запаянная пробирка. Что-то внутри бьётся о стекло.");
                if (sample == null)
                {
                    continue;
                }

                Transform visual = sample.transform.Find("Visuals");
                Part(visual, "Vial", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(0.035f, 0.05f, 0.035f), SampleGlow);
                Part(visual, "Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.115f, 0f), Vector3.zero, new Vector3(0.04f, 0.008f, 0.04f), PaintRed);
                Lamp(sample.transform, "Glint", sample.transform.position + Vector3.up * 0.2f, new Color(0.3f, 1f, 0.45f), 0.25f, 1f, true);

                BoxCollider box = sample.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = new Vector3(0f, 0.07f, 0f);
                    box.size = new Vector3(0.2f, 0.2f, 0.2f);
                }

                added++;
            }

            return added;
        }

        private static Material SampleGlow => Mat("M_Sample_Glow", new Color(0.12f, 0.35f, 0.15f), 0.9f, 0f,
            new Color(0.15f, 0.9f, 0.3f), 0.85f);

        // ------------------------------------------------------------------ security terminal

        private static int SecurityTerminal(Transform parent)
        {
            if (parent.Find("SecurityTerminal") != null)
            {
                return 0;
            }

            // On the lab's west console, facing east into the lab.
            Vector3 top = SurfaceAt(60f, -28f);
            var terminal = new GameObject("SecurityTerminal");
            terminal.transform.SetParent(parent, false);
            terminal.transform.SetPositionAndRotation(top, Quaternion.Euler(0f, 90f, 0f));
            Part(terminal.transform, "Stand", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.08f, 0.16f, 0.08f), MetalDark);
            Part(terminal.transform, "Monitor", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0f), Vector3.zero, new Vector3(0.52f, 0.34f, 0.05f), MetalDark);
            GameObject screen = Part(terminal.transform, "Screen", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0.027f), Vector3.zero,
                new Vector3(0.47f, 0.29f, 0.004f), ScreenOff);

            BoxCollider aim = terminal.AddComponent<BoxCollider>();
            aim.center = new Vector3(0f, 0.3f, 0f);
            aim.size = new Vector3(0.6f, 0.6f, 0.3f);

            InfoPoint info = terminal.AddComponent<InfoPoint>();
            var so = new SerializedObject(info);
            so.FindProperty("prompt").stringValue = "Открыть терминал охраны";
            so.FindProperty("showMap").boolValue = true;
            so.FindProperty("markEnemies").boolValue = true;
            so.FindProperty("useNoise").floatValue = 0.35f;
            so.FindProperty("sheetTitle").stringValue = "ОХРАНА — ДАТЧИКИ ДВИЖЕНИЯ";
            so.FindProperty("needsPower").boolValue = true;
            so.FindProperty("unpoweredPrompt").stringValue = "Терминал охраны (нет питания)";
            so.FindProperty("unpoweredMessage").stringValue = "Не работает. Нужно основное питание — от генератора.";
            so.FindProperty("screen").objectReferenceValue = screen.GetComponent<Renderer>();
            so.FindProperty("screenOff").objectReferenceValue = ScreenOff;
            so.FindProperty("screenOn").objectReferenceValue = ScreenOn;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>
        /// A faint glow on what the player has to find (fuel cans, code notes, staff logs): players follow light, and a
        /// clue nobody can see in the dark only causes frustration. Child lights travel with RandomPlacement.
        /// </summary>
        private static void AddGlints()
        {
            var paths = new List<string> { "Facility/Quests/FuelCan_1", "Facility/Quests/FuelCan_2",
                "Facility/Quests/Note_Code_1", "Facility/Quests/Note_Code_2", "Facility/Quests/Note_Code_3", "Facility/Quests/Torch" };
            foreach (var log in Logs)
            {
                paths.Add("Facility/Side/Log_" + log.id);
            }

            foreach (string path in paths)
            {
                GameObject item = GameObject.Find(path);
                if (item == null || item.transform.Find("Glint") != null)
                {
                    continue;
                }

                bool can = path.Contains("FuelCan");
                bool wall = path.Contains("Note_Code");
                Vector3 at = wall ? item.transform.position + item.transform.forward * 0.4f + Vector3.up * 0.2f
                    : item.transform.position + Vector3.up * (can ? 0.6f : 0.3f);
                Lamp(item.transform, "Glint", at, new Color(1f, 0.82f, 0.55f),
                    wall ? 0.6f : can ? 0.35f : 0.22f, wall ? 1.8f : can ? 1.6f : 1.0f, true);
            }
        }

        // ------------------------------------------------------------------ random spots

        private static void PlaceRandomly()
        {
            // Measure the spots with every pickup out of the way (a fuel can on one spot must not lift the next).
            var pickups = new List<Collider>();
            foreach (KeyPickup pickup in Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None))
            {
                foreach (Collider c in pickup.GetComponentsInChildren<Collider>())
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        pickups.Add(c);
                    }
                }
            }

            try
            {
                PlaceRandomlyMeasured();
            }
            finally
            {
                foreach (Collider c in pickups) c.enabled = true;
            }
        }

        private static void PlaceRandomlyMeasured()
        {
            XZ[] fuel =
            {
                new XZ(1.9f, 12.5f, 80f), new XZ(34.1f, 15f, -100f), new XZ(34.1f, 6f, -80f), new XZ(26f, 22.6f, 170f),
                new XZ(16f, 22.6f, 190f), new XZ(1.9f, 15.5f, 95f),
            };
            Randomize("Facility/Quests/FuelCan_1", "fuel", 0, fuel, 0f);
            Randomize("Facility/Quests/FuelCan_2", "fuel", 1, fuel, 0f);

            RandomizeOnWalls("Facility/Quests/Note_Code_1", "code_storage", StorageNoteSpots);
            RandomizeOnWalls("Facility/Quests/Note_Code_2", "code_assembly", AssemblyNoteSpots);
            RandomizeOnWalls("Facility/Quests/Note_Code_3", "code_lab", LabNoteSpots);

            XZ[] samples =
            {
                // Open bench tops only (2026-11-13: the capsule spots put the vials inside the glass, out of reach).
                new XZ(51.6f, -40.8f), new XZ(54.6f, -40.8f), new XZ(63.6f, -40.8f), new XZ(71f, -40.8f), new XZ(55f, -24.1f),
            };
            for (int i = 1; i <= SideProgress.SamplesTotal; i++)
            {
                Randomize("Facility/Side/Sample_" + i, "samples", i - 1, samples, 0.01f);
            }
        }

        /// <summary>RandomPlacement over wall spots (fixed height, facing into the room).</summary>
        private static void RandomizeOnWalls(string path, string group, WallSpot[] spots)
        {
            GameObject target = GameObject.Find(path);
            if (target == null)
            {
                return;
            }

            RandomPlacement placement = target.GetComponent<RandomPlacement>();
            if (placement == null)
            {
                placement = target.AddComponent<RandomPlacement>();
            }

            var so = new SerializedObject(placement);
            so.FindProperty("group").stringValue = group;
            so.FindProperty("slot").intValue = 0;
            SerializedProperty list = so.FindProperty("spots");
            list.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value = spots[i].Position(NoteHeight);
                list.GetArrayElementAtIndex(i).FindPropertyRelative("yaw").floatValue = spots[i].Yaw;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gives the object a RandomPlacement over surface spots (heights measured now) and puts it on the first.</summary>
        private static void Randomize(string path, string group, int slot, XZ[] spots, float lift)
        {
            GameObject target = GameObject.Find(path);
            if (target == null)
            {
                return;
            }

            RandomPlacement placement = target.GetComponent<RandomPlacement>();
            if (placement == null)
            {
                placement = target.AddComponent<RandomPlacement>();
            }

            // Measure with the item itself out of the way.
            var colliders = new List<Collider>(target.GetComponentsInChildren<Collider>());
            foreach (Collider c in colliders) c.enabled = false;

            var so = new SerializedObject(placement);
            so.FindProperty("group").stringValue = group;
            so.FindProperty("slot").intValue = slot;
            SerializedProperty list = so.FindProperty("spots");
            list.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 top = SurfaceAt(spots[i].x, spots[i].z) + Vector3.up * lift;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value = top;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("yaw").floatValue = spots[i].yaw;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (Collider c in colliders) c.enabled = true;

            // In the editor the item sits on its slot's spot of an unshuffled list (what you see is a valid spot).
            Vector3 first = list.GetArrayElementAtIndex(slot % spots.Length).FindPropertyRelative("position").vector3Value;
            if ((target.transform.position - first).sqrMagnitude > 0.0001f)
            {
                target.transform.SetPositionAndRotation(first, Quaternion.Euler(0f, spots[slot % spots.Length].yaw, 0f));
            }
        }
    }
}
