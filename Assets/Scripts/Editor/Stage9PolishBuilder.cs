using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.GameManagement;
using AshenWoods.Player;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 9 authoring: sound and light polish.
    ///
    ///  - Light: room lamps cast soft shadows (no more light through floors and walls), the
    ///    flashlight is a real torch (bright, long, with a dim spill around the beam), the house
    ///    is darker without power, and a global post-process volume (ACES, vignette, grain,
    ///    muted colour) plus stronger SSAO take away the flat "cardboard" look.
    ///  - Sound: footstep/heartbeat/cue levels tuned so the enemy's steps are the clearest
    ///    thing you hear; every gameplay clip imports as mono so 3D panning is clean.
    ///
    /// Tuned values are written once, while the scene has no "Polish_v1" marker, so later hand
    /// edits on lights and components survive. Delete the marker to re-apply them. Project
    /// settings (shadow atlas, SSAO, import settings) and the volume are ensured every run.
    ///
    /// Menu: Ashen Woods > Stages > Stage 9 - Sound And Light Polish
    /// </summary>
    public static class Stage9PolishBuilder
    {
        private const string MarkerName = "Polish_v1";
        private const string VolumeName = "PostProcess_Volume";
        private const string SpillName = "FlashlightSpill";
        private const string ProfilePath = "Assets/Settings/AshWoods_PostProfile.asset";
        private const string SoundsFolder = "Assets/Audio";

        [MenuItem("Ashen Woods/Stages/Stage 9 - Sound And Light Polish", false, 27)]
        private static void AddStage9()
        {
            if (!HouseBuilder.IsLevelScene())
            {
                EditorUtility.DisplayDialog(
                    "Open a level scene first",
                    "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                    Stage1PlaygroundBuilder.ScenePath + "\n\nNothing was changed.",
                    "OK");
                return;
            }

            int added = HouseBuilder.EnsureLevelPieces()
                        + Stage2InteractionBuilder.EnsureStage2Pieces()
                        + Stage3EnemyBuilder.EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces()
                        + Stage7FlowBuilder.EnsureStage7Pieces()
                        + Stage8ThrowableBuilder.EnsureStage8Pieces()
                        + EnsureStage9Pieces()
                        + Stage10InterfaceBuilder.EnsureStage10Pieces();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Stage 9: {added} change(s). Save the scene with Ctrl+S, then press Play.");
        }

        /// <summary>Ensures the polish. Safe to call repeatedly, from every Ashen Woods menu item.</summary>
        public static int EnsureStage9Pieces()
        {
            int changed = 0;
            changed += EnsureProjectSettings();
            changed += EnsureMonoSounds();
            changed += EnsureVolume();
            changed += EnsureFlashlightSpill();

            if (!HasMarker())
            {
                ApplyTunedLighting();
                ApplyTunedSound();

                var marker = new GameObject(MarkerName) { tag = "EditorOnly" };
                marker.SetActive(false);
                changed++;
                Debug.Log("Stage 9: applied tuned light and sound values (marker 'Polish_v1' added).");
            }

            changed += EnsureShadowTiers();
            changed += EnsureLampCuller();
            return changed;
        }

        /// <summary>
        /// Twenty shadowed point lights halved the frame rate (37 ms against 16 ms measured):
        /// the culler gives shadows only to the nearest lamps and switches off lamps hidden on
        /// the other storey. Lives next to HousePower.
        /// </summary>
        private static int EnsureLampCuller()
        {
            if (Object.FindAnyObjectByType<LampCuller>() != null)
            {
                return 0;
            }

            HousePower power = Object.FindAnyObjectByType<HousePower>();
            GameObject host = power != null ? power.gameObject : new GameObject("HousePower");
            host.AddComponent<LampCuller>();
            return 1;
        }

        /// <summary>The marker is saved inactive, which GameObject.Find cannot see.</summary>
        private static bool HasMarker()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == MarkerName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// URP picks a shadow map size per light from the pipeline's tiers (Light.shadowResolution
        /// is Built-in only): room lamps use the low tier so twenty of them fit the atlas, the
        /// flashlight the high one.
        /// </summary>
        private static int EnsureShadowTiers()
        {
            int changed = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional || light.shadows == LightShadows.None)
                {
                    continue;
                }

                bool torch = light.GetComponentInParent<Camera>() != null;
                int tier = torch ? UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh
                                 : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;

                // The property is read-only; the serialized field is what the Inspector edits.
                UniversalAdditionalLightData data = light.GetUniversalAdditionalLightData();
                if (data == null || data.additionalLightsShadowResolutionTier == tier)
                {
                    continue;
                }

                var so = new SerializedObject(data);
                SerializedProperty property = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
                if (property != null)
                {
                    property.intValue = tier;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }
            }

            return changed;
        }

        // ---------------------------------------------------------------- project

        /// <summary>Shadow atlas big enough for shadowed room lamps, and SSAO that grounds objects.</summary>
        private static int EnsureProjectSettings()
        {
            int changed = 0;
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                return 0;
            }

            var asset = new SerializedObject(pipeline);
            changed += SetInt(asset, "m_AdditionalLightShadowsSupported", 1, true);
            changed += SetInt(asset, "m_AdditionalLightsShadowmapResolution", 4096);
            changed += SetInt(asset, "m_AdditionalLightsShadowResolutionTierLow", 256);
            changed += SetInt(asset, "m_AdditionalLightsShadowResolutionTierMedium", 512);
            changed += SetInt(asset, "m_AdditionalLightsShadowResolutionTierHigh", 1024);
            asset.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty list = asset.FindProperty("m_RendererDataList");
            var renderer = list != null && list.arraySize > 0
                ? list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData
                : null;

            if (renderer != null)
            {
                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                {
                    if (feature == null || !feature.GetType().Name.Contains("AmbientOcclusion"))
                    {
                        continue;
                    }

                    var ssao = new SerializedObject(feature);
                    changed += SetFloat(ssao, "m_Settings.Intensity", 0.9f);
                    changed += SetFloat(ssao, "m_Settings.Radius", 0.45f);
                    changed += SetFloat(ssao, "m_Settings.DirectLightingStrength", 0.45f);
                    ssao.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(pipeline);
                if (renderer != null)
                {
                    EditorUtility.SetDirty(renderer);
                }

                AssetDatabase.SaveAssets();
            }

            return changed;
        }

        /// <summary>
        /// Stereo clips are spatialised poorly; mono clips pan cleanly, which is what tells the
        /// player which side a step came from.
        /// </summary>
        private static int EnsureMonoSounds()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                {
                    continue;
                }

                // Beds and music layers play in 2D: they keep their stereo and stream from disk.
                bool layer = path.Contains("/Ambience/") || path.Contains("/ChaseLoop/") || path.Contains("/TensionLoop/");
                bool dirty = false;
                if (importer.forceToMono == layer)
                {
                    importer.forceToMono = !layer;
                    dirty = true;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                AudioClipLoadType load = layer ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType != load)
                {
                    settings.loadType = load;
                    importer.defaultSampleSettings = settings;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            return changed;
        }

        // ----------------------------------------------------------------- volume

        private static int EnsureVolume()
        {
            int changed = 0;
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);

                Tonemapping tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.ACES);

                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0.2f);
                color.contrast.Override(14f);
                color.saturation.Override(-22f);
                color.colorFilter.Override(new Color(0.96f, 0.95f, 1f));

                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.32f);
                vignette.smoothness.Override(0.45f);

                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.1f);
                bloom.intensity.Override(0.35f);
                bloom.scatter.Override(0.6f);

                FilmGrain grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(0.18f);
                grain.response.Override(0.85f);

                foreach (VolumeComponent component in profile.components)
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                changed++;
            }

            GameObject volumeObject = GameObject.Find(VolumeName);
            if (volumeObject == null)
            {
                volumeObject = new GameObject(VolumeName);
                changed++;
            }

            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                changed++;
            }

            if (volume.sharedProfile == null)
            {
                volume.sharedProfile = profile;
                changed++;
            }

            // Post-processing only shows on cameras that render it.
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data == null)
                {
                    data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                if (!data.renderPostProcessing)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    EditorUtility.SetDirty(data);
                    changed++;
                }
            }

            return changed;
        }

        // ----------------------------------------------------------------- lights

        private static int EnsureFlashlightSpill()
        {
            PlayerFlashlight flashlight = Object.FindAnyObjectByType<PlayerFlashlight>();
            if (flashlight == null)
            {
                return 0;
            }

            var so = new SerializedObject(flashlight);
            Light beam = so.FindProperty("beam").objectReferenceValue as Light;
            if (beam == null)
            {
                return 0;
            }

            int changed = 0;
            Transform spillTransform = beam.transform.parent.Find(SpillName);
            if (spillTransform == null)
            {
                var spillObject = new GameObject(SpillName);
                spillObject.transform.SetParent(beam.transform.parent, false);
                spillObject.transform.localPosition = beam.transform.localPosition;
                spillObject.transform.localRotation = beam.transform.localRotation;

                Light spill = spillObject.AddComponent<Light>();
                spill.type = LightType.Spot;
                spill.color = new Color(1f, 0.95f, 0.86f);
                spill.intensity = 5f;
                spill.range = 9f;
                spill.spotAngle = 105f;
                spill.innerSpotAngle = 50f;
                spill.shadows = LightShadows.None;
                spill.enabled = false;

                spillTransform = spillObject.transform;
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(flashlight, "spill", spillTransform.GetComponent<Light>());
            return changed;
        }

        private static void ApplyTunedLighting()
        {
            // A real torch: physically, intensity falls with the square of the distance, so the
            // old 4.5 (and a first try at 16) barely lit a wall six metres out.
            PlayerFlashlight flashlight = Object.FindAnyObjectByType<PlayerFlashlight>();
            Light beam = flashlight != null
                ? new SerializedObject(flashlight).FindProperty("beam").objectReferenceValue as Light
                : null;

            if (beam != null)
            {
                beam.color = new Color(1f, 0.95f, 0.86f);
                beam.intensity = 48f;
                beam.range = 30f;
                beam.spotAngle = 58f;
                beam.innerSpotAngle = 20f;
                beam.shadows = LightShadows.Soft;
                beam.shadowStrength = 1f;
                beam.shadowNearPlane = 0.1f;
                EditorUtility.SetDirty(beam);
            }

            // Room lamps: soft shadows so floors and walls stop light, a little dimmer overall.
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional
                    || light.GetComponentInParent<PlayerMotor>() != null
                    || light.GetComponentInParent<EnemyBrain>() != null
                    || light.GetComponentInParent<Camera>() != null
                    || AshenWoods.World.IndependentLight.Covers(light))
                {
                    continue;
                }

                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.95f;
                light.shadowNearPlane = 0.2f;
                light.intensity *= 0.85f;
                EditorUtility.SetDirty(light);
            }

            HousePower power = Object.FindAnyObjectByType<HousePower>();
            if (power != null)
            {
                var so = new SerializedObject(power);
                SetFloat(so, "unpoweredIntensity", 0.035f);
                SetColor(so, "unpoweredAmbient", new Color(0.012f, 0.012f, 0.017f));
                SetColor(so, "poweredAmbient", new Color(0.045f, 0.045f, 0.055f));
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            RenderSettings.ambientLight = new Color(0.045f, 0.045f, 0.055f);
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.fogDensity = 0.045f;
        }

        // ------------------------------------------------------------------ sound

        private static void ApplyTunedSound()
        {
            foreach (NoiseSoundPlayer player in Object.FindObjectsByType<NoiseSoundPlayer>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(player);
                SetFloat(so, "footstepVolume", 0.08f);
                SetFloat(so, "maxDistance", 24f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            ApplyEnemyStepsTuning();

            foreach (ThreatFeedback threat in Object.FindObjectsByType<ThreatFeedback>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(threat);
                SetFloat(so, "heartbeatVolume", 0.2f);
                SetFloat(so, "heartbeatThreshold", 0.45f);
                SetFloat(so, "noticeVolume", 0.18f);
                SetFloat(so, "stingVolume", 0.4f);
                SetFloat(so, "cueCooldownSeconds", 8f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>The enemy lives in a prefab; scene instances follow unless overridden.</summary>
        private static void ApplyEnemyStepsTuning()
        {
            foreach (EnemyFootstepAudio steps in Object.FindObjectsByType<EnemyFootstepAudio>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(steps);
                SetFloat(so, "walkVolume", 0.55f);
                SetFloat(so, "chaseVolume", 0.8f);
                SetFloat(so, "maxDistance", 24f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (EnemyHearing hearing in Object.FindObjectsByType<EnemyHearing>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(hearing);
                SetFloat(so, "floorAttenuation", 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------------------------------------------------------------- helpers

        private static int SetInt(SerializedObject so, string path, int value, bool isBool = false)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"Stage 9: '{path}' not found on {so.targetObject.name}; skipped.");
                return 0;
            }

            if (isBool)
            {
                if (property.boolValue == (value != 0))
                {
                    return 0;
                }

                property.boolValue = value != 0;
                return 1;
            }

            if (property.intValue == value)
            {
                return 0;
            }

            property.intValue = value;
            return 1;
        }

        private static int SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"Stage 9: '{path}' not found on {so.targetObject.name}; skipped.");
                return 0;
            }

            if (Mathf.Approximately(property.floatValue, value))
            {
                return 0;
            }

            property.floatValue = value;
            return 1;
        }

        private static void SetColor(SerializedObject so, string path, Color value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null)
            {
                property.colorValue = value;
            }
        }
    }
}
