using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AshenWoods.Interaction;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// First-minute touches (owner, 2026-11-25), once (marker Opening_v2):
    ///  - the janitor's laptop runs on its battery from the start: an in-world briefing from security (how it hunts, how
    ///    to survive it, what it takes to get out), the controls as a game hint under the screen, and the station's
    ///    "power restored" log added on top once the generator runs;
    ///  - the clipboard beside it becomes the janitor's own note (no key names in the world any more);
    ///  - keypads get their LED on the panel face (the light hung 9 cm in front of the model) with a lens mesh, and the
    ///    louder alarm values (Keypad: wrong code = alarm + lockout).
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string BriefingName = "Opening_v2";

        private const string LaptopBriefing =
            "АВАРИЙНЫЙ РЕЖИМ. ПИТАНИЕ ОТ АККУМУЛЯТОРА.\n\n" +
            "ВСЕМУ ПЕРСОНАЛУ, ОСТАВШЕМУСЯ НА СТАНЦИИ\n" +
            "Объект B-7 покинул камеру. Он охотится на звук и плохо видит в темноте, но заметит любой свет.\n\n" +
            "— Двигайтесь медленно. Бег слышен через весь цех.\n" +
            "— Если объект вас заметил, уйдите из поля зрения и укройтесь в шкафчике или под столом.\n" +
            "— В укрытии не дышите, пока он рядом: дыхание за дверцей он слышит.\n" +
            "— Вспышка фонаря сбивает его с ног на несколько секунд.\n" +
            "— Брошенная бутылка или банка уведёт его на шум.\n\n" +
            "ЭВАКУАЦИЯ — через ворота погрузочной зоны в сборочном цехе. Чтобы их поднять, нужно:\n" +
            "1. Включить рубильники в электрощитовой. Её дверь открывает ключ-карта охраны из кабинета лаборатории, " +
            "а код от кабинета знала ночная смена.\n" +
            "2. Заправить генератор — канистры на складе.\n" +
            "3. Открыть вентили гидравлики в комнате под электрощитовой.\n\n" +
            "Не шумите. Не ждите помощи.";

        private const string LaptopPowered =
            "Питание восстановлено.\n\nНарушена изоляция B-7. Объект на свободе.\nОхотится на звук, избегает яркого света.\n\n" +
            "Эвакуируйтесь через погрузочную зону.\nНас не ждите.\n\n— — —";

        private const string JanitorNote =
            "Если ты это читаешь, значит, я так и не вернулся. Пошёл искать Росса.\n\n" +
            "Фонарь оставил на столе. Батарейки валяются по всей станции — подбирай все.\n\n" +
            "Ноутбук работает от аккумулятора. Охрана разослала по нему памятку — прочти, там всё.\n\n" +
            "И не беги. Оно слышит.\n\n— Г.";

        private static int SetUpBriefing()
        {
            if (root == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found == null)
                {
                    return 0;
                }

                root = found.transform;
            }

            if (root.Find(BriefingName) != null)
            {
                return 0;
            }

            int changed = 0;
            GameObject laptop = GameObject.Find("Laptop");
            InfoPoint laptopInfo = laptop != null ? laptop.GetComponent<InfoPoint>() : null;
            if (laptopInfo != null)
            {
                var so = new SerializedObject(laptopInfo);
                so.FindProperty("needsPower").boolValue = false;
                so.FindProperty("showAsSheet").boolValue = true;
                so.FindProperty("prompt").stringValue = "Посмотреть ноутбук";
                so.FindProperty("sheetTitle").stringValue = "ТЕРМИНАЛ ДЕЖУРНОГО";
                so.FindProperty("message").stringValue = LaptopBriefing;
                so.FindProperty("poweredMessage").stringValue = LaptopPowered;
                so.FindProperty("showControls").boolValue = true;
                so.FindProperty("journalLine").stringValue =
                    "Выход — ворота погрузочной зоны. Нужны: ключ-карта (кабинет лаборатории), рубильники, топливо, гидравлика.";
                so.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }

            GameObject clipboard = GameObject.Find("Note_Janitor");
            InfoPoint note = clipboard != null ? clipboard.GetComponent<InfoPoint>() : null;
            if (note != null)
            {
                var so = new SerializedObject(note);
                so.FindProperty("prompt").stringValue = "Прочитать записку";
                so.FindProperty("sheetTitle").stringValue = "ДЛЯ СМЕНЩИКА";
                so.FindProperty("message").stringValue = JanitorNote;
                so.FindProperty("journalLine").stringValue = "Уборщик: в ноутбуке на столе памятка охраны. Не бегать.";
                so.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }

            foreach (Keypad keypad in Object.FindObjectsByType<Keypad>(FindObjectsInactive.Include))
            {
                changed += FitKeypadLed(keypad);
                var so = new SerializedObject(keypad);
                so.FindProperty("wrongNoise").floatValue = 0.9f;
                so.FindProperty("lockoutSeconds").floatValue = 5f;
                so.FindProperty("wrongMessage").stringValue = "Неверный код — завыла тревога! Оно услышит.";
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var marker = new GameObject(BriefingName);
            marker.transform.SetParent(root, false);
            marker.SetActive(false);
            return changed + 1;
        }

        /// <summary>Puts the keypad's LED on its face, above the screen, with a small glowing lens.</summary>
        private static int FitKeypadLed(Keypad keypad)
        {
            Transform body = keypad.transform;
            Renderer bodyRenderer = keypad.GetComponent<Renderer>();
            Transform screen = body.Find("Screen");
            if (bodyRenderer == null || screen == null)
            {
                return 0;
            }

            // The face looks the way the screen sits off the body's centre.
            Vector3 face = screen.position - body.position;
            face.y = 0f;
            face = face.sqrMagnitude > 0.0001f ? face.normalized : -body.right;
            Bounds bounds = bodyRenderer.bounds;
            float halfDepth = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(face.x), 0f, Mathf.Abs(face.z))));
            Vector3 side = Vector3.Cross(Vector3.up, face);
            float halfWidth = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z))));
            // In the dark strip between the screen and the keys, near the right edge.
            Transform keys = body.Find("Keys");
            float y = keys != null ? Mathf.Lerp(screen.position.y, keys.position.y, 0.5f) : bounds.max.y - 0.03f;
            Vector3 onFace = new Vector3(bounds.center.x, y, bounds.center.z)
                             + face * halfDepth - side * (halfWidth - 0.04f);

            Transform lens = body.Find("LedLens");
            if (lens == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.name = "LedLens";
                cube.transform.SetParent(body, true);
                lens = cube.transform;
            }

            lens.position = onFace + face * 0.002f;
            lens.rotation = Quaternion.LookRotation(face, Vector3.up);
            // A 14 mm square, 6 mm deep, whatever the panel's own scale (measured after the turn).
            lens.localScale = Vector3.one;
            Vector3 unit = lens.lossyScale;
            lens.localScale = new Vector3(0.014f / Mathf.Max(0.0001f, unit.x), 0.014f / Mathf.Max(0.0001f, unit.y),
                0.006f / Mathf.Max(0.0001f, unit.z));
            Renderer lensRenderer = lens.GetComponent<Renderer>();
            lensRenderer.sharedMaterial = AtmosphereAssets.Unlit("Facility_KeypadLed", new Color(2.2f, 0.25f, 0.18f), null);
            lensRenderer.shadowCastingMode = ShadowCastingMode.Off;

            Transform led = body.Find("Led");
            if (led != null)
            {
                // Just in front of the lens, so it lights the panel face and nothing floats in the air.
                led.position = onFace + face * 0.015f;
                Light light = led.GetComponent<Light>();
                if (light != null)
                {
                    light.range = 0.3f;
                    light.intensity = 0.05f;
                }
            }

            EditorUtility.SetDirty(body.gameObject);
            return 1;
        }
    }
}
