using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.Enemy;
using AshenWoods.Player;
using AshenWoods.UI;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 12 authoring: the atmosphere pass (after the second playtest).
    ///
    ///  - Player: dust in the air that the torch picks out (FlashlightDust), camera bob, sprint
    ///    lean, strafe roll and a fear shake (CameraFeel), a white-out and ringing ears after the
    ///    flash (FlashScreenEffect).
    ///  - Enemy (scene instance, never the prefab's visuals): glowing eyes and a cold light that
    ///    walks behind it and throws its shadow ahead round corners.
    ///  - Post: a cold blue-green grade and finer grain on the main profile; a chase volume
    ///    with chromatic aberration that fades in with ThreatFeedback.ChaseBlend.
    ///  - House: fog in both corridors (lit particle puffs), moonlit windows (frame, glass and
    ///    a cookie spot light that draws the frame on the floor), stains, cracks and mould on
    ///    walls and floors, cobwebs in ceiling corners. Faulty lamps live in HousePower.
    ///
    /// Additive and idempotent: objects are found by name; scattered marks are placed only when
    /// their group is empty (delete the group to re-scatter). One-time values (fog colour)
    /// are guarded by the inactive root 'Atmosphere_v1'. Called at the end of Stage 11.
    ///
    /// Menu: Ashen Woods > Stages > Stage 12 - Atmosphere
    /// </summary>
    public static class Stage12AtmosphereBuilder
    {
        private const string MarkerName = "Atmosphere_v1";
        private const string MarkerV2Name = "Atmosphere_v2";

        // Second look (owner feedback): corridor fog was hiding the corridor.
        private const float FogBankAlpha = 0.06f;
        private const float FogBankPerMetre = 4f;
        private const float HouseFogDensity = 0.03f;
        private const string RootName = "Atmosphere";
        private const string ChaseVolumeName = "Chase_Volume";
        private const string ChaseProfilePath = "Assets/Settings/AshWoods_ChaseProfile.asset";
        private const string MainProfilePath = "Assets/Settings/AshWoods_PostProfile.asset";
        private const string CobwebMeshPath = AtmosphereAssets.Folder + "/CobwebMesh.asset";

        // House grid (see HouseBuilder.Layout): 12 x 8 cells of 2 m, two storeys.
        private const float MinX = -12f;
        private const float MinZ = -8f;
        private const float Cell = 2f;
        private static readonly float[] StoreyFloor = { 0f, 3.2f };
        private const float GroundCeiling = 3f;
        private const float UpperCeiling = 6f;

        private struct WindowSpot
        {
            public string Name;
            public int Storey;
            public int Cx;
            public int Cz;
            public Vector3 Outward;
        }

        /// <summary>Moonlit windows on outside walls, away from the front door and stairs.</summary>
        private static readonly WindowSpot[] Windows =
        {
            new WindowSpot { Name = "Living", Storey = 0, Cx = 0, Cz = 5, Outward = Vector3.left },
            new WindowSpot { Name = "Library", Storey = 0, Cx = 11, Cz = 4, Outward = Vector3.right },
            new WindowSpot { Name = "Dining", Storey = 0, Cx = 7, Cz = 0, Outward = Vector3.back },
            new WindowSpot { Name = "Kitchen", Storey = 0, Cx = 1, Cz = 0, Outward = Vector3.back },
            new WindowSpot { Name = "Master", Storey = 1, Cx = 1, Cz = 0, Outward = Vector3.back },
            new WindowSpot { Name = "Guest", Storey = 1, Cx = 0, Cz = 5, Outward = Vector3.left },
            new WindowSpot { Name = "Bathroom", Storey = 1, Cx = 11, Cz = 4, Outward = Vector3.right },
            new WindowSpot { Name = "Playroom", Storey = 1, Cx = 5, Cz = 7, Outward = Vector3.forward },

            // Second choices: tried only while that room still has no window.
            new WindowSpot { Name = "Living", Storey = 0, Cx = 0, Cz = 7, Outward = Vector3.left },
            new WindowSpot { Name = "Living", Storey = 0, Cx = 2, Cz = 7, Outward = Vector3.forward },
            new WindowSpot { Name = "Kitchen", Storey = 0, Cx = 2, Cz = 0, Outward = Vector3.back },
            new WindowSpot { Name = "Kitchen", Storey = 0, Cx = 0, Cz = 1, Outward = Vector3.left },
            new WindowSpot { Name = "Master", Storey = 1, Cx = 2, Cz = 0, Outward = Vector3.back },
            new WindowSpot { Name = "Master", Storey = 1, Cx = 0, Cz = 1, Outward = Vector3.left },
            new WindowSpot { Name = "Master", Storey = 1, Cx = 0, Cz = 2, Outward = Vector3.left }
        };

        [MenuItem("Ashen Woods/Stages/Stage 12 - Atmosphere", false, 30)]
        private static void AddStage12()
        {
            if (!HouseBuilder.IsLevelScene())
            {
                EditorUtility.DisplayDialog("Open a level scene first",
                    "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                    Stage1PlaygroundBuilder.ScenePath + "\n\nNothing was changed.", "OK");
                return;
            }

            // Through Stage 11 (which calls this at its end).
            int changed = Stage10InterfaceBuilder.EnsureStage10Pieces();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Stage 12 (via the chain): {changed} change(s). Save the scene with Ctrl+S.");
        }

        public static int EnsureStage12Pieces()
        {
            int changed = 0;

            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                changed += EnsurePlayerPieces(motor.gameObject);
            }

            foreach (EnemyBrain enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
            {
                changed += EnsureEnemyPieces(enemy);
            }

            changed += EnsureFlashScreen();
            changed += EnsureGrade();
            changed += EnsureChaseVolume();

            if (HouseBuilder.IsHouseScene())
            {
                Physics.SyncTransforms();
                Transform root = EnsureRoot();
                changed += EnsureCorridorFog(root);
                changed += EnsureWindows(root);
                changed += EnsureMarks(root);
                changed += EnsureCobwebs(root);
            }

            if (!HasMarker())
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.028f, 0.036f, 0.04f);
                var marker = new GameObject(MarkerName) { tag = "EditorOnly" };
                marker.SetActive(false);
                changed++;
                Debug.Log("Stage 12: applied the atmosphere values (marker 'Atmosphere_v1' added).");
            }

            if (FindRoot(MarkerV2Name) == null)
            {
                changed += ApplySecondLook();
                var marker = new GameObject(MarkerV2Name) { tag = "EditorOnly" };
                marker.SetActive(false);
                changed++;
                Debug.Log("Stage 12: lighter fog and calmer camera applied (marker 'Atmosphere_v2' added).");
            }

            return changed;
        }

        /// <summary>
        /// Once per scene: thinner corridor fog and house fog, a calmer camera (less bob, lean and
        /// fear shake) and a gentler chase pulse. Hand-edited fog banks are left alone.
        /// </summary>
        private static int ApplySecondLook()
        {
            int changed = 0;
            if (HouseBuilder.IsHouseScene())
            {
                RenderSettings.fogDensity = HouseFogDensity;
                changed++;
            }

            GameObject house = GameObject.Find("House");
            Transform fog = house != null ? house.transform.Find(RootName + "/Fog") : null;
            if (fog != null)
            {
                foreach (ParticleSystem bank in fog.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (HandEditTracker.Protected(bank.transform))
                    {
                        continue;
                    }

                    var shape = bank.shape;
                    float length = shape.scale.x;
                    var main = bank.main;
                    Color colour = main.startColor.color;
                    colour.a = FogBankAlpha;
                    main.startColor = colour;
                    main.maxParticles = Mathf.RoundToInt(length * FogBankPerMetre);
                    var emission = bank.emission;
                    emission.rateOverTime = length * FogBankPerMetre / 20f;
                    EditorUtility.SetDirty(bank);
                    changed++;
                }
            }

            foreach (CameraFeel feel in Object.FindObjectsByType<CameraFeel>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(feel, "walkBobHeight", 0.016f);
                Stage1PlaygroundBuilder.SetFloat(feel, "sprintBobHeight", 0.028f);
                Stage1PlaygroundBuilder.SetFloat(feel, "crouchBobHeight", 0.01f);
                Stage1PlaygroundBuilder.SetFloat(feel, "sprintLean", 1.4f);
                Stage1PlaygroundBuilder.SetFloat(feel, "strafeRoll", 0.4f);
                Stage1PlaygroundBuilder.SetFloat(feel, "maxRoll", 1.8f);
                Stage1PlaygroundBuilder.SetFloat(feel, "shakeFromDanger", 0.6f);
                Stage1PlaygroundBuilder.SetFloat(feel, "shakeDegrees", 0.35f);
                Stage1PlaygroundBuilder.SetFloat(feel, "shakeFrequency", 6f);
                changed++;
            }

            foreach (ChasePostEffect chase in Object.FindObjectsByType<ChasePostEffect>(FindObjectsInactive.Include))
            {
                Stage1PlaygroundBuilder.SetFloat(chase, "throb", 0.1f);
                changed++;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ChaseProfilePath);
            if (profile != null && profile.TryGet(out LensDistortion lens))
            {
                lens.intensity.Override(-0.06f); // A pulsing lens read as more shake.
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            return changed;
        }

        // ------------------------------------------------------------------ player

        private static int EnsurePlayerPieces(GameObject player)
        {
            int changed = 0;
            Camera eye = player.GetComponentInChildren<Camera>(true);

            CameraFeel feel = player.GetComponent<CameraFeel>();
            if (feel == null)
            {
                feel = player.AddComponent<CameraFeel>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(feel, "motor", player.GetComponent<PlayerMotor>());
            Stage1PlaygroundBuilder.SetObjectReference(feel, "target", player.GetComponent<PlayerEnemyTarget>());
            Stage1PlaygroundBuilder.SetObjectReference(feel, "hiding", player.GetComponent<PlayerHidingController>());
            Stage1PlaygroundBuilder.SetObjectReference(feel, "threat", Object.FindAnyObjectByType<ThreatFeedback>());
            if (eye != null)
            {
                Stage1PlaygroundBuilder.SetObjectReference(feel, "view", eye.transform);
            }

            // Dust: a particle box riding on the camera root (not the camera, so bob and shake
            // do not shake the air).
            Transform holder = eye != null && eye.transform.parent != null ? eye.transform.parent : player.transform;
            Transform dust = holder.Find("FlashlightDust");
            if (dust == null)
            {
                var dustObject = new GameObject("FlashlightDust");
                dustObject.transform.SetParent(holder, false);
                dustObject.transform.localPosition = new Vector3(0f, 0f, 2.2f);
                dustObject.AddComponent<ParticleSystem>();
                dustObject.AddComponent<FlashlightDust>();
                dust = dustObject.transform;
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(dust.GetComponent<FlashlightDust>(), "material", AtmosphereAssets.Dust);
            return changed;
        }

        // ------------------------------------------------------------------- enemy

        private static int EnsureEnemyPieces(EnemyBrain enemy)
        {
            int changed = 0;
            GameObject root = enemy.gameObject;

            EnemyEyeGlow eyes = root.GetComponent<EnemyEyeGlow>();
            if (eyes == null)
            {
                eyes = root.AddComponent<EnemyEyeGlow>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(eyes, "material", AtmosphereAssets.EyeGlow);
            Stage1PlaygroundBuilder.SetObjectReference(eyes, "brain", enemy);

            EnemyPresenceLight presence = root.GetComponent<EnemyPresenceLight>();
            if (presence == null)
            {
                presence = root.AddComponent<EnemyPresenceLight>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(presence, "brain", enemy);

            // Voice: found / caught / stun lines (recorded clips in Resources/Sounds/Enemy*).
            EnemyVoice voice = root.GetComponent<EnemyVoice>();
            if (voice == null)
            {
                voice = root.AddComponent<EnemyVoice>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(voice, "brain", enemy);

            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(eyes);
                PrefabUtility.RecordPrefabInstancePropertyModifications(presence);
                PrefabUtility.RecordPrefabInstancePropertyModifications(voice);
            }

            return changed;
        }

        // --------------------------------------------------------------------- HUD

        private static int EnsureFlashScreen()
        {
            ThreatFeedback threat = Object.FindAnyObjectByType<ThreatFeedback>();
            FlashlightFlash flash = Object.FindAnyObjectByType<FlashlightFlash>();
            if (threat == null || flash == null)
            {
                return 0;
            }

            int changed = 0;
            var vignette = new SerializedObject(threat).FindProperty("vignette").objectReferenceValue as Image;
            Transform parent = vignette != null ? vignette.transform.parent : threat.transform;

            Transform white = parent.Find("FlashWhiteout");
            if (white == null)
            {
                var whiteObject = new GameObject("FlashWhiteout", typeof(RectTransform), typeof(Image));
                whiteObject.transform.SetParent(parent, false);
                var rect = (RectTransform)whiteObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Image image = whiteObject.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
                if (vignette != null)
                {
                    whiteObject.transform.SetSiblingIndex(vignette.transform.GetSiblingIndex() + 1);
                }

                white = whiteObject.transform;
                changed++;
            }

            FlashScreenEffect effect = threat.GetComponent<FlashScreenEffect>();
            if (effect == null)
            {
                effect = threat.gameObject.AddComponent<FlashScreenEffect>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(effect, "flash", flash);
            Stage1PlaygroundBuilder.SetObjectReference(effect, "whiteout", white.GetComponent<Image>());
            return changed;
        }

        // -------------------------------------------------------------------- post

        /// <summary>Cold blue-green grade on the main profile; added once (by override presence).</summary>
        private static int EnsureGrade()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(MainProfilePath);
            if (profile == null || profile.Has<WhiteBalance>())
            {
                return 0;
            }

            WhiteBalance balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(-16f);
            balance.tint.Override(-7f);

            SplitToning split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.32f, 0.5f, 0.52f));
            split.highlights.Override(new Color(0.62f, 0.66f, 0.7f));
            split.balance.Override(-25f);

            if (profile.TryGet(out ColorAdjustments color))
            {
                color.colorFilter.Override(new Color(0.9f, 0.96f, 1f));
            }

            if (profile.TryGet(out FilmGrain grain))
            {
                grain.type.Override(FilmGrainLookup.Thin2);
                grain.intensity.Override(0.24f);
            }

            foreach (VolumeComponent component in profile.components)
            {
                if (!AssetDatabase.Contains(component))
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return 1;
        }

        private static int EnsureChaseVolume()
        {
            int changed = 0;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ChaseProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ChaseProfilePath);

                ChromaticAberration aberration = profile.Add<ChromaticAberration>(true);
                aberration.intensity.Override(0.85f);

                LensDistortion lens = profile.Add<LensDistortion>(true);
                lens.intensity.Override(-0.06f);

                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.saturation.Override(-25f);
                color.contrast.Override(8f);

                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.42f);
                vignette.smoothness.Override(0.5f);

                foreach (VolumeComponent component in profile.components)
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                changed++;
            }

            GameObject volumeObject = FindRoot(ChaseVolumeName);
            if (volumeObject == null)
            {
                volumeObject = new GameObject(ChaseVolumeName);
                changed++;
            }

            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObject.AddComponent<Volume>();
                changed++;
            }

            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = profile;

            ChasePostEffect effect = volumeObject.GetComponent<ChasePostEffect>();
            if (effect == null)
            {
                effect = volumeObject.AddComponent<ChasePostEffect>();
                changed++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(effect, "threat", Object.FindAnyObjectByType<ThreatFeedback>());
            return changed;
        }

        // ------------------------------------------------------------------- house

        private static Transform EnsureRoot()
        {
            GameObject house = GameObject.Find("House");
            Transform parent = house != null ? house.transform : null;
            Transform root = parent != null ? parent.Find(RootName) : null;
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                root.SetParent(parent, false);
            }

            return root;
        }

        private static Transform Group(Transform root, string name)
        {
            Transform group = root.Find(name);
            if (group == null)
            {
                group = new GameObject(name).transform;
                group.SetParent(root, false);
            }

            return group;
        }

        // ---- fog

        private static int EnsureCorridorFog(Transform root)
        {
            Transform fog = Group(root, "Fog");
            int changed = 0;

            // Ground corridor: row 3, cells 2..11. Upper corridor: row 3, cells 0..11.
            changed += EnsureFogBank(fog, "CorridorFog_G", new Vector3(2f, 0.55f, -1f), new Vector3(19.5f, 0.7f, 1.6f));
            changed += EnsureFogBank(fog, "CorridorFog_U", new Vector3(0f, 3.75f, -1f), new Vector3(23.5f, 0.7f, 1.6f));
            return changed;
        }

        private static int EnsureFogBank(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            if (parent.Find(name) != null || HandEditTracker.WasDeleted(parent, name))
            {
                return 0;
            }

            var bank = new GameObject(name);
            bank.transform.SetParent(parent, false);
            bank.transform.position = centre;

            ParticleSystem system = bank.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 20f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.68f, 0.72f, FogBankAlpha);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.RoundToInt(size.x * FogBankPerMetre);
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = size.x * FogBankPerMetre / 20f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.15f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystemRenderer renderer = bank.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AtmosphereAssets.FogPuff;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 3f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return 1;
        }

        // ---- windows

        private static int EnsureWindows(Transform root)
        {
            Transform group = Group(root, "Windows");
            int changed = 0;

            foreach (WindowSpot spot in Windows)
            {
                string name = "Window_" + spot.Name;
                Transform existing = group.Find(name);
                if (existing != null)
                {
                    if (!HandEditTracker.Protected(existing))
                    {
                        TuneMoonlight(existing.Find("Moonlight")?.GetComponent<Light>());
                    }

                    continue;
                }

                if (HandEditTracker.WasDeleted(group, name))
                {
                    continue; // The owner removed this window.
                }

                float floor = StoreyFloor[spot.Storey];
                Vector3 cellCentre = new Vector3(MinX + Cell * spot.Cx + 1f, floor + 1.65f, MinZ + Cell * spot.Cz + 1f);
                if (!Physics.Raycast(cellCentre, spot.Outward, out RaycastHit hit, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || !IsWall(hit.collider))
                {
                    GameLog.Info($"Stage 12: no clear outside wall for {name} (hit {(hit.collider != null ? hit.collider.name : "nothing")}); skipped.");
                    continue;
                }

                Vector3 face = hit.point;
                if (BlockedByDressing(face, spot.Outward))
                {
                    GameLog.Info($"Stage 12: furniture or decor hangs where {name} would go; trying the next spot.");
                    continue;
                }

                BuildWindow(group, name, face, -spot.Outward);
                changed++;
            }

            return changed;
        }

        /// <summary>True when a Dressing renderer pokes into the window's rectangle on the wall.</summary>
        private static bool BlockedByDressing(Vector3 face, Vector3 outward)
        {
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            var window = new Bounds(face - outward * 0.25f, Vector3.zero);
            window.Encapsulate(face - outward * 0.5f + side * 0.75f + Vector3.up * 0.8f);
            window.Encapsulate(face + side * -0.75f + Vector3.down * 0.65f);

            GameObject dressing = GameObject.Find("Dressing");
            if (dressing == null)
            {
                return false;
            }

            foreach (Renderer renderer in dressing.GetComponentsInChildren<Renderer>())
            {
                if (renderer.bounds.Intersects(window) && renderer.bounds.max.y > window.min.y + 0.05f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void BuildWindow(Transform group, string name, Vector3 face, Vector3 inward)
        {
            const float width = 1.2f;
            const float height = 1.4f;
            const float frameWidth = 0.07f;
            const float depth = 0.06f;

            var window = new GameObject(name);
            window.transform.SetParent(group, false);
            window.transform.SetPositionAndRotation(face, Quaternion.LookRotation(inward, Vector3.up));
            window.AddComponent<IndependentLight>();
            GameObjectUtility.SetStaticEditorFlags(window, 0);

            Material frame = AtmosphereAssets.WindowFrame;

            // Glass: an unlit cold pane just off the wall (local +z points into the room).
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.name = "Glass";
            glass.transform.SetParent(window.transform, false);
            glass.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Quad faces -z; turn it to face the room.
            glass.transform.localScale = new Vector3(width, height, 1f);
            glass.GetComponent<Renderer>().sharedMaterial = AtmosphereAssets.MoonGlass;
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            void Bar(string barName, Vector3 position, Vector3 size)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.name = barName;
                bar.transform.SetParent(window.transform, false);
                bar.transform.localPosition = position;
                bar.transform.localScale = size;
                bar.GetComponent<Renderer>().sharedMaterial = frame;
            }

            float halfW = width * 0.5f, halfH = height * 0.5f;
            Bar("Frame_Top", new Vector3(0f, halfH, depth * 0.5f), new Vector3(width + frameWidth * 2f, frameWidth, depth));
            Bar("Frame_Bottom", new Vector3(0f, -halfH, depth * 0.5f), new Vector3(width + frameWidth * 2f, frameWidth, depth));
            Bar("Frame_Left", new Vector3(-halfW, 0f, depth * 0.5f), new Vector3(frameWidth, height, depth));
            Bar("Frame_Right", new Vector3(halfW, 0f, depth * 0.5f), new Vector3(frameWidth, height, depth));
            Bar("Mullion", new Vector3(0f, 0f, depth * 0.4f), new Vector3(0.045f, height, depth * 0.8f));
            Bar("Transom", new Vector3(0f, 0f, depth * 0.4f), new Vector3(width, 0.045f, depth * 0.8f));
            Bar("Sill", new Vector3(0f, -halfH - 0.05f, 0.08f), new Vector3(width + 0.2f, 0.04f, 0.16f));

            // Moonlight: a cookie spot just inside the pane, aimed down into the room.
            var lightObject = new GameObject("Moonlight");
            lightObject.transform.SetParent(window.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.2f, 0.12f);
            lightObject.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.62f, 1f).normalized, Vector3.up);
            Light moon = lightObject.AddComponent<Light>();
            moon.type = LightType.Spot;
            TuneMoonlight(moon);
        }

        /// <summary>Moonlight values, re-applied every run (the windows are generated, not hand-made).</summary>
        private static void TuneMoonlight(Light moon)
        {
            if (moon == null)
            {
                return;
            }

            moon.type = LightType.Spot;
            moon.color = new Color(0.55f, 0.68f, 0.92f);
            moon.intensity = 14f;
            moon.range = 9f;
            moon.spotAngle = 72f;
            moon.innerSpotAngle = 40f;
            moon.shadows = LightShadows.None;
            moon.cookie = AtmosphereAssets.WindowCookie;
        }

        // ---- marks

        private static int EnsureMarks(Transform root)
        {
            Transform group = Group(root, "Marks");
            if (group.childCount > 0)
            {
                return 0;
            }

            var random = new System.Random(4242);
            int placed = 0;
            WindowCentres.Clear();
            Transform windows = root.Find("Windows");
            if (windows != null)
            {
                foreach (Transform window in windows)
                {
                    WindowCentres.Add(window.position);
                }
            }

            for (int storey = 0; storey < 2; storey++)
            {
                List<Vector3> cells = WalkableCells(storey);
                float floor = StoreyFloor[storey];
                float ceiling = storey == 0 ? GroundCeiling : UpperCeiling;

                placed += Scatter(group, cells, random, 16, "Stain", AtmosphereAssets.Stain, floor, ceiling, wall: true, 0.5f, 1.3f, 0.3f, ceiling - floor - 0.25f);
                placed += Scatter(group, cells, random, 10, "Crack", AtmosphereAssets.Crack, floor, ceiling, wall: true, 0.5f, 1.1f, 0.9f, ceiling - floor - 0.1f);
                placed += Scatter(group, cells, random, 8, "Mould", AtmosphereAssets.Mould, floor, ceiling, wall: true, 0.5f, 1f, ceiling - floor - 0.7f, ceiling - floor - 0.15f);
                placed += Scatter(group, cells, random, 7, "Mould", AtmosphereAssets.Mould, floor, ceiling, wall: true, 0.4f, 0.8f, 0.05f, 0.4f);
                placed += Scatter(group, cells, random, 12, "FloorStain", AtmosphereAssets.Stain, floor, ceiling, wall: false, 0.6f, 1.4f, 0f, 0f);
            }

            return placed;
        }

        /// <summary>
        /// Places up to <paramref name="count"/> quads. Wall marks: a ray from a random cell centre
        /// in a random direction at a random height must hit a house wall (not furniture) within
        /// 1.2 m. Floor marks: the floor under a random point must be bare.
        /// </summary>
        private static int Scatter(Transform group, List<Vector3> cells, System.Random random, int count, string kind,
            Material material, float floor, float ceiling, bool wall, float minSize, float maxSize, float minHeight, float maxHeight)
        {
            if (material == null || cells.Count == 0)
            {
                return 0;
            }

            Vector3[] directions = { Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int placed = 0;
            for (int attempt = 0; attempt < count * 6 && placed < count; attempt++)
            {
                Vector3 cell = cells[random.Next(cells.Count)];
                float size = Mathf.Lerp(minSize, maxSize, (float)random.NextDouble());
                Vector3 position;
                Quaternion rotation;

                if (wall)
                {
                    float height = floor + Mathf.Lerp(minHeight, maxHeight, (float)random.NextDouble());
                    Vector3 origin = new Vector3(cell.x + (float)(random.NextDouble() - 0.5) * 1.2f, height, cell.z + (float)(random.NextDouble() - 0.5) * 1.2f);
                    Vector3 direction = directions[random.Next(4)];
                    if (!Physics.Raycast(origin, direction, out RaycastHit hit, 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        || !IsWall(hit.collider))
                    {
                        continue;
                    }

                    // Keep the whole quad on the wall: below the ceiling and above the floor.
                    float top = ceiling - 0.02f, bottom = floor + 0.02f;
                    if (hit.point.y + size * 0.5f > top || hit.point.y - size * 0.5f < bottom)
                    {
                        size = Mathf.Min(size, 2f * Mathf.Min(top - hit.point.y, hit.point.y - bottom));
                        if (size < 0.25f)
                        {
                            continue;
                        }
                    }

                    if (NearWindow(hit.point, size))
                    {
                        continue;
                    }

                    position = hit.point + hit.normal * (0.004f + placed * 0.0004f);
                    rotation = Quaternion.LookRotation(-hit.normal, Vector3.up) * Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
                }
                else
                {
                    Vector3 origin = new Vector3(cell.x + (float)(random.NextDouble() - 0.5) * 1.4f, floor + 1f, cell.z + (float)(random.NextDouble() - 0.5) * 1.4f);
                    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        || !hit.collider.name.StartsWith("House_Floor") && hit.collider.name != "House_Ceiling"
                        || Mathf.Abs(hit.point.y - floor) > 0.1f)
                    {
                        continue;
                    }

                    position = hit.point + Vector3.up * (0.003f + placed * 0.0003f);
                    rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward) * Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
                }

                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = $"{kind}_{(floor > 1f ? "U" : "G")}_{group.childCount:00}";
                quad.transform.SetParent(group, false);
                quad.transform.SetPositionAndRotation(position, rotation);
                quad.transform.localScale = new Vector3(size, size * Mathf.Lerp(0.75f, 1.15f, (float)random.NextDouble()), 1f);
                Renderer renderer = quad.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(quad, StaticEditorFlags.BatchingStatic);
                placed++;
            }

            return placed;
        }

        private static readonly List<Vector3> WindowCentres = new List<Vector3>();

        private static bool NearWindow(Vector3 point, float size)
        {
            foreach (Vector3 centre in WindowCentres)
            {
                Vector3 offset = point - centre;
                if (Mathf.Abs(offset.y) < 0.8f + size * 0.5f && new Vector2(offset.x, offset.z).magnitude < 0.8f + size * 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- cobwebs

        private static int EnsureCobwebs(Transform root)
        {
            Transform group = Group(root, "Cobwebs");
            if (group.childCount > 0)
            {
                return 0;
            }

            Mesh mesh = CobwebMesh();
            Material material = AtmosphereAssets.Cobweb;
            var random = new System.Random(77);
            var used = new HashSet<Vector2Int>();
            int placed = 0;

            for (int storey = 0; storey < 2; storey++)
            {
                float ceiling = storey == 0 ? GroundCeiling : UpperCeiling;
                foreach (Vector3 cell in WalkableCells(storey))
                {
                    Vector3 origin = new Vector3(cell.x, ceiling - 0.08f, cell.z);
                    foreach (Vector3 dx in new[] { Vector3.left, Vector3.right })
                    foreach (Vector3 dz in new[] { Vector3.back, Vector3.forward })
                    {
                        if (!Physics.Raycast(origin, dx, out RaycastHit hx, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !IsWall(hx.collider)
                            || !Physics.Raycast(origin, dz, out RaycastHit hz, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !IsWall(hz.collider))
                        {
                            continue;
                        }

                        var corner = new Vector3(hx.point.x, ceiling, hz.point.z);
                        var key = new Vector2Int(Mathf.RoundToInt(corner.x * 10f), Mathf.RoundToInt(corner.z * 10f) + storey * 100000);
                        if (!used.Add(key) || random.NextDouble() > 0.45)
                        {
                            continue;
                        }

                        // Local +x runs along one wall into the room, local +z along the other.
                        Vector3 alongX = -dx;
                        Vector3 alongZ = -dz;
                        Quaternion rotation = Quaternion.LookRotation(alongZ, Vector3.up);
                        Vector3 right = rotation * Vector3.right;
                        float mirror = Vector3.Dot(right, alongX) > 0f ? 1f : -1f;
                        float size = Mathf.Lerp(0.55f, 0.95f, (float)random.NextDouble());

                        var web = new GameObject($"Cobweb_{(storey == 0 ? "G" : "U")}_{placed:00}");
                        web.transform.SetParent(group, false);
                        web.transform.SetPositionAndRotation(corner + (alongX + alongZ) * 0.01f + Vector3.down * 0.01f, rotation);
                        web.transform.localScale = new Vector3(size * mirror, size, size);
                        web.AddComponent<MeshFilter>().sharedMesh = mesh;
                        MeshRenderer renderer = web.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        GameObjectUtility.SetStaticEditorFlags(web, StaticEditorFlags.BatchingStatic);
                        placed++;
                    }
                }
            }

            return placed;
        }

        /// <summary>
        /// A sagging web across a ceiling corner: corner at the origin, one edge along +x, one
        /// along +z (both just under the ceiling), the middle drooping. UV (0,0) is the corner.
        /// </summary>
        private static Mesh CobwebMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CobwebMeshPath);
            if (mesh != null)
            {
                return mesh;
            }

            mesh = new Mesh { name = "CobwebMesh" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, -0.04f, 0f),
                new Vector3(0.62f, -0.32f, 0.62f),
                new Vector3(0f, -0.04f, 1f),
                new Vector3(0f, -0.55f, 0f) // down the vertical corner edge
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.72f, 0.72f), new Vector2(0f, 1f), new Vector2(0.35f, 0.35f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 1, 2, 4, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, CobwebMeshPath);
            return mesh;
        }

        // ---- helpers

        /// <summary>Cell centres (at floor height) that have floor under them, per storey.</summary>
        private static List<Vector3> WalkableCells(int storey)
        {
            var cells = new List<Vector3>();
            float floor = StoreyFloor[storey];
            for (int cz = 0; cz < 8; cz++)
            for (int cx = 0; cx < 12; cx++)
            {
                var centre = new Vector3(MinX + Cell * cx + 1f, floor, MinZ + Cell * cz + 1f);
                if (Physics.Raycast(centre + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && Mathf.Abs(hit.point.y - floor) < 0.1f
                    && (hit.collider.name.StartsWith("House_Floor") || hit.collider.name.StartsWith("Furn_") == false && hit.collider.name.StartsWith("House_") && !hit.collider.name.Contains("Stair")))
                {
                    cells.Add(centre);
                }
            }

            return cells;
        }

        private static bool IsWall(Collider collider)
        {
            return collider != null && collider.name.StartsWith("House_") && collider.name.Contains("_Wall");
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static bool HasMarker() => FindRoot(MarkerName) != null;
    }
}
