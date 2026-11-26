using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Modelled props (made in Blender by the scripts in Tools/Blender, exported as FBX to
    /// Assets/Art/Generated/Facility/Models) and the research lab that is built from them.
    ///
    /// Axes of an imported model: Blender x/y/z become Unity -x / -z / y, so a wall panel runs
    /// from its pivot towards local -x and stands out of the wall towards local -z. Material
    /// slots are named by role (Metal_Dark, Glass, Emit_Cyan, ...) and remapped on import to the
    /// facility's URP materials, which carry a generated wear texture (UVs: 1 unit = 2 m).
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string ModelFolder = ArtFolder + "/Models";
        private const string ModelImportVersion = "facility-models-v1";
        private const string LabVersionName = "Lab_v2";

        private static readonly string[] ModelsWithoutCollider =
        {
            "Lab_FloorTrench", "Elec_CableTray", "Prop_RubberMat", "Prop_WallClock", "Prop_CorkBoard", "Horror_CoveredBody2", "Horror_BodyBag2", "Door_Breaker", "Breaker_Panel", "Breaker_SwitchBase", "Breaker_Lever", "Item_Battery", "Prop_EmergencyLamp", "Hall_ChainHoist", "Ceiling_Truss", "Elec_CableBundle",
            // Horror pass: decals and the throwables' looks (the items carry their own colliders).
            "Item_Bottle", "Item_Can", "Horror_BloodPool", "Horror_BloodTrail", "Horror_ClawMarks", "Horror_Papers",
        };

        // ------------------------------------------------------------- materials

        private static Material LabWhite => Mat("M_Lab_White", new Color(0.62f, 0.64f, 0.66f), 0.5f, 0.05f);
        private static Material Grate => Mat("M_Grate", new Color(0.2f, 0.21f, 0.22f), 0.45f, 0.75f);
        private static Material EmitCyan => Mat("M_Emit_Cyan", new Color(0.05f, 0.3f, 0.4f), 0.4f, 0f,
            new Color(0.2f, 0.8f, 1f) * 2.2f);

        private static Dictionary<string, Material> SlotMaterials()
        {
            return new Dictionary<string, Material>
            {
                { "Metal_Dark", MetalDark }, { "Metal_Mid", MetalMid }, { "Paint_White", LabWhite }, { "Skin", Skin }, { "Bag", BodyBagVinyl },
                { "Paint_Yellow", PaintYellow }, { "Rubber", Rubber }, { "Glass", Glass }, { "Liquid", Liquid },
                { "Emit_Cyan", EmitCyan }, { "Emit_Red", WarningRed }, { "Screen", ScreenOff }, { "Grate", Grate },
                { "Paint_Blue", RackBlue }, { "Paint_Red", PaintRed }, { "Cardboard", Cardboard }, { "Wood", Wood },
                { "Wrap", Wrap }, { "Blood", Blood }, { "Blood_Dry", BloodDry }, { "Sheet", Sheet }, { "Cloth", Cloth },
                { "Paper", Paper }, { "Bottle_Glass", BottleGlass }, { "Tin", Tin },
            };
        }

        /// <summary>A tileable grime texture: blotchy value noise, streaks and scratches, drawn once.</summary>
        private static Texture2D WearTexture()
        {
            string path = ArtFolder + "/Tex_Wear.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 512;
            var pixels = new Color32[size * size];
            var rng = new System.Random(1234);
            float[] lattice = new float[64 * 64];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rng.NextDouble();

            float Noise(float u, float v, int cells)
            {
                float x = u * cells, y = v * cells;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                float L(int a, int b) => lattice[((b % cells + cells) % cells) * 64 + ((a % cells + cells) % cells)];
                return Mathf.Lerp(Mathf.Lerp(L(x0, y0), L(x0 + 1, y0), fx), Mathf.Lerp(L(x0, y0 + 1), L(x0 + 1, y0 + 1), fx), fy);
            }

            var value = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Noise(u, v, 4) * 0.45f + Noise(u, v, 8) * 0.25f + Noise(u, v, 16) * 0.18f + Noise(u, v, 48) * 0.12f;
                    float streak = Noise(u * 0.25f, v, 32);                        // vertical run marks
                    value[y * size + x] = 0.78f + (n - 0.5f) * 0.38f - Mathf.Max(0f, streak - 0.62f) * 0.5f;
                }
            }

            for (int s = 0; s < 260; s++)
            {
                int x = rng.Next(size), y = rng.Next(size);
                float angle = (float)rng.NextDouble() * Mathf.PI;
                int length = 6 + rng.Next(40);
                for (int k = 0; k < length; k++)
                {
                    int px = ((int)(x + Mathf.Cos(angle) * k) % size + size) % size;
                    int py = ((int)(y + Mathf.Sin(angle) * k) % size + size) % size;
                    value[py * size + px] += 0.16f;
                }
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(value[i] * 255f), 0, 255);
                pixels[i] = new Color32(b, b, b, 255);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Puts the wear texture on the opaque surface materials (once; hand-set maps are kept).</summary>
        private static void ApplyWear()
        {
            Texture2D wear = WearTexture();
            foreach (Material m in new[] { MetalDark, MetalMid, LabWhite, PaintYellow, PaintRed, Grate, Rubber })
            {
                if (m != null && m.GetTexture("_BaseMap") == null)
                {
                    m.SetTexture("_BaseMap", wear);
                    EditorUtility.SetDirty(m);
                }
            }
        }

        /// <summary>Import settings for every modelled prop: no animation, slot materials remapped.</summary>
        private static void ConfigureModels()
        {
            if (!AssetDatabase.IsValidFolder(ModelFolder))
            {
                return;
            }

            Dictionary<string, Material> slots = SlotMaterials();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.userData == ModelImportVersion)
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = System.Array.IndexOf(ModelsWithoutCollider, name) < 0 && !name.StartsWith("Prop2_");
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (KeyValuePair<string, Material> slot in slots)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot.Key), slot.Value);
                }

                importer.userData = ModelImportVersion;
                importer.SaveAndReimport();
            }
        }

        private static GameObject Model(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/" + name + ".fbx");
        }

        /// <summary>Places a modelled prop once (by name under the parent).</summary>
        private static int PutModel(Transform parent, string name, string model, Vector3 position, float yaw)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            GameObject source = Model(model);
            if (source == null)
            {
                Debug.LogWarning("[Facility] Model missing (run the Blender scripts in Tools/Blender): " + model);
                return 0;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = true;
            }

            return 1;
        }

        /// <summary>
        /// A 6 m wall panel on the room side of a kit wall segment. The panel runs from its pivot
        /// towards local -x and stands 0.51 m out of the wall towards local -z.
        /// </summary>
        private static int WallPanel(Transform parent, string name, string model, Side side, float wallCoord, float segment,
            float y = 0f)
        {
            const float face = 0.15f; // half the kit wall's thickness
            Vector3 pivot;
            float yaw;
            switch (side)
            {
                case Side.West: pivot = new Vector3(wallCoord + face, y, segment + Module); yaw = -90f; break;
                case Side.East: pivot = new Vector3(wallCoord - face, y, segment); yaw = 90f; break;
                case Side.South: pivot = new Vector3(segment, y, wallCoord + face); yaw = 180f; break;
                default: pivot = new Vector3(segment + Module, y, wallCoord - face); yaw = 0f; break;
            }

            return PutModel(parent, name, model, pivot, yaw);
        }

        // ------------------------------------------------------------------- lab

        private static readonly float[] CapsuleXs = { 54f, 60f, 66f, 72f, 78f };
        private const float CapsuleRowNorth = -17.5f;
        private const float CapsuleRowSouth = -36.5f;
        internal static readonly Vector3 ContainmentCenter = new Vector3(66f, 0f, -27f);

        /// <summary>
        /// Research lab v2: clad walls with light strips, two rows of modelled containment
        /// capsules over glowing floor trenches, the broken central chamber with a control desk on
        /// each side, trusses with sagging cables under the roof, benches along the walls, a server
        /// row in the south-east, mist and drifting dust. The office keeps its own furniture.
        /// </summary>
        private static int DressLab()
        {
            int added = 0;
            added += RemoveLabV1();

            Transform walls = Group("Lab/Walls");
            foreach (float s in new[] { -42f, -36f, -24f, -18f })
                added += WallPanel(walls, $"W_{s:0}", "Lab_WallPanel", Side.West, 48f, s);
            foreach (float s in new[] { 48f, 60f, 66f, 72f, 78f })
                added += WallPanel(walls, $"N_{s:0}", "Lab_WallPanel", Side.North, -12f, s);
            foreach (float s in new[] { 48f, 54f, 60f, 66f, 72f, 78f, 84f, 90f })
                added += WallPanel(walls, $"S_{s:0}", "Lab_WallPanel", Side.South, -42f, s);
            foreach (float s in new[] { -42f, -36f, -30f })
                added += WallPanel(walls, $"E_{s:0}", "Lab_WallPanel", Side.East, 96f, s);

            Transform t = Group("Lab/Props");

            // Capsule rows: the north row faces south, the south row north (console towards the aisle).
            foreach (float x in CapsuleXs)
            {
                bool broken = Mathf.Approximately(x, 60f);
                added += PutModel(t, $"Capsule_N_{x:0}", "Lab_CapsuleTank", new Vector3(x, 0f, CapsuleRowNorth), 180f);
                added += PutModel(t, $"Capsule_S_{x:0}", broken ? "Lab_CapsuleTank_Broken" : "Lab_CapsuleTank",
                    new Vector3(x, 0f, CapsuleRowSouth), 0f);
            }

            // Floor trenches in front of each row, and one from the west door to the chamber.
            for (int k = 0; k < 5; k++)
            {
                float x = 57f + k * 6f;
                added += PutModel(t, $"Trench_N_{k}", "Lab_FloorTrench", new Vector3(x, 0f, -20.6f), 0f);
                added += PutModel(t, $"Trench_S_{k}", "Lab_FloorTrench", new Vector3(x, 0f, -33.4f), 0f);
            }

            added += PutModel(t, "Trench_W_0", "Lab_FloorTrench", new Vector3(55f, 0f, -27f), 0f);

            added += PutModel(t, "Containment", "Lab_Containment", ContainmentCenter, 0f);
            added += PutModel(t, "Console_W", "Lab_Console", new Vector3(59.6f, 0f, -27f), -90f);
            added += PutModel(t, "Console_E", "Lab_Console", new Vector3(72.4f, 0f, -27f), 90f);
            added += LabTerminalNote();

            // Trusses under the roof (they hang 0.85 m below their pivot; cables sag further).
            for (int k = 0; k < 7; k++)
            {
                float x = 56f + k * 6f;
                if (k < 5) // the north row stops at the office's glass wall (x 84)
                    added += PutModel(t, $"Truss_N_{k}", "Ceiling_Truss", new Vector3(x, 8.75f, -22.5f), 0f);
                added += PutModel(t, $"Truss_S_{k}", "Ceiling_Truss", new Vector3(x, 8.75f, -31.5f), 0f);
            }

            // Benches: the front edge sits at the pivot, the back 0.85 m behind it against the panels.
            foreach (float x in new[] { 63f, 66f, 72f, 75f, 78f }) // clear of the wall fan at x 54..60
                added += PutModel(t, $"Bench_N_{x:0}", "Lab_Bench", new Vector3(x, 0f, -13.5f), 180f);
            foreach (float x in new[] { 53f, 56f, 62f, 65f, 73f, 76f, 79f })
                added += PutModel(t, $"Bench_S_{x:0}", "Lab_Bench", new Vector3(x, 0f, -40.5f), 0f);

            // Server row along the east wall, south of the office.
            for (int k = 0; k < 8; k++)
                added += PutModel(t, $"Rack_{k}", "Lab_ServerRack", new Vector3(94.84f, 0f, -40.4f + k * 0.82f), -90f);
            for (int k = 0; k < 5; k++)
                added += PutModel(t, $"Rack_B_{k}", "Lab_ServerRack", new Vector3(94.84f, 0f, -29.5f + k * 0.82f), -90f);

            added += Put(t, "Drum_1", "Barrel", new Vector3(86.5f, 0f, -40.8f), 0f);
            added += Put(t, "Drum_2", "Barrel", new Vector3(87.4f, 0f, -40.6f), 50f);
            added += Put(t, "Drum_3", "Barrel", new Vector3(86.9f, 0f, -39.9f), 120f);
            added += Put(t, "Cart", "Cart", new Vector3(83f, 0f, -30f), 200f);
            added += Put(t, "Exit_W", "Exit Sign", new Vector3(48.6f, 3.6f, -27f), 0f);

            added += LabAtmosphere();
            added += DressOffice();
            return added;
        }

        /// <summary>The office (glass walls, keycard on the desk): desk, monitor, cabinets, a frantic whiteboard.</summary>
        private static int DressOffice()
        {
            Transform o = Group("Lab/Office");
            int added = 0;
            added += PutModel(o, "Furn_Desk", "Furn_Desk", new Vector3(90f, 0f, -12.8f), 180f);
            added += PutModel(o, "Furn_Monitor", "Furn_Monitor", new Vector3(90.35f, 0.78f, -12.6f), 180f);
            added += PutModel(o, "Furn_Chair", "Furn_OfficeChair_Fallen", new Vector3(91.3f, 0f, -14.5f), 40f);
            added += PutModel(o, "Furn_Filing_0", "Furn_FilingCabinet", new Vector3(95.5f, 0f, -22.4f), -90f);
            added += PutModel(o, "Furn_Filing_1", "Furn_FilingCabinet_Open", new Vector3(95.5f, 0f, -21.85f), -90f);
            added += PutModel(o, "Furn_Filing_2", "Furn_FilingCabinet", new Vector3(95.5f, 0f, -21.3f), -90f);
            added += PutModel(o, "Furn_Whiteboard", "Furn_Whiteboard", new Vector3(95.82f, 0f, -17f), -90f);
            added += PutModel(o, "Horror_Papers", "Horror_Papers", new Vector3(88.6f, 0f, -16.4f), 70f);
            return added;
        }

        /// <summary>Low mist around the broken chamber and dust drifting through the light.</summary>
        private static int LabAtmosphere()
        {
            Transform t = Group("Lab/Atmosphere");
            int added = 0;
            if (t.Find("Mist") == null)
            {
                ParticleSystem mist = Particles(t, "Mist", ContainmentCenter + new Vector3(0f, 0.3f, 0f), AtmosphereAssets.FogPuff,
                    new Color(0.35f, 0.75f, 0.6f, 0.11f), new Vector3(16f, 0.4f, 14f), 40, 2.2f, 4.5f, 14f, 0.06f);
                added += mist != null ? 1 : 0;
            }

            if (t.Find("Dust") == null)
            {
                ParticleSystem dust = Particles(t, "Dust", new Vector3(72f, 4f, -27f), AtmosphereAssets.Dust,
                    new Color(0.85f, 0.95f, 1f, 0.35f), new Vector3(46f, 7f, 28f), 220, 0.02f, 0.05f, 20f, 0.05f);
                added += dust != null ? 1 : 0;
            }

            return added;
        }

        private static ParticleSystem Particles(Transform parent, string name, Vector3 center, Material material, Color color,
            Vector3 box, int count, float minSize, float maxSize, float lifetime, float speed)
        {
            if (material == null)
            {
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = count / lifetime;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = speed * 2f;
            noise.frequency = 0.2f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>
        /// Lab v1 was built from primitives. Once, before v2 is laid: remove its props and capsule
        /// lights, and move what stood against the walls (lockers, the lab terminal note, the
        /// battery and bottle on the old benches, the enemy start) clear of the new panels.
        /// </summary>
        private static int RemoveLabV1()
        {
            Transform lab = Group("Lab");
            if (lab.Find(LabVersionName) != null)
            {
                return 0;
            }

            int changed = 0;
            Transform props = lab.Find("Props");
            if (props != null)
            {
                Object.DestroyImmediate(props.gameObject);
                changed++;
            }

            Transform lights = root.Find("Lights");
            if (lights != null)
            {
                for (int i = lights.childCount - 1; i >= 0; i--)
                {
                    Transform child = lights.GetChild(i);
                    if (child.name.StartsWith("Capsule_") || child.name == "Containment")
                    {
                        Object.DestroyImmediate(child.gameObject);
                        changed++;
                    }
                }
            }

            changed += MoveIfFound("Locker_Lab_W", new Vector3(49.1f, 0f, -39f));
            changed += MoveIfFound("Locker_Lab_N", new Vector3(81.5f, 0f, -13.1f));
            changed += MoveIfFound("Locker_Lab_S", new Vector3(68f, 0f, -40.9f));
            changed += MoveIfFound("Battery_5", new Vector3(74.5f, 0.97f, -40.9f));
            changed += MoveIfFound("Throwable_Bottle_6", new Vector3(61.5f, 1.0f, -13.1f));
            changed += DestroyNamed("Terminal_Lab");
            changed += MoveIfFound("Enemy_Patroller", EnemyStart); // v1's start is inside the new chamber

            var marker = new GameObject(LabVersionName);
            marker.SetActive(false);
            marker.transform.SetParent(lab, false);
            return changed + 1;
        }

        private static int MoveIfFound(string name, Vector3 position)
        {
            GameObject target = GameObject.Find(name);
            if (target == null)
            {
                return 0;
            }

            target.transform.position = position;
            return 1;
        }

        /// <summary>The lab terminal note now lives on the west console (always readable: backup power).</summary>
        private static int LabTerminalNote()
        {
            Transform console = root.Find("Lab/Props/Console_W");
            if (console == null)
            {
                return 0;
            }

            InfoPoint existing = console.GetComponent<InfoPoint>();
            if (existing != null)
            {
                // Playtest 2026-11-11: the terminal shows the facility plan, not the stun tip.
                var map = new SerializedObject(existing);
                if (map.FindProperty("showMap").boolValue)
                {
                    return 0;
                }

                map.FindProperty("showMap").boolValue = true;
                map.FindProperty("sheetTitle").stringValue = "FACILITY LAYOUT";
                map.ApplyModifiedPropertiesWithoutUndo();
                return 1;
            }

            BoxCollider hit = console.gameObject.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = new Vector3(0f, 1.2f, 0f);
            hit.size = new Vector3(2.4f, 0.8f, 1f);
            InfoPoint info = console.gameObject.AddComponent<InfoPoint>();
            var so = new SerializedObject(info);
            so.FindProperty("prompt").stringValue = "Посмотреть терминал";
            so.FindProperty("showMap").boolValue = true;
            so.FindProperty("sheetTitle").stringValue = "FACILITY LAYOUT";
            so.FindProperty("message").stringValue =
                "B-7 WEEK 31: subject no longer responds to sedation. Reacts violently to noise. A strong flash " +
                "stuns it for several seconds - the only reliable way to get past it. Keep the lab doors sealed.";
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }
    }
}
