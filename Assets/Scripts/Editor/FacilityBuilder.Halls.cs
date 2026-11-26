using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Second visual pass for the storage, assembly and electrical halls: clad wall segments where
    /// nothing stands against the wall, pipe risers, roof trusses, modelled conveyors, ship parts,
    /// robot arms, transformers and switchgear in place of the first primitive versions, and dust.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string HallsVersionName = "Halls_v2";

        private static int DressHallsV2()
        {
            int added = RemoveHallsV1();

            // ---------------------------------------------------------- assembly
            Transform aw = Group("Assembly/Walls");
            foreach (float s in new[] { 48f, 54f, 66f, 72f, 90f })
                added += WallPanel(aw, $"N_{s:0}", "Hall_WallPanel", Side.North, 30f, s);
            foreach (float s in new[] { 60f, 66f, 72f, 78f, 84f, 90f }) // 54: the door to the lab airlock
                added += WallPanel(aw, $"S_{s:0}", "Hall_WallPanel", Side.South, 0f, s);
            foreach (float s in new[] { 6f, 24f })
                added += WallPanel(aw, $"W_{s:0}", "Hall_WallPanel", Side.West, 48f, s);

            Transform a = Group("Assembly/Machines");
            foreach (float x in new[] { 56f, 62f, 68f, 78f, 84f, 90f })
                added += PutModel(a, $"Conveyor_A_{x:0}", "Factory_Conveyor", new Vector3(x, 0f, 8f), 0f);
            foreach (float x in new[] { 60f, 66f, 78f, 84f })
                added += PutModel(a, $"Conveyor_B_{x:0}", "Factory_Conveyor", new Vector3(x, 0f, 22f), 0f);

            added += PutModel(a, "ShipHull", "Factory_ShipHull", new Vector3(59f, 0f, 15f), 0f);
            added += PutModel(a, "Jig", "Factory_Jig", new Vector3(72f, 0f, 15f), 0f);
            added += PutModel(a, "Engine", "Factory_Engine", new Vector3(85f, 0f, 15f), 0f);
            // An arm reaches along its local -z; each one turns towards its station.
            added += PutModel(a, "Arm_1", "Factory_RobotArm", new Vector3(63.2f, 0f, 11.4f), 130f);
            added += PutModel(a, "Arm_2", "Factory_RobotArm", new Vector3(68.2f, 0f, 18.8f), -44f);
            added += PutModel(a, "Arm_3", "Factory_RobotArm", new Vector3(81.4f, 0f, 11.6f), -132f);
            for (int k = 0; k < 7; k++)
                added += PutModel(a, $"Truss_{k}", "Ceiling_Truss", new Vector3(56f + k * 6f, 8.75f, 15f), 0f);
            added += PutModel(a, "Riser_W", "Hall_PipeRiser", new Vector3(48.7f, 0f, 14.6f), 90f);
            added += PutModel(a, "Riser_E1", "Hall_PipeRiser", new Vector3(95.3f, 0f, 9f), -90f);
            added += PutModel(a, "Riser_E2", "Hall_PipeRiser", new Vector3(95.3f, 0f, 21f), -90f);

            // -------------------------------------------------------- electrical
            Transform ew = Group("Electrical/Walls");
            foreach (float s in new[] { 0f, 6f, 18f })
                added += WallPanel(ew, $"N_{s:0}", "Hall_WallPanel", Side.North, -12f, s);
            foreach (float s in new[] { 0f, 6f, 12f, 24f })
                added += WallPanel(ew, $"S_{s:0}", "Hall_WallPanel", Side.South, -42f, s);
            foreach (float s in new[] { -42f, -36f })
                added += WallPanel(ew, $"E_{s:0}", "Hall_WallPanel", Side.East, 36f, s);

            Transform e = Group("Electrical/Machines");
            added += PutModel(e, "Transformer_1", "Elec_Transformer", new Vector3(29f, 0f, -38.3f), 0f);
            added += PutModel(e, "Transformer_2", "Elec_Transformer", new Vector3(23.5f, 0f, -32.8f), 0f);
            added += PutModel(e, "Transformer_3", "Elec_Transformer", new Vector3(30.5f, 0f, -32.8f), 0f);
            added += PutModel(e, "Switchgear_1", "Elec_Switchgear", new Vector3(0.85f, 0f, -38f), 90f);
            added += PutModel(e, "Switchgear_2", "Elec_Switchgear", new Vector3(0.85f, 0f, -35f), 90f);
            added += PutModel(e, "Switchgear_3", "Elec_Switchgear", new Vector3(0.85f, 0f, -32f), 90f);
            added += PutModel(e, "Riser_1", "Hall_PipeRiser", new Vector3(0.7f, 0f, -15.2f), 90f);
            added += PutModel(e, "Riser_2", "Hall_PipeRiser", new Vector3(0.7f, 9f, -15.2f), 90f);
            added += PutModel(e, "Riser_3", "Hall_PipeRiser", new Vector3(35.3f, 0f, -39f), -90f);
            for (int k = 0; k < 6; k++)
                added += PutModel(e, $"Truss_{k}", "Ceiling_Truss", new Vector3(6f + k * 6f, 17.75f, -33f), 0f);

            Transform b = Group("Electrical/BreakerRoom");
            added += PutModel(b, "Switchgear_1", "Elec_Switchgear", new Vector3(26f, BreakerFloor, -12.85f), 180f);
            added += PutModel(b, "Switchgear_2", "Elec_Switchgear", new Vector3(29f, BreakerFloor, -12.85f), 180f);

            // -------------------------------------------------------------- dust
            added += HallDust("Storage/Atmosphere", new Vector3(18f, 4f, 15f), new Vector3(34f, 7f, 28f));
            added += HallDust("Assembly/Atmosphere", new Vector3(72f, 4f, 15f), new Vector3(46f, 7f, 28f));
            added += HallDust("Electrical/Atmosphere", new Vector3(18f, 8f, -27f), new Vector3(34f, 15f, 28f));
            return added;
        }

        private static int HallDust(string group, Vector3 center, Vector3 size)
        {
            Transform t = Group(group);
            if (t.Find("Dust") != null)
            {
                return 0;
            }

            return Particles(t, "Dust", center, AtmosphereAssets.Dust, new Color(0.9f, 0.95f, 1f, 0.3f), size,
                260, 0.02f, 0.05f, 20f, 0.05f) != null ? 1 : 0;
        }

        /// <summary>Once: the primitive machines of pass one go; things against the walls move clear of the panels.</summary>
        private static int RemoveHallsV1()
        {
            if (root.Find(HallsVersionName) != null)
            {
                return 0;
            }

            int removed = 0;
            Transform assembly = root.Find("Assembly/Props");
            if (assembly != null)
            {
                for (int i = assembly.childCount - 1; i >= 0; i--)
                {
                    Transform c = assembly.GetChild(i);
                    if (c.name.StartsWith("Conveyor_") || c.name == "Station_Hull" || c.name == "Station_Engine"
                        || c.name.StartsWith("Arm_") || c.name.StartsWith("Crane_"))
                    {
                        Object.DestroyImmediate(c.gameObject);
                        removed++;
                    }
                }
            }

            Transform electric = root.Find("Electrical/Props");
            if (electric != null)
            {
                for (int i = electric.childCount - 1; i >= 0; i--)
                {
                    Transform c = electric.GetChild(i);
                    if (c.name.StartsWith("Cabinet_") || c.name.StartsWith("Transformer_") || c.name.StartsWith("Stripe_")
                        || c.name == "Pipes_2" || c.name == "Pipes_3" || c.name == "Tray_S")
                    {
                        Object.DestroyImmediate(c.gameObject);
                        removed++;
                    }
                }
            }

            Transform breaker = root.Find("Electrical/BreakerRoom");
            if (breaker != null)
            {
                for (int i = breaker.childCount - 1; i >= 0; i--)
                {
                    if (breaker.GetChild(i).name.StartsWith("Cabinet_"))
                    {
                        Object.DestroyImmediate(breaker.GetChild(i).gameObject);
                        removed++;
                    }
                }
            }

            // This locker stood inside the solid breaker tower.
            removed += MoveIfFound("Locker_Electric_E", new Vector3(35.42f, 0f, -31.5f));

            var marker = new GameObject(HallsVersionName);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return removed + 1;
        }
    }
}
