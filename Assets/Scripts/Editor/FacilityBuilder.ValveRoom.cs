using UnityEditor;
using UnityEngine;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The valve room (2026-11-13, owner: "no way in"): the ground floor of the breaker tower (x 24..36, z -24..-12)
    /// was a closed box with the valves and the VALVE ORDER board inside. Two kit "Wall Door" sections replace plain
    /// ones - west (x 24, z -24..-18, right below the breaker door) and south (z -24, x 30..36) - and each gets an
    /// unlocked copy of the breaker room's door, placed the same way relative to its frame.
    /// One-time swap under the TowerDoors_v1 marker; the shell code itself now lays these sections as doors.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string TowerDoorsMarker = "TowerDoors_v1";

        private static int OpenValveRoom()
        {
            Transform electric = Group("Shell/Electrical");
            int added = 0;

            if (root.Find(TowerDoorsMarker) == null)
            {
                foreach (string name in new[] { "TowerW0_-24_0", "TowerS0_30_0" })
                {
                    Transform plain = electric.Find(name);
                    if (plain != null)
                    {
                        Object.DestroyImmediate(plain.gameObject);
                    }
                }

                // Same pivots/yaws as WallLine: east-facing x run (pivot at s + 6, yaw 180), north-facing z run (yaw 90).
                added += Put(electric, "TowerW0_-24_0", "Wall Door", new Vector3(24f, 0f, -18f), 180f);
                added += Put(electric, "TowerS0_30_0", "Wall Door", new Vector3(30f, 0f, -24f), 90f);

                RewordValveSteps();

                var flag = new GameObject(TowerDoorsMarker);
                flag.SetActive(false);
                flag.transform.SetParent(root, false);
                added++;
            }

            added += ValveRoomDoor("Door_ValveRoom_W", electric.Find("TowerW0_-24_0"));
            added += ValveRoomDoor("Door_ValveRoom_S", electric.Find("TowerS0_30_0"));
            return added;
        }

        /// <summary>
        /// A copy of the breaker room door, put on <paramref name="frame"/> exactly as the original sits on its own
        /// frame (the breaker door's frame is the same kit piece, 9 m up). Unlocked; it opens into the valve room.
        /// </summary>
        private static int ValveRoomDoor(string name, Transform frame)
        {
            if (frame == null || GameObject.Find(name) != null)
            {
                return 0;
            }

            GameObject source = GameObject.Find("Door_BreakerRoom");
            Transform sourceFrame = Group("Shell/Electrical").Find("TowerW1_-24_9");
            if (source == null || sourceFrame == null)
            {
                Debug.LogWarning("[Facility] " + name + ": no breaker door or frame to copy.");
                return 0;
            }

            Vector3 localPosition = sourceFrame.InverseTransformPoint(source.transform.position);
            Quaternion localRotation = Quaternion.Inverse(sourceFrame.rotation) * source.transform.rotation;

            GameObject door = Object.Instantiate(source, Group("Quests"));
            door.name = name;
            door.transform.SetPositionAndRotation(frame.TransformPoint(localPosition), frame.rotation * localRotation);

            var so = new SerializedObject(door.GetComponent<Door>());
            so.FindProperty("isLocked").boolValue = false;
            so.FindProperty("requiredKeyId").stringValue = string.Empty;
            so.FindProperty("openPrompt").stringValue = "Открыть комнату вентилей";
            so.FindProperty("closePrompt").stringValue = "Закрыть дверь";
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>The valve step, its hint and the board's journal line now name the room, not "the east wall".</summary>
        private static void RewordValveSteps()
        {
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                SerializedProperty steps = so.FindProperty("steps");
                for (int i = 0; i < steps.arraySize; i++)
                {
                    SerializedProperty step = steps.GetArrayElementAtIndex(i);
                    if (step.FindPropertyRelative("id").stringValue == "valves")
                    {
                        step.FindPropertyRelative("description").stringValue =
                            "Open the hydraulic valves in the order on the yellow VALVE ORDER board (valve room, under the breaker room)";
                        step.FindPropertyRelative("hint").stringValue =
                            "The valve room is the ground floor under the breaker room in the electrical hall - doors on its west and south sides. The yellow board inside gives the order. J recalls it.";
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            InfoPoint board = GameObject.Find("Facility/Quests/Note_Valves")?.GetComponent<InfoPoint>();
            if (board != null)
            {
                var b = new SerializedObject(board);
                b.FindProperty("journalLine").stringValue = "Порядок вентилей (комната под электрощитовой): <b>{valves}</b>";
                b.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
