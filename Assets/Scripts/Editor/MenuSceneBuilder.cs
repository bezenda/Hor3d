using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.GameManagement;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Builds the title screen, Assets/Scenes/AshWoods_Menu.unity: a placeholder mansion of
    /// primitive shapes (central block, two wings, corner towers, porch, roofs and chimneys)
    /// behind a gravel court with a fountain, a hedged garden with topiary, bushes and bare
    /// trees, an iron fence with a half-open gate, lamp posts, ground fog and a huge moon.
    /// The camera looks down over the garden at the house with the moon above it.
    ///
    /// Each part of the set is built once under 'Menu_Set' (delete a part to rebuild it);
    /// camera, lights, volume and UI are found by name and re-pointed every run. The scene is
    /// put first in the build settings, before the house.
    ///
    /// The mansion is a stand-in sized for a larger house later: 42 m wide, three storeys in
    /// the middle.
    ///
    /// Menu: Ashen Woods > Level > Create Main Menu Scene
    /// </summary>
    public static partial class MenuSceneBuilder
    {
        internal const string ScenePath = "Assets/Scenes/AshWoods_Menu.unity";
        private const string ProfilePath = "Assets/Settings/AshWoods_MenuProfile.asset";
        private const string MeshAssetPath = AtmosphereAssets.Folder + "/MenuMeshes.asset";
        private const string FontPath = "Assets/Art/Fonts/Resources/Fonts/Inter-SemiBold.ttf";
        private const string BodyFontPath = "Assets/Art/Fonts/Resources/Fonts/Inter-Regular.ttf";

        private static readonly Color FogColour = new Color(0.085f, 0.092f, 0.105f);
        // Title screen v2: the station seen from the burnt treeline (the mansion framing was (0,15,64) -> (0,5.5,0)).
        private static readonly Vector3 MoonPosition = new Vector3(91f, 167f, -403f);
        private static readonly Vector3 CameraPosition = new Vector3(-14f, 2.6f, 34f);
        private static readonly Vector3 CameraTarget = new Vector3(6f, 5f, -8f);
        private static readonly Vector3 FountainCentre = new Vector3(0f, 0f, 15f);

        private static Dictionary<string, Mesh> meshes;

        [MenuItem("Ashen Woods/Level/Create Main Menu Scene", false, 2)]
        private static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int changed = CreateOrUpdate();
            Debug.Log($"Main menu scene: {changed} change(s), saved to {ScenePath}.");
        }

        /// <summary>Opens (or creates) the menu scene, fills in what is missing and saves it.</summary>
        public static int CreateOrUpdate()
        {
            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            meshes = null;
            int changed = 0;
            Transform set = Root("Menu_Set");

            changed += Part(set, "Ground", BuildGround);
            changed += Part(set, "Mansion", BuildMansion);
            changed += Part(set, "Fountain", BuildFountain);
            changed += Part(set, "Garden", BuildGarden);
            changed += Part(set, "Fence", BuildFence);
            changed += Part(set, "Sky", BuildSky);
            changed += Part(set, "GroundFog", BuildGroundFog);
            changed += BuildStation(set); // title screen v2: retires the mansion parts once

            changed += EnsureEnvironment();
            changed += EnsureLights();
            changed += EnsureCamera();
            changed += EnsureVolume();
            changed += EnsureInterface();
            changed += InterfaceRedesignBuilder.Apply(); // Interface v2 look over the parts above
            EnsureBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changed;
        }

        // ---------------------------------------------------------------- set parts

        private static int Part(Transform set, string name, System.Action<Transform> build)
        {
            if (set.Find(name) != null || HandEditTracker.WasDeleted(set, name))
            {
                return 0; // Built already (hand edits inside are kept), or deleted by the owner.
            }

            var part = new GameObject(name).transform;
            part.SetParent(set, false);
            build(part);
            foreach (Transform child in part.GetComponentsInChildren<Transform>())
            {
                if (child.GetComponent<ParticleSystem>() == null && child.GetComponent<Light>() == null && child.GetComponent<Billboard>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }

            return 1;
        }

        private static Material Stone => HouseBuilder.PolyHavenSurface("castle_brick_07", 2.4f, 0.15f, new Color(0.62f, 0.6f, 0.58f));
        private static Material Roof => HouseBuilder.PolyHavenSurface("roof_slates_02", 2f, 0.3f, new Color(0.45f, 0.47f, 0.52f));
        private static Material Grass => HouseBuilder.PolyHavenSurface("leafy_grass", 3f, 0.05f, new Color(0.38f, 0.42f, 0.36f));
        private static Material Gravel => HouseBuilder.PolyHavenSurface("gravel_floor", 2f, 0.1f, new Color(0.6f, 0.6f, 0.6f));
        private static Material RoughStone => HouseBuilder.PolyHavenSurface("rustic_stone_wall", 1.6f, 0.15f, new Color(0.7f, 0.7f, 0.68f));
        private static Material Trim => AtmosphereAssets.Lit("Menu_Trim", new Color(0.36f, 0.35f, 0.33f), 0.2f);
        private static Material DarkWood => AtmosphereAssets.Lit("Menu_DarkWood", new Color(0.09f, 0.06f, 0.045f), 0.3f);
        private static Material Glass => AtmosphereAssets.Lit("Menu_WindowGlass", new Color(0.02f, 0.03f, 0.045f), 0.92f);
        private static Material LitWindow => AtmosphereAssets.Unlit("Menu_LitWindow", new Color(2.4f, 1.55f, 0.7f), null);
        private static Material Iron => AtmosphereAssets.Lit("Menu_Iron", new Color(0.035f, 0.035f, 0.04f), 0.55f);
        private static Material Hedge => AtmosphereAssets.Lit("Menu_Hedge", new Color(0.05f, 0.085f, 0.05f), 0.1f);
        private static Material Leaves => AtmosphereAssets.Lit("Menu_Leaves", new Color(0.045f, 0.07f, 0.045f), 0.08f);
        private static Material Bark => AtmosphereAssets.Lit("Menu_Bark", new Color(0.07f, 0.06f, 0.055f), 0.1f);
        private static Material Soil => AtmosphereAssets.Lit("Menu_Soil", new Color(0.05f, 0.04f, 0.035f), 0.05f);
        private static Material Flowers => AtmosphereAssets.Lit("Menu_Flowers", new Color(0.55f, 0.55f, 0.66f), 0.2f);
        private static Material Water => AtmosphereAssets.Lit("Menu_Water", new Color(0.04f, 0.07f, 0.1f), 0.96f);
        private static Material Lantern => AtmosphereAssets.Unlit("Menu_Lantern", new Color(2.6f, 1.7f, 0.8f), null);

        private static void BuildGround(Transform part)
        {
            Box(part, "Lawn", new Vector3(0f, -0.05f, 0f), new Vector3(600f, 0.1f, 600f), Grass);
            Cylinder(part, "Court", FountainCentre + Vector3.up * 0.02f, 9.5f, 0.04f, Gravel);
            Box(part, "Path_Front", new Vector3(0f, 0.025f, 33f), new Vector3(5f, 0.05f, 18f), Gravel);
            Box(part, "Path_House", new Vector3(0f, 0.025f, 2.5f), new Vector3(6f, 0.05f, 6f), Gravel);
            Box(part, "Drive_Left", new Vector3(-14f, 0.02f, 2.5f), new Vector3(16f, 0.04f, 3f), Gravel);
            Box(part, "Drive_Right", new Vector3(14f, 0.02f, 2.5f), new Vector3(16f, 0.04f, 3f), Gravel);
        }

        // ------------------------------------------------------------------ mansion

        private static void BuildMansion(Transform part)
        {
            // Bodies: front faces +z at z = -4.
            Box(part, "Centre", new Vector3(0f, 5f, -9f), new Vector3(18f, 10f, 10f), Stone);
            Box(part, "Wing_L", new Vector3(-15f, 3.5f, -8f), new Vector3(12f, 7f, 8f), Stone);
            Box(part, "Wing_R", new Vector3(15f, 3.5f, -8f), new Vector3(12f, 7f, 8f), Stone);
            Box(part, "Plinth", new Vector3(0f, 0.4f, -8f), new Vector3(42.4f, 0.8f, 8.6f), RoughStone);

            // Roofs.
            Prism(part, "Roof_Centre", new Vector3(0f, 10f, -9f), new Vector3(19f, 4.6f, 11f), Roof, 0f);
            Prism(part, "Roof_Wing_L", new Vector3(-15f, 7f, -8f), new Vector3(12.6f, 3.4f, 9f), Roof, 0f);
            Prism(part, "Roof_Wing_R", new Vector3(15f, 7f, -8f), new Vector3(12.6f, 3.4f, 9f), Roof, 0f);
            Prism(part, "Dormer_Gable", new Vector3(0f, 10f, -4.6f), new Vector3(3.6f, 2.6f, 6f), Roof, 90f);

            // Cornices.
            Box(part, "Cornice_Centre", new Vector3(0f, 9.9f, -3.85f), new Vector3(18.6f, 0.4f, 0.5f), Trim);
            Box(part, "Cornice_L", new Vector3(-15f, 6.9f, -3.85f), new Vector3(12.4f, 0.35f, 0.45f), Trim);
            Box(part, "Cornice_R", new Vector3(15f, 6.9f, -3.85f), new Vector3(12.4f, 0.35f, 0.45f), Trim);
            Box(part, "Band_Centre", new Vector3(0f, 3.55f, -3.9f), new Vector3(18.3f, 0.2f, 0.3f), Trim);
            Box(part, "Band_Upper", new Vector3(0f, 6.75f, -3.9f), new Vector3(18.3f, 0.2f, 0.3f), Trim);

            // Towers on the outer front corners.
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "L" : "R";
                var foot = new Vector3(side * 21f, 0f, -4f);
                Cylinder(part, "Tower_" + s, foot + Vector3.up * 6.5f, 2.6f, 13f, Stone);
                Cylinder(part, "Tower_Ring_" + s, foot + Vector3.up * 13f, 2.85f, 0.4f, Trim);
                Cone(part, "Tower_Roof_" + s, foot + Vector3.up * 13.2f, 3.1f, 6f, Roof);
                Cylinder(part, "Tower_Spike_" + s, foot + Vector3.up * 19.9f, 0.06f, 1.6f, Iron);
                for (int floor = 0; floor < 3; floor++)
                {
                    Window(part, $"Tower_Window_{s}_{floor}", foot + new Vector3(0f, 2.2f + floor * 3.8f, 2.62f), 0.8f, 1.6f, floor == 2 && side > 0f);
                }
            }

            // Chimneys.
            Box(part, "Chimney_CL", new Vector3(-6f, 13.2f, -10f), new Vector3(1.3f, 4.2f, 1.3f), Stone);
            Box(part, "Chimney_CR", new Vector3(6f, 13.2f, -8f), new Vector3(1.3f, 4.2f, 1.3f), Stone);
            Box(part, "Chimney_WL", new Vector3(-17f, 9.4f, -9f), new Vector3(1f, 3f, 1f), Stone);
            Box(part, "Chimney_WR", new Vector3(13f, 9.4f, -7f), new Vector3(1f, 3f, 1f), Stone);

            // Porch: columns, roof, pediment, steps, door.
            foreach (float x in new[] { -4f, -1.6f, 1.6f, 4f })
            {
                Cylinder(part, $"Column_{x:0.0}", new Vector3(x, 3.4f, -0.9f), 0.3f, 5.2f, Trim);
                Box(part, $"Column_Base_{x:0.0}", new Vector3(x, 0.95f, -0.9f), new Vector3(0.8f, 0.3f, 0.8f), Trim);
            }

            Box(part, "Porch_Floor", new Vector3(0f, 0.6f, -2.3f), new Vector3(10f, 0.4f, 3.6f), RoughStone);
            Box(part, "Porch_Roof", new Vector3(0f, 6.2f, -2.3f), new Vector3(10f, 0.45f, 3.9f), Trim);
            Prism(part, "Pediment", new Vector3(0f, 6.42f, -2.3f), new Vector3(3.9f, 2.2f, 10f), Stone, 90f);
            Box(part, "Step_1", new Vector3(0f, 0.12f, 0.6f), new Vector3(7f, 0.24f, 1.2f), RoughStone);
            Box(part, "Step_2", new Vector3(0f, 0.36f, 0.05f), new Vector3(6.4f, 0.24f, 1f), RoughStone);
            Box(part, "Door", new Vector3(0f, 2.5f, -3.95f), new Vector3(2.2f, 3.4f, 0.15f), DarkWood);
            Box(part, "Door_Frame", new Vector3(0f, 4.35f, -3.9f), new Vector3(2.9f, 0.35f, 0.25f), Trim);
            Window(part, "Fanlight", new Vector3(0f, 4.9f, -3.95f), 1.6f, 0.5f, true);

            // Windows: ground floor beside the porch, two upper floors across the front.
            var random = new System.Random(5);
            foreach (float x in new[] { -7.5f, -5.5f, 5.5f, 7.5f })
            {
                Window(part, $"Window_C0_{x:0.0}", new Vector3(x, 2f, -3.95f), 1.1f, 2f, random.NextDouble() < 0.15);
            }

            for (int floor = 1; floor < 3; floor++)
            {
                for (float x = -7.5f; x <= 7.6f; x += 2.5f)
                {
                    bool lit = floor == 1 ? Mathf.Approximately(x, -2.5f) : Mathf.Approximately(x, 5f);
                    Window(part, $"Window_C{floor}_{x:0.0}", new Vector3(x, 1.95f + floor * 3.2f, -3.95f), 1.1f, 1.9f, lit);
                }
            }

            foreach (float side in new[] { -1f, 1f })
            {
                for (int floor = 0; floor < 2; floor++)
                {
                    foreach (float offset in new[] { 10.8f, 13.4f, 16f, 18.6f })
                    {
                        bool lit = side < 0f && floor == 0 && Mathf.Approximately(offset, 13.4f);
                        Window(part, $"Window_W{(side < 0 ? "L" : "R")}{floor}_{offset:0.0}",
                            new Vector3(side * offset, 2f + floor * 3.2f, -3.95f), 1f, 1.8f, lit);
                    }
                }
            }
        }

        /// <summary>A window on a +z facing wall: glass (dark or lit), lintel and sill.</summary>
        private static void Window(Transform parent, string name, Vector3 centre, float width, float height, bool lit)
        {
            Box(parent, name, centre, new Vector3(width, height, 0.12f), lit ? LitWindow : Glass);
            Box(parent, name + "_Lintel", centre + new Vector3(0f, height * 0.5f + 0.12f, 0.05f), new Vector3(width + 0.35f, 0.24f, 0.22f), Trim);
            Box(parent, name + "_Sill", centre + new Vector3(0f, -height * 0.5f - 0.06f, 0.1f), new Vector3(width + 0.3f, 0.12f, 0.3f), Trim);
            Box(parent, name + "_Mullion", centre + new Vector3(0f, 0f, 0.07f), new Vector3(0.07f, height, 0.04f), DarkWood);
            Box(parent, name + "_Transom", centre + new Vector3(0f, height * 0.12f, 0.07f), new Vector3(width, 0.07f, 0.04f), DarkWood);
        }

        // ------------------------------------------------------------------ fountain

        private static void BuildFountain(Transform part)
        {
            Vector3 c = FountainCentre;
            Cylinder(part, "Basin", c + Vector3.up * 0.4f, 3.4f, 0.8f, RoughStone);
            Cylinder(part, "Basin_Rim", c + Vector3.up * 0.84f, 3.55f, 0.12f, Trim);
            Cylinder(part, "Pool", c + Vector3.up * 0.7f, 3.05f, 0.1f, Water);
            Cylinder(part, "Pedestal", c + Vector3.up * 1.4f, 0.45f, 1.8f, RoughStone);
            Cylinder(part, "Bowl", c + Vector3.up * 2.35f, 1.35f, 0.3f, RoughStone);
            Cylinder(part, "Bowl_Water", c + Vector3.up * 2.51f, 1.18f, 0.04f, Water);
            Cylinder(part, "Pedestal_Top", c + Vector3.up * 2.95f, 0.22f, 0.9f, RoughStone);
            Cylinder(part, "Bowl_Top", c + Vector3.up * 3.45f, 0.65f, 0.2f, RoughStone);
            Sphere(part, "Finial", c + Vector3.up * 3.75f, 0.25f, Trim);

            // Spray: a jet from the top that falls back into the bowls.
            var spray = new GameObject("Spray");
            spray.transform.SetParent(part, false);
            spray.transform.position = c + Vector3.up * 3.95f;
            spray.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem system = spray.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(0.75f, 0.85f, 0.95f, 0.55f);
            main.gravityModifier = 1f;
            main.maxParticles = 400;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 260f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;
            ParticleSystemRenderer renderer = spray.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AtmosphereAssets.Dust;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // -------------------------------------------------------------------- garden

        private static void BuildGarden(Transform part)
        {
            var random = new System.Random(31);

            // A hedge ring round the court, open at the front and back for the path.
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) / segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                if (Mathf.Abs(dir.x) < 0.25f)
                {
                    // Path gaps: put a topiary cone either side instead.
                    continue;
                }

                Vector3 at = FountainCentre + dir * 10.6f;
                var hedge = Box(part, $"Hedge_Ring_{i:00}", at + Vector3.up * 0.55f, new Vector3(3.6f, 1.1f, 0.9f), Hedge);
                hedge.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { FountainCentre.z + 10.6f, FountainCentre.z - 10.6f })
                {
                    Vector3 at = new Vector3(side * 3.4f, 0f, z);
                    Box(part, $"Pot_{side}_{z:0}", at + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, 0.9f), RoughStone);
                    Cone(part, $"Topiary_{side}_{z:0}", at + Vector3.up * 0.6f, 0.75f, 2.6f, Hedge);
                }

                // Hedges along the front path and the house path.
                for (float z = 27f; z < 41f; z += 3.2f)
                {
                    Box(part, $"Hedge_Path_{side}_{z:0}", new Vector3(side * 3.6f, 0.55f, z), new Vector3(0.9f, 1.1f, 2.8f), Hedge);
                }

                Box(part, $"Hedge_House_{side}", new Vector3(side * 4.6f, 0.5f, 2.6f), new Vector3(0.8f, 1f, 3.4f), Hedge);

                // Flower beds inside the ring.
                for (int b = 0; b < 2; b++)
                {
                    Vector3 bed = FountainCentre + new Vector3(side * 6.2f, 0.06f, b == 0 ? -4.5f : 4.5f);
                    Box(part, $"Bed_{side}_{b}", bed, new Vector3(2.4f, 0.12f, 1.4f), Soil);
                    for (int f = 0; f < 9; f++)
                    {
                        Vector3 offset = new Vector3((float)(random.NextDouble() - 0.5) * 2f, 0.18f, (float)(random.NextDouble() - 0.5) * 1.1f);
                        Sphere(part, $"Flower_{side}_{b}_{f}", bed + offset, 0.13f + (float)random.NextDouble() * 0.06f, Flowers);
                    }
                }

                // Lamp posts at the court entrances.
                foreach (float z in new[] { FountainCentre.z + 12.5f, FountainCentre.z - 12.5f })
                {
                    LampPost(part, $"Lamp_{side}_{z:0}", new Vector3(side * 3.4f, 0f, z));
                }
            }

            // Bushes in the lawns either side.
            for (int i = 0; i < 46; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (11f + (float)random.NextDouble() * 26f);
                float z = -1f + (float)random.NextDouble() * 38f;
                if (Mathf.Abs(x) < 23f && z < 3f)
                {
                    continue; // In front of the wings: keep the drive clear.
                }

                float size = 0.6f + (float)random.NextDouble() * 0.8f;
                Sphere(part, $"Bush_{i:00}", new Vector3(x, size * 0.6f, z), size, Leaves);
                if (random.NextDouble() < 0.5)
                {
                    Sphere(part, $"Bush_{i:00}_b", new Vector3(x + size * 0.7f, size * 0.45f, z + 0.3f), size * 0.7f, Leaves);
                }
            }

            // Trees: leafy ones in the lawns, bare ones behind the house against the moon.
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = new Vector3(side * (27f + (float)random.NextDouble() * 14f), 0f, -8f + (float)random.NextDouble() * 44f);
                LeafyTree(part, $"Tree_{i:00}", at, 0.85f + (float)random.NextDouble() * 0.4f, random);
            }

            for (int i = 0; i < 12; i++)
            {
                var at = new Vector3(-34f + i * 6.2f + (float)(random.NextDouble() - 0.5) * 3f, 0f, -20f - (float)random.NextDouble() * 14f);
                BareTree(part, $"BareTree_{i:00}", at, 0.9f + (float)random.NextDouble() * 0.6f, random);
            }
        }

        private static void LampPost(Transform parent, string name, Vector3 foot)
        {
            Cylinder(parent, name + "_Post", foot + Vector3.up * 1.6f, 0.08f, 3.2f, Iron);
            Box(parent, name + "_Lantern", foot + Vector3.up * 3.45f, new Vector3(0.32f, 0.5f, 0.32f), Lantern);
            Box(parent, name + "_Cap", foot + Vector3.up * 3.76f, new Vector3(0.45f, 0.1f, 0.45f), Iron);

            var lightObject = new GameObject(name + "_Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = foot + Vector3.up * 3.3f;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.intensity = 3.2f;
            light.range = 9f;
            light.shadows = LightShadows.None;
        }

        private static void LeafyTree(Transform parent, string name, Vector3 foot, float scale, System.Random random)
        {
            Cylinder(parent, name + "_Trunk", foot + Vector3.up * 2.2f * scale, 0.35f * scale, 4.4f * scale, Bark);
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = new Vector3((float)(random.NextDouble() - 0.5) * 2.4f, 4.6f + (float)random.NextDouble() * 1.8f, (float)(random.NextDouble() - 0.5) * 2.4f) * scale;
                Sphere(parent, $"{name}_Crown_{i}", foot + offset, (2f + (float)random.NextDouble() * 0.8f) * scale, Leaves);
            }
        }

        private static void BareTree(Transform parent, string name, Vector3 foot, float scale, System.Random random)
        {
            float height = 7f * scale;
            Cylinder(parent, name + "_Trunk", foot + Vector3.up * height * 0.5f, 0.3f * scale, height, Bark);
            for (int i = 0; i < 6; i++)
            {
                float y = height * (0.45f + 0.5f * (float)random.NextDouble());
                float yaw = (float)random.NextDouble() * 360f;
                float tilt = 30f + (float)random.NextDouble() * 35f;
                float length = (1.8f + (float)random.NextDouble() * 2.2f) * scale;
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                Vector3 start = foot + Vector3.up * y;
                var branch = Cylinder(parent, $"{name}_Branch_{i}", start + rotation * Vector3.up * length * 0.5f, 0.09f * scale, length, Bark);
                branch.transform.rotation = rotation;

                // A twig off the end of each branch.
                Vector3 tip = start + rotation * Vector3.up * length;
                Quaternion twigRotation = rotation * Quaternion.Euler(0f, 90f, 25f);
                var twig = Cylinder(parent, $"{name}_Twig_{i}", tip + twigRotation * Vector3.up * 0.5f * scale, 0.04f * scale, 1f * scale, Bark);
                twig.transform.rotation = twigRotation;
            }
        }

        // --------------------------------------------------------------------- fence

        private static void BuildFence(Transform part)
        {
            const float frontZ = 42f;
            const float halfWidth = 46f;
            const float gateHalf = 3.2f;

            // Gate pillars.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 pillar = new Vector3(side * (gateHalf + 0.6f), 0f, frontZ);
                Box(part, $"GatePillar_{side}", pillar + Vector3.up * 1.8f, new Vector3(1.2f, 3.6f, 1.2f), RoughStone);
                Box(part, $"GatePillar_Cap_{side}", pillar + Vector3.up * 3.7f, new Vector3(1.5f, 0.25f, 1.5f), Trim);
                Sphere(part, $"GatePillar_Ball_{side}", pillar + Vector3.up * 4.25f, 0.45f, Trim);
            }

            // Gate leaves, swung half open into the grounds.
            foreach (float side in new[] { -1f, 1f })
            {
                var hinge = new GameObject($"GateLeaf_{side}");
                hinge.transform.SetParent(part, false);
                hinge.transform.position = new Vector3(side * gateHalf, 0f, frontZ);
                hinge.transform.rotation = Quaternion.Euler(0f, side < 0f ? 28f : 152f, 0f);
                Transform leaf = hinge.transform;
                const float width = 3.1f;
                for (float x = 0.12f; x < width; x += 0.24f)
                {
                    float arch = 2.4f + Mathf.Sin(x / width * Mathf.PI * 0.5f) * 0.5f;
                    LocalBox(leaf, $"Bar_{x:0.00}", new Vector3(x, arch * 0.5f, 0f), new Vector3(0.05f, arch, 0.05f), Iron);
                    LocalCone(leaf, $"Tip_{x:0.00}", new Vector3(x, arch, 0f), 0.07f, 0.25f, Iron);
                }

                LocalBox(leaf, "Rail_Low", new Vector3(width * 0.5f, 0.35f, 0f), new Vector3(width, 0.08f, 0.07f), Iron);
                LocalBox(leaf, "Rail_Mid", new Vector3(width * 0.5f, 1.4f, 0f), new Vector3(width, 0.08f, 0.07f), Iron);
                LocalBox(leaf, "Frame", new Vector3(0.04f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 0.1f), Iron);
            }

            // Railings: front either side of the gate, then down both sides.
            void Run(string name, Vector3 from, Vector3 to, float spacing)
            {
                Vector3 delta = to - from;
                int posts = Mathf.Max(1, Mathf.RoundToInt(delta.magnitude / spacing));
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 at = Vector3.Lerp(from, to, i / (float)posts);
                    Box(part, $"{name}_Post_{i:00}", at + Vector3.up * 1f, new Vector3(0.09f, 2f, 0.09f), Iron);
                    Cone(part, $"{name}_Tip_{i:00}", at + Vector3.up * 2f, 0.08f, 0.26f, Iron);
                }

                Vector3 middle = (from + to) * 0.5f;
                Quaternion along = Quaternion.LookRotation(delta.normalized, Vector3.up);
                Box(part, name + "_RailLow", middle + Vector3.up * 0.35f, new Vector3(0.06f, 0.06f, delta.magnitude), Iron).transform.rotation = along;
                Box(part, name + "_RailHigh", middle + Vector3.up * 1.75f, new Vector3(0.06f, 0.06f, delta.magnitude), Iron).transform.rotation = along;
            }

            Run("Fence_FrontL", new Vector3(-halfWidth, 0f, frontZ), new Vector3(-gateHalf - 1.2f, 0f, frontZ), 0.6f);
            Run("Fence_FrontR", new Vector3(gateHalf + 1.2f, 0f, frontZ), new Vector3(halfWidth, 0f, frontZ), 0.6f);
            Run("Fence_SideL", new Vector3(-halfWidth, 0f, frontZ), new Vector3(-halfWidth, 0f, -30f), 1.2f);
            Run("Fence_SideR", new Vector3(halfWidth, 0f, frontZ), new Vector3(halfWidth, 0f, -30f), 1.2f);
        }

        // ----------------------------------------------------------------------- sky

        private static void BuildSky(Transform part)
        {
            Material moonMaterial = AssetDatabase.LoadAssetAtPath<Material>(AtmosphereAssets.Folder + "/Mat_Moon.mat");
            if (moonMaterial == null)
            {
                moonMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Mat_Moon" };
                moonMaterial.mainTexture = AtmosphereAssets.Moon;
                moonMaterial.color = new Color(1.5f, 1.55f, 1.7f, 1f);
                AssetDatabase.CreateAsset(moonMaterial, AtmosphereAssets.Folder + "/Mat_Moon.mat");
            }

            GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(moon.GetComponent<Collider>());
            moon.name = "Moon";
            moon.transform.SetParent(part, false);
            moon.transform.position = MoonPosition;
            moon.transform.localScale = Vector3.one * 100f;
            moon.transform.rotation = Quaternion.LookRotation(MoonPosition - CameraPosition, Vector3.up);
            Renderer renderer = moon.GetComponent<Renderer>();
            renderer.sharedMaterial = moonMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            moon.AddComponent<Billboard>();

            // Stars on a dome, kept clear of the horizon.
            Material starMaterial = AssetDatabase.LoadAssetAtPath<Material>(AtmosphereAssets.Folder + "/Mat_Star.mat");
            if (starMaterial == null)
            {
                starMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Mat_Star" };
                starMaterial.mainTexture = AtmosphereAssets.SoftDotTexture;
                AssetDatabase.CreateAsset(starMaterial, AtmosphereAssets.Folder + "/Mat_Star.mat");
            }

            var stars = new GameObject("Stars");
            stars.transform.SetParent(part, false);
            stars.transform.position = new Vector3(0f, 40f, 0f);
            stars.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem system = stars.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.75f, 0.9f, 0.35f), new Color(0.9f, 0.92f, 1f, 0.9f));
            main.maxParticles = 700;
            main.prewarm = false;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 700) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 420f;
            shape.radiusThickness = 0f;
            ParticleSystemRenderer starRenderer = stars.GetComponent<ParticleSystemRenderer>();
            starRenderer.sharedMaterial = starMaterial;
            starRenderer.minParticleSize = 0.0008f;
            starRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void BuildGroundFog(Transform part)
        {
            var fog = new GameObject("Fog");
            fog.transform.SetParent(part, false);
            fog.transform.position = new Vector3(0f, 0.7f, 12f);
            ParticleSystem system = fog.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 30f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(22f, 32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.5f, 0.56f, 0.62f, 0.07f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 140;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 5f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(96f, 1.2f, 70f);
            ParticleSystem.VelocityOverLifetimeModule wind = system.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.World;
            wind.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            wind.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            wind.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = fog.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AtmosphereAssets.FogPuff;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 4f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------- lights, camera, post, UI

        private static int EnsureEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.12f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f; // title v2: haze so the burnt trunks stand out as silhouettes
            RenderSettings.fogColor = FogColour;
            return 0;
        }

        private static int EnsureLights()
        {
            int changed = 0;
            GameObject moonLight = RootObject("Moonlight", ref changed);
            Light light = Need<Light>(moonLight);
            if (!HandEditTracker.Protected(moonLight.transform)) // Hand-tuned: keep.
            {
                light.type = LightType.Directional;
                light.color = new Color(0.62f, 0.72f, 0.98f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.85f;

                // From the moon's side but steeper, so the garden is lit and the house rims.
                Vector3 fromMoon = (Vector3.zero - MoonPosition).normalized;
                fromMoon.y = -0.75f;
                moonLight.transform.rotation = Quaternion.LookRotation(fromMoon.normalized, Vector3.up);
            }

            GameObject fill = RootObject("FrontFill", ref changed);
            Light fillLight = Need<Light>(fill);
            if (HandEditTracker.Protected(fill.transform))
            {
                return changed;
            }

            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.32f, 0.4f, 0.6f);
            fillLight.intensity = 0.6f;
            fillLight.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(28f, 200f, 0f);
            return changed;
        }

        private static int EnsureCamera()
        {
            int changed = 0;
            GameObject cameraObject = RootObject("MenuCamera", ref changed);
            cameraObject.tag = "MainCamera";
            Camera camera = Need<Camera>(cameraObject);
            bool handTuned = HandEditTracker.Protected(cameraObject.transform);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColour;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 900f;
            if (!handTuned)
            {
                camera.fieldOfView = 55f;
                cameraObject.transform.position = CameraPosition;
                cameraObject.transform.rotation = Quaternion.LookRotation(CameraTarget - CameraPosition, Vector3.up);
            }

            var data = Need<UniversalAdditionalCameraData>(cameraObject);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            MenuCameraDrift drift = Need<MenuCameraDrift>(cameraObject);
            if (handTuned)
            {
                return changed;
            }

            var so = new SerializedObject(drift);
            so.FindProperty("lookAt").vector3Value = CameraTarget;
            so.FindProperty("sway").vector3Value = new Vector3(1.2f, 0.25f, 0.6f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static int EnsureVolume()
        {
            int changed = 0;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.85f);
                bloom.intensity.Override(0.9f);
                bloom.scatter.Override(0.72f);
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0.35f);
                color.contrast.Override(14f);
                color.saturation.Override(-18f);
                WhiteBalance balance = profile.Add<WhiteBalance>(true);
                balance.temperature.Override(-20f);
                balance.tint.Override(-6f);
                SplitToning split = profile.Add<SplitToning>(true);
                split.shadows.Override(new Color(0.3f, 0.48f, 0.52f));
                split.highlights.Override(new Color(0.62f, 0.66f, 0.72f));
                split.balance.Override(-20f);
                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.38f);
                vignette.smoothness.Override(0.5f);
                FilmGrain grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin2);
                grain.intensity.Override(0.25f);
                foreach (VolumeComponent component in profile.components)
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                changed++;
            }

            GameObject volumeObject = RootObject("Menu_Volume", ref changed);
            Volume volume = Need<Volume>(volumeObject);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return changed;
        }

        private static int EnsureInterface()
        {
            int changed = 0;
            Font title = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Font body = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            if (title == null) title = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (body == null) body = title;

            GameObject canvasObject = RootObject("Menu_Canvas", ref changed);
            Canvas canvas = Need<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = Need<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            Transform root = canvasObject.transform;

            // A soft dark band behind the column keeps the text readable over the scene.
            Image shade = UiImage(root, "Shade", new Color(0f, 0f, 0f, 0.45f));
            Place(shade.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(820f, 0f));

            Text titleText = UiText(root, "Title", "ASHEN WOODS", title, 76, new Color(0.88f, 0.86f, 0.82f));
            Place(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 190f), new Vector2(760f, 100f));

            Text subtitle = UiText(root, "Subtitle", "Света нет. Что-то в темноте прислушивается.", body, 26, new Color(0.62f, 0.6f, 0.56f));
            Place(subtitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(154f, 128f), new Vector2(760f, 40f));

            Button play = UiButton(root, "Play", "Новая игра", body, new Vector2(150f, 20f));
            Button settings = UiButton(root, "Settings", "Настройки", body, new Vector2(150f, -46f));
            Button quit = UiButton(root, "Quit", "Выйти", body, new Vector2(150f, -112f));

            Text hint = UiText(root, "Hint", "Enter — начать", body, 18, new Color(0.45f, 0.44f, 0.41f));
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(154f, 60f), new Vector2(500f, 30f));

            Button back;
            GameObject panel = EnsureSettingsPanel(root, title, body, out back);

            Image fade = UiImage(root, "Fade", Color.black);
            Place(fade.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fade.transform.SetAsLastSibling();

            MainMenuController controller = Need<MainMenuController>(canvasObject);
            Stage1PlaygroundBuilder.SetObjectReference(controller, "playButton", play);
            Stage1PlaygroundBuilder.SetObjectReference(controller, "quitButton", quit);
            Stage1PlaygroundBuilder.SetObjectReference(controller, "settingsButton", settings);
            Stage1PlaygroundBuilder.SetObjectReference(controller, "settingsPanel", panel);
            Stage1PlaygroundBuilder.SetObjectReference(controller, "settingsBackButton", back);
            Stage1PlaygroundBuilder.SetObjectReference(controller, "fade", fade);
            var so = new SerializedObject(controller);
            so.FindProperty("gameScene").stringValue = System.IO.Path.GetFileNameWithoutExtension(FacilityBuilder.FacilityScenePath);
            SerializedProperty titleOnly = so.FindProperty("titleOnly");
            GameObject[] column = { play.gameObject, settings.gameObject, quit.gameObject, hint.gameObject };
            titleOnly.arraySize = column.Length;
            for (int i = 0; i < column.Length; i++)
            {
                titleOnly.GetArrayElementAtIndex(i).objectReferenceValue = column[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject events = RootObject("EventSystem", ref changed);
            if (events.GetComponent<EventSystem>() == null)
            {
                events.AddComponent<EventSystem>();
            }

            if (events.GetComponent<StandaloneInputModule>() == null)
            {
                events.AddComponent<StandaloneInputModule>();
            }

            return changed;
        }

        /// <summary>
        /// The settings column of the title screen: Difficulty first, then the pause screen's rows
        /// (Stage4MenuBuilder.SettingsRows). Same row names as in the level, so the one SettingsMenu
        /// component fills in the values and handles the arrows. Hidden until Settings is clicked.
        /// </summary>
        private static GameObject EnsureSettingsPanel(Transform root, Font titleFont, Font body, out Button back)
        {
            Transform existing = root.Find("SettingsPanel");
            GameObject panel = existing != null ? existing.gameObject : new GameObject("SettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            Place((RectTransform)panel.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Need<AshenWoods.UI.SettingsMenu>(panel);

            Color ink = new Color(0.86f, 0.84f, 0.8f);
            Color muted = new Color(0.62f, 0.6f, 0.56f);
            Text heading = UiText(panel.transform, "Heading", "Настройки", titleFont, 34, ink);
            Place(heading.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(154f, 66f), new Vector2(560f, 50f));

            var rows = new List<string[]> { new[] { "Difficulty", "Сложность" } };
            for (int i = 0; i < Stage4MenuBuilder.SettingsRows.GetLength(0); i++)
            {
                rows.Add(new[] { Stage4MenuBuilder.SettingsRows[i, 0], Stage4MenuBuilder.SettingsRows[i, 1] });
            }

            const float rowStep = 46f;
            float y = 14f;
            foreach (string[] entry in rows)
            {
                Transform rowTransform = panel.transform.Find("Row_" + entry[0]);
                GameObject row = rowTransform != null ? rowTransform.gameObject : new GameObject("Row_" + entry[0], typeof(RectTransform));
                row.transform.SetParent(panel.transform, false);
                Place((RectTransform)row.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                Text label = UiText(row.transform, "Label", entry[1], body, 20, muted);
                Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(154f, y), new Vector2(300f, 40f));

                StepButton(row.transform, "Prev", "‹", body, new Vector2(470f, y));
                Text value = UiText(row.transform, "Value", string.Empty, body, 20, ink);
                value.alignment = TextAnchor.MiddleCenter;
                Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(514f, y), new Vector2(170f, 40f));
                StepButton(row.transform, "Next", "›", body, new Vector2(688f, y));
                y -= rowStep;
            }

            back = UiButton(panel.transform, "BackButton", "Back", body, new Vector2(150f, y - 26f));

            Text hint = UiText(panel.transform, "Hint", "Esc — назад", body, 18, new Color(0.45f, 0.44f, 0.41f));
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(154f, 60f), new Vector2(500f, 30f));

            panel.SetActive(false);
            return panel;
        }

        /// <summary>A small square arrow button (‹ or ›) at a left-anchored position.</summary>
        private static void StepButton(Transform row, string name, string glyph, Font font, Vector2 position)
        {
            Button button = UiButton(row, name, glyph, font, position);
            Place((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), position, new Vector2(40f, 40f));
            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 24;
            Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static Button UiButton(Transform root, string name, string label, Font font, Vector2 position)
        {
            Image image = UiImage(root, name, new Color(1f, 1f, 1f, 0.05f));
            Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), position, new Vector2(320f, 52f));
            image.raycastTarget = true;

            Button button = Need<Button>(image.gameObject);
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.05f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.14f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.24f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.targetGraphic = image;

            Text text = UiText(image.transform, "Label", label, font, 24, new Color(0.86f, 0.84f, 0.8f));
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(-22f, 0f));
            text.alignment = TextAnchor.MiddleLeft;
            return button;
        }

        private static Image UiImage(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = Need<Image>(go);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text UiText(Transform parent, string name, string content, Font font, int size, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = Need<Text>(go);
            if (existing != null && HandEditTracker.Protected(go.transform))
            {
                return text; // Hand-edited wording or style.
            }

            text.text = content;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (HandEditTracker.Protected(rect))
            {
                return; // Moved by hand: keep it there.
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            if (anchorMin == anchorMax)
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            else
            {
                // Stretched: position/size are offsets from the anchors.
                rect.offsetMin = position;
                rect.offsetMax = size;
                if (anchorMin.x == 0f && anchorMax.x == 0f)
                {
                    rect.offsetMax = new Vector2(size.x, 0f);
                }
            }
        }

        private static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (!scenes.Exists(s => s.path == FacilityBuilder.FacilityScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(FacilityBuilder.FacilityScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------- primitives

        /// <summary>The component on <paramref name="go"/>, added when missing (no '??': Unity's fake null).</summary>
        private static T Need<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
        }

        private static Transform Root(string name)
        {
            int ignored = 0;
            return RootObject(name, ref ignored).transform;
        }

        private static GameObject RootObject(string name, ref int changed)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            changed++;
            return new GameObject(name);
        }

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.position = centre;
            box.transform.localScale = size;
            box.AddComponent<MeshFilter>().sharedMesh = HouseBuilder.MetricBoxMesh(size);
            box.AddComponent<MeshRenderer>().sharedMaterials = new[] { material, material };
            return box;
        }

        private static GameObject LocalBox(Transform parent, string name, Vector3 local, Vector3 size, Material material)
        {
            GameObject box = Box(parent, name, Vector3.zero, size, material);
            box.transform.localPosition = local;
            box.transform.localRotation = Quaternion.identity;
            return box;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 centre, float radius, float height, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.position = centre;
            cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            return cylinder;
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 centre, float radius, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.position = centre;
            sphere.transform.localScale = Vector3.one * radius * 2f;
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            return sphere;
        }

        /// <summary>A cone standing on <paramref name="foot"/> (tower roofs, topiary, railing tips).</summary>
        private static GameObject Cone(Transform parent, string name, Vector3 foot, float radius, float height, Material material)
        {
            var cone = new GameObject(name);
            cone.transform.SetParent(parent, false);
            cone.transform.position = foot;
            cone.transform.localScale = new Vector3(radius, height, radius);
            cone.AddComponent<MeshFilter>().sharedMesh = CachedMesh("Cone16", ConeMesh);
            cone.AddComponent<MeshRenderer>().sharedMaterial = material;
            return cone;
        }

        private static GameObject LocalCone(Transform parent, string name, Vector3 local, float radius, float height, Material material)
        {
            GameObject cone = Cone(parent, name, Vector3.zero, radius, height, material);
            cone.transform.localPosition = local;
            cone.transform.localRotation = Quaternion.identity;
            return cone;
        }

        /// <summary>
        /// A gable roof: a triangular prism sitting on <paramref name="baseCentre"/>, ridge along
        /// its local x. <paramref name="size"/> is (length, height, depth); UVs are in metres.
        /// </summary>
        private static GameObject Prism(Transform parent, string name, Vector3 baseCentre, Vector3 size, Material material, float yaw)
        {
            var prism = new GameObject(name);
            prism.transform.SetParent(parent, false);
            prism.transform.position = baseCentre;
            prism.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            string key = $"Prism_{size.x:0.##}_{size.y:0.##}_{size.z:0.##}";
            prism.AddComponent<MeshFilter>().sharedMesh = CachedMesh(key, () => PrismMesh(size));
            prism.AddComponent<MeshRenderer>().sharedMaterial = material;
            return prism;
        }

        private static Mesh CachedMesh(string key, System.Func<Mesh> build)
        {
            if (meshes == null)
            {
                meshes = new Dictionary<string, Mesh>();
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MeshAssetPath))
                {
                    if (asset is Mesh existing)
                    {
                        meshes[existing.name] = existing;
                    }
                }
            }

            if (meshes.TryGetValue(key, out Mesh mesh) && mesh != null)
            {
                return mesh;
            }

            mesh = build();
            mesh.name = key;
            if (AssetDatabase.LoadMainAssetAtPath(MeshAssetPath) == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
            }

            meshes[key] = mesh;
            return mesh;
        }

        private static Mesh ConeMesh()
        {
            const int sides = 16;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                int first = vertices.Count;
                vertices.Add(new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)));
                vertices.Add(new Vector3(0f, 1f, 0f));
                vertices.Add(new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)));
                uvs.Add(new Vector2(i / (float)sides * 6f, 0f));
                uvs.Add(new Vector2((i + 0.5f) / sides * 6f, 3f));
                uvs.Add(new Vector2((i + 1) / (float)sides * 6f, 0f));
                triangles.AddRange(new[] { first, first + 1, first + 2 });
            }

            return Finish(vertices, uvs, triangles, new Vector3(0f, 0.3f, 0f));
        }

        /// <summary>
        /// Builds the mesh, turning every triangle to face away from <paramref name="inside"/>
        /// (Unity's front face has normal Cross(b - a, c - a)).
        /// </summary>
        private static Mesh Finish(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 inside)
        {
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
                {
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh PrismMesh(Vector3 size)
        {
            float hx = size.x * 0.5f, h = size.y, hz = size.z * 0.5f;
            float slope = Mathf.Sqrt(hz * hz + h * h);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                uvs.AddRange(new[] { ua, ub, uc, ud });
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c });
                uvs.AddRange(new[] { ua, ub, uc });
                triangles.AddRange(new[] { first, first + 1, first + 2 });
            }

            var ridgeL = new Vector3(-hx, h, 0f);
            var ridgeR = new Vector3(hx, h, 0f);
            // Front slope (+z) and back slope (-z), wound to face outwards.
            Quad(new Vector3(-hx, 0f, hz), ridgeL, ridgeR, new Vector3(hx, 0f, hz),
                new Vector2(0f, 0f), new Vector2(0f, slope), new Vector2(size.x, slope), new Vector2(size.x, 0f));
            Quad(new Vector3(hx, 0f, -hz), ridgeR, ridgeL, new Vector3(-hx, 0f, -hz),
                new Vector2(0f, 0f), new Vector2(0f, slope), new Vector2(size.x, slope), new Vector2(size.x, 0f));
            // Gable ends.
            Tri(new Vector3(-hx, 0f, -hz), ridgeL, new Vector3(-hx, 0f, hz),
                new Vector2(0f, 0f), new Vector2(hz, h), new Vector2(size.z, 0f));
            Tri(new Vector3(hx, 0f, hz), ridgeR, new Vector3(hx, 0f, -hz),
                new Vector2(0f, 0f), new Vector2(hz, h), new Vector2(size.z, 0f));

            return Finish(vertices, uvs, triangles, new Vector3(0f, h * 0.3f, 0f));
        }
    }
}
