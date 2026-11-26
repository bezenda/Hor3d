using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Zone layout (owner, 2026-11-11: "objects are scattered at random"). Replaces the scattered
    /// Fill_v1 dressing with a planned layout: in the assembly hall a racked storage strip along the north
    /// wall (aisles to break sight and loop round), receiving lanes and an engine bay along the south wall,
    /// conveyors rebuilt from `Factory_Conveyor` with drive ends and operator stands; in the lab a server
    /// room of back-to-back rack rows, analysis islands and a triage row by the west door. Every position is
    /// the centre of the item's footprint on the floor, every turn a multiple of 90 degrees.
    /// Models: Tools/Blender/facility_zones.py (build_zones()). Marker: Zones_v1.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string ZonesName = "Zones_v1";

        private struct Item
        {
            public string Asset;   // model name, or "kit:" + kit prefab
            public float X, Z, Yaw;

            public Item(string asset, float x, float z, float yaw = 0f)
            {
                Asset = asset;
                X = x;
                Z = z;
                Yaw = yaw;
            }
        }

        private static int Zones()
        {
            if (root.Find(ZonesName) != null)
            {
                return 0;
            }

            int changed = 0;
            changed += ClearDressing("Assembly/Dressing");
            changed += ClearDressing("Lab/Dressing");
            changed += RebuildConveyors();

            var assembly = new List<Item>();

            // North wall: storage strip. Back-to-back rack pairs run from the wall into the hall; 1.8 m aisles
            // between them, a 1 m service gap along the wall, 2 m clear to conveyor line B.
            float[] rackRows = { 52.5f, 56.5f, 68.5f, 72.5f, 76.5f, 86.5f };
            for (int i = 0; i < rackRows.Length; i++)
            {
                string a = i % 2 == 0 ? "Storage_PalletRack" : "Storage_PalletRack_B";
                string b = i % 2 == 0 ? "Storage_PalletRack_B" : "Storage_PalletRack";
                assembly.Add(new Item(a, rackRows[i] - 0.56f, 26.6f, 90f));
                assembly.Add(new Item(b, rackRows[i] + 0.56f, 26.6f, 270f));
            }

            // South wall: receiving lanes (2 x 2 wrapped stacks on pallets), shelving, gas store.
            foreach (float x0 in new[] { 66.6f, 72.6f })
            {
                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iz = 0; iz < 2; iz++)
                    {
                        string stack = (ix + iz) % 2 == 0 ? "Prop_CrateStack" : "Prop_CrateStack_B";
                        assembly.Add(new Item(stack, x0 + ix * 1.45f, 2.0f + iz * 1.25f));
                    }
                }
            }

            assembly.Add(new Item("Furn_MetalShelf", 62.9f, 0.92f));
            assembly.Add(new Item("Furn_MetalShelf_B", 86.9f, 0.92f));
            assembly.Add(new Item("Furn_MetalShelf", 88.8f, 0.92f));
            assembly.Add(new Item("Prop_GasCylinders", 91.6f, 1.4f, 90f));

            // Engine bay: an engine on its stand, a hoist over it, a tool cart and stand beside it.
            assembly.Add(new Item("Factory_Engine", 81.8f, 3.2f));
            assembly.Add(new Item("Prop_ToolCart", 78.3f, 2.2f, 90f));
            assembly.Add(new Item("Factory_ControlStand", 78.3f, 4.6f, 90f));

            // Operator stands beside each conveyor group, on the aisle side.
            assembly.Add(new Item("Factory_ControlStand", 66.6f, 6.4f, 0f));
            assembly.Add(new Item("Factory_ControlStand", 73.4f, 6.4f, 0f));
            assembly.Add(new Item("Factory_ControlStand", 64.6f, 23.6f, 180f));
            assembly.Add(new Item("Factory_ControlStand", 73.4f, 23.6f, 180f));

            changed += Lay(Group("Assembly/Dressing"), assembly);
            changed += Hang(Group("Assembly/Dressing"), new[]
            {
                new Vector3(59f, 0f, 15f), new Vector3(85f, 0f, 15f), new Vector3(81.8f, 0f, 3.2f),
            });
            changed += Stripes(Group("Assembly/Dressing"), new[]
            {
                // Storage strip edge, receiving lane outlines.
                new Vector4(49.4f, 24.2f, 84.8f, 24.2f), new Vector4(87.8f, 24.2f, 89f, 24.2f),
                new Vector4(65.6f, 4.6f, 75.6f, 4.6f), new Vector4(65.6f, 1.0f, 65.6f, 4.6f),
                new Vector4(75.6f, 1.0f, 75.6f, 4.6f), new Vector4(70.1f, 1.0f, 70.1f, 4.6f),
                // Walkways along line A.
                new Vector4(50f, 6.9f, 90f, 6.9f), new Vector4(50f, 9.1f, 90f, 9.1f),
            });

            var lab = new List<Item>();

            // Server room (east): two back-to-back rows of racks, 1.9 m aisle between them.
            for (int i = 0; i < 8; i++)
            {
                float x = 86.4f + i * 0.8f;
                // Backs together, fronts (model +z) out into the aisles.
                lab.Add(new Item("Lab_ServerRack", x, -27.9f, 0f));
                lab.Add(new Item("Lab_ServerRack", x, -28.95f, 180f));
                lab.Add(new Item("Lab_ServerRack", x, -31.9f, 0f));
                lab.Add(new Item("Lab_ServerRack", x, -32.95f, 180f));
            }

            lab.Add(new Item("Furn_Desk", 91.6f, -38.6f, 0f));
            lab.Add(new Item("Furn_OfficeChair", 91.6f, -37.8f, 180f));
            lab.Add(new Item("Furn_FilingCabinet", 93.4f, -38.6f, 0f));

            // Analysis island east of the containment: two benches back to back, a chair at each.
            lab.Add(new Item("Lab_Bench", 77.5f, -26.55f, 180f));
            lab.Add(new Item("Lab_Bench", 77.5f, -27.45f, 0f));
            lab.Add(new Item("Furn_OfficeChair", 77.5f, -25.6f, 180f));
            lab.Add(new Item("Furn_OfficeChair", 76.6f, -28.4f, 0f));

            // West: triage row by the door, the same island north of the door lane.
            lab.Add(new Item("Horror_Gurney", 50.4f, -32.6f, 90f));
            lab.Add(new Item("Horror_Gurney", 50.4f, -35.2f, 90f));
            lab.Add(new Item("Horror_BiohazardBin", 49.4f, -37.4f));
            lab.Add(new Item("Lab_Bench", 55.5f, -23.4f, 180f));
            lab.Add(new Item("Lab_Bench", 55.5f, -24.3f, 0f));
            lab.Add(new Item("Furn_OfficeChair", 55.5f, -25.2f, 0f));
            lab.Add(new Item("Lab_SpecimenShelf", 49.0f, -15.3f, 90f));
            lab.Add(new Item("Lab_SpecimenShelf", 49.0f, -17.0f, 90f));

            changed += Lay(Group("Lab/Dressing"), lab);
            changed += Stripes(Group("Lab/Dressing"), new[]
            {
                new Vector4(85.6f, -25.6f, 85.6f, -35.4f), new Vector4(85.6f, -25.6f, 93.4f, -25.6f),
            });

            Marker(ZonesName);
            return changed;
        }

        private static int ClearDressing(string path)
        {
            Transform group = root.Find(path);
            if (group == null)
            {
                return 0;
            }

            int removed = group.childCount;
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(group.GetChild(i).gameObject);
            }

            return removed;
        }

        /// <summary>The old conveyors are merged meshes: hide them, keep the pivot, hang the model under it.</summary>
        private static int RebuildConveyors()
        {
            Transform machines = root.Find("Assembly/Machines");
            GameObject model = Model("Factory_Conveyor");
            GameObject drive = Model("Factory_ConveyorDrive");
            if (machines == null || model == null)
            {
                return 0;
            }

            int changed = 0;
            var ends = new Dictionary<float, List<float>>();
            foreach (Transform t in machines)
            {
                if (!t.name.StartsWith("Conveyor_") || t.Find("Model") != null)
                {
                    continue;
                }

                foreach (Renderer r in t.GetComponents<Renderer>())
                {
                    r.enabled = false;
                }

                foreach (Collider c in t.GetComponents<Collider>())
                {
                    c.enabled = false;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Model";
                instance.transform.SetParent(t, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.isStatic = true;
                changed++;

                float z = Mathf.Round(t.position.z * 10f) / 10f;
                if (!ends.ContainsKey(z))
                {
                    ends[z] = new List<float>();
                }

                ends[z].Add(t.position.x);
            }

            // A line is a run of 6 m segments; a drive goes on each open end.
            if (drive != null)
            {
                foreach (KeyValuePair<float, List<float>> line in ends)
                {
                    var xs = new HashSet<float>(line.Value);
                    foreach (float x in line.Value)
                    {
                        if (!xs.Contains(x + 6f))
                        {
                            PlaceDrive(machines, drive, new Vector3(x, 0f, line.Key), 0f);
                        }

                        if (!xs.Contains(x - 6f))
                        {
                            PlaceDrive(machines, drive, new Vector3(x - 6f, 0f, line.Key), 180f);
                        }
                    }
                }
            }

            return changed;
        }

        private static void PlaceDrive(Transform parent, GameObject drive, Vector3 position, float yaw)
        {
            string name = $"Drive_{position.x:0}_{position.z:0}";
            if (parent.Find(name) != null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(drive);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            instance.isStatic = true;
        }

        /// <summary>Places each item with its footprint centred on (X, Z), resting on the floor.</summary>
        private static int Lay(Transform parent, List<Item> items, float floorY = 0f, string prefix = "Zone")
        {
            int added = 0;
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                GameObject source = item.Asset.StartsWith("kit:") ? Pack(item.Asset.Substring(4)) : Model(item.Asset);
                if (source == null)
                {
                    Debug.LogWarning("[Facility] Zones: missing " + item.Asset);
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = $"{prefix}_{i:00}_{item.Asset.Replace("kit:", string.Empty).Replace(' ', '_')}";
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, item.Yaw, 0f));
                Bounds local = RendererBounds(instance);
                instance.transform.position = new Vector3(item.X - local.center.x, floorY - local.min.y + 0.002f, item.Z - local.center.z);
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.isStatic = true;
                }

                // Report (not refuse) overlaps, so the layout list can be corrected by hand.
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }

                Bounds placed = RendererBounds(instance);
                Vector3 half = placed.extents - new Vector3(0.04f, 0.04f, 0.04f);
                foreach (Collider hit in Physics.OverlapBox(placed.center + Vector3.up * 0.04f, half, Quaternion.identity, ~0,
                             QueryTriggerInteraction.Ignore))
                {
                    if (hit.bounds.max.y > 0.08f && !hit.transform.IsChildOf(parent))
                    {
                        Debug.Log($"[Facility] Zones: {instance.name} touches {hit.name}");
                        break;
                    }
                }

                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = true;
                }

                Physics.SyncTransforms();
                added++;
            }

            return added;
        }

        private static int Hang(Transform parent, Vector3[] spots)
        {
            GameObject hoist = Model("Hall_ChainHoist");
            if (hoist == null)
            {
                return 0;
            }

            for (int i = 0; i < spots.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(hoist);
                instance.name = $"Zone_Hoist_{i}";
                instance.transform.SetParent(parent, false);
                instance.transform.position = spots[i];
                instance.isStatic = true;
            }

            return spots.Length;
        }

        /// <summary>Yellow floor lines from (x, z) to (z, w); no colliders.</summary>
        private static int Stripes(Transform parent, Vector4[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                Vector3 a = new Vector3(lines[i].x, 0.004f, lines[i].y);
                Vector3 b = new Vector3(lines[i].z, 0.004f, lines[i].w);
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"Zone_Stripe_{i}";
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
                stripe.transform.SetParent(parent, false);
                stripe.transform.position = (a + b) * 0.5f;
                stripe.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                stripe.transform.localScale = new Vector3(0.1f, 0.006f, Vector3.Distance(a, b));
                stripe.GetComponent<Renderer>().sharedMaterial = PaintYellow;
                stripe.isStatic = true;
            }

            return lines.Length;
        }
    }
}
