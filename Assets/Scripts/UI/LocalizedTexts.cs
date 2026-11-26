using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.World;

namespace AshenWoods.UI
{
    /// <summary>
    /// Keeps every uGUI Text and world TextMesh in the current language without touching the scenes: the scenes hold the
    /// Russian originals; whenever a text shows a dictionary line of the other language, it is swapped (Russian -> English
    /// in English, and back when Russian is chosen again). Text set from code each frame is already in the right language
    /// (Loc.Pick / Loc.T) and is simply not found in the dictionary.
    ///
    /// Cheap: each frame only a reference compare per text; a text is looked up only when its string changed. The list of
    /// texts is refreshed on scene load and twice a second (runtime-built HUD pieces). Runs last in LateUpdate, before
    /// the canvases rebuild, so a swapped line never shows for a frame in the wrong language.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class LocalizedTexts : MonoBehaviour
    {
        private static LocalizedTexts instance;

        private readonly List<Text> texts = new List<Text>();
        private readonly List<TextMesh> meshes = new List<TextMesh>();
        private readonly Dictionary<int, string> seen = new Dictionary<int, string>();
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("LocalizedTexts");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LocalizedTexts>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Loc.Changed += OnLanguageChanged;
            Scan();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Loc.Changed -= OnLanguageChanged;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        private void OnLanguageChanged()
        {
            seen.Clear();
            Scan();
            LateUpdate();
        }

        /// <summary>Swaps one string if the dictionary has it in the other language.</summary>
        public static bool Translate(string current, out string result)
        {
            result = null;
            if (string.IsNullOrEmpty(current))
            {
                return false;
            }

            return Loc.English ? Loc.TryEnglish(current, out result) : Loc.TryRussian(current, out result);
        }

        private void Scan()
        {
            texts.Clear();
            texts.AddRange(FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            meshes.Clear();
            meshes.AddRange(FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            nextScan = Time.unscaledTime + 0.5f;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                Scan();
            }

            for (int i = texts.Count - 1; i >= 0; i--)
            {
                Text text = texts[i];
                if (text == null)
                {
                    texts.RemoveAt(i);
                    continue;
                }

                string current = text.text;
                int id = text.GetInstanceID();
                if (seen.TryGetValue(id, out string last) && ReferenceEquals(last, current))
                {
                    continue;
                }

                if (Translate(current, out string swapped))
                {
                    text.text = swapped;
                    current = text.text;
                }

                seen[id] = current;
            }

            for (int i = meshes.Count - 1; i >= 0; i--)
            {
                TextMesh mesh = meshes[i];
                if (mesh == null)
                {
                    meshes.RemoveAt(i);
                    continue;
                }

                string current = mesh.text;
                int id = mesh.GetInstanceID();
                if (seen.TryGetValue(id, out string last) && ReferenceEquals(last, current))
                {
                    continue;
                }

                if (Translate(current, out string swapped))
                {
                    mesh.text = swapped;
                    current = mesh.text;
                }

                seen[id] = current;
            }
        }
    }
}
