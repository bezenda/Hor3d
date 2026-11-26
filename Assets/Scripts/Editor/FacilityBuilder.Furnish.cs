using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Horror pass, part two: modelled furniture in place of v1's primitive boxes (janitor's closet,
    /// office, breaker room, workbench), the throwables in the same modelled style, and the horror
    /// dressing (Tools/Blender/facility_horror.py, build_horror()): a body under a sheet, a body
    /// bag, gurneys, a torn-open specimen cage, blood pools and drag marks, claw marks, scattered
    /// papers, specimen jars, tool carts, gas cylinders, warning signs.
    ///
    /// Placed by name, so a deleted piece is rebuilt and a moved one stays where it was put.
    /// The primitive furniture goes once (marker Furnish_v1); the items that stood on it are set
    /// down on the new surfaces at the same time.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string FurnishVersionName = "Furnish_v1";

        private static Material Blood => Mat("M_Blood", new Color(0.16f, 0.008f, 0.008f), 0.85f, 0f);
        private static Material BloodDry => Mat("M_Blood_Dry", new Color(0.075f, 0.018f, 0.012f), 0.3f, 0f);
        private static Material Sheet => Mat("M_Sheet", new Color(0.52f, 0.52f, 0.48f), 0.12f, 0f);
        private static Material BottleGlass => Mat("M_Bottle_Glass", new Color(0.05f, 0.17f, 0.08f), 0.93f, 0f);
        private static Material Tin => Mat("M_Tin", new Color(0.62f, 0.63f, 0.65f), 0.6f, 0.9f);

        private static int FurnishHorror()
        {
            int added = 0;
            bool firstRun = root.Find(FurnishVersionName) == null;
            if (firstRun)
            {
                added += RemovePrimitiveFurniture();
            }

            added += PlaceHorrorDressing();
            added += ThrowableLooks();
            added += FurnishV2();

            if (firstRun)
            {
                Physics.SyncTransforms();
                SettleItems();
                var marker = new GameObject(FurnishVersionName);
                marker.SetActive(false);
                marker.transform.SetParent(root, false);
                added++;
            }

            return added;
        }

        /// <summary>Once: v1's furniture made of cubes and cylinders goes (the models replace it).</summary>
        private static int RemovePrimitiveFurniture()
        {
            int removed = 0;
            removed += DeleteChildren("Janitor/Props", "Desk_Top", "Desk_Leg0", "Desk_Leg1", "Desk_Leg2", "Desk_Leg3",
                "Chair_Seat", "Chair_Back", "Chair_Leg", "Mug", "Papers", "Shelf_1", "Shelf_2", "Bucket", "Mop", "Bucket_2",
                "Cot", "Cot_Frame", "Blanket", "Bag_0", "Bag_1", "Bag_2", "Bag_3", "Paper_0", "Paper_1", "Paper_2", "Paper_3",
                "Toolbox", "Cable_Floor");
            removed += DeleteChildren("Lab/Office", "Desk_Top", "Desk_Leg0", "Desk_Leg1", "Desk_Leg2", "Desk_Leg3", "Monitor",
                "Monitor_Screen", "Chair", "Filing_0", "Filing_1", "Filing_2", "Whiteboard", "Whiteboard_Ink");
            removed += DeleteChildren("Electrical/BreakerRoom", "Desk_Top", "Desk_Leg0", "Desk_Leg1", "Desk_Leg2", "Desk_Leg3");
            removed += DeleteChildren("Assembly/Props", "Bench_Top", "Bench_Leg0", "Bench_Leg1", "Bench_Leg2", "Bench_Leg3",
                "ToolBoard", "Tool_0", "Tool_1", "Tool_2", "Tool_3", "Tool_4", "Tool_5");
            return removed;
        }

        private static int PlaceHorrorDressing()
        {
            int added = 0;

            // Research lab: where it got out.
            Transform lab = Group("Horror/Lab");
            added += PutModel(lab, "Gurney", "Horror_Gurney", new Vector3(52.5f, 0f, -20.5f), 90f);
            added += PutModel(lab, "BodyBag", "Horror_BodyBag", new Vector3(52.5f, 0.85f, -20.5f), 90f);
            added += PutModel(lab, "Gurney_Fallen", "Horror_Gurney_Fallen", new Vector3(80.5f, 0f, -24.5f), 30f);
            added += PutModel(lab, "CoveredBody", "Horror_CoveredBody", new Vector3(62.5f, 0f, -31.8f), 15f);
            added += PutModel(lab, "BloodPool", "Horror_BloodPool", new Vector3(60.2f, 0f, -34.6f), 0f);
            added += PutModel(lab, "BloodTrail", "Horror_BloodTrail", new Vector3(58.2f, 0f, -31.2f), 0f);
            added += PutModel(lab, "Cage", "Horror_Cage", new Vector3(88.5f, 0f, -35.5f), 200f);
            added += PutModel(lab, "SpecimenShelf", "Lab_SpecimenShelf", new Vector3(70.5f, 0f, -12.9f), 180f);
            added += PutModel(lab, "Biohazard_1", "Horror_BiohazardBin", new Vector3(82.5f, 0f, -40.6f), 0f);
            added += PutModel(lab, "Biohazard_2", "Horror_BiohazardBin", new Vector3(51f, 0f, -13.3f), 40f);
            added += PutModel(lab, "Warning", "Prop_WarningSign", new Vector3(49.8f, 0f, -29.6f), 70f);
            added += PutModel(lab, "OfficeTrail", "Horror_BloodTrail", new Vector3(86.5f, 0f, -20.5f), 0f);

            // Storage: something was dragged down an aisle.
            Transform storage = Group("Horror/Storage");
            added += PutModel(storage, "BodyBag", "Horror_BodyBag", new Vector3(10.2f, 0f, 7.05f), 80f);
            added += PutModel(storage, "BloodTrail", "Horror_BloodTrail", new Vector3(14f, 0f, 7f), 180f);
            added += PutModel(storage, "Papers", "Horror_Papers", new Vector3(17.8f, 0f, 10.8f), 30f);
            added += PutModel(storage, "ClawMarks", "Horror_ClawMarks", new Vector3(8.5f, 0f, 23.84f), 180f);

            // Assembly: work stopped mid-shift.
            Transform assembly = Group("Horror/Assembly");
            added += PutModel(assembly, "ToolCart_1", "Prop_ToolCart", new Vector3(64f, 0f, 4.5f), 20f);
            added += PutModel(assembly, "ToolCart_2", "Prop_ToolCart", new Vector3(80f, 0f, 26f), -30f);
            added += PutModel(assembly, "GasCylinders", "Prop_GasCylinders", new Vector3(95.5f, 0f, 7.6f), 180f);
            added += PutModel(assembly, "Warning", "Prop_WarningSign", new Vector3(52f, 0f, 21.5f), -20f);

            // Electrical hall and the airlocks.
            Transform electrical = Group("Horror/Electrical");
            added += PutModel(electrical, "Warning_1", "Prop_WarningSign", new Vector3(17.6f, 0f, -13.4f), 15f);
            added += PutModel(electrical, "Warning_2", "Prop_WarningSign", new Vector3(6.8f, 0f, -15.8f), -10f);
            Transform airlocks = Group("Horror/Airlocks");
            added += PutModel(airlocks, "ClawMarks", "Horror_ClawMarks", new Vector3(54.85f, 0f, -8.5f), 90f);
            added += PutModel(airlocks, "Papers", "Horror_Papers", new Vector3(15f, 0f, -9f), 10f);
            return added;
        }

        /// <summary>Bottles and cans wear the modelled looks (the items keep their own colliders and physics).</summary>
        private static int ThrowableLooks()
        {
            GameObject items = GameObject.Find("Items");
            if (items == null)
            {
                return 0;
            }

            int changed = 0;
            foreach (Transform item in items.transform)
            {
                string model = item.name.StartsWith("Throwable_Bottle") ? "Item_Bottle"
                    : item.name.StartsWith("Throwable_Can") ? "Item_Can" : null;
                Transform visuals = model != null ? item.Find("Visuals") : null;
                if (visuals == null || visuals.Find("Model") != null || Model(model) == null)
                {
                    continue;
                }

                for (int i = visuals.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(visuals.GetChild(i).gameObject);
                }

                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(Model(model));
                instance.name = "Model";
                instance.transform.SetParent(visuals, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                changed++;
            }

            return changed;
        }

        private const string FurnishV2Name = "Furnish_v2";

        /// <summary>
        /// Once, after looking at v1 in place: the hiding lockers get the modelled steel locker (v1's
        /// boxes stay as invisible colliders), the office whiteboard moves off the wall joint, and the
        /// glowing strips and capsule liquid go down further - they still lit the lab like signage.
        /// </summary>
        private static int FurnishV2()
        {
            if (root.Find(FurnishV2Name) != null || Model("Furn_Locker") == null)
            {
                return 0;
            }

            GameObject contents = UnityEditor.PrefabUtility.LoadPrefabContents(LockerPrefabPath);
            Transform visuals = contents.transform.Find("Visuals");
            if (visuals != null && visuals.Find("Model") == null)
            {
                foreach (MeshRenderer r in visuals.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.enabled = false;
                }

                var model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(Model("Furn_Locker"));
                model.name = "Model";
                model.transform.SetParent(visuals, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c); // the prefab's own boxes collide; the hide point sits inside
                }
            }

            UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents, LockerPrefabPath);
            UnityEditor.PrefabUtility.UnloadPrefabContents(contents);

            Nudge("Lab/Office/Furn_Whiteboard", new Vector3(0f, 0f, 2f));
            ScaleEmission(EmitCyan, 0.45f);
            ScaleEmission(Liquid, 0.5f);

            var marker = new GameObject(FurnishV2Name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return 1;
        }

        /// <summary>Items and notes that lay on the old furniture are set down on the new surfaces.</summary>
        private static void SettleItems()
        {
            Settle("Battery_1", new Vector3(9.1f, 1.2f, 29.2f));
            Settle("Throwable_Bottle_1", new Vector3(1.6f, 1.0f, 29.42f));    // second shelf, its clear middle
            Settle("Keycard", new Vector3(89.4f, 1.2f, -13.0f));
            Settle("Throwable_Bottle_7", new Vector3(90.7f, 1.2f, -12.6f));
            Settle("Battery_4", new Vector3(62.6f, 1.4f, 28.95f));
            Settle("Note_Janitor", new Vector3(7.98f, 1.2f, 29.1f));
            Settle("Note_Breaker", new Vector3(26.2f, BreakerFloor + 1.2f, -16.2f));
            Settle("Battery_3", new Vector3(9.43f, LevelOne + 1.2f, -33.27f));
        }

        /// <summary>Moves an object to (x, z) and onto the first solid surface below 'from'.</summary>
        private static void Settle(string name, Vector3 from)
        {
            GameObject target = GameObject.Find(name);
            if (target == null)
            {
                return;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 surface = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(target.transform) || hit.distance >= best)
                {
                    continue;
                }

                best = hit.distance;
                surface = hit.point;
            }

            if (best == float.MaxValue)
            {
                return;
            }

            target.transform.position = new Vector3(from.x, target.transform.position.y, from.z);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            float bottomOffset = target.transform.position.y - bounds.min.y;
            target.transform.position = new Vector3(from.x, surface.y + bottomOffset + 0.003f, from.z);
        }
    }
}
