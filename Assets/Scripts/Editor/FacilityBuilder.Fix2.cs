using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Playtest 2026-11-11 (second round). Models from Tools/Blender/facility_fix2.py:
    /// breaker panel and switches, battery, wall emergency lamps (the red boxes go), support posts
    /// under the electrical hall's upper stair landing, the workbench off the vent, and more dressing
    /// along the walls of the lab and the assembly hall. One-time blocks, guarded by markers.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string Fix2Name = "Fix2_v1";
        private const string FillName = "Fill_v1";

        private static int Fix2()
        {
            int changed = 0;
            if (root.Find(Fix2Name) == null)
            {
                changed += EmergencyLamps();
                changed += BreakerVisuals();
                changed += BatteryVisuals();
                changed += RevertRiserMaterials();
                changed += MoveWorkbench();
                changed += StairPosts();
                Marker(Fix2Name);
            }

            if (root.Find(FillName) == null)
            {
                ventBounds = null;
                changed += FillAssembly();
                changed += FillLab();
                Marker(FillName);
            }

            return changed;
        }

        private static void Marker(string name)
        {
            var marker = new GameObject(name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
        }

        // ------------------------------------------------------------------ red boxes

        /// <summary>Every lens box goes; where it sat on a wall a modelled beacon takes its place.</summary>
        private static int EmergencyLamps()
        {
            Transform lights = root.Find("Lights");
            GameObject model = Model("Prop_EmergencyLamp");
            if (lights == null)
            {
                return 0;
            }

            int changed = 0;
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var lenses = new List<Transform>();
            foreach (Transform t in lights)
            {
                if (t.name.StartsWith("Emergency_Lens_"))
                {
                    lenses.Add(t);
                }
            }

            foreach (Transform lens in lenses)
            {
                if (!lens.TryGetComponent(out Renderer box))
                {
                    continue;
                }

                bool wasShown = box.enabled;
                box.enabled = false;
                changed++;
                if (!wasShown || model == null)
                {
                    continue;
                }

                bool found = false;
                RaycastHit nearest = default;
                foreach (Vector3 direction in directions)
                {
                    if (Physics.Raycast(lens.position, direction, out RaycastHit hit, 0.4f, ~0, QueryTriggerInteraction.Ignore)
                        && hit.collider.transform != lens && (!found || hit.distance < nearest.distance))
                    {
                        nearest = hit;
                        found = true;
                    }
                }

                if (!found)
                {
                    continue;
                }

                Vector3 normal = new Vector3(nearest.normal.x, 0f, nearest.normal.z).normalized;
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(model);
                lamp.name = lens.name.Replace("Emergency_Lens_", "Emergency_Beacon_");
                lamp.transform.SetParent(lights, false);
                lamp.transform.SetPositionAndRotation(
                    new Vector3(nearest.point.x, lens.position.y, nearest.point.z), Quaternion.LookRotation(normal, Vector3.up));
                lamp.isStatic = true;
            }

            return changed;
        }

        // ------------------------------------------------------------------ breaker panel

        private static int BreakerVisuals()
        {
            GameObject panel = GameObject.Find("BreakerPanel");
            if (panel == null || Model("Breaker_Panel") == null)
            {
                return 0;
            }

            Transform box = panel.transform.Find("Panel");
            if (box != null && box.TryGetComponent(out Renderer boxRenderer))
            {
                boxRenderer.enabled = false;
                AttachModel(panel.transform, "Breaker_Panel", "PanelModel", box.localPosition);
            }

            foreach (PuzzleSwitch toggle in panel.GetComponentsInChildren<PuzzleSwitch>(true))
            {
                Transform t = toggle.transform;
                Hide(t.Find("Body"));
                Hide(t.Find("Lever/Handle"));
                AttachModel(t, "Breaker_SwitchBase", "BaseModel", Vector3.zero);
                Transform lever = t.Find("Lever");
                if (lever != null)
                {
                    AttachModel(lever, "Breaker_Lever", "LeverModel", Vector3.zero);
                }
            }

            return 1;
        }

        private static void Hide(Transform t)
        {
            if (t != null && t.TryGetComponent(out Renderer r))
            {
                r.enabled = false;
            }
        }

        private static void AttachModel(Transform parent, string model, string name, Vector3 localPosition)
        {
            if (parent.Find(name) != null || Model(model) == null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Model(model));
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
        }

        // ------------------------------------------------------------------ batteries

        private static int BatteryVisuals()
        {
            int changed = 0;
            foreach (BatteryPickup battery in Object.FindObjectsByType<BatteryPickup>(FindObjectsInactive.Include))
            {
                Transform t = battery.transform;
                if (t.Find("Model") != null)
                {
                    continue;
                }

                Hide(t.Find("Visuals"));
                Hide(t.Find("Visuals/Tip"));
                Hide(t.Find("Tip"));
                AttachModel(t, "Item_Battery", "Model", Vector3.zero);
                changed++;
            }

            return changed;
        }

        // ------------------------------------------------------------------ pipe risers

        /// <summary>The risers' wheel slot was overridden to plain red; the new model paints it itself.</summary>
        private static int RevertRiserMaterials()
        {
            int changed = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if ((r.name.StartsWith("Riser") || r.name.Contains("PipeRiser")) && PrefabUtility.IsPartOfPrefabInstance(r))
                {
                    PrefabUtility.RevertObjectOverride(r, InteractionMode.AutomatedAction);
                    changed++;
                }
            }

            return changed;
        }

        // ------------------------------------------------------------------ workbench

        /// <summary>The assembly workbench stood in front of the north vent fan (x 58.5..61.5).</summary>
        private static int MoveWorkbench()
        {
            Transform bench = root.Find("Assembly/Props/Furn_Workbench");
            if (bench == null || bench.position.x > 63f)
            {
                return 0;
            }

            Vector3 shift = new Vector3(63.9f - bench.position.x, 0f, 0f);
            Bounds area = new Bounds(bench.position + Vector3.up, new Vector3(3.2f, 2.4f, 1.4f));
            GameObject items = GameObject.Find("Items");
            if (items != null)
            {
                foreach (Transform item in items.transform)
                {
                    if (area.Contains(item.position))
                    {
                        item.position += shift;
                    }
                }
            }

            bench.position += shift;
            return 1;
        }

        // ------------------------------------------------------------------ stair posts

        /// <summary>
        /// The kit stair landing on deck one (electrical hall) has legs that ended 4.5 m above the floor.
        /// Each foot of a raised stair that has nothing under it gets a post down to the floor.
        /// </summary>
        private static int StairPosts()
        {
            GameObject post = Model("Catwalk_Post");
            if (post == null)
            {
                return 0;
            }

            int added = 0;
            foreach (Transform stair in root.GetComponentsInChildren<Transform>(true))
            {
                if (!stair.name.StartsWith("Stair_") || stair.name.Contains("Ramp") || stair.position.y < 0.5f)
                {
                    continue;
                }

                foreach (MeshFilter filter in stair.GetComponentsInChildren<MeshFilter>())
                {
                    foreach (Vector3 foot in Feet(filter))
                    {
                        // Something solid just under the foot (a deck): it stands on that.
                        if (Physics.CheckBox(foot + Vector3.down * 0.2f, new Vector3(0.08f, 0.15f, 0.08f), Quaternion.identity,
                                ~0, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        if (!Physics.Raycast(foot + Vector3.down * 0.05f, Vector3.down, out RaycastHit floor, 12f, ~0,
                                QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(post);
                        instance.name = $"{stair.name}_Post_{added}";
                        instance.transform.SetParent(stair.parent, false);
                        instance.transform.position = floor.point;
                        instance.transform.localScale = new Vector3(1f, (foot.y - floor.point.y) / 4.5f, 1f);
                        instance.isStatic = true;
                        added++;
                    }
                }
            }

            return added;
        }

        /// <summary>Lowest vertices of a mesh, grouped into feet (world space).</summary>
        private static List<Vector3> Feet(MeshFilter filter)
        {
            var feet = new List<Vector3>();
            var counts = new List<int>();
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                return feet;
            }

            Vector3[] vertices = mesh.vertices;
            float lowest = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = filter.transform.TransformPoint(vertices[i]);
                lowest = Mathf.Min(lowest, vertices[i].y);
            }

            foreach (Vector3 v in vertices)
            {
                if (v.y > lowest + 0.03f)
                {
                    continue;
                }

                int k = feet.FindIndex(f => new Vector2(f.x - v.x, f.z - v.z).magnitude < 0.35f);
                if (k < 0)
                {
                    feet.Add(v);
                    counts.Add(1);
                }
                else
                {
                    feet[k] = (feet[k] * counts[k] + v) / (counts[k] + 1);
                    counts[k]++;
                }
            }

            return feet;
        }

        // ------------------------------------------------------------------ dressing

        private struct Spot
        {
            public string Asset;     // a model name, or "kit:" + kit prefab name
            public char Wall;        // N, S, E, W, or ' ' for free-standing
            public float A;          // along the wall (x for N/S, z for E/W) or x when free-standing
            public float B;          // z when free-standing
            public float Yaw;        // free-standing only

            public Spot(string asset, char wall, float a, float b = 0f, float yaw = 0f)
            {
                Asset = asset;
                Wall = wall;
                A = a;
                B = b;
                Yaw = yaw;
            }
        }

        private static int FillAssembly()
        {
            Transform parent = Group("Assembly/Dressing");
            var spots = new[]
            {
                new Spot("Furn_MetalShelf", 'N', 51.6f), new Spot("Prop_GasCylinders", 'N', 56.6f),
                new Spot("Furn_MetalShelf_B", 'N', 68.2f), new Spot("Furn_MetalShelf", 'N', 70.2f),
                new Spot("kit:Pallet", 'N', 74.5f), new Spot("kit:Barrel", 'N', 76.6f), new Spot("kit:Barrel", 'N', 77.6f),
                new Spot("Furn_MetalShelf_B", 'N', 88.2f), new Spot("Furn_MetalShelf", 'N', 90.2f), new Spot("Furn_TrashBags", 'N', 92.6f),
                new Spot("Furn_MetalShelf", 'S', 64.4f), new Spot("Furn_MetalShelf_B", 'S', 66.4f),
                new Spot("kit:Barrel", 'S', 69f), new Spot("kit:Barrel", 'S', 70f),
                new Spot("Factory_Engine", 'S', 78f), new Spot("kit:Pallet", 'S', 86f), new Spot("kit:Crate Long", 'S', 87.6f),
                new Spot("Furn_MetalShelf", 'S', 90.6f), new Spot("Prop_GasCylinders", 'S', 93.4f),
                new Spot("Furn_MetalShelf", 'W', 3.6f), new Spot("kit:Pallet", 'W', 7.4f), new Spot("kit:Barrel", 'W', 27.6f),
                new Spot("kit:Pallet", 'E', 3f), new Spot("Furn_MetalShelf_B", 'E', 23.4f),
                new Spot("kit:Crate Long", ' ', 56f, 9f, 15f), new Spot("kit:Crate Short", ' ', 57.2f, 9.6f, 40f),
                new Spot("kit:Pallet", ' ', 88f, 22f, 5f), new Spot("kit:Crate Short", ' ', 88.4f, 22.4f, 20f),
                new Spot("Prop_ToolCart", ' ', 66f, 22f, 70f), new Spot("kit:Barrel", ' ', 82f, 6f),
                new Spot("kit:Barrel", ' ', 82.9f, 6.5f), new Spot("Elec_CableDrum", ' ', 91f, 4f, 0f),
                // Open strips along the north and south walls.
                new Spot("kit:Pallet", ' ', 52f, 26f, 10f), new Spot("kit:Crate Long", ' ', 55f, 26.5f, 80f),
                new Spot("Prop_ToolCart", ' ', 64f, 26f, 15f), new Spot("kit:Barrel", ' ', 70f, 26f),
                new Spot("kit:Barrel", ' ', 70.9f, 25.6f), new Spot("kit:Pallet", ' ', 76f, 25.5f, -8f),
                new Spot("kit:Crate Short", ' ', 77.6f, 26.4f, 25f), new Spot("kit:Crate Long", ' ', 88f, 26f, 5f),
                new Spot("kit:Pallet", ' ', 52f, 4f, 3f), new Spot("kit:Barrel", ' ', 60f, 3.4f),
                new Spot("kit:Barrel", ' ', 60.9f, 4f), new Spot("kit:Crate Long", ' ', 70f, 4f, 90f),
                new Spot("kit:Pallet", ' ', 80f, 4f, -6f), new Spot("kit:Crate Short", ' ', 81.6f, 4.6f, 12f),
                new Spot("Furn_TrashBags", ' ', 86f, 3.5f, 40f),
                // Volume: wrapped carton stacks in the strips, chain hoists over the lines.
                new Spot("Prop_CrateStack", ' ', 58f, 26.5f, 8f), new Spot("Prop_CrateStack_B", ' ', 67f, 25.8f, -12f),
                new Spot("Prop_CrateStack", ' ', 73f, 26.2f, 95f), new Spot("Prop_CrateStack_B", ' ', 84.5f, 25.7f, 4f),
                new Spot("Prop_CrateStack", ' ', 91f, 25.8f, -6f), new Spot("Prop_CrateStack", ' ', 56f, 4.5f, 3f),
                new Spot("Prop_CrateStack_B", ' ', 65f, 3.6f, 80f), new Spot("Prop_CrateStack", ' ', 74f, 4.2f, -4f),
                new Spot("Prop_CrateStack_B", ' ', 90f, 4.4f, 10f), new Spot("Prop_CrateStack", 'N', 81f),
                new Spot("Hall_ChainHoist", 'H', 60f, 15f, 0f), new Spot("Hall_ChainHoist", 'H', 70f, 22f, 90f),
                new Spot("Hall_ChainHoist", 'H', 81f, 8f, 0f), new Spot("Hall_ChainHoist", 'H', 88f, 15f, 30f),
                new Spot("Hall_ChainHoist", 'H', 56f, 22f, 0f),
            };

            return Dress(parent, spots, new Rect(48.6f, 0.6f, 46.8f, 28.8f));
        }

        private static int FillLab()
        {
            Transform parent = Group("Lab/Dressing");
            var spots = new[]
            {
                new Spot("Lab_ServerRack", 'S', 51.2f), new Spot("Lab_ServerRack", 'S', 52.1f), new Spot("Lab_ServerRack", 'S', 53f),
                new Spot("Lab_SpecimenShelf", 'S', 64f), new Spot("Lab_SpecimenShelf", 'S', 65.7f),
                new Spot("Lab_Bench", 'S', 73f), new Spot("Horror_Cage", 'S', 78.4f), new Spot("Horror_BiohazardBin", 'S', 80.2f),
                new Spot("Lab_ServerRack", 'S', 87f), new Spot("Lab_ServerRack", 'S', 87.9f),
                new Spot("Lab_Bench", 'N', 66.5f), new Spot("Lab_SpecimenShelf", 'N', 70.5f), new Spot("Horror_Gurney", 'N', 74.5f),
                new Spot("Horror_BiohazardBin", 'N', 77f), new Spot("Furn_FilingCabinet", 'N', 79f), new Spot("Furn_FilingCabinet", 'N', 79.6f),
                new Spot("Lab_Bench", 'W', -38.5f), new Spot("Furn_Desk", 'W', -34f), new Spot("Horror_Cage", 'W', -19.5f),
                new Spot("Lab_SpecimenShelf", 'W', -15.4f),
                new Spot("Horror_Gurney", ' ', 66f, -21f, 20f), new Spot("Lab_Bench", ' ', 72f, -20f, 0f),
                new Spot("Furn_OfficeChair", ' ', 71f, -21.2f, 160f), new Spot("Horror_BiohazardBin", ' ', 75.2f, -20.6f),
                new Spot("kit:Cart", ' ', 80f, -30f, 30f), new Spot("Horror_Gurney_Fallen", ' ', 56f, -22f, 100f),
                new Spot("kit:Crate Short", ' ', 90f, -36f, 10f), new Spot("kit:Crate Short", ' ', 90.6f, -36.9f, 35f),
                // The open floor between the tank rows.
                new Spot("Lab_Bench", ' ', 55f, -31f, 0f), new Spot("Horror_Gurney", ' ', 53f, -25f, 30f),
                new Spot("kit:Cart", ' ', 57.5f, -33f, 60f), new Spot("Lab_Bench", ' ', 79f, -27f, 90f),
                new Spot("Horror_BiohazardBin", ' ', 77f, -25f), new Spot("Furn_Desk", ' ', 89f, -30f, 0f),
                new Spot("Furn_OfficeChair", ' ', 89f, -31f, 170f), new Spot("Lab_ServerRack", ' ', 87f, -34f, 0f),
                new Spot("Lab_ServerRack", ' ', 87.8f, -34f, 0f), new Spot("Lab_ServerRack", ' ', 88.6f, -34f, 0f),
                new Spot("Horror_BiohazardBin", ' ', 92f, -30f), new Spot("kit:Crate Short", ' ', 93f, -33f, 15f),
                new Spot("Furn_FilingCabinet", ' ', 91f, -26f, 180f), new Spot("Furn_FilingCabinet", ' ', 91.6f, -26f, 180f),
            };

            return Dress(parent, spots, new Rect(48.6f, -41.4f, 46.8f, 28.8f));
        }

        /// <summary>
        /// Places each spot unless it would overlap something, cover a vent, or stand in a doorway.
        /// Wall spots stand with their back to the wall and their front (model +z) facing into the room.
        /// </summary>
        private static int Dress(Transform parent, Spot[] spots, Rect hall)
        {
            var doorways = new List<Vector3>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Exit_") || t.name.StartsWith("Airlock_"))
                {
                    doorways.Add(t.position);
                }
            }

            foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include))
            {
                doorways.Add(door.transform.position);
            }

            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();
            if (exit != null)
            {
                doorways.Add(exit.transform.position);
            }

            int added = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                Spot spot = spots[i];
                string name = $"Dress_{i:00}_{spot.Asset.Replace("kit:", string.Empty).Replace(' ', '_')}";
                if (parent.Find(name) != null)
                {
                    continue;
                }

                GameObject source = spot.Asset.StartsWith("kit:") ? Pack(spot.Asset.Substring(4)) : Model(spot.Asset);
                if (source == null)
                {
                    continue;
                }

                Vector3 inward;
                Vector3 probe;
                switch (spot.Wall)
                {
                    case 'N': inward = Vector3.back; probe = new Vector3(spot.A, 1f, hall.yMax - 3f); break;
                    case 'S': inward = Vector3.forward; probe = new Vector3(spot.A, 1f, hall.yMin + 3f); break;
                    case 'E': inward = Vector3.left; probe = new Vector3(hall.xMax - 3f, 1f, spot.A); break;
                    case 'W': inward = Vector3.right; probe = new Vector3(hall.xMin + 3f, 1f, spot.A); break;
                    default: inward = Vector3.zero; probe = new Vector3(spot.A, 1f, spot.B); break;
                }

                if (spot.Wall == 'H')
                {
                    // Hangs from the roof: its height is in the model; nothing on the floor to test.
                    var hanging = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    hanging.name = name;
                    hanging.transform.SetParent(parent, false);
                    hanging.transform.SetPositionAndRotation(new Vector3(spot.A, 0f, spot.B), Quaternion.Euler(0f, spot.Yaw, 0f));
                    hanging.isStatic = true;
                    added++;
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = name;
                instance.transform.SetParent(parent, false);
                Quaternion rotation = inward != Vector3.zero
                    ? Quaternion.LookRotation(inward, Vector3.up)
                    : Quaternion.Euler(0f, spot.Yaw, 0f);
                instance.transform.SetPositionAndRotation(Vector3.zero, rotation);
                Bounds local = RendererBounds(instance);

                Vector3 centre;
                if (inward != Vector3.zero)
                {
                    // The kit walls have pilasters and skirting: find the face that stands furthest
                    // into the room across the prop's width and height.
                    Vector3 tangent = Vector3.Cross(Vector3.up, inward);
                    float width = Mathf.Abs(Vector3.Dot(local.size, new Vector3(Mathf.Abs(tangent.x), 0f, Mathf.Abs(tangent.z))));
                    float depth = Mathf.Abs(Vector3.Dot(local.size, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))));
                    float face = float.NegativeInfinity;
                    Vector3 facePoint = Vector3.zero;
                    for (float u = -0.5f; u <= 0.501f; u += 0.25f)
                    {
                        for (float y = 0.08f; y <= Mathf.Min(local.size.y, 2.6f); y += 0.3f)
                        {
                            Vector3 from = new Vector3(probe.x, y, probe.z) + tangent * (u * width);
                            foreach (RaycastHit hit in Physics.RaycastAll(from, -inward, 4f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                if (IsStructure(hit.transform) && Vector3.Dot(hit.point, inward) > face)
                                {
                                    face = Vector3.Dot(hit.point, inward);
                                    facePoint = hit.point;
                                }
                            }
                        }
                    }

                    if (float.IsNegativeInfinity(face))
                    {
                        Debug.Log($"[Facility] Dressing skipped {name}: no wall found");
                        Object.DestroyImmediate(instance);
                        continue;
                    }

                    Vector3 along = new Vector3(probe.x, 0f, probe.z);
                    centre = along - inward * Vector3.Dot(along, inward) + inward * (face + depth / 2f + 0.06f);
                }
                else
                {
                    centre = new Vector3(probe.x, 0f, probe.z);
                }

                Vector3 offset = new Vector3(centre.x - local.center.x, -local.min.y + 0.002f, centre.z - local.center.z);
                instance.transform.position = offset;
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false; // not part of its own overlap test
                }

                Bounds placed = RendererBounds(instance);
                string blocked = BlocksSomething(placed, inward, doorways);
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = true;
                }

                if (blocked != null)
                {
                    Debug.Log($"[Facility] Dressing skipped {name}: {blocked}");
                    Object.DestroyImmediate(instance);
                    continue;
                }

                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.isStatic = true;
                }

                Physics.SyncTransforms();
                added++;
            }

            return added;
        }

        private static Bounds RendererBounds(GameObject instance)
        {
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
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

        /// <summary>A wall section or support of the building shell.</summary>
        private static bool IsStructure(Transform t)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
            {
                if (p.name == "Walls" || p.name == "Shell")
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Bounds> ventBounds;

        private static List<Bounds> VentBounds()
        {
            if (ventBounds != null)
            {
                return ventBounds;
            }

            ventBounds = new List<Bounds>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("vent_fan") || n.Contains("vent_guard") || n.Contains("vent_rotor"))
                {
                    ventBounds.Add(r.bounds);
                }
            }

            return ventBounds;
        }

        private static string BlocksSomething(Bounds placed, Vector3 inward, List<Vector3> doorways)
        {
            Vector3 flat = new Vector3(placed.center.x, 0f, placed.center.z);
            float reach = Mathf.Max(placed.extents.x, placed.extents.z);
            foreach (Vector3 door in doorways)
            {
                if (Vector3.Distance(flat, new Vector3(door.x, 0f, door.z)) < reach + 2.6f)
                {
                    return "doorway";
                }
            }

            // Anything solid inside the footprint (floor and wall faces excluded by the small inset).
            Vector3 half = placed.extents - new Vector3(0.05f, 0.05f, 0.05f);
            half.y = Mathf.Max(0.05f, half.y - 0.03f);
            Collider[] hits = Physics.OverlapBox(placed.center + Vector3.up * 0.04f, half, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit.bounds.max.y > 0.08f && (inward == Vector3.zero || !IsStructure(hit.transform)))
                {
                    return "overlaps " + hit.name;
                }
            }

            // A vent fan or guard in the wall behind it (or anywhere in its footprint).
            Bounds reachBack = placed;
            reachBack.Expand(new Vector3(Mathf.Abs(inward.x) * 1.2f + 0.2f, 0.4f, Mathf.Abs(inward.z) * 1.2f + 0.2f));
            foreach (Bounds vent in VentBounds())
            {
                if (vent.Intersects(reachBack))
                {
                    return "vent";
                }
            }

            // Two metres free in front, so it never pinches a walkway shut.
            if (inward != Vector3.zero)
            {
                Vector3 front = placed.center + inward * (Mathf.Abs(Vector3.Dot(placed.extents, inward)) + 0.9f);
                if (Physics.CheckBox(front + Vector3.up * 0.2f, new Vector3(0.3f, 0.6f, 0.3f), Quaternion.identity, ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    return "front blocked";
                }
            }

            return null;
        }
    }
}
