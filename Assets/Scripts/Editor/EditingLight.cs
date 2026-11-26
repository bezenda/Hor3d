using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Bright light for hand-placing props in the dark levels (owner, 2026-11-26).
    /// Menu: Ashen Woods > Level > Bright Editing Light (toggle), Ctrl+Alt+L.
    ///
    /// Nothing in the scene changes, so nothing of it can be saved or reach a build:
    ///  - every Scene view: scene lighting off (flat, fully lit), fog and post effects off;
    ///  - four shadowless directional lights from all sides, hidden and never saved (HideAndDontSave), so the Game view
    ///    in edit mode is readable too. They are removed before Play starts and come back after it if the toggle is on.
    /// The toggle is an EditorPref and survives restarts. Switching it off puts the Scene views back to normal.
    /// </summary>
    [InitializeOnLoad]
    public static class EditingLight
    {
        private const string MenuPath = "Ashen Woods/Level/Bright Editing Light (toggle) %&l";
        private const string Pref = "AshenWoods.EditingLight";
        private const string RigName = "__EditingLight";

        static EditingLight()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    RemoveRig();
                }
                else if (state == PlayModeStateChange.EnteredEditMode && On)
                {
                    EditorApplication.delayCall += Apply;
                }
            };

            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                if (On && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Apply();
                }
            };

            EditorApplication.delayCall += () =>
            {
                if (On && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Apply();
                }
            };
        }

        private static bool On
        {
            get => EditorPrefs.GetBool(Pref, false);
            set => EditorPrefs.SetBool(Pref, value);
        }

        [MenuItem(MenuPath, false, 40)]
        private static void Toggle()
        {
            On = !On;
            if (On)
            {
                Apply();
            }
            else
            {
                RemoveRig();
                SetSceneViews(false);
            }

            Debug.Log(On
                ? "[EditingLight] On: Scene view unlit and bright, fog and post off. Not saved with the scene."
                : "[EditingLight] Off.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, On);
            return !EditorApplication.isPlaying;
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SetSceneViews(true);
            RemoveRig();

            var rig = new GameObject(RigName) { hideFlags = HideFlags.HideAndDontSave };
            AddLight(rig.transform, new Vector3(55f, 30f, 0f), 1.4f);
            AddLight(rig.transform, new Vector3(40f, 210f, 0f), 1.0f);
            AddLight(rig.transform, new Vector3(15f, 120f, 0f), 0.7f);
            AddLight(rig.transform, new Vector3(-35f, 300f, 0f), 0.5f); // from below: undersides of shelves and pipes
            SceneView.RepaintAll();
        }

        private static void AddLight(Transform parent, Vector3 euler, float intensity)
        {
            var go = new GameObject("Light") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(euler);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = new Color(1f, 0.98f, 0.95f);
            light.shadows = LightShadows.None;
        }

        private static void RemoveRig()
        {
            // Hidden objects are not found by name; look through every loaded GameObject.
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == RigName && go.transform.parent == null
                    && (go.hideFlags & HideFlags.DontSave) != 0)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void SetSceneViews(bool bright)
        {
            foreach (SceneView view in SceneView.sceneViews)
            {
                view.sceneLighting = !bright;
                view.sceneViewState.showFog = !bright;
                view.sceneViewState.showImageEffects = !bright;
                view.Repaint();
            }
        }
    }
}
