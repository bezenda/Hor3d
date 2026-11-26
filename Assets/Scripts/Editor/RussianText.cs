using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Russian-only text (owner, 2026-11-25): English is gone from the game, all player-facing text is Russian and
    /// every new text must be written in Russian. Builders write Russian now, but many scene texts were written once
    /// (objective chain, notes, prompts) and are kept on later runs, so this pass swaps every string field in the open
    /// scene (and in the prefabs) that still holds a known English text for its Russian one. Additive and idempotent:
    /// a field that holds anything else (a hand edit, Russian text) is left alone. FacilityBuilder, MenuSceneBuilder
    /// and InterfaceRedesignBuilder run it at the end, so a re-run never brings English back.
    /// </summary>
    public static class RussianText
    {
        [MenuItem("Ashen Woods/Stages/Russian Text (Open Scene)")]
        private static void Menu()
        {
            int changed = ApplyToOpenScene() + ApplyToPrefabs();
            Debug.Log($"Russian text: {changed} field(s) translated.");
        }

        /// <summary>Translates every known English string field in the open scenes. Returns how many changed.</summary>
        public static int ApplyToOpenScene()
        {
            int changed = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                int before = changed;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        changed += Translate(behaviour);
                    }

                    // 3D labels (TextMesh is not a MonoBehaviour): the Russian is longer, so the letters shrink to keep
                    // the label on its plate.
                    foreach (TextMesh label in root.GetComponentsInChildren<TextMesh>(true))
                    {
                        if (label != null && WorldLabels.TryGetValue(label.text, out string russian))
                        {
                            label.characterSize *= Mathf.Min(1f, (float)label.text.Length / russian.Length);
                            label.text = russian;
                            EditorUtility.SetDirty(label);
                            changed++;
                        }
                    }
                }

                if (changed > before)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            return changed;
        }

        /// <summary>Translates the prefabs under Assets/Prefabs; a prefab with nothing to change is not re-saved.</summary>
        public static int ApplyToPrefabs()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !NeedsWork(asset))
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                int here = 0;
                foreach (MonoBehaviour behaviour in contents.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    here += Translate(behaviour);
                }

                if (here > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    changed += here;
                }

                PrefabUtility.UnloadPrefabContents(contents);
            }

            return changed;
        }

        /// <summary>The Russian text for a known English one, or the text itself.</summary>
        public static string Ru(string english)
        {
            return english != null && Table.TryGetValue(english, out string russian) ? russian : english;
        }

        private static bool NeedsWork(GameObject asset)
        {
            foreach (MonoBehaviour behaviour in asset.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && Translate(behaviour, true) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Translate(MonoBehaviour behaviour, bool dryRun = false)
        {
            if (behaviour == null)
            {
                return 0;
            }

            var so = new SerializedObject(behaviour);
            SerializedProperty it = so.GetIterator();
            int changed = 0;
            bool enter = true;
            while (it.Next(enter))
            {
                enter = it.propertyType != SerializedPropertyType.String && it.propertyType != SerializedPropertyType.ObjectReference;
                if (it.propertyType != SerializedPropertyType.String || IsIdentifier(it.name))
                {
                    continue;
                }

                string russian;
                bool found = Table.TryGetValue(it.stringValue, out russian);
                if (found && Revisions.TryGetValue(russian, out string revised))
                {
                    russian = revised;
                }
                else if (!found)
                {
                    found = Revisions.TryGetValue(it.stringValue, out russian);
                }

                if (!found && behaviour is AshenWoods.Interaction.HidingSpot && it.name == "exitPrompt"
                    && it.stringValue == "Выйти")
                {
                    found = true;
                    russian = "Вылезти";
                }

                if (found && russian != it.stringValue)
                {
                    if (!dryRun)
                    {
                        it.stringValue = russian;
                    }

                    changed++;
                }
            }

            if (changed > 0 && !dryRun)
            {
                // Never through Undo: HandEditTracker must not take this for a hand edit.
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        /// <summary>Fields that hold ids, not words ("torch" is both a key id and a display name).</summary>
        private static bool IsIdentifier(string field)
        {
            return field.EndsWith("Id") || field.EndsWith("id") || field.EndsWith("Ids") || field == "group"
                   || field == "rowName" || field == "gameScene" || field.EndsWith("Prefix") || field.EndsWith("Token")
                   || field.EndsWith("Trigger") || field.EndsWith("Axis") || field.EndsWith("Button") || field == "m_Name";
        }

        /// <summary>
        /// Second pass (2026-11-25, owner: the first Russian read like a machine translation): earlier Russian wording ->
        /// the rewritten one. Scenes saved with the old wording are brought up to date by the same pass.
        /// </summary>
        /// <summary>3D labels in the world (TextMesh).</summary>
        private static readonly Dictionary<string, string> WorldLabels = new Dictionary<string, string>
        {
            { "VALVE ORDER", "ПОРЯДОК ВЕНТИЛЕЙ" },
            { "STEAM VENT", "СБРОС ПАРА" },
            { "BAY HYDRAULICS", "ГИДРАВЛИКА ВОРОТ" },
        };

        private static readonly Dictionary<string, string> Revisions = new Dictionary<string, string>
        {
            { "Вертикальная синхронизация", "Синхронизация кадров" },
            { "ПКМ / G  БРОСОК", "ЛКМ  БРОСОК" },
            { "Панель пищит: неверный код.", "Неверный код — завыла тревога! Оно услышит." },
            { "Поймало!", "Оно тебя схватило!" },
            { "Ты трясёшь фонарь. Он снова загорается - ненадолго.", "Ты трясёшь фонарь — он снова загорается. Ненадолго." },
            { "Оглушено! Беги!", "Оно оглушено — беги!" },
            { "<color=#D9A657>WASD</color>  ходьба     <color=#D9A657>Shift</color>  бег / задержать дыхание     <color=#D9A657>C</color>  пригнуться     <color=#D9A657>E</color>  действие     <color=#D9A657>F</color>  фонарь     <color=#D9A657>Q</color>  вспышка     <color=#D9A657>G</color>  бросок     <color=#D9A657>X</color>  поставить", "<color=#D9A657>WASD</color>  идти     <color=#D9A657>Shift</color>  бежать / не дышать     <color=#D9A657>C</color>  пригнуться     <color=#D9A657>E</color>  использовать     <color=#D9A657>F</color>  фонарь     <color=#D9A657>Q</color>  вспышка     <color=#D9A657>G</color>  бросить     <color=#D9A657>X</color>  положить" },
            { "Карманы пусты. Всё, что подберёшь - ключи, карты, топливо, - окажется здесь.", "Карманы пусты. Всё, что подберёшь, — ключи, карты, топливо — окажется здесь." },
            { "<color=#D9A657>Вверх</color> / <color=#D9A657>Вниз</color>  выбрать        <color=#D9A657>Влево</color> / <color=#D9A657>Вправо</color>  изменить        <color=#D9A657>Esc</color>  назад", "<color=#D9A657>↑ ↓</color>  выбрать        <color=#D9A657>← →</color>  изменить        <color=#D9A657>Esc</color>  назад" },
            { "Поле зрения", "Угол обзора" },
            { "Деревья смыкаются за спиной. Никто не идёт следом. Пока.", "Лес смыкается за спиной. Погони нет. Пока что." },
            { "Новая батарейка. Фонарь протянет ещё немного.", "Свежая батарейка — фонарь продержится дольше." },
            { "Спрятаться в шкафчике", "Спрятаться в шкафчик" },
            { "Стол уборщика прямо здесь, в каморке, где ты очнулся.", "Стол уборщика — прямо здесь, в каморке, где всё началось." },
            { "Попади в кабинет лаборатории. Код написан на трёх записках на стенах склада, сборочного цеха и лаборатории.", "Попади в кабинет лаборатории. Код от двери — на трёх записках: на складе, в сборочном цехе и в лаборатории." },
            { "Три записки висят на стенах склада, сборочного цеха и лаборатории и слегка светятся. J показывает цифры, которые ты уже знаешь.", "Записки висят на стенах склада, сборочного цеха и лаборатории и слегка светятся. Найденные цифры — в дневнике (J)." },
            { "Поднимись по мосткам в электроцехе к электрощитовой.", "Поднимись по мосткам электроцеха к электрощитовой." },
            { "В электроцехе мостки ведут к двери электрощитовой высоко у восточной стены.", "Мостки в электроцехе ведут к двери электрощитовой — она высоко у восточной стены." },
            { "Включи автоматы.", "Включи рубильники на главном щите." },
            { "Подними все рубильники на щите.", "Все рубильники на щите должны стоять вверху." },
            { "Канистры стоят на полу склада, у стен.", "Канистры стоят на складе, на полу у стен." },
            { "Генератор стоит на нижнем этаже электроцеха, пусковой пульт - у его восточного края.", "Генератор — на нижнем этаже электроцеха, пусковой пульт у его восточного края." },
            { "Открой гидравлические вентили в комнате вентилей под электрощитовой. Порядок - на жёлтой доске.", "Открой вентили гидравлики в комнате под электрощитовой. Порядок — на жёлтой доске." },
            { "Комната вентилей - под электрощитовой в электроцехе, двери с запада и с юга. Жёлтая доска внутри показывает порядок, а J его помнит.", "Комната вентилей — под электрощитовой, двери с западной и южной стороны. Порядок написан на жёлтой доске внутри и записан в дневнике (J)." },
            { "Подними ворота погрузочной зоны в сборочном цехе - и доживи, пока они откроются.", "Подними ворота погрузочной зоны в сборочном цехе и продержись, пока они открываются." },
            { "Ворота - на восточной стене сборочного цеха. Пока они поднимаются: прячься, слепи вспышкой, бросай предметы - просто выживи.", "Ворота — на восточной стене сборочного цеха. Пока они ползут вверх, прячься, слепи его вспышкой, отвлекай бросками — главное, выживи." },
            { "Ты выбрался.", "Ты на свободе." },
            { "Нет питания управления", "Нет питания" },
            { "Мёртво. На цепи управления нет питания.", "Не работает: цепь управления обесточена." },
            { "Не тот вентиль. Давление падает с грохотом - начинай заново.", "Не тот вентиль. Давление падает с грохотом — начинай заново." },
            { "Питание управления вернулось, но основной свет не горит. Похоже, в генераторе нет топлива.", "Щит ожил, но основной свет так и не загорелся. Похоже, генератор без топлива." },
            { "Заперто на болты (нужен лом)", "Привинчено намертво (нужен лом)" },
            { "Рубильник с лязгом встаёт вверх. Что-то гудит.", "Рубильник с лязгом встаёт на место. Где-то загудело." },
            { "Пульт мёртв. Воротам нужно питание.", "Пульт мёртв — воротам нужно питание." },
            { "Ворота поднимаются...", "Ворота поднимаются…" },
            { "Бежать через погрузочную зону", "Бежать наружу" },
            { "Прочитать ноутбук", "Посмотреть ноутбук" },
            { "Питание восстановлено.\n\nНарушена изоляция B-7. Образец подвижен.\nОхотится на звук и избегает яркого света.\n\nЭвакуируйтесь через погрузочную зону.\nНас не ждите.", "Питание восстановлено.\n\nНарушена изоляция B-7. Объект на свободе.\nОхотится на звук, избегает яркого света.\n\nЭвакуируйтесь через погрузочную зону.\nНас не ждите." },
            { "Мёртво. В здании нигде нет питания.", "Не включается. Во всём здании нет электричества." },
            { "Прочитать терминал", "Посмотреть терминал" },
            { "B-7, НЕДЕЛЯ 31: субъект больше не реагирует на седативные. Яростно реагирует на шум. Сильная вспышка оглушает его на несколько секунд - единственный надёжный способ пройти мимо. Двери лаборатории держать закрытыми.", "B-7, НЕДЕЛЯ 31: объект больше не реагирует на седативные препараты. Агрессивно реагирует на шум. Сильная вспышка оглушает его на несколько секунд — единственный надёжный способ пройти мимо. Двери лаборатории держать закрытыми." },
            { "Прочитать планшет", "Прочитать записи на планшете" },
            { "Если ты это читаешь - ты ещё жив. Постарайся таким и остаться.\n\n<b>Ходьба</b>   WASD.  Shift - бег (недолго).  C - пригнуться.  Пробел - прыжок.\n<b>Действие</b>   E - двери, шкафчики, записки, предметы.\n<b>Фонарь</b>   F - вкл / выкл. Запасные батарейки лежат на столах.\n<b>Вспышка</b>   Q - вспышка света. Сбивает его с ног на несколько секунд. Тратит заряд.\n<b>Бросок</b>   зажми G, прицелься, отпусти. Бутылки и банки - грохот уводит его в сторону.\n\nОно охотится на звук. Иди шагом, пригибайся, беги, только когда иначе никак. Если оно тебя увидело - уйди из поля зрения и спрячься в шкафчик. Долго рядом с ним не задерживайся - оно залезает в голову.\n\n<b>Как выбраться</b>: ключ-карта доктора Хейл - в кабинете лаборатории. Хейл заперла его на кодовый замок, а ночная смена знала только по одной цифре - их записки развешаны по цехам. Ключ-карта открывает электрощитовую (электроцех, вверх по мосткам). Автоматы дают только питание управления: генератор без топлива, топливо хранится на складе. Когда будет основное питание, открой гидравлику ворот (порядок на схеме) и подними ворота погрузочной зоны в сборочном цехе. Это долго и громко. Продержись.", "Если ты это читаешь — значит, ещё не поздно. Постарайся, чтобы так и осталось.\n\n<b>Движение</b>   WASD.  Shift — бег (недолго).  C — пригнуться.  Пробел — прыжок.\n<b>Действие</b>   E — двери, шкафчики, записки, предметы.\n<b>Фонарь</b>   F — включить / выключить. Запасные батарейки лежат на столах.\n<b>Вспышка</b>   Q — яркая вспышка. Валит его с ног на несколько секунд, но тратит заряд.\n<b>Бросок</b>   зажми G, прицелься, отпусти. Бутылки и банки — грохот уводит его в сторону.\n\nОно охотится на звук. Иди шагом, пригибайся, беги только в крайнем случае. Если оно тебя заметило — уйди из поля зрения и спрячься в шкафчик. Не задерживайся рядом с ним надолго — оно забирается в голову.\n\n<b>Как выбраться</b>: ключ-карта доктора Хейл — в кабинете лаборатории. Хейл заперла его на кодовый замок, а ночная смена знала лишь по одной цифре — их записки развешаны по цехам. Ключ-карта открывает электрощитовую (электроцех, вверх по мосткам). Рубильники дают только питание управления: генератор пуст, топливо хранится на складе. Когда появится основное питание, открой гидравлику ворот (порядок — на схеме) и подними ворота погрузочной зоны в сборочном цехе. Это долго и громко. Продержись." },
            { "НОЧНАЯ СМЕНА - ПРОЧТИ", "НОЧНОЙ СМЕНЕ — ПРОЧЕСТЬ" },
            { "F фонарь, Q вспышка, зажми G - бросок, E действие, C пригнуться. Оно охотится на звук.", "F — фонарь, Q — вспышка, G (зажать) — бросок, E — действие, C — пригнуться. Оно охотится на звук." },
            { "СБРОС ГЛАВНОГО ЩИТА: все три рубильника ВВЕРХ. Если свет в цехе не загорелся, переключи их ещё раз.", "СБРОС ГЛАВНОГО ЩИТА: все три рубильника — ВВЕРХ. Если свет в цехе не загорелся, переключите их ещё раз." },
            { "Тяжёлый стальной фонарь. Ещё работает - F включает его.", "Тяжёлый стальной фонарь. Работает — включается клавишей F." },
            { "Кодовая панель (открыто)", "Кодовый замок (открыт)" },
            { "Панель гудит. Неверный код.", "Неверный код — завыла тревога! Оно услышит." },
            { "Полная канистра. Тяжёлая - с ней не побегаешь.", "Полная канистра. Тяжёлая — с ней не побегаешь." },
            { "Стартер мёртв - нет питания управления. Видимо, автоматы выключены.", "Стартер не отвечает — нет питания управления. Видимо, рубильники на щите опущены." },
            { "Топливо залито, стартер дёрнут. Генератор кашляет и ревёт - по всей станции мигая загорается свет. Это слышали все.", "Топливо залито, стартер дёрнут. Генератор кашляет и ревёт — по всей станции, мигая, загорается свет. Это слышали все." },
            { "Бак совсем сухой. Двух канистр должно хватить.", "Бак сухой. Двух канистр должно хватить." },
            { "Крутится свободно, и ничего не происходит. У насосов ещё нет питания.", "Крутится вхолостую. У насосов ещё нет питания." },
            { "Последний вентиль открыт. Трубы грохочут и вздрагивают до самой погрузочной зоны - в гидравлике есть давление.", "Последний вентиль открыт. Трубы грохочут до самой погрузочной зоны — в гидравлике появилось давление." },
            { "Хейл ОПЯТЬ сменила замок в кабинете.\n\nЯ успел увидеть только ПЕРВУЮ цифру, пока она не закрыла дверь:\n\n        <b>{code1}</b>\n\nОстальное спроси у Пита в сборочном.\n\n- М.", "Хейл ОПЯТЬ сменила код на кабинете.\n\nЯ успел разглядеть только ПЕРВУЮ цифру, пока она не захлопнула дверь:\n\n        <b>{code1}</b>\n\nОстальное спроси у Пита из сборочного.\n\n— М." },
            { "СКЛАД - НОЧНАЯ СМЕНА", "СКЛАД — НОЧНАЯ СМЕНА" },
            { "Код кабинета - первая цифра: <b>{code1}</b> (склад)", "Код кабинета, первая цифра: <b>{code1}</b> (склад)" },
            { "Пит,\n\nСРЕДНЯЯ цифра нового кода кабинета:\n\n        <b>{code2}</b>\n\nЦеликом нигде не записывай. Она нам головы оторвёт.\n\n- Р.", "Пит,\n\nСРЕДНЯЯ цифра нового кода от кабинета:\n\n        <b>{code2}</b>\n\nЦеликом нигде не записывай — она нам головы оторвёт.\n\n— Р." },
            { "Код кабинета - средняя цифра: <b>{code2}</b> (сборочный цех)", "Код кабинета, средняя цифра: <b>{code2}</b> (сборочный цех)" },
            { "Кодовый замок кабинета, ПОСЛЕДНЯЯ цифра: <b>{code3}</b>\n(первые две - спроси на складе и в сборочном)\n\nB-7 сегодня опять скребся в стекло. Третий раз за неделю.\nКогда горит свет, он затихает.", "Кабинет, кодовый замок, ПОСЛЕДНЯЯ цифра: <b>{code3}</b>\n(первые две — спроси на складе и в сборочном)\n\nB-7 сегодня опять скрёбся в стекло. Третий раз за неделю.\nКогда горит свет, он затихает." },
            { "ЛАБОРАТОРИЯ - КРЫЛО B", "ЛАБОРАТОРИЯ — КРЫЛО B" },
            { "Код кабинета - последняя цифра: <b>{code3}</b> (лаборатория)", "Код кабинета, последняя цифра: <b>{code3}</b> (лаборатория)" },
            { "Насосы работают только от ОСНОВНОГО питания (генератор).\n\nОткрывай вентили линии в ЭТОМ порядке и никак иначе:\n\n              <b>{valves}</b>\n\nНе тот порядок = обратный удар. Всё закроется, начнёшь заново - и грохот разнесётся далеко.\n\nКогда давление поднимется, ворота в сборочном цехе можно поднять. Это долго. Это громко.", "Насосы работают только от ОСНОВНОГО питания (генератора).\n\nОткрывать вентили строго в ЭТОМ порядке:\n\n              <b>{valves}</b>\n\nПерепутаешь порядок — будет гидроудар. Всё закроется, придётся начинать заново, а грохот разнесётся по всей станции.\n\nКогда давление поднимется, можно будет открыть ворота в сборочном цехе. Это долго. И громко." },
            { "ГИДРАВЛИКА ПОГРУЗОЧНОЙ ЗОНЫ - ПЕРЕЗАПУСК", "ГИДРАВЛИКА ВОРОТ — ПЕРЕЗАПУСК" },
            { "Порядок вентилей (комната вентилей, под электрощитовой): <b>{valves}</b>", "Порядок вентилей (комната под электрощитовой): <b>{valves}</b>" },
            { "Стравить пар (громко - приманит его сюда)", "Стравить пар (громко — приманит его сюда)" },
            { "Давление растёт. Отойди подальше - оно это услышит. (Это отвлечение, а не часть пути наружу.)", "Давление растёт. Отойди подальше — оно это услышит. Это лишь отвлечение, к выходу не ведёт." },
            { "Тому, кто это читает:\n\nв вентиляции над складом что-то есть. Оно скребётся всю ночь и идёт на звук моей тележки. Мне не кажется.\n\nЯ просил заварить вентиляцию. Никто не отвечает.", "Тому, кто это читает:\n\nв вентиляции над складом что-то есть. Оно скребётся всю ночь и ползёт на звук моей тележки. Мне не мерещится.\n\nЯ просил заварить вентиляцию. Никто не отвечает." },
            { "Хейл спустилась лично. Северную вентиляцию заварить сегодня ночью, без вопросов, двойная оплата.\n\nРосс спросил, что держат в лаборатории. Она велела ему идти домой. Домой он не пошёл.", "Хейл спустилась сама. Северную вентиляцию заварить сегодня же ночью, без вопросов, оплата двойная.\n\nРосс спросил, что держат в лаборатории. Она велела ему идти домой. Домой он не пошёл." },
            { "БРИГАДИР - НОЧНАЯ СМЕНА", "БРИГАДИР — НОЧНАЯ СМЕНА" },
            { "С этой минуты во всех цехах - ПРОТОКОЛ ТИШИНЫ.\n\nНе бегать. Не ронять инструменты. Никаких раций.\nЕсли погас свет - оставайтесь на месте и не шевелитесь.\n\nВ темноте оно видит плохо. Ему это и не нужно.\n\n- Э. Хейл", "С этой минуты во всех цехах — РЕЖИМ ТИШИНЫ.\n\nНе бегать. Не ронять инструмент. Никаких раций.\nЕсли погас свет — оставайтесь на месте и не двигайтесь.\n\nВ темноте оно видит плохо. Ему это и не нужно.\n\n— Э. Хейл" },
            { "ПАМЯТКА - ВСЕМУ ПЕРСОНАЛУ", "ПАМЯТКА ДЛЯ ВСЕГО ПЕРСОНАЛА" },
            { "Найден в Пепельном лесу, к югу от старого карьера.\n\nКрайне не переносит яркий свет - фотовспышка валит его на несколько секунд. Слух далеко за пределами всего, что мы измеряли.\n\nОн начал подражать голосам ночной смены.", "Найден в Пепельном лесу, к югу от старого карьера.\n\nЯркий свет переносит крайне плохо — фотовспышка валит его с ног на несколько секунд. Слух намного острее всего, что мы когда-либо измеряли.\n\nОн начал подражать голосам ночной смены." },
            { "ЖУРНАЛ ИССЛЕДОВАНИЙ - B-7, ДЕНЬ 31", "ЖУРНАЛ ИССЛЕДОВАНИЙ — B-7, ДЕНЬ 31" },
            { "Он повторил то, что Росс сказал в свою последнюю ночь, его голосом, в точности:\n\n«А, ещё одна жертва».\n\nРосса мы так и не нашли. Стекло B-7 треснуло изнутри.", "Он повторил то, что Росс сказал в свою последнюю ночь. Его голосом, слово в слово:\n\n«А, ещё одна жертва».\n\nРосса мы так и не нашли. Стекло камеры B-7 треснуло изнутри." },
            { "ЖУРНАЛ ИССЛЕДОВАНИЙ - B-7, ДЕНЬ 40", "ЖУРНАЛ ИССЛЕДОВАНИЙ — B-7, ДЕНЬ 40" },
            { "Я сама отключила питание. Я спрятала код, слила топливо, заперла ворота.\n\nЕсли оно доберётся до леса, оно доберётся и до города.\n\nКто бы это ни читал: если откроешь ворота, убедись, что наружу выйдешь только ты.", "Питание я отключила сама. Я спрятала код, слила топливо, заперла ворота.\n\nЕсли оно доберётся до леса, оно доберётся и до города.\n\nКто бы ты ни был: если откроешь ворота, убедись, что наружу выйдешь только ты." },
            { "Э. ХЕЙЛ - ЛИЧНОЕ", "Э. ХЕЙЛ — ЛИЧНОЕ" },
            { "Запаянная пробирка. Что-то внутри дёргается о стекло.", "Запаянная пробирка. Что-то внутри бьётся о стекло." },
            { "ОХРАНА - ДАТЧИКИ ДВИЖЕНИЯ", "ОХРАНА — ДАТЧИКИ ДВИЖЕНИЯ" },
            { "Мёртво. Нужно основное питание - генератор.", "Не работает. Нужно основное питание — от генератора." },
            { "Изолирующий гель, 40 бочек. Седативное (ветеринарное), 12 ящиков. Сырое мясо, 200 кг - еженедельно.\n\nПолучила: д-р Э. Хейл. Назначение: B-7.\n\nЧто съедает 200 кг в неделю?", "Изолирующий гель — 40 бочек. Седативное (ветеринарное) — 12 ящиков. Сырое мясо — 200 кг еженедельно.\n\nПолучатель: д-р Э. Хейл. Назначение: B-7.\n\nЧто съедает 200 кг в неделю?" },
            { "СКЛАД - НАКЛАДНАЯ 14", "СКЛАД — НАКЛАДНАЯ № 14" },
            { "Выйти из укрытия", "Вылезти" },
        };

        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            { "Caught!", "Оно тебя схватило!" },
            { "The flashlight is dead. Find a battery.", "Фонарь сел. Найди батарейку." },
            { "You have no torch.", "У тебя нет фонаря." },
            { "You shake the torch. It sputters back - for a moment.", "Ты трясёшь фонарь — он снова загорается. Ненадолго." },
            { "Not enough charge for a flash.", "Не хватает заряда для вспышки." },
            { "The flash is still recharging.", "Вспышка ещё заряжается." },
            { "Stunned! Run!", "Оно оглушено — беги!" },
            { "The woods are holding their breath.", "Лес затаил дыхание." },
            { "Back to the game", "Вернуться в игру" },
            { "<color=#D9A657>Esc</color>  back to the game        <color=#D9A657>Tab</color>  satchel        <color=#D9A657>J</color>  journal", "<color=#D9A657>Esc</color>  вернуться в игру        <color=#D9A657>Tab</color>  сумка        <color=#D9A657>J</color>  дневник" },
            { "<color=#D9A657>WASD</color>  move     <color=#D9A657>Shift</color>  run / hold breath     <color=#D9A657>C</color>  crouch     <color=#D9A657>E</color>  use     <color=#D9A657>F</color>  torch     <color=#D9A657>Q</color>  flash     <color=#D9A657>G</color>  throw     <color=#D9A657>X</color>  put down", "<color=#D9A657>WASD</color>  идти     <color=#D9A657>Shift</color>  бежать / не дышать     <color=#D9A657>C</color>  пригнуться     <color=#D9A657>E</color>  использовать     <color=#D9A657>F</color>  фонарь     <color=#D9A657>Q</color>  вспышка     <color=#D9A657>G</color>  бросить     <color=#D9A657>X</color>  положить" },
            { "Settings", "Настройки" },
            { "Main menu", "Главное меню" },
            { "Quit to desktop", "Выйти из игры" },
            { "Your pockets are empty. Whatever you pick up - keys, cards, fuel - ends up here.", "Карманы пусты. Всё, что подберёшь, — ключи, карты, топливо — окажется здесь." },
            { "KEYS", "КЛЮЧИ" },
            { "Close", "Закрыть" },
            { "<color=#D9A657>Tab</color>  close        <color=#D9A657>Esc</color>  pause", "<color=#D9A657>Tab</color>  закрыть        <color=#D9A657>Esc</color>  пауза" },
            { "Back", "Назад" },
            { "<color=#D9A657>Up</color> / <color=#D9A657>Down</color>  choose        <color=#D9A657>Left</color> / <color=#D9A657>Right</color>  change        <color=#D9A657>Esc</color>  back", "<color=#D9A657>↑ ↓</color>  выбрать        <color=#D9A657>← →</color>  изменить        <color=#D9A657>Esc</color>  назад" },
            { "Graphics quality", "Качество графики" },
            { "Frame limit", "Ограничение кадров" },
            { "V-Sync", "Синхронизация кадров" },
            { "Field of view", "Угол обзора" },
            { "Brightness", "Яркость" },
            { "Mouse sensitivity", "Чувствительность мыши" },
            { "Volume", "Громкость" },
            { "Difficulty", "Сложность" },
            { "No objectives configured.", "Задания не настроены." },
            { "Ashen Woods Research Station", "Исследовательская станция «Пепельный лес»" },
            { "You wake on the floor of the janitor's closet. The power is out. Somewhere in the building, something is moving.", "Ты приходишь в себя на полу каморки уборщика. Света нет. Где-то в здании что-то движется." },
            { "It hunts by sound. Walk, crouch, and throw something to send it the other way.", "Оно охотится на звук. Иди шагом, пригибайся и бросай предметы, чтобы увести его." },
            { "Try again", "Попробовать снова" },
            { "<color=#D9A657>R</color>  try again", "<color=#D9A657>R</color>  попробовать снова" },
            { "The trees close behind you. Nothing follows. For now.", "Лес смыкается за спиной. Погони нет. Пока что." },
            { "Play again", "Играть снова" },
            { "<color=#D9A657>R</color>  play again", "<color=#D9A657>R</color>  играть снова" },
            { "F  FLASHLIGHT", "F  ФОНАРЬ" },
            { "RMB / G  THROW", "ЛКМ  БРОСОК" },
            { "Open the breaker room", "Открыть электрощитовую" },
            { "Close the door", "Закрыть дверь" },
            { "Breaker room (keycard required)", "Электрощитовая (нужна ключ-карта)" },
            { "The reader blinks red. It wants a security keycard.", "Считыватель мигает красным. Нужна ключ-карта охраны." },
            { "security keycard", "ключ-карта охраны" },
            { "Take battery", "Взять батарейку" },
            { "Fresh battery. The flashlight will last a while longer.", "Свежая батарейка — фонарь продержится дольше." },
            { "The flashlight is already fully charged.", "Фонарь и так полностью заряжен." },
            { "Nothing to put a battery in.", "Некуда вставить батарейку." },
            { "bottle", "бутылка" },
            { "tin can", "жестяная банка" },
            { "Hide in the locker", "Спрятаться в шкафчик" },
            { "Step out", "Выйти" },
            { "Crawl under the desk", "Залезть под стол" },
            { "Crawl out", "Вылезти" },
            { "Find a torch. The janitor's desk should have one.", "Найди фонарь. Он должен быть на столе уборщика." },
            { "The janitor's desk is right here in the closet you woke up in.", "Стол уборщика — прямо здесь, в каморке, где всё началось." },
            { "Get into the lab office. Its code is on three notes pinned to walls in storage, assembly and the lab.", "Попади в кабинет лаборатории. Код от двери — на трёх записках: на складе, в сборочном цехе и в лаборатории." },
            { "The three notes are pinned to walls in storage, assembly and the lab, and they glow a little. J shows the digits you already have.", "Записки висят на стенах склада, сборочного цеха и лаборатории и слегка светятся. Найденные цифры — в дневнике (J)." },
            { "Take the security keycard from the lab office.", "Возьми ключ-карту охраны в кабинете лаборатории." },
            { "The keycard lies on a desk in the lab office, along its north wall.", "Ключ-карта лежит на столе в кабинете лаборатории, у северной стены." },
            { "Climb the catwalks in the electrical hall to the breaker room.", "Поднимись по мосткам электроцеха к электрощитовой." },
            { "In the electrical hall, the catwalks climb to the breaker room door high on the east side.", "Мостки в электроцехе ведут к двери электрощитовой — она высоко у восточной стены." },
            { "Reset the breakers.", "Включи рубильники на главном щите." },
            { "Throw every switch on the breaker panel up.", "Все рубильники на щите должны стоять вверху." },
            { "Bring two cans of fuel for the generator. They're in the storage hall.", "Принеси две канистры топлива для генератора. Они на складе." },
            { "The fuel cans stand on the storage hall floor, against the walls.", "Канистры стоят на складе, на полу у стен." },
            { "Start the generator on the electrical hall's ground floor.", "Запусти генератор на нижнем этаже электроцеха." },
            { "The generator is on the electrical hall's ground floor; the starter post is at its east end.", "Генератор — на нижнем этаже электроцеха, пусковой пульт у его восточного края." },
            { "Open the hydraulic valves in the valve room, under the breaker room. The order is on the yellow board.", "Открой вентили гидравлики в комнате под электрощитовой. Порядок — на жёлтой доске." },
            { "The valve room is under the breaker room in the electrical hall, with doors on its west and south sides. The yellow board inside shows the order - and J remembers it.", "Комната вентилей — под электрощитовой, двери с западной и южной стороны. Порядок написан на жёлтой доске внутри и записан в дневнике (J)." },
            { "Lift the loading bay door in assembly - and stay alive until it opens.", "Подними ворота погрузочной зоны в сборочном цехе и продержись, пока они открываются." },
            { "The bay door is on the assembly hall's east wall. While it lifts: hide, flash, throw - just stay alive.", "Ворота — на восточной стене сборочного цеха. Пока они ползут вверх, прячься, слепи его вспышкой, отвлекай бросками — главное, выживи." },
            { "You got out.", "Ты на свободе." },
            { "No control power", "Нет питания" },
            { "Dead. The control circuit has no power.", "Не работает: цепь управления обесточена." },
            { "Wrong valve. The pressure drops with a bang - start again.", "Не тот вентиль. Давление падает с грохотом — начинай заново." },
            { "Control power is back - but the main lights stay dead. The generator must be dry.", "Щит ожил, но основной свет так и не загорелся. Похоже, генератор без топлива." },
            { "Screwed shut (needs a crowbar)", "Привинчено намертво (нужен лом)" },
            { "The panel is screwed shut. It needs a crowbar.", "Панель привинчена намертво. Нужен лом." },
            { "Switch on", "Включить" },
            { "Switch off", "Выключить" },
            { "The switch clunks up. Something hums.", "Рубильник с лязгом встаёт на место. Где-то загудело." },
            { "The switch clunks back down.", "Рубильник с лязгом падает вниз." },
            { "Loading bay door", "Ворота погрузочной зоны" },
            { "The controls are dead. The bay door needs power.", "Пульт мёртв — воротам нужно питание." },
            { "Open the loading bay", "Открыть ворота погрузочной зоны" },
            { "The bay door grinds up into the dark. Cold air. You run.", "Ворота со скрежетом уходят вверх, в темноту. Холодный воздух. Ты бежишь." },
            { "The bay door is opening...", "Ворота поднимаются…" },
            { "The bay door shudders and starts to grind upwards. It is loud. Hold on.", "Ворота вздрагивают и со скрежетом ползут вверх. Это громко. Продержись." },
            { "Escape through the loading bay", "Бежать наружу" },
            { "Bay door (no hydraulic pressure)", "Ворота (нет давления в гидравлике)" },
            { "The motor whines, but the door will not lift. The hydraulics have no pressure.", "Мотор воет, но ворота не поднимаются. В гидравлике нет давления." },
            { "Janitor's Closet", "Каморка уборщика" },
            { "Storage Hall", "Склад" },
            { "Assembly Hall", "Сборочный цех" },
            { "Lab Office", "Кабинет лаборатории" },
            { "Research Lab", "Лаборатория" },
            { "Electrical Hall", "Электроцех" },
            { "Breaker Room", "Электрощитовая" },
            { "Valve Room", "Комната вентилей" },
            { "Read the laptop", "Посмотреть ноутбук" },
            { "Power restored.\n\nContainment B-7 breached. Specimen mobile.\nIt hunts by sound and avoids bright light.\n\nEvacuate through the loading bay.\nDo not wait for us.", "Питание восстановлено.\n\nНарушена изоляция B-7. Объект на свободе.\nОхотится на звук, избегает яркого света.\n\nЭвакуируйтесь через погрузочную зону.\nНас не ждите." },
            { "SITE LOG", "ЖУРНАЛ СТАНЦИИ" },
            { "Laptop (no power)", "Ноутбук (нет питания)" },
            { "Dead. Nothing in the building has power.", "Не включается. Во всём здании нет электричества." },
            { "Read the terminal", "Посмотреть терминал" },
            { "B-7 WEEK 31: subject no longer responds to sedation. Reacts violently to noise. A strong flash stuns it for several seconds - the only reliable way to get past it. Keep the lab doors sealed.", "B-7, НЕДЕЛЯ 31: объект больше не реагирует на седативные препараты. Агрессивно реагирует на шум. Сильная вспышка оглушает его на несколько секунд — единственный надёжный способ пройти мимо. Двери лаборатории держать закрытыми." },
            { "FACILITY LAYOUT", "СХЕМА СТАНЦИИ" },
            { "Read the clipboard", "Прочитать записи на планшете" },
            { "If you are reading this, you are still alive. Keep it that way.\n\n<b>Move</b>   WASD.  Shift - sprint (short).  C - crouch.  Space - jump.\n<b>Use</b>   E - doors, lockers, notes, items.\n<b>Torch</b>   F - on / off. Spare batteries lie on tables.\n<b>Flash</b>   Q - a burst of light. Knocks it down for a few seconds. Costs battery.\n<b>Throw</b>   hold G, aim, let go. Bottles and cans - the crash draws it away.\n\nIt hunts by sound. Walk, crouch, run only when you must. If it sees you, break the line of sight and get into a locker. Do not stay close to it for long - it gets into your head.\n\n<b>Way out</b>: Dr. Hale's keycard is in the lab office. She locked it with a keypad and the night crew only ever knew a digit each - their notes are around the halls. The keycard opens the breaker room (electrical hall, up the catwalks). The breakers only bring control power: the generator is dry, the fuel is kept in the storage hall. With main power, open the bay hydraulics (the order is on the schematic) and lift the loading bay door in assembly. It is slow and loud. Hold out.", "Если ты это читаешь — значит, ещё не поздно. Постарайся, чтобы так и осталось.\n\n<b>Движение</b>   WASD.  Shift — бег (недолго).  C — пригнуться.  Пробел — прыжок.\n<b>Действие</b>   E — двери, шкафчики, записки, предметы.\n<b>Фонарь</b>   F — включить / выключить. Запасные батарейки лежат на столах.\n<b>Вспышка</b>   Q — яркая вспышка. Валит его с ног на несколько секунд, но тратит заряд.\n<b>Бросок</b>   зажми G, прицелься, отпусти. Бутылки и банки — грохот уводит его в сторону.\n\nОно охотится на звук. Иди шагом, пригибайся, беги только в крайнем случае. Если оно тебя заметило — уйди из поля зрения и спрячься в шкафчик. Не задерживайся рядом с ним надолго — оно забирается в голову.\n\n<b>Как выбраться</b>: ключ-карта доктора Хейл — в кабинете лаборатории. Хейл заперла его на кодовый замок, а ночная смена знала лишь по одной цифре — их записки развешаны по цехам. Ключ-карта открывает электрощитовую (электроцех, вверх по мосткам). Рубильники дают только питание управления: генератор пуст, топливо хранится на складе. Когда появится основное питание, открой гидравлику ворот (порядок — на схеме) и подними ворота погрузочной зоны в сборочном цехе. Это долго и громко. Продержись." },
            { "NIGHT CREW - READ THIS", "НОЧНОЙ СМЕНЕ — ПРОЧЕСТЬ" },
            { "F torch, Q flash, hold G to throw, E use, C crouch. It hunts by sound.", "F — фонарь, Q — вспышка, G (зажать) — бросок, E — действие, C — пригнуться. Оно охотится на звук." },
            { "Read the instructions", "Прочитать инструкцию" },
            { "MAIN BREAKER RESET: all three switches UP. If the hall lights do not come on, cycle them again.", "СБРОС ГЛАВНОГО ЩИТА: все три рубильника — ВВЕРХ. Если свет в цехе не загорелся, переключите их ещё раз." },
            { "torch", "фонарь" },
            { "A heavy steel torch. It still works - F switches it on.", "Тяжёлый стальной фонарь. Работает — включается клавишей F." },
            { "Open the office", "Открыть кабинет" },
            { "Lab office (keypad lock)", "Кабинет лаборатории (кодовый замок)" },
            { "Locked. A keypad beside the door blinks red.", "Заперто. Кодовая панель у двери мигает красным." },
            { "Use the keypad", "Ввести код" },
            { "Keypad (unlocked)", "Кодовый замок (открыт)" },
            { "The lock clacks open.", "Замок щёлкает и открывается." },
            { "The keypad buzzes. Wrong code.", "Неверный код — завыла тревога! Оно услышит." },
            { "fuel can", "канистра с топливом" },
            { "A full fuel can. Heavy - no running while you carry it.", "Полная канистра. Тяжёлая — с ней не побегаешь." },
            { "Generator (no control power)", "Генератор (нет питания управления)" },
            { "The starter is dead - no control power. The breakers must be down.", "Стартер не отвечает — нет питания управления. Видимо, рубильники на щите опущены." },
            { "Fuel in, starter pulled. The generator coughs, then roars - lights stutter on across the facility. Everything heard that.", "Топливо залито, стартер дёрнут. Генератор кашляет и ревёт — по всей станции, мигая, загорается свет. Это слышали все." },
            { "Generator (no fuel)", "Генератор (нет топлива)" },
            { "The tank is bone dry. Two cans of fuel should get it going.", "Бак сухой. Двух канистр должно хватить." },
            { "Pour in the fuel and pull the starter", "Залить топливо и дёрнуть стартер" },
            { "Starter", "Стартер" },
            { "The generator coughs and roars.", "Генератор кашляет и ревёт." },
            { "Valve (pumps have no power)", "Вентиль (у насосов нет питания)" },
            { "It turns freely and nothing happens. The pumps have no power yet.", "Крутится вхолостую. У насосов ещё нет питания." },
            { "The last valve opens. Pipes bang and shudder all the way to the loading bay - the hydraulics have pressure.", "Последний вентиль открыт. Трубы грохочут до самой погрузочной зоны — в гидравлике появилось давление." },
            { "Open valve A", "Открыть вентиль A" },
            { "Valve A", "Вентиль A" },
            { "Valve A turns with a long hiss.", "Вентиль A поворачивается с долгим шипением." },
            { "Open valve B", "Открыть вентиль B" },
            { "Valve B", "Вентиль B" },
            { "Valve B turns with a long hiss.", "Вентиль B поворачивается с долгим шипением." },
            { "Open valve C", "Открыть вентиль C" },
            { "Valve C", "Вентиль C" },
            { "Valve C turns with a long hiss.", "Вентиль C поворачивается с долгим шипением." },
            { "Open valve D", "Открыть вентиль D" },
            { "Valve D", "Вентиль D" },
            { "Valve D turns with a long hiss.", "Вентиль D поворачивается с долгим шипением." },
            { "Read the note", "Прочитать записку" },
            { "Hale changed the office lock AGAIN.\n\nI only caught the FIRST number before she shut the door:\n\n        <b>{code1}</b>\n\nAsk Pete in assembly for the rest.\n\n- M.", "Хейл ОПЯТЬ сменила код на кабинете.\n\nЯ успел разглядеть только ПЕРВУЮ цифру, пока она не захлопнула дверь:\n\n        <b>{code1}</b>\n\nОстальное спроси у Пита из сборочного.\n\n— М." },
            { "STORES - NIGHT SHIFT", "СКЛАД — НОЧНАЯ СМЕНА" },
            { "Office code - first digit: <b>{code1}</b> (stores)", "Код кабинета, первая цифра: <b>{code1}</b> (склад)" },
            { "Pete -\n\nthe MIDDLE digit of the new office code is\n\n        <b>{code2}</b>\n\nDo not write the whole thing down anywhere. She will have our heads.\n\n- R.", "Пит,\n\nСРЕДНЯЯ цифра нового кода от кабинета:\n\n        <b>{code2}</b>\n\nЦеликом нигде не записывай — она нам головы оторвёт.\n\n— Р." },
            { "ASSEMBLY", "СБОРОЧНЫЙ ЦЕХ" },
            { "Office code - middle digit: <b>{code2}</b> (assembly)", "Код кабинета, средняя цифра: <b>{code2}</b> (сборочный цех)" },
            { "Office keypad, LAST digit: <b>{code3}</b>\n(first two - ask stores and assembly)\n\nB-7 was scratching at the glass again tonight. Third time this week.\nIt goes quiet when the lights are on.", "Кабинет, кодовый замок, ПОСЛЕДНЯЯ цифра: <b>{code3}</b>\n(первые две — спроси на складе и в сборочном)\n\nB-7 сегодня опять скрёбся в стекло. Третий раз за неделю.\nКогда горит свет, он затихает." },
            { "LAB - B WING", "ЛАБОРАТОРИЯ — КРЫЛО B" },
            { "Office code - last digit: <b>{code3}</b> (lab)", "Код кабинета, последняя цифра: <b>{code3}</b> (лаборатория)" },
            { "Read the valve order", "Прочитать порядок вентилей" },
            { "Pumps run on MAIN power only (generator).\n\nOpen the line valves in THIS order and no other:\n\n              <b>{valves}</b>\n\nWrong order = blowback. Everything shuts and you start again - and the bang carries.\n\nWith the pressure up, the bay door in assembly can be lifted. It is slow. It is loud.", "Насосы работают только от ОСНОВНОГО питания (генератора).\n\nОткрывать вентили строго в ЭТОМ порядке:\n\n              <b>{valves}</b>\n\nПерепутаешь порядок — будет гидроудар. Всё закроется, придётся начинать заново, а грохот разнесётся по всей станции.\n\nКогда давление поднимется, можно будет открыть ворота в сборочном цехе. Это долго. И громко." },
            { "LOADING BAY HYDRAULICS - RESTART", "ГИДРАВЛИКА ВОРОТ — ПЕРЕЗАПУСК" },
            { "Valve order (valve room, under the breaker room): <b>{valves}</b>", "Порядок вентилей (комната под электрощитовой): <b>{valves}</b>" },
            { "Vent steam (loud - draws it here)", "Стравить пар (громко — приманит его сюда)" },
            { "Pressure is building. Get clear - it will hear this. (A distraction, not part of the way out.)", "Давление растёт. Отойди подальше — оно это услышит. Это лишь отвлечение, к выходу не ведёт." },
            { "Steam release (venting)", "Сброс пара (стравливается)" },
            { "Steam release (no pressure yet)", "Сброс пара (давление ещё не набралось)" },
            { "Steam release (dry)", "Сброс пара (пусто)" },
            { "Open the valve room", "Открыть комнату вентилей" },
            { "Read the log", "Прочитать журнал" },
            { "To whoever reads these:\n\nsomething is in the ducts over storage. It scratches all night and it follows the sound of my cart. I am not imagining it.\n\nI asked for the vents to be sealed. Nobody answers.", "Тому, кто это читает:\n\nв вентиляции над складом что-то есть. Оно скребётся всю ночь и ползёт на звук моей тележки. Мне не мерещится.\n\nЯ просил заварить вентиляцию. Никто не отвечает." },
            { "COMPLAINT", "ЖАЛОБА" },
            { "Hale came down in person. North vents to be welded tonight, no questions, double pay.\n\nRoss asked what they were keeping in the lab. She told him to go home. He did not go home.", "Хейл спустилась сама. Северную вентиляцию заварить сегодня же ночью, без вопросов, оплата двойная.\n\nРосс спросил, что держат в лаборатории. Она велела ему идти домой. Домой он не пошёл." },
            { "FOREMAN - NIGHT SHIFT", "БРИГАДИР — НОЧНАЯ СМЕНА" },
            { "Effective immediately: SILENCE PROTOCOL in all halls.\n\nNo running. No dropped tools. No radios.\nIf the lights fail, stay where you are and keep still.\n\nIt cannot see well in the dark. It does not need to.\n\n- E. Hale", "С этой минуты во всех цехах — РЕЖИМ ТИШИНЫ.\n\nНе бегать. Не ронять инструмент. Никаких раций.\nЕсли погас свет — оставайтесь на месте и не двигайтесь.\n\nВ темноте оно видит плохо. Ему это и не нужно.\n\n— Э. Хейл" },
            { "MEMO - ALL STAFF", "ПАМЯТКА ДЛЯ ВСЕГО ПЕРСОНАЛА" },
            { "Hale told me to drain the generator and leave the cans in stores. \"If the power stays down, the bay door stays down.\"\n\nShe is not trying to keep thieves out. She is trying to keep something IN.", "Хейл велела слить топливо из генератора и оставить канистры на складе. «Пока нет питания, ворота не поднимутся».\n\nОна не воров боится. Она хочет что-то удержать ВНУТРИ." },
            { "ELECTRICIAN'S LOG", "ЖУРНАЛ ЭЛЕКТРИКА" },
            { "Recovered from the Ashen Woods, south of the old quarry.\n\nAversion to bright light is extreme - a photoflash drops it for seconds. Hearing is far beyond anything we measured.\n\nIt has started to imitate the voices of the night crew.", "Найден в Пепельном лесу, к югу от старого карьера.\n\nЯркий свет переносит крайне плохо — фотовспышка валит его с ног на несколько секунд. Слух намного острее всего, что мы когда-либо измеряли.\n\nОн начал подражать голосам ночной смены." },
            { "RESEARCH LOG - B-7, DAY 31", "ЖУРНАЛ ИССЛЕДОВАНИЙ — B-7, ДЕНЬ 31" },
            { "It repeated something Ross said on his last night, in his voice, perfectly:\n\n\"Ah, another victim.\"\n\nWe never found Ross. The glass of B-7 is cracked from the inside.", "Он повторил то, что Росс сказал в свою последнюю ночь. Его голосом, слово в слово:\n\n«А, ещё одна жертва».\n\nРосса мы так и не нашли. Стекло камеры B-7 треснуло изнутри." },
            { "RESEARCH LOG - B-7, DAY 40", "ЖУРНАЛ ИССЛЕДОВАНИЙ — B-7, ДЕНЬ 40" },
            { "I cut the power myself. I hid the code, I drained the fuel, I locked the bay.\n\nIf it reaches the woods it reaches the town.\n\nWhoever reads this: if you open that door, make sure you are the only thing that walks out.", "Питание я отключила сама. Я спрятала код, слила топливо, заперла ворота.\n\nЕсли оно доберётся до леса, оно доберётся и до города.\n\nКто бы ты ни был: если откроешь ворота, убедись, что наружу выйдешь только ты." },
            { "E. HALE - PERSONAL", "Э. ХЕЙЛ — ЛИЧНОЕ" },
            { "B-7 tissue sample", "образец ткани B-7" },
            { "A sealed vial. Something in it twitches against the glass.", "Запаянная пробирка. Что-то внутри бьётся о стекло." },
            { "Use the security terminal", "Открыть терминал охраны" },
            { "Nothing written here.", "Здесь ничего не написано." },
            { "SECURITY - MOTION SENSORS", "ОХРАНА — ДАТЧИКИ ДВИЖЕНИЯ" },
            { "Security terminal (no power)", "Терминал охраны (нет питания)" },
            { "Dead. It needs the main power - the generator.", "Не работает. Нужно основное питание — от генератора." },
            { "Containment gel, 40 drums. Sedative (vet grade), 12 crates. Raw meat, 200 kg - weekly.\n\nSigned for by Dr. E. Hale. Destination: B-7.\n\nWhat eats 200 kg a week?", "Изолирующий гель — 40 бочек. Седативное (ветеринарное) — 12 ящиков. Сырое мясо — 200 кг еженедельно.\n\nПолучатель: д-р Э. Хейл. Назначение: B-7.\n\nЧто съедает 200 кг в неделю?" },
            { "STORES - MANIFEST 14", "СКЛАД — НАКЛАДНАЯ № 14" },
            { "The power is out. Something in the dark is listening.", "Света нет. Что-то в темноте прислушивается." },
            { "Continue", "Продолжить" },
            { "New game", "Новая игра" },
            { "Quit", "Выйти" },
            { "Open door", "Открыть дверь" },
            { "Close door", "Закрыть дверь" },
            { "Locked (needs an iron key)", "Заперто (нужен железный ключ)" },
            { "Locked. It needs an iron key.", "Заперто. Нужен железный ключ." },
            { "crowbar", "лом" },
            { "iron key", "железный ключ" },
            { "Hide under the bed", "Спрятаться под кроватью" },
            { "Hide", "Спрятаться" },
            { "Leave hiding place", "Вылезти" },
        };
    }
}
