using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// What fills the rooms. Kit props where the kit has them (shelves, pallets, crates,
    /// barrels, carts, pipes, fuse boxes); the rest (laptop, conveyors, ship parts, robot arms,
    /// containment capsules, lab benches) is built from primitives with generated materials.
    /// Everything is named, so a deleted group is rebuilt on the next run and nothing else moves.
    /// </summary>
    public static partial class FacilityBuilder
    {
        // ---------------------------------------------------------------- storage

        /// <summary>Shelf rows sit on z 5, 9, ... 25, leaving 2.25 m aisles on the odd metres.</summary>
        private static int DressStorage()
        {
            Transform shelves = Group("Storage/Shelves");
            Transform props = Group("Storage/Props");
            int added = 0;

            float[] rows = { 5f, 9f, 13f, 17f, 21f };
            for (int r = 0; r < rows.Length; r++)
            {
                // Two groups per row with a cross aisle around x 17..19.5.
                added += ShelfRun(shelves, $"Row{r}A", 3.5f, 2, rows[r], r);
                added += ShelfRun(shelves, $"Row{r}B", 19.6f, 2, rows[r], r + 3);
            }

            // The north row stops short of the janitor's closet.
            added += ShelfRun(shelves, "Row5", 13.8f, 3, 25f, 11);

            // Loose stock in the lanes and along the walls.
            added += PutAligned(props, "Pallet_W1", "Pallet Variation 01", 0f, new Vector3(0.4f, 0f, 3f));
            added += PutAligned(props, "Pallet_W2", "Pallet Variation 05", 90f, new Vector3(0.4f, 0f, 6.5f));
            added += PutAligned(props, "Pallet_E1", "Pallet Variation 09", 0f, new Vector3(34.2f, 0f, 2f));
            added += PutAligned(props, "Pallet_E2", "Pallet Variation 03", 0f, new Vector3(34.2f, 0f, 11f));
            added += PutAligned(props, "Pallet_N1", "Pallet Variation 06", 0f, new Vector3(30.4f, 0f, 28.2f));
            added += PutAligned(props, "Pallet_Aisle", "Pallet Variation 07", 0f, new Vector3(17.2f, 0f, 14.3f));
            added += Put(props, "Cart_1", "Cart", new Vector3(18f, 0f, 7f), 75f);
            added += Put(props, "Cart_2", "Cart", new Vector3(33.8f, 0f, 27.5f), 180f);
            added += Put(props, "Bin_1", "Garbage Bin", new Vector3(0.6f, 0f, 22.8f), 0f);
            added += Put(props, "Bin_2", "Garbage Bin", new Vector3(35.4f, 0f, 19.8f), 0f);
            added += Put(props, "Barrel_1", "Barrel", new Vector3(0.7f, 0f, 9f), 30f);
            added += Put(props, "Barrel_2", "Barrel", new Vector3(1.6f, 0f, 9.3f), 110f);
            added += Put(props, "Barrel_3", "Barrel", new Vector3(35.2f, 0f, 6.5f), 200f);
            added += Put(props, "Extinguisher_1", "Fire Extinguisher", new Vector3(0f, 0f, 18.5f), 0f);
            added += Put(props, "Extinguisher_2", "Fire Extinguisher", new Vector3(36f, 0f, 12.5f), 180f);
            added += Put(props, "Pipes_W", "Pipes 02", new Vector3(0f, 0.4f, 27f), 0f);
            added += Put(props, "Cam_1", "Security Cam", new Vector3(0f, 6f, 1f), 0f);
            added += Put(props, "Exit_S", "Exit Sign", new Vector3(15f, 3.6f, 0f), 270f);
            added += Put(props, "Exit_E", "Exit Sign", new Vector3(36f, 3.6f, 21f), 180f);

            // Ducts under the roof.
            for (int k = 0; k < 5; k++) // 6 m pieces along z: 0 .. 30, the hall's depth
            {
                added += Put(props, "Duct_" + k, "Duct Straight", new Vector3(10.5f, 7.6f, k * 6f), 0f);
            }

            return added;
        }

        private static int ShelfRun(Transform parent, string prefix, float startX, int count, float z, int seed)
        {
            int added = 0;
            for (int k = 0; k < count; k++)
            {
                int variant = 1 + (seed + k * 2) % 6;
                float pivotX = startX + 6.17f + k * 6.3f;
                added += Put(parent, $"{prefix}_{k}", "Shelf Variation 0" + variant, new Vector3(pivotX, 0f, z), 0f);
            }

            return added;
        }

        // ---------------------------------------------------------------- janitor

        /// <summary>
        /// The start: a cramped, cluttered closet with a dead laptop and the first note. Furniture
        /// is modelled (Tools/Blender/facility_horror.py); v1's primitive boxes are removed once by
        /// <see cref="RemovePrimitiveFurniture"/>.
        /// </summary>
        private static int DressJanitor()
        {
            Transform t = Group("Janitor/Props");
            int added = 0;

            // Desk against the north wall with the laptop and a chair.
            Vector3 desk = new Vector3(8.6f, 0f, 29.3f);
            added += PutModel(t, "Furn_Desk", "Furn_Desk", desk, 180f);
            added += Laptop(t, "Laptop", desk + new Vector3(-0.2f, 0.78f, 0f)) ? 1 : 0;
            added += PutModel(t, "Furn_Chair", "Furn_OfficeChair", new Vector3(8.75f, 0f, 28.35f), 8f);

            // Steel shelving with cleaning supplies, a mop bucket, bin bags, a cot.
            added += PutModel(t, "Furn_Shelf_1", "Furn_MetalShelf", new Vector3(1.6f, 0f, 29.45f), 180f);
            added += PutModel(t, "Furn_Shelf_2", "Furn_MetalShelf_B", new Vector3(3.5f, 0f, 29.45f), 180f);
            added += PutModel(t, "Furn_MopBucket", "Furn_MopBucket", new Vector3(5.6f, 0f, 28.8f), 30f);
            added += PutModel(t, "Furn_TrashBags", "Furn_TrashBags", new Vector3(7.15f, 0f, 29.5f), 0f);
            added += PutModel(t, "Furn_Cot", "Furn_Cot", new Vector3(10.65f, 0f, 26.6f), 90f);
            added += PutModel(t, "Horror_Papers", "Horror_Papers", new Vector3(5f, 0f, 26.6f), 0f);

            added += Put(t, "Barrel", "Barrel", new Vector3(0.7f, 0f, 25.1f), 40f);
            added += Put(t, "Cart", "Cart", new Vector3(1.3f, 0f, 26.6f), 90f); // clear of the door
            added += Put(t, "Bin", "Garbage Bin", new Vector3(10.2f, 0f, 29.4f), 0f);
            added += Put(t, "Crate_1", "Crate Short", new Vector3(9.3f, 0f, 24.3f), 5f);
            added += Put(t, "Crate_2", "Crate Long", new Vector3(10.2f, 0f, 24.3f), 0f);
            added += Put(t, "Crate_3", "Crate Short", new Vector3(9.4f, 0.6f, 24.35f), 20f);
            added += Put(t, "Fusebox", "Fusebox 01", new Vector3(0f, 1.5f, 27.5f), 0f);
            added += Put(t, "Pipes", "Pipes 03", new Vector3(12f, 4.0f, 24.3f), 180f);
            added += Put(t, "Extinguisher", "Fire Extinguisher", new Vector3(12f, 0f, 29f), 180f);
            added += Put(t, "Hanging_Light", "Hanging Light", new Vector3(6f, JanitorCeiling, 27f), 0f);

            // More clutter: a loaded pallet mid-room, stacked crates.
            added += PutAligned(t, "Pallet_Mid", "Pallet Variation 05", 90f, new Vector3(6.3f, 0f, 24.4f));
            added += Put(t, "Crate_4", "Crate Long", new Vector3(0.3f, 0f, 27.6f), 0f);
            added += Put(t, "Crate_5", "Crate Short", new Vector3(0.35f, 0.6f, 27.9f), 12f);
            added += Put(t, "Crate_6", "Crate Short", new Vector3(0.4f, 1.2f, 27.85f), -8f);
            added += Put(t, "Barrel_2", "Barrel", new Vector3(1.4f, 0f, 24.9f), 160f);
            return added;
        }

        private static bool Table(Transform parent, string name, Vector3 at, Vector2 size, float height, Material top)
        {
            if (parent.Find(name + "_Top") != null)
            {
                return false;
            }

            Box(parent, name + "_Top", at + new Vector3(0f, height - 0.03f, 0f), new Vector3(size.x, 0.06f, size.y), top);
            float lx = size.x * 0.5f - 0.06f;
            float lz = size.y * 0.5f - 0.06f;
            int n = 0;
            foreach (Vector2 c in new[] { new Vector2(-lx, -lz), new Vector2(lx, -lz), new Vector2(-lx, lz), new Vector2(lx, lz) })
            {
                Box(parent, name + "_Leg" + n++, at + new Vector3(c.x, (height - 0.06f) * 0.5f, c.y),
                    new Vector3(0.05f, height - 0.06f, 0.05f), MetalMid, false);
            }

            return true;
        }

        /// <summary>Laptop on a desk; the screen lights up once the power is back (InfoPoint).</summary>
        private static bool Laptop(Transform parent, string name, Vector3 at)
        {
            if (parent.Find(name) != null)
            {
                return false;
            }

            var laptop = new GameObject(name);
            laptop.transform.SetParent(parent, false);
            laptop.transform.position = at;
            Box(laptop.transform, "Base", at + new Vector3(0f, 0.012f, 0f), new Vector3(0.38f, 0.024f, 0.26f), MetalDark, false);
            GameObject lid = Box(laptop.transform, "Lid", at + new Vector3(0f, 0.14f, 0.13f), new Vector3(0.38f, 0.25f, 0.012f), MetalDark, false);
            lid.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
            GameObject screen = Box(laptop.transform, "Screen", at + new Vector3(0f, 0.14f, 0.122f), new Vector3(0.34f, 0.21f, 0.004f), ScreenOff, false);
            screen.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

            BoxCollider hit = laptop.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0.12f, 0.05f);
            hit.size = new Vector3(0.45f, 0.3f, 0.4f);
            hit.isTrigger = true;
            return true;
        }

        /// <summary>A 1.8 m metal rack with four shelves of bottles, boxes and rags.</summary>
        private static bool MetalShelf(Transform parent, string name, Vector3 at, float yaw)
        {
            if (parent.Find(name) != null)
            {
                return false;
            }

            Transform shelf = new GameObject(name).transform;
            shelf.SetParent(parent, false);
            shelf.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            Material[] stuff = { PaintRed, PaintYellow, Plastic, Cloth };
            for (int level = 0; level < 4; level++)
            {
                float y = 0.15f + level * 0.55f;
                Box(shelf, "Board" + level, at + new Vector3(0f, y, 0f), new Vector3(1.6f, 0.03f, 0.5f), MetalMid);
                for (int k = 0; k < 4; k++)
                {
                    float h = Hash(at.x + k, y, level);
                    if (h < 0.3f)
                    {
                        continue;
                    }

                    Vector3 p = at + new Vector3(-0.6f + k * 0.4f, y + 0.12f, (h - 0.5f) * 0.15f);
                    if (h > 0.65f)
                        Cyl(shelf, $"Bottle{level}_{k}", p, 0.09f, 0.22f, stuff[k % stuff.Length], false);
                    else
                        Box(shelf, $"Box{level}_{k}", p + new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.26f, 0.32f), Paper, false, h * 30f);
                }
            }

            foreach (Vector2 c in new[] { new Vector2(-0.78f, -0.23f), new Vector2(0.78f, -0.23f), new Vector2(-0.78f, 0.23f), new Vector2(0.78f, 0.23f) })
            {
                Box(shelf, $"Post_{c.x:0.0}_{c.y:0.0}", at + new Vector3(c.x, 0.95f, c.y), new Vector3(0.04f, 1.9f, 0.04f), MetalDark);
            }

            return true;
        }

        // ------------------------------------------------------------- electrical

        private static int DressElectrical()
        {
            Transform t = Group("Electrical/Props");
            int added = 0;

            
            // Cable trays above deck one, along the west and south walls.
            Box(t, "Tray_W", new Vector3(0.45f, 6.8f, -27f), new Vector3(0.5f, 0.12f, 29.5f), MetalMid, false);
            for (int k = 0; k < 4; k++)
            {
                Cyl(t, "Cable_W" + k, new Vector3(0.35f + k * 0.08f, 6.9f, -27f), 0.07f, 29.5f, Rubber, false, new Vector3(90f, 0f, 0f));
            }

            // Kit props.
            added += Put(t, "Fusebox_1", "Fusebox 01", new Vector3(0f, 2f, -29f), 0f);
            added += Put(t, "Fusebox_2", "Fusebox 02", new Vector3(0f, 3.4f, -22f), 0f);
            added += Put(t, "Pipes_1", "Pipes 01", new Vector3(0f, -0.5f, -19f), 0f);
            added += Put(t, "Barrel_1", "Barrel", new Vector3(34.8f, 0f, -40.8f), 10f);
            added += Put(t, "Barrel_2", "Barrel", new Vector3(33.9f, 0f, -41f), 70f);
            added += Put(t, "Cart", "Cart", new Vector3(8f, 0f, -14f), 100f);
            added += PutAligned(t, "Pallet_1", "Pallet Variation 02", 0f, new Vector3(20f, 0f, -14f));
            added += Put(t, "Crate_Spur", "Crate Short", new Vector3(9.1f, LevelOne, -33.6f), 0f); // at the dead end (v2 grid)
            added += Put(t, "Exit_N", "Exit Sign", new Vector3(15f, 3.6f, -12f), 90f);
            added += Put(t, "Exit_E", "Exit Sign", new Vector3(36f, 3.6f, -27f), 180f);

            // Breaker room (on the tower, 9 m up): panel on the east wall (placed by the gameplay
            // step), fuse boxes, cabinets, cables up the wall, a desk with the reset instructions.
            Transform b = Group("Electrical/BreakerRoom");
            added += Put(b, "Fusebox_1", "Fusebox 01", new Vector3(36f, BreakerFloor + 1.6f, -21.5f), 180f);
            added += Put(b, "Fusebox_2", "Fusebox 02", new Vector3(36f, BreakerFloor + 3.6f, -15f), 180f);
            added += Put(b, "Pipes", "Pipes 03", new Vector3(36f, BreakerFloor + 3.9f, -24f), 180f);
            for (int k = 0; k < 4; k++)
            {
                Cyl(b, "Riser_" + k, new Vector3(35.6f, BreakerFloor + 2.9f, -19.6f + k * 0.12f), 0.08f, 3.2f, Rubber, false);
            }

            added += PutModel(b, "Furn_Desk", "Furn_Desk", new Vector3(26.2f, BreakerFloor, -16f), 90f);
            added += PutModel(b, "Furn_Chair", "Furn_OfficeChair_Fallen", new Vector3(27.4f, BreakerFloor, -17.6f), 140f);
            Box(b, "Stripe", new Vector3(35f, BreakerFloor + 0.005f, -18f), new Vector3(0.1f, 0.01f, 3f), PaintYellow, false);
            return added;
        }

        // --------------------------------------------------------------- assembly

        private static int DressAssembly()
        {
            Transform t = Group("Assembly/Props");
            int added = 0;


            // Three assembly stations between the lines: hull section, wing, engine.
            added += WingStation(t, "Station_Wing", new Vector3(72f, 0f, 15f)) ? 1 : 0;


            // Loading area by the bay door (east), lane to the door kept clear.
            added += PutAligned(t, "Load_P1", "Pallet Variation 01", 0f, new Vector3(91.5f, 0f, 3f));
            added += PutAligned(t, "Load_P2", "Pallet Variation 04", 0f, new Vector3(91.5f, 0f, 5f));
            added += PutAligned(t, "Load_P3", "Pallet Variation 05", 0f, new Vector3(93.5f, 0f, 3.5f));
            added += PutAligned(t, "Load_P4", "Pallet Variation 02", 0f, new Vector3(91.5f, 0f, 22.5f));
            added += PutAligned(t, "Load_P5", "Pallet Variation 06", 0f, new Vector3(93.6f, 0f, 24.5f));
            added += Put(t, "Load_Cart", "Cart", new Vector3(89.5f, 0f, 26.5f), 30f);
            Box(t, "Bay_Stripe_N", new Vector3(93f, 0.005f, 18.6f), new Vector3(6f, 0.01f, 0.2f), PaintYellow, false);
            Box(t, "Bay_Stripe_S", new Vector3(93f, 0.005f, 11.4f), new Vector3(6f, 0.01f, 0.2f), PaintYellow, false);

            // Workbench with its tool wall on the north side.
            added += PutModel(t, "Furn_Workbench", "Furn_Workbench", new Vector3(62f, 0f, 29.15f), 180f);

            added += Put(t, "Barrel_1", "Barrel", new Vector3(49f, 0f, 1f), 0f);
            added += Put(t, "Barrel_2", "Barrel", new Vector3(49.9f, 0f, 1.2f), 60f);
            added += Put(t, "Bin", "Garbage Bin", new Vector3(66f, 0f, 29.4f), 0f);
            added += Put(t, "Extinguisher", "Fire Extinguisher", new Vector3(48f, 0f, 12.5f), 0f);
            added += Put(t, "Exit_Bay", "Exit Sign", new Vector3(96f, 5.6f, 15f), 180f);
            added += Put(t, "Exit_W", "Exit Sign", new Vector3(48f, 3.6f, 21f), 0f);
            added += Put(t, "Cam", "Security Cam", new Vector3(96f, 6f, 28f), 180f);
            return added;
        }

        private static bool WingStation(Transform parent, string name, Vector3 at)
        {
            if (parent.Find(name) != null)
            {
                return false;
            }

            Transform s = new GameObject(name).transform;
            s.SetParent(parent, false);
            foreach (Vector2 c in new[] { new Vector2(-2.4f, -1f), new Vector2(2.4f, -1f), new Vector2(-2.4f, 1f), new Vector2(2.4f, 1f) })
            {
                Box(s, $"Stand_{c.x:0}_{c.y:0}", at + new Vector3(c.x, 0.55f + (c.y > 0 ? 0.25f : 0f), c.y), new Vector3(0.25f, 1.1f + (c.y > 0 ? 0.5f : 0f), 0.25f), MetalDark);
            }

            GameObject wing = Box(s, "Wing", at + new Vector3(0f, 1.35f, 0f), new Vector3(6.4f, 0.2f, 3f), MetalMid);
            wing.transform.rotation = Quaternion.Euler(-9f, 0f, 0f);
            GameObject flap = Box(s, "Flap", at + new Vector3(0f, 1.15f, -1.75f), new Vector3(5.8f, 0.12f, 0.6f), PaintYellow, false);
            flap.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            Box(s, "Zone", at + new Vector3(0f, 0.005f, -2.4f), new Vector3(7f, 0.01f, 0.15f), PaintYellow, false);
            return true;
        }

    }
}
