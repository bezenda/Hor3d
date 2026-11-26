using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Owner's notes 2026-11-11 (third round): the bodies remodelled (Tools/Blender/facility_bodies.py:
    /// a cloth-simulated sheet over a body, a vinyl body bag), the body-bag gurney moved off the floor trench
    /// into the triage row, the breaker room door widened to the kit frame's opening (2.11 m, hinge on the
    /// frame edge), and the breaker room and the lab office dressed as rooms with a purpose.
    /// Marker: Rooms_v1.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string RoomsName = "Rooms_v1";

        private static Material Skin => Mat("M_Skin", new Color(0.4f, 0.36f, 0.33f), 0.3f, 0f);
        private static Material BodyBagVinyl => Mat("M_BodyBag", new Color(0.045f, 0.055f, 0.055f), 0.62f, 0.05f);

        private static int Rooms()
        {
            if (root.Find(RoomsName) != null)
            {
                return 0;
            }

            int changed = SwapBodies() + WidenBreakerDoor() + DressBreakerRoom() + DressLabOffice();
            Marker(RoomsName);
            return changed;
        }

        // ------------------------------------------------------------------ bodies

        private static int SwapBodies()
        {
            int changed = 0;
            Transform covered = root.Find("Horror/Lab/CoveredBody");
            if (covered != null && Model("Horror_CoveredBody2") != null)
            {
                HideRenderers(covered);
                // The old model lay head to -x, the new one head to +x.
                PlaceModel(covered.parent, "CoveredBody2", "Horror_CoveredBody2",
                    new Vector3(covered.position.x, 0.002f, covered.position.z), covered.eulerAngles.y + 180f);
                changed++;
            }

            Transform gurney = root.Find("Horror/Lab/Gurney");
            Transform bag = root.Find("Horror/Lab/BodyBag");
            if (gurney != null)
            {
                // It stood across the floor trench by the north tanks: into the triage row by the west door.
                gurney.position = new Vector3(50.4f, gurney.position.y, -30.0f);
                gurney.rotation = Quaternion.Euler(0f, 90f, 0f);
                Physics.SyncTransforms();
                changed++;
            }

            if (bag != null && gurney != null && Model("Horror_BodyBag2") != null)
            {
                HideRenderers(bag);
                float top = 0.85f;
                var hits = Physics.RaycastAll(gurney.position + Vector3.up * 2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.transform.IsChildOf(gurney))
                    {
                        top = hit.point.y;
                        break;
                    }
                }

                // Model lies along +x (head); the gurney runs along its own x.
                PlaceModel(gurney.parent, "BodyBag2", "Horror_BodyBag2",
                    new Vector3(gurney.position.x, top + 0.003f, gurney.position.z), gurney.eulerAngles.y);
                changed++;
            }

            return changed;
        }

        private static void HideRenderers(Transform t)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }
        }

        private static GameObject PlaceModel(Transform parent, string name, string model, Vector3 position, float yaw,
            Vector3? scale = null)
        {
            if (parent.Find(name) != null || Model(model) == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Model(model));
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (scale.HasValue)
            {
                instance.transform.localScale = scale.Value;
            }

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = true;
            }

            return instance;
        }

        // ------------------------------------------------------------------ breaker door

        /// <summary>
        /// The kit frame's clear opening is 2.05 x 2.45 (z -20.0..-22.05) with 0.27 chamfers; the leaf was
        /// 1.62 and left gaps. Hinge moves to the frame edge; the box collider grows to the new leaf.
        /// </summary>
        private static int WidenBreakerDoor()
        {
            GameObject door = GameObject.Find("Door_BreakerRoom");
            if (door == null)
            {
                return 0;
            }

            const float width = 2.11f;
            Vector3 p = door.transform.position;
            door.transform.position = new Vector3(p.x, p.y, -19.97f);
            Transform panel = door.transform.Find("Hinge/Panel");
            if (panel != null)
            {
                panel.localPosition = new Vector3(width / 2f, panel.localPosition.y, panel.localPosition.z);
                panel.localScale = new Vector3(width, panel.localScale.y, panel.localScale.z);
            }

            Transform handle = door.transform.Find("Hinge/Handle");
            if (handle != null)
            {
                handle.localPosition = new Vector3(width - 0.18f, handle.localPosition.y, handle.localPosition.z);
            }

            return 1;
        }

        // ------------------------------------------------------------------ breaker room

        /// <summary>
        /// Breaker room (x 24.15..35.45, z -23.85..-12.55, floor 9): switchgear on the north wall, breaker
        /// panel on the east. Batteries along the south wall, a records corner by the desk, insulating mats in
        /// front of the live gear, cable trays round the ceiling. The middle stays open: it is a place to be
        /// cornered in, so there must be room to dodge.
        /// </summary>
        private static int DressBreakerRoom()
        {
            Transform room = root.Find("Electrical/BreakerRoom");
            if (room == null)
            {
                return 0;
            }

            Transform parent = Group("Electrical/BreakerRoom/Dressing");
            const float floor = 9f;
            var items = new List<Item>
            {
                new Item("Elec_BatteryRack", 29.4f, -23.4f, 90f),
                new Item("Elec_BatteryRack", 32.7f, -23.4f, 90f),
                new Item("Furn_MetalShelf_B", 25.3f, -23.55f, 0f),
                new Item("Furn_FilingCabinet", 24.5f, -13.25f, 90f),
                new Item("Furn_FilingCabinet", 24.5f, -13.8f, 90f),
                new Item("Furn_MetalShelf", 24.42f, -18.3f, 90f),
                new Item("Furn_MetalShelf", 33.8f, -12.85f, 180f),
                new Item("Prop_ToolCart", 31.2f, -21.6f, 0f),
                new Item("kit:Garbage Bin", 24.6f, -14.6f),
                new Item("kit:Fire Extinguisher", 35.25f, -18.3f, 270f),
            };
            int added = Lay(parent, items, floor, "Breaker");

            PlaceModel(parent, "Mat_Switchgear", "Prop_RubberMat", new Vector3(29f, floor + 0.001f, -13.85f), 0f,
                new Vector3(6f, 1f, 1.1f));
            PlaceModel(parent, "Mat_Panel", "Prop_RubberMat", new Vector3(34.85f, floor + 0.001f, -16.6f), 0f,
                new Vector3(1.0f, 1f, 1.6f));

            // Trays: along the north wall and down the east wall, hung under the 13.3 m roof.
            PlaceModel(parent, "Tray_N1", "Elec_CableTray", new Vector3(30.5f, 12.55f, -13.25f), 0f);
            PlaceModel(parent, "Tray_N2", "Elec_CableTray", new Vector3(35.0f, 12.55f, -13.25f), 0f, new Vector3(0.75f, 1f, 1f));
            PlaceModel(parent, "Tray_E1", "Elec_CableTray", new Vector3(34.9f, 12.55f, -22.5f), 90f);
            PlaceModel(parent, "Tray_E2", "Elec_CableTray", new Vector3(34.9f, 12.55f, -16.5f), 90f, new Vector3(0.53f, 1f, 1f));

            PlaceModel(parent, "Clock", "Prop_WallClock", new Vector3(24.2f, 11.4f, -16f), 90f);
            PlaceOnTop(parent, "Desk_Monitor", "Furn_Monitor", 26.2f, -16.1f, 90f, floor + 2.5f);
            return added + 8;
        }

        // ------------------------------------------------------------------ lab office

        /// <summary>
        /// Lab office (x 84.55..95.45, z -23.45..-12.55, door in the west wall at z -21): a row of desks on the
        /// north wall, a meeting table in the middle, records along the south wall, the board and clock on the
        /// east wall. The keycard desk (x 90) is kept where it is; one chair lies knocked over.
        /// </summary>
        private static int DressLabOffice()
        {
            Transform parent = Group("Lab/Office/Dressing");
            var items = new List<Item>
            {
                new Item("Furn_Desk", 87.2f, -12.95f, 180f),
                new Item("Furn_Desk", 92.8f, -12.95f, 180f),
                new Item("Furn_OfficeChair", 87.2f, -14.0f, 0f),
                new Item("Furn_OfficeChair_Fallen", 92.6f, -14.6f, 70f),
                new Item("Furn_Desk", 89.7f, -18.6f, 0f),
                new Item("Furn_Desk", 91.3f, -18.6f, 0f),
                new Item("Furn_OfficeChair", 89.7f, -19.55f, 0f),
                new Item("Furn_OfficeChair", 91.3f, -19.55f, 0f),
                new Item("Furn_OfficeChair", 89.7f, -17.6f, 180f),
                new Item("Furn_OfficeChair", 91.5f, -17.4f, 200f),
                new Item("Furn_MetalShelf", 88.2f, -23.15f, 0f),
                new Item("Furn_MetalShelf_B", 90.0f, -23.15f, 0f),
                new Item("Prop_WaterCooler", 86.4f, -23.1f, 0f),
                new Item("Prop_DeadPlant", 85.2f, -13.2f),
                new Item("kit:Garbage Bin", 94.9f, -12.95f),
            };
            int added = Lay(parent, items, 0f, "Office");
            PlaceOnTop(parent, "Monitor_W", "Furn_Monitor", 87.4f, -12.9f, 180f, 2.5f);
            PlaceOnTop(parent, "Monitor_E", "Furn_Monitor", 92.6f, -12.9f, 180f, 2.5f);
            PlaceModel(parent, "Board", "Prop_CorkBoard", new Vector3(95.42f, 1.6f, -19.6f), 270f);
            PlaceModel(parent, "Clock", "Prop_WallClock", new Vector3(95.42f, 2.3f, -17.9f), 270f);
            return added + 4;
        }

        /// <summary>A model set down on whatever is under (x, z) - a monitor on a desk.</summary>
        private static void PlaceOnTop(Transform parent, string name, string model, float x, float z, float yaw, float fromY)
        {
            Physics.SyncTransforms();
            if (!Physics.Raycast(new Vector3(x, fromY, z), Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            GameObject instance = PlaceModel(parent, name, model, Vector3.zero, yaw);
            if (instance == null)
            {
                return;
            }

            Bounds b = RendererBounds(instance);
            instance.transform.position = new Vector3(x - b.center.x, hit.point.y - b.min.y + 0.002f, z - b.center.z);
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
        }
    }
}
