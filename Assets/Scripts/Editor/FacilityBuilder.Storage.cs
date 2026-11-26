using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Third visual pass: the storage hall gets 7 m pallet racks along its walls, roof trusses, a parked
    /// forklift and lane markings; the electrical hall gets a modelled diesel generator in place of the
    /// primitive one, battery racks, cable drums, cables on the floor, hazard markings and ducts high up
    /// on its 18 m walls. Models come from Tools/Blender/facility_assets.py (build_storage()).
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string HallsV3Name = "Halls_v3";

        private static Material RackBlue => Mat("M_Rack_Blue", new Color(0.08f, 0.18f, 0.34f), 0.45f, 0.3f);
        private static Material Cardboard => Mat("M_Cardboard", new Color(0.45f, 0.33f, 0.2f), 0.12f);
        private static Material Wood => Mat("M_Wood", new Color(0.42f, 0.32f, 0.2f), 0.15f);
        private static Material Wrap => Mat("M_Wrap", new Color(0.55f, 0.58f, 0.6f), 0.75f);

        private static int DressHallsV3()
        {
            int added = RemoveHallsV3Leftovers();

            // ------------------------------------------------------------ storage
            // A rack is 1.1 m deep and runs 6 m along its local +z from the pivot.
            Transform racks = Group("Storage/Racks");
            int n = 0;
            foreach (float x in new[] { 0.6f, 6.6f, 18f, 24f, 29.9f })
                added += PutModel(racks, $"S_{x:0}", RackModel(n++), new Vector3(x, 0f, 0.75f), 90f);
            foreach (float x in new[] { 12.2f, 18.2f, 24.2f })
                added += PutModel(racks, $"N_{x:0}", RackModel(n++), new Vector3(x, 0f, 29.25f), 90f);
            added += PutModel(racks, "E_13", RackModel(n), new Vector3(35.3f, 0f, 12.8f), 0f);

            Transform sp = Group("Storage/Props");
            // Forks towards +x, parked along the south lane.
            added += PutModel(sp, "Forklift", "Storage_Forklift", new Vector3(26f, 0f, 2.75f), 90f);

            // Trusses run along z here, clear of the lamps (x 6/18/30) and the duct (x 10.5).
            Transform st = Group("Storage/Roof");
            foreach (float x in new[] { 3f, 12f, 24f, 33f })
                for (int k = 0; k < 4; k++)
                    added += PutModel(st, $"Truss_{x:0}_{k}", "Ceiling_Truss", new Vector3(x, 8.75f, 3f + k * 6f), 90f);

            Transform lines = Group("Storage/FloorLines");
            if (lines.childCount == 0)
            {
                FloorLine(lines, "Cross_W", new Vector3(16.25f, 0f, 12.7f), new Vector3(0.1f, 0f, 22.2f));
                FloorLine(lines, "Cross_E", new Vector3(19.45f, 0f, 12.7f), new Vector3(0.1f, 0f, 22.2f));
                FloorLine(lines, "South_W", new Vector3(6.6f, 0f, 1.5f), new Vector3(12f, 0f, 0.1f));
                FloorLine(lines, "South_E", new Vector3(27f, 0f, 1.5f), new Vector3(17.8f, 0f, 0.1f));
                FloorLine(lines, "North", new Vector3(21.2f, 0f, 28.5f), new Vector3(18f, 0f, 0.1f));
                FloorLine(lines, "East", new Vector3(34.6f, 0f, 15.8f), new Vector3(0.1f, 0f, 6f));
                added += 6;
            }

            // --------------------------------------------------------- electrical
            Transform e = Group("Electrical/Machines");
            added += PutModel(e, "Generator", "Elec_Generator", new Vector3(13f, 0f, -25f), 0f);
            added += PutModel(e, "Batteries_1", "Elec_BatteryRack", new Vector3(19.8f, 0f, -29.5f), 0f);
            added += PutModel(e, "Batteries_2", "Elec_BatteryRack", new Vector3(22f, 0f, -29.5f), 0f);
            added += PutModel(e, "Drum_1", "Elec_CableDrum", new Vector3(6.5f, 0f, -18f), 20f);
            added += PutModel(e, "Drum_2", "Elec_CableDrum", new Vector3(20.8f, 0f, -17.2f), 75f);
            added += PutModel(e, "Drum_3", "Elec_CableDrum", new Vector3(22.7f, 0f, -15.6f), -10f);
            added += PutModel(e, "Cables_1", "Elec_CableBundle", new Vector3(0.9f, 0f, -26.6f), 0f);
            added += PutModel(e, "Cables_2", "Elec_CableBundle", new Vector3(1f, 0f, -32.6f), 0f);
            added += PutModel(e, "Cables_3", "Elec_CableBundle", new Vector3(10f, 0f, -32.6f), 0f);

            // Fuel drums by the generator, spare stock in the free corners.
            Transform ep = Group("Electrical/Props");
            added += Put(ep, "Fuel_1", "Barrel", new Vector3(18.3f, 0f, -24.2f), 25f);
            added += Put(ep, "Fuel_2", "Barrel", new Vector3(19.1f, 0f, -23.4f), 140f);
            added += Put(ep, "Fuel_3", "Barrel", new Vector3(18.4f, 0f, -22.5f), 260f);
            added += PutAligned(ep, "Pallet_2", "Pallet Variation 08", 90f, new Vector3(6.5f, 0f, -22f));
            added += Put(ep, "Spare_1", "Crate Long", new Vector3(5.6f, 0f, -29.4f), 90f);
            added += Put(ep, "Spare_2", "Crate Short", new Vector3(5.7f, 0.6f, -29.2f), 15f);

            // Ducts high on the west and south walls (under the trusses, over the catwalk rails).
            Transform d = Group("Electrical/Ducts");
            for (int k = 0; k < 4; k++)
                added += Put(d, "Duct_W" + k, "Duct Straight", new Vector3(1.2f, 12.5f, -40.6f + k * 6f), 0f);
            for (int k = 0; k < 6; k++)
                added += Put(d, "Duct_S" + k, "Duct Straight", new Vector3(k * 6f, 12.5f, -41.3f), 90f);

            Transform hz = Group("Electrical/FloorLines");
            if (hz.childCount == 0)
            {
                // Keep-out frame round the generator, and in front of the battery racks.
                FloorLine(hz, "Gen_N", new Vector3(13f, 0f, -23.2f), new Vector3(7.6f, 0f, 0.12f));
                FloorLine(hz, "Gen_S", new Vector3(13f, 0f, -26.8f), new Vector3(7.6f, 0f, 0.12f));
                FloorLine(hz, "Gen_W", new Vector3(9.2f, 0f, -25f), new Vector3(0.12f, 0f, 3.72f));
                FloorLine(hz, "Gen_E", new Vector3(16.8f, 0f, -25f), new Vector3(0.12f, 0f, 3.72f));
                FloorLine(hz, "Bat_N", new Vector3(20.9f, 0f, -26.1f), new Vector3(3.2f, 0f, 0.12f));
                FloorLine(hz, "Bat_S", new Vector3(20.9f, 0f, -29.9f), new Vector3(3.2f, 0f, 0.12f));
                added += 6;
            }

            return added;
        }

        private static string RackModel(int index) => index % 2 == 0 ? "Storage_HighRack_A" : "Storage_HighRack_B";

        /// <summary>A painted line on the floor (no collider). Size y is ignored.</summary>
        private static void FloorLine(Transform parent, string name, Vector3 center, Vector3 size)
        {
            Box(parent, name, center + new Vector3(0f, 0.004f, 0f), new Vector3(size.x, 0.008f, size.z), PaintYellow, false);
        }

        /// <summary>Once: props standing where the racks and the modelled generator now go.</summary>
        private static int RemoveHallsV3Leftovers()
        {
            if (root.Find(HallsV3Name) != null)
            {
                return 0;
            }

            int changed = 0;
            changed += DeleteChildren("Storage/Props", "Crates_1", "Crates_2", "Crates_3", "Fusebox_S", "Pipes_S");
            changed += DeleteChildren("Electrical/Props", "Generator", "Generator_Base", "Generator_Panel", "Generator_Warn");

            Transform props = root.Find("Storage/Props");
            if (props != null)
            {
                Transform bin = props.Find("Bin_2");
                if (bin != null)
                {
                    bin.position = new Vector3(35.4f, 0f, 19.8f);
                    changed++;
                }

                Transform pallet = props.Find("Pallet_N1");
                if (pallet != null)
                {
                    pallet.position += new Vector3(0.4f, 0f, 0f);
                    changed++;
                }
            }

            var marker = new GameObject(HallsV3Name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return changed + 1;
        }

        private static int DeleteChildren(string group, params string[] names)
        {
            Transform parent = root.Find(group);
            if (parent == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (string name in names)
            {
                Transform child = parent.Find(name);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
            }

            return removed;
        }
    }
}
