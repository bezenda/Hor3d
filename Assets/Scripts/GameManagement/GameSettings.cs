using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AshenWoods.Player;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// Player options: graphics quality, frame limit, V-Sync, field of view, brightness, mouse
    /// sensitivity and volume. Stored in PlayerPrefs and applied with <see cref="ApplyAll"/>.
    ///
    /// Quality presets (what costs frames in this house, measured):
    ///   Low    - 75 % render scale, no lamp or torch shadows, no SSAO, no post-processing;
    ///   Medium - 90 % render scale, torch shadow + 1 shadowed lamp, post-processing, no SSAO;
    ///   High   - full resolution, torch + 2 shadowed lamps, SSAO, post-processing.
    ///
    /// In the editor the pipeline asset's render scale and SSAO switch are put back when play
    /// mode ends, so trying a preset never edits the project.
    /// </summary>
    public static class GameSettings
    {
        public static string[] QualityNames => World.Loc.English ? QualityNamesEn : QualityNamesRu;
        private static readonly string[] QualityNamesRu = { "Низкое", "Среднее", "Высокое" };
        private static readonly string[] QualityNamesEn = { "Low", "Medium", "High" };
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, -1 };

        private const string Prefix = "AshenWoods.Settings.";

        public static int Quality
        {
            get => PlayerPrefs.GetInt(Prefix + "Quality", 2);
            set => PlayerPrefs.SetInt(Prefix + "Quality", Mathf.Clamp(value, 0, QualityNames.Length - 1));
        }

        /// <summary>Index into <see cref="FrameLimits"/>.</summary>
        public static int FrameLimit
        {
            get => PlayerPrefs.GetInt(Prefix + "FrameLimit", 1);
            set => PlayerPrefs.SetInt(Prefix + "FrameLimit", Mathf.Clamp(value, 0, FrameLimits.Length - 1));
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(Prefix + "VSync", 0) == 1;
            set => PlayerPrefs.SetInt(Prefix + "VSync", value ? 1 : 0);
        }

        public static int FieldOfView
        {
            get => PlayerPrefs.GetInt(Prefix + "Fov", 70);
            set => PlayerPrefs.SetInt(Prefix + "Fov", Mathf.Clamp(value, 60, 95));
        }

        /// <summary>-3..+3 steps of exposure.</summary>
        public static int Brightness
        {
            get => PlayerPrefs.GetInt(Prefix + "Brightness", 0);
            set => PlayerPrefs.SetInt(Prefix + "Brightness", Mathf.Clamp(value, -3, 3));
        }

        /// <summary>1..20; 6 is the default look speed.</summary>
        public static int Sensitivity
        {
            get => PlayerPrefs.GetInt(Prefix + "Sensitivity", 6);
            set => PlayerPrefs.SetInt(Prefix + "Sensitivity", Mathf.Clamp(value, 1, 20));
        }

        /// <summary>0..10 (tenths of full volume).</summary>
        public static int Volume
        {
            get => PlayerPrefs.GetInt(Prefix + "Volume", 10);
            set => PlayerPrefs.SetInt(Prefix + "Volume", Mathf.Clamp(value, 0, 10));
        }

        /// <summary>0 Easy, 1 Normal, 2 Hard (World.Difficulty holds the multipliers).</summary>
        public static int Difficulty
        {
            get => PlayerPrefs.GetInt(Prefix + "Difficulty", 1);
            set
            {
                PlayerPrefs.SetInt(Prefix + "Difficulty", Mathf.Clamp(value, 0, AshenWoods.World.Difficulty.Names.Length - 1));
                AshenWoods.World.Difficulty.Level = Difficulty;
            }
        }

        /// <summary>Called once at start-up: the stored difficulty becomes the live one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadDifficulty()
        {
            AshenWoods.World.Difficulty.Level = Difficulty;
        }

        public static string FrameLimitName(int index)
        {
            int limit = FrameLimits[Mathf.Clamp(index, 0, FrameLimits.Length - 1)];
            return limit < 0 ? World.Loc.Pick("Без ограничения", "Unlimited") : limit + World.Loc.Pick(" к/с", " fps");
        }

        // ------------------------------------------------------------------ apply

        private static bool assetStateSaved;
        private static float savedRenderScale = 1f;
        private static bool savedSsao = true;

        /// <summary>Applies every option to the running game.</summary>
        public static void ApplyAll()
        {
            ApplyQuality();
            ApplyFrameRate();
            ApplyView();
            ApplyInput();
            AudioListener.volume = Volume / 10f;
            PlayerPrefs.Save();
        }

        public static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FrameLimits[FrameLimit];
        }

        public static void ApplyInput()
        {
            PlayerLook look = Object.FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.Sensitivity = Sensitivity * 0.02f;
            }
        }

        public static void ApplyView()
        {
            // Field of view belongs to the player's eye; the title screen's framed shot keeps its own lens.
            Camera camera = Camera.main;
            if (camera != null && Object.FindAnyObjectByType<PlayerLook>() != null)
            {
                camera.fieldOfView = FieldOfView;
            }

            foreach (Volume volume in Object.FindObjectsByType<Volume>())
            {
                if (!volume.isGlobal || volume.sharedProfile == null)
                {
                    continue;
                }

                // Each volume keeps its own authored exposure: the base is read from the asset
                // (sharedProfile), never from the per-run copy. One shared base used to be taken
                // from whichever volume FindObjectsByType listed first - in the facility that
                // order changes between launches, so the Grade volume's lift was sometimes
                // stamped onto the house profile and the whole level came out bright.
                if (!volume.sharedProfile.TryGet(out ColorAdjustments authored))
                {
                    continue;
                }

                // volume.profile is a per-run copy, so the asset is never edited.
                if (volume.profile.TryGet(out ColorAdjustments color))
                {
                    if (authored.postExposure.overrideState)
                    {
                        color.postExposure.Override(authored.postExposure.value + Brightness * 0.25f);
                    }
                }
            }
        }

        public static void ApplyQuality()
        {
            int quality = Quality;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            SaveAssetState(pipeline);

            if (pipeline != null)
            {
                pipeline.renderScale = quality == 0 ? 0.75f : quality == 1 ? 0.9f : 1f;
                SetSsao(pipeline, quality == 2);
            }

            if (LampCuller.Instance != null)
            {
                LampCuller.Instance.ShadowBudget = quality == 0 ? 0 : quality == 1 ? 1 : 2;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.renderPostProcessing = quality > 0;
            }

            // The torch is the narrow spot on the camera; its wide spill never casts shadows.
            foreach (Light light in camera.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Spot && light.spotAngle < 80f)
                {
                    light.shadows = quality == 0 ? LightShadows.None : LightShadows.Soft;
                }
            }
        }

        private static void SetSsao(UniversalRenderPipelineAsset pipeline, bool on)
        {
            foreach (ScriptableRendererData renderer in RendererData(pipeline))
            {
                if (renderer == null)
                {
                    continue;
                }

                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion"))
                    {
                        feature.SetActive(on);
                    }
                }
            }
        }

        private static ScriptableRendererData[] RendererData(UniversalRenderPipelineAsset pipeline)
        {
            FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(pipeline) as ScriptableRendererData[] ?? new ScriptableRendererData[0];
        }

        private static void SaveAssetState(UniversalRenderPipelineAsset pipeline)
        {
            if (assetStateSaved || pipeline == null)
            {
                return;
            }

            assetStateSaved = true;
            savedRenderScale = pipeline.renderScale;
            savedSsao = true;

            foreach (ScriptableRendererData renderer in RendererData(pipeline))
            {
                if (renderer == null)
                {
                    continue;
                }

                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion"))
                    {
                        savedSsao = feature.isActive;
                    }
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += RestoreOnExit;
#endif
        }

#if UNITY_EDITOR
        private static void RestoreOnExit(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            UnityEditor.EditorApplication.playModeStateChanged -= RestoreOnExit;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                pipeline.renderScale = savedRenderScale;
                SetSsao(pipeline, savedSsao);
            }

            assetStateSaved = false;
        }
#endif
    }
}
