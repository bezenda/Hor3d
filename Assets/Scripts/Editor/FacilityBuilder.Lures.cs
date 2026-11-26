using UnityEditor;
using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Phase 3 (2026-11-12): more ways to play the enemy. Hiding under desks (low view, peeking out between the
    /// legs) next to the lockers, and a steam release valve in every hall (NoiseLure): turn it, get clear, and the
    /// venting draws the enemy there. Made once by name; wall spots were measured free on 2026-11-12.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private struct DeskSpot
        {
            public string desk;
            public string name;
            public DeskSpot(string desk, string name) { this.desk = desk; this.name = name; }
        }

        private static readonly DeskSpot[] DeskSpots =
        {
            new DeskSpot("Facility/Electrical/Props/Furn_Desk", "Hide_Desk_Janitor"),
            new DeskSpot("Facility/Lab/Office/Furn_Desk", "Hide_Desk_Office"),
            new DeskSpot("Facility/Lab/Office/Dressing/Office_04_Furn_Desk", "Hide_Desk_Office_Island"),
            new DeskSpot("Facility/Electrical/BreakerRoom/Furn_Desk", "Hide_Desk_Breaker"),
        };

        private static int SetUpHidingAndLures()
        {
            int added = 0;
            added += LineLockerInside();
            added += DeskHidingSpots();

            Transform lures = Group("Quests/Lures");
            added += SteamLure(lures, "Steam_Storage", new Vector3(0.55f, 0f, 6f), Vector3.right);
            added += SteamLure(lures, "Steam_Assembly", new Vector3(90f, 0f, 29.43f), Vector3.back);
            added += SteamLure(lures, "Steam_Lab", new Vector3(66f, 0f, -41.43f), Vector3.forward); // lab N wall x 54..60 is the ring airlock
            added += SteamLure(lures, "Steam_Electrical", new Vector3(0.15f, 0f, -21f), Vector3.right);
            return added;
        }

        /// <summary>
        /// The steel locker model is single-sided: from inside, its left wall did not render and the room showed
        /// through (owner, 2026-11-13). Thin double-faced panels just inside the shell line it: both sides, back
        /// and roof. Mesh bounds: x +-0.56, z -0.36..0.39, y 0..2.14. No colliders (the old boxes keep those).
        /// </summary>
        private static int LineLockerInside()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(LockerPrefabPath);
            if (asset == null || asset.transform.Find("Visuals/Lining") != null)
            {
                return 0;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(LockerPrefabPath);
            try
            {
                Transform visuals = contents.transform.Find("Visuals");
                Renderer model = visuals != null ? visuals.Find("Model")?.GetComponent<Renderer>() : null;
                if (visuals == null || model == null)
                {
                    return 0;
                }

                Material material = model.sharedMaterials.Length > 0 ? model.sharedMaterials[0] : MetalMid;
                var lining = new GameObject("Lining").transform;
                lining.SetParent(visuals, false);
                Part(lining, "Left", PrimitiveType.Cube, new Vector3(-0.515f, 1.07f, 0.0f), Vector3.zero, new Vector3(0.012f, 1.98f, 0.66f), material);
                Part(lining, "Right", PrimitiveType.Cube, new Vector3(0.515f, 1.07f, 0.0f), Vector3.zero, new Vector3(0.012f, 1.98f, 0.66f), material);
                Part(lining, "Back", PrimitiveType.Cube, new Vector3(0f, 1.07f, -0.325f), Vector3.zero, new Vector3(1.02f, 1.98f, 0.012f), material);
                Part(lining, "Roof", PrimitiveType.Cube, new Vector3(0f, 2.05f, 0.0f), Vector3.zero, new Vector3(1.02f, 0.012f, 0.66f), material);
                foreach (Renderer r in lining.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, LockerPrefabPath);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>The desk model has drawers on its local -x side; the knee space is local +x, open to +z.</summary>
        private static int DeskHidingSpots()
        {
            GameObject spotsRoot = GameObject.Find("HidingSpots");
            if (spotsRoot == null)
            {
                return 0;
            }

            int added = 0;
            foreach (DeskSpot spot in DeskSpots)
            {
                if (spotsRoot.transform.Find(spot.name) != null)
                {
                    continue;
                }

                Transform desk = FindDesk(spot.desk);
                if (desk == null)
                {
                    Debug.LogWarning("[Facility] No desk for " + spot.name + " at " + spot.desk);
                    continue;
                }

                Bounds bounds = RendererBounds(desk);
                Quaternion facing = Quaternion.Euler(0f, desk.eulerAngles.y, 0f);
                Vector3 floor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + facing * new Vector3(0.4f, 0f, 0.02f);

                var go = new GameObject(spot.name);
                go.transform.SetParent(spotsRoot.transform, false);
                go.transform.SetPositionAndRotation(floor + Vector3.up * 0.35f, facing);
                BoxCollider aim = go.AddComponent<BoxCollider>();
                aim.isTrigger = true;
                aim.size = new Vector3(0.7f, 0.6f, 0.7f);

                var hide = new GameObject("HidePoint");
                hide.transform.SetParent(go.transform, false);
                hide.transform.SetPositionAndRotation(floor, facing);

                HidingSpot hiding = go.AddComponent<HidingSpot>();
                var so = new SerializedObject(hiding);
                so.FindProperty("hidePoint").objectReferenceValue = hide.transform;
                so.FindProperty("eyeHeight").floatValue = 0.5f;
                so.FindProperty("enterPrompt").stringValue = "Залезть под стол";
                so.FindProperty("exitPrompt").stringValue = "Crawl out";
                so.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }

            return added;
        }

        private static Transform FindDesk(string path)
        {
            GameObject found = GameObject.Find(path);
            if (found != null)
            {
                return found.transform;
            }

            // The janitor's desk lives under whichever props group the dressing pass used.
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == leaf && path.Contains(t.parent != null ? t.parent.name : "#"))
                {
                    return t;
                }
            }

            return null;
        }

        private static Bounds RendererBounds(Transform t)
        {
            Bounds bounds = new Bounds(t.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (first)
                {
                    bounds = r.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return bounds;
        }

        /// <summary>A riser pipe with a red release lever and a gauge, venting towards <paramref name="outward"/>.</summary>
        private static int SteamLure(Transform parent, string name, Vector3 wallFoot, Vector3 outward)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);
            var lure = new GameObject(name);
            lure.transform.SetParent(parent, false);
            lure.transform.SetPositionAndRotation(wallFoot + outward * 0.14f + Vector3.up * 1.25f, facing);

            Vector3 riser = wallFoot + outward * 0.1f;
            Cyl(lure.transform, "Riser", riser + Vector3.up * 1.4f, 0.11f, 2.8f, MetalMid, false);
            // Yellow and black, a lever not a wheel, and a STEAM VENT plate: it must not read as a puzzle valve.
            Part(lure.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), Vector3.zero, new Vector3(0.22f, 0.22f, 0.1f), PaintYellow);
            Part(lure.transform, "Stripe", PrimitiveType.Cube, new Vector3(0f, 0f, 0.072f), new Vector3(0f, 0f, 45f), new Vector3(0.05f, 0.3f, 0.005f), Rubber);
            Part(lure.transform, "Lever", PrimitiveType.Cube, new Vector3(0.14f, 0.08f, 0.09f), new Vector3(0f, 0f, 35f), new Vector3(0.26f, 0.04f, 0.04f), MetalDark);
            Part(lure.transform, "Gauge", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0.04f), new Vector3(90f, 0f, 0f), new Vector3(0.1f, 0.01f, 0.1f), Plastic);
            Part(lure.transform, "Nozzle", PrimitiveType.Cylinder, new Vector3(0f, -0.14f, 0.1f), new Vector3(60f, 0f, 0f), new Vector3(0.05f, 0.06f, 0.05f), MetalMid);
            Box(lure.transform, "Tag", wallFoot + outward * 0.012f + Vector3.up * 1.72f, new Vector3(0.5f, 0.14f, 0.012f), PaintYellow, false,
                Quaternion.LookRotation(outward).eulerAngles.y);
            LabelFacing(lure.transform, "TagText", wallFoot + outward * 0.022f + Vector3.up * 1.72f, "СБРОС ПАРА", 0.05f, Color.black, outward);

            BoxCollider aim = lure.AddComponent<BoxCollider>();
            aim.center = new Vector3(0f, 0.05f, 0.05f);
            aim.size = new Vector3(0.45f, 0.55f, 0.3f);

            ParticleSystem steam = SteamParticles(lure.transform);
            NoiseLure noise = lure.AddComponent<NoiseLure>();
            var so = new SerializedObject(noise);
            so.FindProperty("steam").objectReferenceValue = steam;
            so.FindProperty("prompt").stringValue = "Стравить пар (громко — приманит его сюда)";
            so.FindProperty("armedMessage").stringValue = "Давление растёт. Отойди подальше — оно это услышит. Это лишь отвлечение, к выходу не ведёт.";
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>World-space letters readable by someone facing the wall (looking along -outward).</summary>
        private static void LabelFacing(Transform parent, string name, Vector3 position, string text, float size, Color color, Vector3 outward)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward));
            TextMesh mesh = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static ParticleSystem SteamParticles(Transform parent)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -0.18f, 0.14f);
            go.transform.localRotation = Quaternion.Euler(20f, 0f, 0f); // out of the wall, a little down

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new Color(0.85f, 0.88f, 0.9f, 0.18f);
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 70f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.03f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.2f));

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
