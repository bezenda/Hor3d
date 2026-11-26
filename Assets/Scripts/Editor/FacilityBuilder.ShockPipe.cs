using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The live pipe (2026-11-26). The kit's "Pipes 03" by the janitor's closet (Janitor/Props/Pipes) hangs across the
    /// aisle at head height, a placement slip the owner wanted kept as a feature: its wall clamps are stripped from the
    /// mesh (a copy, <c>Pipe_NoClamps.asset</c>; the kit prefab is untouched), a few frayed wires poke out of its free
    /// end, and a <see cref="ShockHazard"/> sparks there now and then and shocks a player who walks into it.
    /// Idempotent: the mesh copy is made once, everything else is found by name and re-pointed each run.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string LivePipeName = "LivePipe";

        private static int SetUpShockPipe()
        {
            Transform pipe = root.Find("Janitor/Props/Pipes");
            MeshFilter filter = pipe != null ? pipe.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                return 0;
            }

            int changed = 0;
            Mesh stripped = StrippedPipeMesh(filter.sharedMesh);
            if (stripped != null && filter.sharedMesh != stripped)
            {
                filter.sharedMesh = stripped;
                changed++;
            }

            // The horizontal run in world space: the lowest vertices (the riser climbs from its end into the wall).
            Vector3[] vertices = filter.sharedMesh.vertices;
            float minY = float.MaxValue;
            var world = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                world[i] = pipe.TransformPoint(vertices[i]);
                minY = Mathf.Min(minY, world[i].y);
            }

            Bounds run = default;
            Bounds riser = default;
            bool haveRun = false;
            bool haveRiser = false;
            foreach (Vector3 v in world)
            {
                if (v.y < minY + 0.2f)
                {
                    if (!haveRun) { run = new Bounds(v, Vector3.zero); haveRun = true; }
                    else run.Encapsulate(v);
                }
                else if (v.y > minY + 0.5f)
                {
                    if (!haveRiser) { riser = new Bounds(v, Vector3.zero); haveRiser = true; }
                    else riser.Encapsulate(v);
                }
            }

            if (!haveRun)
            {
                return changed;
            }

            // Along the longer horizontal side; the free end is the one away from the riser.
            bool alongZ = run.size.z >= run.size.x;
            Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
            Vector3 endA = run.center - axis * (alongZ ? run.extents.z : run.extents.x);
            Vector3 endB = run.center + axis * (alongZ ? run.extents.z : run.extents.x);
            Vector3 riserAt = haveRiser ? riser.center : endA;
            riserAt.y = run.center.y;
            Vector3 freeEnd = (endA - riserAt).sqrMagnitude > (endB - riserAt).sqrMagnitude ? endA : endB;
            Vector3 outward = (freeEnd - run.center).normalized;

            Transform live = pipe.Find(LivePipeName);
            if (live == null)
            {
                live = new GameObject(LivePipeName).transform;
                live.SetParent(pipe, false);
                changed++;
            }

            live.SetPositionAndRotation(run.center, Quaternion.identity);
            live.localScale = new Vector3(1f / pipe.lossyScale.x, 1f / pipe.lossyScale.y, 1f / pipe.lossyScale.z);

            BoxCollider zone = live.GetComponent<BoxCollider>();
            if (zone == null)
            {
                zone = live.gameObject.AddComponent<BoxCollider>();
                changed++;
            }

            zone.isTrigger = true;
            zone.enabled = false;
            zone.center = Vector3.zero;
            // A little proud of the pipe so brushing it counts; not much lower, so a crouch passes underneath.
            zone.size = new Vector3(run.size.x + (alongZ ? 0.12f : 0.02f), run.size.y + 0.1f, run.size.z + (alongZ ? 0.02f : 0.12f));

            // The break sits where the pipe is in the open: its far end disappears into a shelf, so the middle of the
            // longest stretch with nothing around it (the aisle) is where sparks are seen.
            Vector3 crack = OpenStretchMiddle(endA, endB, pipe) ?? freeEnd;
            float diameter = Mathf.Max(run.size.y, alongZ ? run.size.x : run.size.z);

            Transform spark = live.Find("SparkPoint");
            if (spark == null)
            {
                spark = new GameObject("SparkPoint").transform;
                spark.SetParent(live, false);
                changed++;
            }

            spark.position = crack - Vector3.up * (diameter * 0.35f);

            changed += FrayedWires(live, crack, axis, diameter);

            ShockHazard hazard = live.GetComponent<ShockHazard>();
            if (hazard == null)
            {
                hazard = live.gameObject.AddComponent<ShockHazard>();
                changed++;
            }

            var so = new SerializedObject(hazard);
            so.FindProperty("zone").objectReferenceValue = zone;
            so.FindProperty("sparkPoint").objectReferenceValue = spark;
            so.FindProperty("sparkMaterial").objectReferenceValue = SparkMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        /// <summary>The kit pipe without its clamps: only the largest connected piece of the mesh is kept.</summary>
        private static Mesh StrippedPipeMesh(Mesh source)
        {
            string folder = ArtFolder + "/Meshes";
            string path = folder + "/Pipe_NoClamps.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Stage1PlaygroundBuilder.EnsureFolder(ArtFolder, "Meshes");
            }

            Vector3[] v = source.vertices;
            int[] triangles = source.triangles;
            var parent = new int[v.Length];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            void Join(int a, int b) => parent[Find(a)] = Find(b);

            // Split normals/UVs duplicate positions: weld by position first, then by triangle.
            var byPosition = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
                if (byPosition.TryGetValue(key, out int first)) Join(i, first);
                else byPosition[key] = i;
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Join(triangles[i], triangles[i + 1]);
                Join(triangles[i + 1], triangles[i + 2]);
            }

            var sizes = new Dictionary<int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                int r = Find(i);
                sizes[r] = sizes.TryGetValue(r, out int n) ? n + 1 : 1;
            }

            int keep = -1;
            int best = 0;
            foreach (KeyValuePair<int, int> pair in sizes)
            {
                if (pair.Value > best)
                {
                    best = pair.Value;
                    keep = pair.Key;
                }
            }

            var kept = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (Find(triangles[i]) == keep)
                {
                    kept.Add(triangles[i]);
                    kept.Add(triangles[i + 1]);
                    kept.Add(triangles[i + 2]);
                }
            }

            Mesh mesh = Object.Instantiate(source);
            mesh.name = "Pipe_NoClamps";
            mesh.subMeshCount = 1;
            mesh.SetTriangles(kept, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            Debug.Log($"[Facility] Live pipe: kept {best} of {v.Length} vertices (the clamps are gone).");
            return mesh;
        }

        /// <summary>Middle of the longest run between two points with no collider within 0.25 m (null: none).</summary>
        private static Vector3? OpenStretchMiddle(Vector3 a, Vector3 b, Transform ignore)
        {
            Physics.SyncTransforms();
            const float step = 0.1f;
            int samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
            int bestStart = -1, bestLength = 0, runStart = -1;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / samples);
                bool open = true;
                foreach (Collider c in Physics.OverlapSphere(p, 0.25f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!c.transform.IsChildOf(ignore) && c.GetComponentInParent<CharacterController>() == null)
                    {
                        open = false;
                        break;
                    }
                }

                if (open && runStart < 0) runStart = i;
                if ((!open || i == samples) && runStart >= 0)
                {
                    int end = open ? i : i - 1;
                    if (end - runStart + 1 > bestLength)
                    {
                        bestLength = end - runStart + 1;
                        bestStart = runStart;
                    }

                    runStart = -1;
                }
            }

            if (bestStart < 0)
            {
                return null;
            }

            return Vector3.Lerp(a, b, (bestStart + (bestLength - 1) * 0.5f) / samples);
        }

        /// <summary>
        /// A few thin wires out of a split in the pipe, splayed sideways and down so they are there to see from the aisle
        /// but easy to miss in the dark; dark insulation, copper tips. Rebuilt when the split moves.
        /// </summary>
        private static int FrayedWires(Transform live, Vector3 crack, Vector3 axis, float pipeDiameter)
        {
            Transform wires = live.Find("Wires");
            if (wires != null && Vector3.Distance(wires.position, crack) < 0.01f)
            {
                return 0;
            }

            if (wires != null)
            {
                Object.DestroyImmediate(wires.gameObject);
            }

            wires = new GameObject("Wires").transform;
            wires.SetParent(live, false);
            wires.position = crack;
            Material copper = Mat("M_Copper", new Color(0.55f, 0.27f, 0.12f), 0.6f, 0.9f);

            var random = new System.Random(2611);
            float r = pipeDiameter * 0.3f;
            Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;
            for (int i = 0; i < 4; i++)
            {
                // Around the underside of the pipe, alternating sides, leaning a little along it.
                float angle = -Mathf.PI * 0.5f + (i - 1.5f) * 0.7f + ((float)random.NextDouble() - 0.5f) * 0.3f;
                Vector3 radial = side * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                Vector3 start = crack + axis * (((float)random.NextDouble() - 0.5f) * 0.05f) + radial * r;
                Vector3 dir = (radial + axis * (((float)random.NextDouble() - 0.5f) * 0.8f) + Vector3.down * 0.4f).normalized;
                float length = 0.06f + (float)random.NextDouble() * 0.1f;
                float thick = 0.007f + (float)random.NextDouble() * 0.004f;
                Vector3 euler = Quaternion.FromToRotation(Vector3.up, dir).eulerAngles;

                Cyl(wires, "Wire_" + i, start + dir * (length * 0.5f), thick, length, Rubber, false, euler);
                Cyl(wires, "Tip_" + i, start + dir * (length + 0.006f), thick * 0.6f, 0.012f, copper, false, euler);
            }

            foreach (Transform child in wires)
            {
                child.gameObject.isStatic = false;
                child.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return 1;
        }

        /// <summary>Additive streak material for the sparks (the built-in soft particle dot, warm white).</summary>
        private static Material SparkMaterial()
        {
            string path = ArtFolder + "/M_Spark.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_Spark" };
            material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            material.SetColor("_BaseColor", new Color(2.4f, 2f, 1.5f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
