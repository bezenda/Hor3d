using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The facility's horror pass (owner's playtest notes of 2026-11-10):
    ///
    ///  - light like the house prototype: lamps in pools with dark between them, no sky sheen
    ///    on the metal, near-black ambient while the power is out, dimmer glowing props, a
    ///    facility-only grade (darker, colder, heavier vignette);
    ///  - sound: room-tone beds and far-off groans (AmbiencePlayer), hall reverb, impacts that
    ///    echo through the whole complex, steps that ring on grating, machines that hum once the
    ///    power is back;
    ///  - an enemy that sees down a whole hall (50 m, slower to make out far and dark targets),
    ///    hears far, hurries to an echo, searches past the last sighting and keeps drifting
    ///    towards the player's part of the level; a faster player with more stamina;
    ///  - the overlaps a collider check found (props in wall supports, the breaker panel behind
    ///    a wall support, lockers in wall panels, a bottle in the desk).
    ///
    /// Values are written once (marker Horror_v1), so hand tuning afterwards is kept; the
    /// components (ambience, reverb zones, hums) are added by name on every run.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string HorrorVersionName = "Horror_v1";
        private const string FacilityProfilePath = ArtFolder + "/Facility_PostProfile.asset";

        private static int DressHorror()
        {
            int added = 0;
            if (FindRoot(HorrorVersionName) == null)
            {
                TuneLights();
                TuneEnemyAndPlayer();
                FixOverlaps();

                var marker = new GameObject(HorrorVersionName);
                marker.SetActive(false);
                added++;
            }

            if (FindRoot(HorrorFixesName) == null && root.Find(FurnishVersionName) != null)
            {
                FixOverlapsV2();
                var marker = new GameObject(HorrorFixesName);
                marker.SetActive(false);
                added++;
            }

            if (FindRoot(HorrorLightsName) == null)
            {
                PoolLights();
                var marker = new GameObject(HorrorLightsName);
                marker.SetActive(false);
                added++;
            }

            if (FindRoot(HorrorExposureName) == null && FindRoot(HorrorLightsName) != null)
            {
                BalanceExposure();
                var marker = new GameObject(HorrorExposureName);
                marker.SetActive(false);
                added++;
            }

            added += EnsureGrade();
            added += EnsureSoundscape();
            return added;
        }

        private const string HorrorExposureName = "Horror_v4";
        private const string EnvironmentCubemapPath = ArtFolder + "/Env_DarkRoom.cubemap";

        /// <summary>
        /// Measured in the editor after v3: the kit's surfaces are metal, and metal shows almost no
        /// diffuse light - it reads by what it reflects. The default sky made every hall a flat blue
        /// (v1's complaint); with reflections near zero even the lamp pools stayed black. So:
        ///  - reflections of a dark, neutral room (a generated grey cubemap) instead of the sky;
        ///  - their strength follows the power: 0.3 in the dark, 0.55 once the breakers are reset;
        ///  - the house's shared post profile (ACES, contrast) crushes a scene this dim, so the
        ///    facility grade sits above it and only lifts exposure (+1.8), plus a cold shadow tint;
        ///  - the hanging lamps 1.6 times brighter.
        /// Unpowered the halls show only silhouettes and the emergency lights: a flashlight job.
        /// </summary>
        private static void BalanceExposure()
        {
            Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(EnvironmentCubemapPath);
            if (cube == null)
            {
                cube = new Cubemap(32, TextureFormat.RGBA32, true);
                foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                             CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                {
                    var pixels = new Color[32 * 32];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        float g = face == CubemapFace.PositiveY ? 0.22f
                            : face == CubemapFace.NegativeY ? 0.08f
                            : Mathf.Lerp(0.09f, 0.2f, (i / 32) / 31f); // walls: darker low, lighter high
                        pixels[i] = new Color(g, g, g * 1.03f, 1f);
                    }

                    cube.SetPixels(pixels, face);
                }

                cube.Apply(true);
                AssetDatabase.CreateAsset(cube, EnvironmentCubemapPath);
            }

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = 0.55f;

            HousePower power = Object.FindAnyObjectByType<HousePower>();
            if (power != null)
            {
                var so = new SerializedObject(power);
                so.FindProperty("unpoweredReflection").floatValue = 0.3f;
                so.FindProperty("poweredReflection").floatValue = 0.55f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform grade = root.Find("Atmosphere/Grade");
            Volume volume = grade != null ? grade.GetComponent<Volume>() : null;
            if (volume != null)
            {
                volume.priority = 20f; // above the shared post volume (10), so its exposure counts
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FacilityProfilePath);
            if (profile != null)
            {
                if (profile.TryGet(out ColorAdjustments color))
                {
                    color.postExposure.Override(0.2f); // 1.8 only ever showed through a GameSettings bug; the dark look is the one played
                    color.contrast.overrideState = false;   // the shared profile's look stays
                    color.saturation.overrideState = false;
                    color.colorFilter.overrideState = false;
                }

                if (profile.TryGet(out Vignette vignette))
                {
                    vignette.active = false; // the chase volume drives the vignette
                }

                EditorUtility.SetDirty(profile);
            }

            Transform lights = root.Find("Lights");
            if (lights != null)
            {
                foreach (Light light in lights.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Spot && !light.name.StartsWith("Electric_"))
                    {
                        light.intensity *= 1.6f;
                    }
                }
            }
        }

        private const string HorrorLightsName = "Horror_v3";

        /// <summary>
        /// Playtest of v1: with the power on the floors stayed almost black. A URP point light fades
        /// with the square of the distance, and the hall lamps hang 8 m (electrical hall 17 m) up. The
        /// hanging lamps become spots pointing down: a pool of light under each, dark between them,
        /// one shadow map instead of six. Unpowered they keep 1.4 % (HousePower), so the halls stay
        /// a flashlight job until the breakers are reset. The grade lets a little more through.
        /// </summary>
        private static void PoolLights()
        {
            Transform lights = root.Find("Lights");
            if (lights != null)
            {
                foreach (Light light in lights.GetComponentsInChildren<Light>(true))
                {
                    string n = light.name;
                    bool hall = n.StartsWith("Storage_") || n.StartsWith("Assembly_") || n.StartsWith("Lab_");
                    bool tall = n.StartsWith("Electric_");
                    if ((!hall && !tall) || IndependentLight.Covers(light) || n.EndsWith("_Fixture"))
                    {
                        continue;
                    }

                    light.type = LightType.Spot;
                    light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    light.spotAngle = tall ? 52f : 64f;
                    light.innerSpotAngle = tall ? 20f : 26f;
                    light.range = tall ? 24f : 13f;
                    light.intensity = tall ? 200f : (n.StartsWith("Lab_") ? 26f : 33f);
                }
            }

            HousePower power = Object.FindAnyObjectByType<HousePower>();
            if (power != null)
            {
                var so = new SerializedObject(power);
                so.FindProperty("unpoweredAmbient").colorValue = new Color(0.005f, 0.005f, 0.007f);
                so.FindProperty("poweredAmbient").colorValue = new Color(0.016f, 0.016f, 0.02f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FacilityProfilePath);
            if (profile != null && profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.Override(-0.15f);
                color.contrast.Override(16f);
                EditorUtility.SetDirty(profile);
            }
        }

        private const string HorrorFixesName = "Horror_v2";

        /// <summary>What the collider check still found after the furniture models went in.</summary>
        private static void FixOverlapsV2()
        {
            Transform drum = root.Find("Electrical/Machines/Drum_3");
            if (drum != null)
            {
                drum.SetPositionAndRotation(new Vector3(27.5f, 0f, -27.5f), Quaternion.Euler(0f, 200f, 0f));
            }

            foreach (string s in new[] { "N_12", "N_18", "N_24" })
                Nudge("Storage/Racks/" + s, new Vector3(0f, 0f, -0.11f));
            Nudge("Storage/Racks/E_13", new Vector3(-0.16f, 0f, 0f));

            // The office desk stood in the north wall's support; its monitor and what lies on it move along.
            Nudge("Lab/Office/Furn_Desk", new Vector3(0f, 0f, -0.2f));
            Nudge("Lab/Office/Furn_Monitor", new Vector3(0f, 0f, -0.2f));
            Physics.SyncTransforms();
            Settle("Keycard", new Vector3(89.4f, 1.2f, -13.05f));
            Settle("Throwable_Bottle_7", new Vector3(90.7f, 1.2f, -12.78f));

            MoveIfFound("Locker_Lab_N", new Vector3(81.62f, 0f, -13.1f));
        }

        // ------------------------------------------------------------------ light

        private static void TuneLights()
        {
            Transform lights = root.Find("Lights");
            if (lights != null)
            {
                foreach (Light light in lights.GetComponentsInChildren<Light>(true))
                {
                    string n = light.name;
                    if (n.StartsWith("Emergency"))
                    {
                        light.intensity = 1.8f;
                        light.range = 7f;
                    }
                    else if (n.StartsWith("Capsule_"))
                    {
                        light.intensity = 0.7f;
                        light.range = 4f;
                    }
                    else if (n == "Containment")
                    {
                        light.intensity = 0.9f;
                    }
                    else if (n.StartsWith("Electric_"))
                    {
                        light.intensity = 12f; // 17 m up: needs more to reach the floor
                        light.range = 20f;
                    }
                    else if (n == "Breaker_Room")
                    {
                        light.intensity = 2.5f;
                        light.range = 7f;
                    }
                    else if (n.EndsWith("_Lamp"))
                    {
                        light.intensity = 2.2f; // airlocks: low ceilings
                        light.range = 6f;
                    }
                    else if (!IndependentLight.Covers(light))
                    {
                        // Hall lamps hang 7.9 m up: a range of 11 lights a 7.5 m circle of floor, so the
                        // pools no longer merge into one even wash.
                        light.intensity = n.StartsWith("Lab_") ? 6f : 7.5f;
                        light.range = 11f;
                    }
                }
            }

            HousePower power = Object.FindAnyObjectByType<HousePower>();
            if (power != null)
            {
                var so = new SerializedObject(power);
                so.FindProperty("unpoweredIntensity").floatValue = 0.014f;
                so.FindProperty("unpoweredAmbient").colorValue = new Color(0.003f, 0.003f, 0.0045f);
                so.FindProperty("poweredAmbient").colorValue = new Color(0.012f, 0.012f, 0.016f);
                so.FindProperty("flickerEveryNth").intValue = 2;
                so.FindProperty("faultyEveryNth").intValue = 3;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // The default sky reflected in every metal surface lit the halls a flat blue.
            RenderSettings.reflectionIntensity = 0.1f;
            RenderSettings.ambientLight = new Color(0.012f, 0.012f, 0.016f);
            RenderSettings.fogDensity = 0.038f;
            RenderSettings.fogColor = new Color(0.012f, 0.013f, 0.016f);

            ScaleEmission(EmitCyan, 0.32f);
            ScaleEmission(Liquid, 0.35f);
            ScaleEmission(ScreenOn, 0.6f);
            ScaleEmission(WarningRed, 0.7f);
        }

        private static void ScaleEmission(Material material, float factor)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            material.SetColor("_EmissionColor", material.GetColor("_EmissionColor") * factor);
            EditorUtility.SetDirty(material);
        }

        /// <summary>A facility-only global volume over the shared post profile: darker, colder, heavier edges.</summary>
        private static int EnsureGrade()
        {
            Transform t = Group("Atmosphere");
            if (t.Find("Grade") != null)
            {
                return 0;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FacilityProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, FacilityProfilePath);

                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(-0.35f);
                color.contrast.Override(22f);
                color.saturation.Override(-32f);
                color.colorFilter.Override(new Color(0.9f, 0.95f, 1f));

                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.42f);
                vignette.smoothness.Override(0.45f);

                ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
                smh.shadows.Override(new Vector4(0.92f, 0.97f, 1.05f, -0.08f));

                foreach (VolumeComponent c in profile.components)
                {
                    AssetDatabase.AddObjectToAsset(c, profile);
                }

                AssetDatabase.SaveAssets();
            }

            var go = new GameObject("Grade");
            go.transform.SetParent(t, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f; // over the shared post volume, under the chase volume's blend
            volume.sharedProfile = profile;
            return 1;
        }

        // ------------------------------------------------------------------ AI, player

        private static void TuneEnemyAndPlayer()
        {
            EnemyVision vision = Object.FindAnyObjectByType<EnemyVision>();
            if (vision != null)
            {
                var so = new SerializedObject(vision);
                so.FindProperty("viewDistance").floatValue = 50f;
                so.FindProperty("viewAngle").floatValue = 130f;
                so.FindProperty("closeRange").floatValue = 2.5f;
                so.FindProperty("sharpDistance").floatValue = 14f;
                so.FindProperty("farClarity").floatValue = 0.3f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EnemyBrain brain = Object.FindAnyObjectByType<EnemyBrain>();
            if (brain != null)
            {
                var so = new SerializedObject(brain);
                so.FindProperty("patrolSpeed").floatValue = 1.6f;
                so.FindProperty("waypointPauseSeconds").floatValue = 1.5f;
                so.FindProperty("prowlBias").floatValue = 0.6f;
                so.FindProperty("prowlCandidates").intValue = 4;
                so.FindProperty("loseSightSeconds").floatValue = 4f;
                so.FindProperty("investigateSpeed").floatValue = 2.6f;
                so.FindProperty("alertInvestigateSpeed").floatValue = 3.4f;
                so.FindProperty("searchSeconds").floatValue = 7f;
                so.FindProperty("searchSweeps").intValue = 3;
                so.FindProperty("searchSweepDistance").floatValue = 8f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EnemyHearing hearing = Object.FindAnyObjectByType<EnemyHearing>();
            if (hearing != null)
            {
                var so = new SerializedObject(hearing);
                so.FindProperty("hearingRange").floatValue = 32f;
                so.FindProperty("wallAttenuation").floatValue = 0.6f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EnemyNavigation navigation = Object.FindAnyObjectByType<EnemyNavigation>();
            if (navigation != null)
            {
                Stage1PlaygroundBuilder.SetFloat(navigation, "nodeSearchRadius", 8f);
            }

            NoiseSystem noise = Object.FindAnyObjectByType<NoiseSystem>();
            if (noise != null)
            {
                var so = new SerializedObject(noise);
                so.FindProperty("maxRadius").floatValue = 26f;
                so.FindProperty("impactsEcho").boolValue = true;
                so.FindProperty("storeySplitHeight").floatValue = 1000f; // tall halls, no storeys
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            NoiseSoundPlayer sounds = Object.FindAnyObjectByType<NoiseSoundPlayer>();
            if (sounds != null)
            {
                var so = new SerializedObject(sounds);
                so.FindProperty("footstepSound").enumValueIndex = (int)GameSound.StepConcrete;
                so.FindProperty("metalFootstepSound").enumValueIndex = (int)GameSound.StepMetal;
                so.FindProperty("maxDistance").floatValue = 30f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            ThreatFeedback threat = Object.FindAnyObjectByType<ThreatFeedback>();
            if (threat != null)
            {
                Stage1PlaygroundBuilder.SetFloat(threat, "farDistance", 22f);
            }

            // The halls are big: a faster sprint that lasts longer, so a spotted player can get
            // away and hide (the chase is 4 m/s). Height stays human (1.8 m, eyes at 1.62).
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                var so = new SerializedObject(motor);
                so.FindProperty("walkSpeed").floatValue = 2.6f;
                so.FindProperty("sprintSpeed").floatValue = 5.4f;
                so.FindProperty("sprintSeconds").floatValue = 7f;
                so.FindProperty("staminaRefillSeconds").floatValue = 6f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerEnemyTarget target = Object.FindAnyObjectByType<PlayerEnemyTarget>();
            if (target != null)
            {
                var so = new SerializedObject(target);
                so.FindProperty("darkVisibility").floatValue = 0.6f;
                so.FindProperty("crouchedDarkVisibility").floatValue = 0.35f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // -------------------------------------------------------------- overlaps

        /// <summary>Things the collider check found inside walls, supports or each other.</summary>
        private static void FixOverlaps()
        {
            // The breaker panel hung on the wall joint: the wall support ran down its middle.
            PuzzleController panel = BreakerPanel();
            if (panel != null && Mathf.Abs(panel.transform.position.z + 18f) < 0.05f)
            {
                panel.transform.position = new Vector3(panel.transform.position.x, panel.transform.position.y, -16.6f);
            }

            Nudge("Electrical/BreakerRoom/Stripe", new Vector3(0f, 0f, 1.4f));
            Lamp(root.Find("Lights"), "Breaker_Panel_Glow", new Vector3(35.1f, BreakerFloor + 2.4f, -16.6f),
                new Color(1f, 0.25f, 0.12f), 0.8f, 3.5f, true);

            // Cabinets against the west wall stood in its supports; the fuse box above them in a cabinet.
            foreach (string s in new[] { "Switchgear_1", "Switchgear_2", "Switchgear_3" })
                Nudge("Electrical/Machines/" + s, new Vector3(0.42f, 0f, 0f));
            Nudge("Electrical/Props/Fusebox_1", new Vector3(0f, 0f, 1.6f));
            Nudge("Electrical/Machines/Drum_3", new Vector3(-0.9f, 0f, -0.4f));
            Nudge("Electrical/Machines/Drum_1", new Vector3(2f, 0f, 0.6f)); // clear of stair 1 (v2 grid)
            Nudge("Electrical/Props/Pallet_1", new Vector3(0f, 0f, -0.25f));

            // Storage: racks and a pallet in the wall supports.
            foreach (string s in new[] { "S_1", "S_7", "S_18", "S_24", "S_30" })
                Nudge("Storage/Racks/" + s, new Vector3(0f, 0f, 0.38f));
            foreach (string s in new[] { "N_12", "N_18", "N_24" })
                Nudge("Storage/Racks/" + s, new Vector3(0f, 0f, -0.08f));
            Nudge("Storage/Racks/E_13", new Vector3(-0.32f, 0f, 0f));
            Nudge("Storage/Props/Pallet_E2", new Vector3(-0.45f, 0f, -0.7f));

            Nudge("Assembly/Props/Bin", new Vector3(0f, 0f, -0.45f));
            MoveIfFound("Throwable_Bottle_7", new Vector3(90.8f, 0.97f, -13.2f));

            // Lockers stood half inside the wall cladding.
            MoveIfFound("Locker_Electric_E", new Vector3(35.05f, 0f, -31.5f));
            MoveIfFound("Locker_Assembly_W", new Vector3(48.95f, 0f, 6f));
        }

        private static void Nudge(string path, Vector3 offset)
        {
            Transform t = root.Find(path);
            if (t != null)
            {
                t.position += offset;
            }
        }

        // -------------------------------------------------------------- sound

        private static int EnsureSoundscape()
        {
            Transform t = Group("Audio");
            int added = 0;

            if (t.Find("Ambience") == null)
            {
                var go = new GameObject("Ambience");
                go.transform.SetParent(t, false);
                go.AddComponent<AmbiencePlayer>();
                added++;
            }

            // Reverb: big bare halls ring, the airlocks are tight tubes, the janitor's closet is dead.
            added += Reverb(t, "Reverb_Storage", new Vector3(18f, 4f, 15f), 22f, AudioReverbPreset.Hangar);
            added += Reverb(t, "Reverb_Assembly", new Vector3(72f, 4f, 15f), 27f, AudioReverbPreset.Hangar);
            added += Reverb(t, "Reverb_Electrical", new Vector3(18f, 6f, -27f), 22f, AudioReverbPreset.Hangar);
            added += Reverb(t, "Reverb_Lab", new Vector3(72f, 4f, -27f), 27f, AudioReverbPreset.Auditorium);
            added += Reverb(t, "Reverb_Janitor", new Vector3(6f, 1.5f, 27f), 4f, AudioReverbPreset.Room);
            foreach (Vector3 p in new[] { new Vector3(42f, 2f, 21f), new Vector3(15f, 2f, -6f), new Vector3(42f, 2f, -27f),
                         new Vector3(57f, 2f, -6f), new Vector3(21f, BreakerFloor + 2f, -21f) })
                added += Reverb(t, $"Reverb_Airlock_{p.x:0}_{p.z:0}", p, 5.5f, AudioReverbPreset.SewerPipe);

            // Machines that hum once the power is back.
            added += Hum("Electrical/Machines/Generator", 0.55f, 0.9f, 22f);
            added += Hum("Electrical/Machines/Transformer_1", 0.35f, 1.05f, 14f);
            added += Hum("Electrical/Machines/Transformer_2", 0.3f, 0.97f, 14f);
            added += Hum("Electrical/Machines/Transformer_3", 0.3f, 1.1f, 14f);
            added += Hum("Lab/Props/Rack_3", 0.25f, 1.3f, 10f);
            added += Hum("Lab/Props/Containment", 0.3f, 0.8f, 16f);
            added += Hum("Assembly/Machines/Conveyor_A_68", 0.2f, 1.2f, 12f);
            return added;
        }

        private static int Reverb(Transform parent, string name, Vector3 center, float radius, AudioReverbPreset preset)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            AudioReverbZone zone = go.AddComponent<AudioReverbZone>();
            zone.reverbPreset = preset;
            zone.minDistance = radius * 0.75f;
            zone.maxDistance = radius;
            return 1;
        }

        private static int Hum(string path, float volume, float pitch, float range)
        {
            Transform machine = root.Find(path);
            if (machine == null || machine.Find("Hum") != null)
            {
                return 0;
            }

            var go = new GameObject("Hum");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = Vector3.up * 1.2f;
            AudioSource source = go.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = range;
            source.dopplerLevel = 0f;
            source.playOnAwake = false;
            PoweredSound hum = go.AddComponent<PoweredSound>();
            var so = new SerializedObject(hum);
            so.FindProperty("volume").floatValue = volume;
            so.FindProperty("pitch").floatValue = pitch;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }
    }
}
