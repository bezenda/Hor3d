using UnityEditor;
using UnityEngine;
using AshenWoods.GameManagement;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Interface v2 copy pass (2026-11-19): the objective lines and stuck-hints read the way a person would say
    /// them - one sentence, the place said naturally instead of in brackets - and every objective starts with a
    /// verb, so the pause screen can say "You were trying to ...". A line is only replaced while it still holds the
    /// old builder text: anything rewritten by hand stays.
    /// </summary>
    public static partial class FacilityBuilder
    {
        // id -> old builder text, new text
        private static readonly string[,] ObjectiveCopy =
        {
            { "torch", "Find a torch (janitor's desk)", "Найди фонарь. Он должен быть на столе уборщика." },
            { "office_code", "Open the lab office - its code is split across three notes (storage, assembly, lab)",
                "Get into the lab office. Its code is split across three notes - in storage, assembly and the lab." },
            { "office_code", "Open the lab office - its code is on three notes pinned to walls (storage, assembly, lab)",
                "Попади в кабинет лаборатории. Код от двери — на трёх записках: на складе, в сборочном цехе и в лаборатории." },
            { "valves", "Open the hydraulic valves in the order on the yellow VALVE ORDER board (valve room, under the breaker room)",
                "Открой вентили гидравлики в комнате под электрощитовой. Порядок — на жёлтой доске." },
            { "keycard", "Take the security keycard (lab office)", "Возьми ключ-карту охраны в кабинете лаборатории." },
            { "breaker_room", "Climb the catwalks to the breaker room (electrical hall)",
                "Поднимись по мосткам электроцеха к электрощитовой." },
            { "breakers", "Reset the breakers", "Включи рубильники на главном щите." },
            { "fuel", "Find fuel for the generator (storage hall)", "Принеси две канистры топлива для генератора. Они на складе." },
            { "generator", "Start the generator (electrical hall, ground floor)", "Запусти генератор на нижнем этаже электроцеха." },
            { "valves", "Open the hydraulic valves in the order on the schematic (electrical hall, east wall)",
                "Open the hydraulic valves in the order on the yellow board." },
            { "escape", "Lift the loading bay door and hold out until it is open (assembly hall)",
                "Подними ворота погрузочной зоны в сборочном цехе и продержись, пока они открываются." },
        };

        private static readonly string[,] HintCopy =
        {
            { "office_code",
                "Three notes are pinned to the walls: one in storage, one in assembly, one in the lab. They glow faintly. J shows the digits you found.",
                "Записки висят на стенах склада, сборочного цеха и лаборатории и слегка светятся. Найденные цифры — в дневнике (J)." },
            { "valves",
                "The valve room is the ground floor under the breaker room - doors on its west and south sides. The yellow board inside gives the order. J recalls it.",
                "The valve room is under the breaker room, with doors on its west and south sides. The yellow board inside shows the order - and J remembers it." },
            { "valves",
                "The valve room is the ground floor under the breaker room in the electrical hall - doors on its west and south sides. The yellow board inside gives the order. J recalls it.",
                "Комната вентилей — под электрощитовой, двери с западной и южной стороны. Порядок написан на жёлтой доске внутри и записан в дневнике (J)." },
            { "escape",
                "The bay door is on the assembly hall's east wall. Once it starts lifting, hide, flash, throw - stay alive.",
                "Ворота — на восточной стене сборочного цеха. Пока они ползут вверх, прячься, слепи его вспышкой, отвлекай бросками — главное, выживи." },
        };

        private static int RewriteCopy()
        {
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager == null)
            {
                return 0;
            }

            var so = new SerializedObject(manager);
            SerializedProperty steps = so.FindProperty("steps");
            int changed = 0;
            for (int i = 0; i < steps.arraySize; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                string id = step.FindPropertyRelative("id").stringValue;
                changed += Swap(step.FindPropertyRelative("description"), id, ObjectiveCopy);
                changed += Swap(step.FindPropertyRelative("hint"), id, HintCopy);
            }

            if (changed > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static int Swap(SerializedProperty property, string id, string[,] table)
        {
            if (property == null)
            {
                return 0;
            }

            for (int i = 0; i < table.GetLength(0); i++)
            {
                if (table[i, 0] == id && property.stringValue == table[i, 1])
                {
                    property.stringValue = table[i, 2];
                    return 1;
                }
            }

            return 0;
        }
    }
}
