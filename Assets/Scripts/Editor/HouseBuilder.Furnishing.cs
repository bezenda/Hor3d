using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Furnishing v3: a lived-in, present-day house (the enemy's home) instead of an antique
    /// shop. Two kinds of pieces:
    ///
    ///  - Models: CC0 Poly Haven furniture (sofas, lounge chair, side tables, cabinet, desk...)
    ///    fitted to a box by HouseBuilder.Dressing.FitModel.
    ///  - Kits: pieces Poly Haven has no modern model for, built from textured boxes - kitchen
    ///    runs with wall cabinets, a fridge, chests of drawers, book-filled shelving, a TV
    ///    unit, a washing machine, a toilet, plain tables. Kits are made in the piece's own
    ///    space (front +z, standing on y 0) so yaw and position work exactly like models.
    ///
    /// Sizes are real-world furniture sizes, a touch generous, so every piece reads at the
    /// same scale next to the 1.8 m player and enemy. Layout rules (navigation): solids stand
    /// against a wall, or are centred between cell centres with the short side under 1 m, so
    /// no patrol cell loses its node. Gameplay objects (wardrobes, beds, the breaker, items)
    /// keep their places; batteries and bottles settle onto whatever is now under them.
    /// </summary>
    public static partial class HouseBuilder
    {
        private const string DressingVersionName = "Dressing_v5";

        // Kit ids.
        private const string KitCounter = "counter";
        private const string KitUppers = "uppers";
        private const string KitFridge = "fridge";
        private const string KitDrawers = "drawers";
        private const string KitShelf = "shelf";
        private const string KitTvUnit = "tvunit";
        private const string KitWasher = "washer";
        private const string KitToilet = "toilet";
        private const string KitTable = "table";
        private const string KitVanity = "vanity";
        private const string KitLinenShelf = "linenshelf";   // shelving with folded towels
        private const string KitStoreShelf = "storeshelf";   // shelving with boxes and jars
        private const string KitBasket = "basket";           // laundry basket with clothes
        private const string KitIroning = "ironing";         // ironing board with an iron
        private const string KitBottles = "bottles";         // detergent bottles on a surface
        private const string KitTowelRail = "towelrail";     // wall rail with a towel
        private const string KitMirror = "mirror";           // wall mirror
        private const string KitBasinTop = "basintop";       // basin and tap on a sink box
        private const string KitTubTop = "tubtop";           // rim, water and tap on the tub box

        private static Furnish K(string name, string kit, float x, float y, float z, float yaw, float w, float h, float d)
        {
            return new Furnish
            {
                Name = name, Kit = kit, Base = new Vector3(x, y, z), Yaw = yaw,
                Fit = new Vector3(w, h, d), Solid = true
            };
        }

        /// <summary>A kit without collider or navigation footprint (wall cabinets).</summary>
        private static Furnish KD(string name, string kit, float x, float y, float z, float yaw, float w, float h, float d)
        {
            return new Furnish
            {
                Name = name, Kit = kit, Base = new Vector3(x, y, z), Yaw = yaw,
                Fit = new Vector3(w, h, d), Solid = false
            };
        }

        /// <summary>
        /// Every piece. Yaw: 0 front faces +z (stands against the north wall), 180 faces -z
        /// (south wall), 90 faces +x (west wall), -90 faces -x (east wall). Wall faces sit 0.1 m
        /// from the grid line.
        /// </summary>
        private static readonly Furnish[] FurnitureList =
        {
            // ---------------------------------------------------------- ground floor

            // Kitchen (x -12..-4, z -8..-2). Wardrobe hiding spot at x -6 on the north wall.
            K("Kitchen_Counter", KitCounter, -9.33f, 0f, -7.58f, 0f, 5.1f, 0.92f, 0.62f),
            KD("Kitchen_Uppers", KitUppers, -9.33f, 1.5f, -7.72f, 0f, 5.1f, 0.72f, 0.34f),
            K("Kitchen_Fridge", KitFridge, -11.53f, 0f, -6.6f, 90f, 0.8f, 1.95f, 0.72f),
            K("Kitchen_CounterWest", KitCounter, -11.59f, 0f, -4.5f, 90f, 2.4f, 0.92f, 0.62f),
            K("Kitchen_Island", KitCounter, -8f, 0f, -4f, 0f, 1.8f, 0.92f, 0.9f),
            D("Kitchen_StoolA", "bar_chair_round_01", -8.5f, 0f, -3.22f, 180f, 0.42f, 0.78f, 0.42f, false),
            D("Kitchen_StoolB", "bar_chair_round_01", -7.5f, 0f, -3.22f, 180f, 0.42f, 0.78f, 0.42f, false),
            D("Kitchen_Clock", "wall_clock", -4.12f, 2.05f, -7f, -90f, 0.36f, 0.36f, 0.06f, false),
            D("Kitchen_Vase", "ceramic_vase_01", -8.3f, 1.2f, -4.1f, 0f, 0.18f, 0.3f, 0.18f),

            // Back hall: a shoe cabinet under the stair.
            K("BackHall_Shoes", KitDrawers, -3.73f, 0f, -7.2f, 90f, 0.9f, 0.55f, 0.36f),

            // Dining room (x 0..8, z -8..-2).
            K("Dining_Table", KitTable, 4f, 0f, -4f, 0f, 2.2f, 0.76f, 1f),
            D("Dining_ChairA", "dining_chair_02", 3.4f, 0f, -4.78f, 0f, 0.48f, 0.95f, 0.5f, false),
            D("Dining_ChairB", "dining_chair_02", 4.6f, 0f, -4.78f, 0f, 0.48f, 0.95f, 0.5f, false),
            D("Dining_ChairC", "dining_chair_02", 3.4f, 0f, -3.22f, 180f, 0.48f, 0.95f, 0.5f, false),
            D("Dining_ChairD", "dining_chair_02", 4.6f, 0f, -3.22f, 180f, 0.48f, 0.95f, 0.5f, false),
            F("Dining_Sideboard", "modern_wooden_cabinet", 4.5f, 0f, -7.62f, 0f, 1.8f, 0.85f, 0.48f),
            D("Dining_Plant", "potted_plant_04", 7.5f, 0f, -7.45f, 0f, 0.55f, 1.2f, 0.55f, false),
            D("Dining_Frame", "hanging_picture_frame_02", 0.12f, 1.55f, -4f, 90f, 0.8f, 1f, 0.05f, false),

            // Utility room (x 8..12, z -8..-2). Wardrobe hiding spot at x 10 on the north wall.
            K("Utility_Washer", KitWasher, 11.6f, 0f, -5.4f, -90f, 0.62f, 0.86f, 0.62f),
            K("Utility_Dryer", KitWasher, 11.6f, 0f, -4.7f, -90f, 0.62f, 0.86f, 0.62f),
            F("Utility_Rack", "worn_metal_rack", 11.68f, 0f, -3.1f, -90f, 1.2f, 1.9f, 0.42f),
            F("Utility_Boxes", "cardboard_box_01", 8.6f, 0f, -7.6f, 0f, 0.6f, 0.45f, 0.5f),
            KD("Utility_Bottles", KitBottles, 11.6f, 0.86f, -5.05f, -90f, 0.5f, 0.28f, 0.3f),
            K("Utility_Basket", KitBasket, 11.6f, 0f, -6.55f, -90f, 0.5f, 0.55f, 0.36f),
            KD("Utility_Ironing", KitIroning, 8.3f, 0f, -3f, 90f, 1.2f, 0.88f, 0.36f),

            // Pantry (locked): a tall shelf.
            K("Pantry_Shelf", KitStoreShelf, -11.72f, 0f, -1f, 90f, 1.5f, 2.1f, 0.36f),

            // Living room (x -12..-2, z 0..8). The low table over the key is the crouch roof.
            F("Living_Sofa", "sofa_02", -4f, 0f, 4f, -90f, 2.3f, 0.85f, 0.95f),
            F("Living_CoffeeTable", "modern_coffee_table_01", -6f, 0f, 4f, 90f, 1.2f, 0.42f, 0.65f),
            K("Living_TvUnit", KitTvUnit, -11.7f, 0f, 4f, 90f, 2f, 0.48f, 0.42f),
            F("Living_Armchair", "mid_century_lounge_chair", -8f, 0f, 6.6f, 150f, 0.8f, 0.85f, 0.85f),
            F("Living_SideTable", "side_table_01", -4f, 0f, 5.75f, 0f, 0.5f, 0.55f, 0.5f),
            K("Living_Bookcase", KitShelf, -9.6f, 0f, 7.72f, 180f, 1.8f, 2.1f, 0.36f),
            D("Living_Plant", "potted_plant_04", -11.45f, 0f, 7.45f, 0f, 0.55f, 1.2f, 0.55f, false),
            D("Living_Pillows", "throw_pillows_01", -4f, 0.45f, 4f, -90f, 1.2f, 0.4f, 0.4f),
            D("Living_Vase", "ceramic_vase_01", -11.2f, 1.6f, 0.7f, 0f, 0.2f, 0.32f, 0.2f),
            D("Living_Frame", "hanging_picture_frame_03", -4.5f, 1.6f, 0.12f, 0f, 0.7f, 0.9f, 0.05f, false),

            // Foyer.
            K("Foyer_Console", KitDrawers, -1.72f, 0f, 6.2f, 90f, 1.2f, 0.85f, 0.4f),
            D("Foyer_Frame", "standing_picture_frame_01", -1.75f, 0.85f, 5.8f, 90f, 0.22f, 0.28f, 0.12f),
            D("Foyer_Plant", "potted_plant_04", -1.45f, 0f, 7.45f, 0f, 0.5f, 1.1f, 0.5f, false),

            // Library / den (x 4..12, z 0..4 and x 4..8, z 4..8).
            K("Library_ShelfWest", KitShelf, 5.1f, 0f, 0.3f, 0f, 1.8f, 2.2f, 0.38f),
            K("Library_ShelfEast", KitShelf, 9.6f, 0f, 0.3f, 0f, 2.4f, 2.2f, 0.38f),
            F("Library_Desk", "metal_office_desk", 8f, 0f, 2f, 0f, 1.4f, 0.76f, 0.7f),
            D("Library_Chair", "dining_chair_02", 8f, 0f, 2.75f, 180f, 0.48f, 0.95f, 0.5f, false),
            D("Library_Laptop", "classic_laptop", 8.25f, 1.2f, 1.9f, 180f, 0.34f, 0.24f, 0.26f),
            F("Library_Sofa", "sofa_03", 11.55f, 0f, 2f, -90f, 2f, 0.85f, 0.9f),
            D("Library_Plant", "potted_plant_04", 4.5f, 0f, 3.5f, 0f, 0.5f, 1.1f, 0.5f, false),

            // Toilet (x 8..12, z 4..8).
            K("Toilet_Bowl", KitToilet, 9.4f, 0f, 7.62f, 180f, 0.42f, 0.8f, 0.68f),
            KD("Toilet_SinkTop", KitBasinTop, 11.65f, 0.9f, 7f, -90f, 0.6f, 0.2f, 0.5f),
            KD("Toilet_Mirror", KitMirror, 11.88f, 1.2f, 7f, -90f, 0.5f, 0.7f, 0.03f),
            KD("Toilet_TowelRail", KitTowelRail, 10.6f, 1.0f, 7.9f, 180f, 0.5f, 0.5f, 0.06f),

            // ----------------------------------------------------------- upper floor

            // Master bedroom (x -12..-4, z -8..-2). Bed hiding spot at x -6, wardrobe at x -9.
            F("Master_NightstandL", "side_table_01", -7.15f, U, -7.6f, 0f, 0.48f, 0.55f, 0.42f),
            F("Master_NightstandR", "side_table_01", -4.85f, U, -7.6f, 0f, 0.48f, 0.55f, 0.42f),
            K("Master_Drawers", KitDrawers, -11.65f, U, -5f, 90f, 1.5f, 0.95f, 0.5f),
            D("Master_Frame", "hanging_picture_frame_02", -11.88f, U + 1.6f, -5f, 90f, 0.9f, 1.1f, 0.05f, false),
            D("Master_Vase", "ceramic_vase_01", -7.15f, U + 1f, -7.6f, 0f, 0.16f, 0.26f, 0.16f),
            F("Master_Chair", "mid_century_lounge_chair", -10.6f, U, -3f, 135f, 0.8f, 0.85f, 0.85f),

            // Linen room: one cell wide, so only the north wall takes solids.
            K("Linen_Basket", KitBasket, -1f, U, -7.73f, 0f, 0.5f, 0.55f, 0.34f),
            K("Linen_Shelf", KitLinenShelf, -3f, U, -7.72f, 0f, 1.4f, 2.1f, 0.36f),

            // Study (x 0..8, z -8..-2).
            F("Study_Desk", "metal_office_desk", 2f, U, -7.55f, 0f, 1.6f, 0.76f, 0.7f),
            D("Study_Chair", "dining_chair_02", 2f, U, -6.75f, 180f, 0.48f, 0.95f, 0.5f, false),
            D("Study_Laptop", "classic_laptop", 1.7f, U + 1.2f, -7.6f, 0f, 0.34f, 0.24f, 0.26f),
            K("Study_Shelf", KitShelf, 6.4f, U, -7.7f, 0f, 2.4f, 2.1f, 0.38f),
            F("Study_Cabinet", "modern_wooden_cabinet", 7.72f, U, -3f, -90f, 1.2f, 0.85f, 0.45f),
            D("Study_Plant", "potted_plant_04", 0.5f, U, -2.5f, 0f, 0.5f, 1.1f, 0.5f, false),

            // Storeroom (x 8..12, z -8..-2): the enemy's start and the breaker (north wall, x 9.55..10.45).
            F("Store_Rack", "worn_metal_rack", 11.68f, U, -5f, -90f, 1.2f, 1.9f, 0.42f),
            F("Store_CrateA", "wooden_crate_01", 11.5f, U, -7.45f, 10f, 0.7f, 0.7f, 0.7f),
            F("Store_Boxes", "cardboard_box_01", 8.6f, U, -7.5f, -20f, 0.6f, 0.45f, 0.5f),
            K("Store_Shelf", KitStoreShelf, 8.28f, U, -3f, 90f, 1.2f, 2f, 0.36f),

            // Guest bedroom (x -12..-4, z 0..8). Bed hiding spot at x -8, wardrobe on the west wall.
            F("Guest_NightstandL", "side_table_01", -9.15f, U, 7.6f, 180f, 0.48f, 0.55f, 0.42f),
            F("Guest_NightstandR", "side_table_01", -6.85f, U, 7.6f, 180f, 0.48f, 0.55f, 0.42f),
            K("Guest_Drawers", KitDrawers, -10f, U, 0.36f, 0f, 1.4f, 0.95f, 0.5f),
            D("Guest_Frame", "hanging_picture_frame_03", -6f, U + 1.6f, 7.88f, 180f, 0.7f, 0.9f, 0.05f, false),
            F("Guest_Desk", "metal_office_desk", -4.45f, U, 2f, -90f, 1f, 0.76f, 0.7f),
            D("Guest_DeskChair", "dining_chair_02", -5.2f, U, 2f, 90f, 0.48f, 0.95f, 0.5f, false),
            F("Guest_Chair", "mid_century_lounge_chair", -11f, U, 6.9f, 135f, 0.8f, 0.85f, 0.85f),
            D("Guest_Plant", "potted_plant_04", -4.5f, U, 7.45f, 0f, 0.5f, 1.1f, 0.5f, false),

            // Playroom (x -4..4, z 0..8). Bed hiding spot at x -2.
            F("Play_Table", "painted_wooden_table", 0f, U, 3f, 0f, 1f, 0.6f, 0.7f),
            F("Play_Chair", "painted_wooden_chair_01", 0f, U, 2.2f, 0f, 0.4f, 0.7f, 0.4f),
            K("Play_Shelf", KitShelf, 3f, U, 7.7f, 180f, 1.4f, 1.1f, 0.36f),
            F("Play_Ottoman", "Ottoman_01", -3.3f, U, 4.8f, 0f, 0.6f, 0.42f, 0.6f),

            // Bathroom (x 4..12, z 0..4 and x 4..8, z 4..8). Tub on the east wall.
            K("Bath_Vanity", KitVanity, 8f, U, 0.42f, 0f, 1.2f, 0.86f, 0.52f),
            K("Bath_Toilet", KitToilet, 4.43f, U, 2f, 90f, 0.42f, 0.8f, 0.68f),
            KD("Bath_TubTop", KitTubTop, 11.6f, U + 0.6f, 2f, -90f, 1.8f, 0.05f, 0.6f),
            KD("Bath_TowelRail", KitTowelRail, 5.1f, U + 1.0f, 0.1f, 0f, 0.6f, 0.5f, 0.06f),

            // Small room (x 8..12, z 4..8): a box room with a desk.
            F("Small_Desk", "metal_office_desk", 9.6f, U, 7.55f, 180f, 1.4f, 0.76f, 0.7f),
            F("Small_Boxes", "cardboard_box_01", 8.5f, U, 4.6f, 15f, 0.6f, 0.45f, 0.5f),

            // Corridors: pictures between the doorways.
            D("CorridorG_FrameA", "hanging_picture_frame_02", -5f, 1.6f, -1.88f, 0f, 0.7f, 0.9f, 0.05f, false),
            D("CorridorG_FrameB", "hanging_picture_frame_03", 6f, 1.6f, -1.88f, 0f, 0.6f, 0.8f, 0.05f, false),
            D("CorridorU_FrameA", "hanging_picture_frame_03", -6f, U + 1.6f, -1.88f, 0f, 0.6f, 0.8f, 0.05f, false),
            D("CorridorU_FrameB", "hanging_picture_frame_02", 8f, U + 1.6f, -1.88f, 0f, 0.7f, 0.9f, 0.05f, false),
        };

        // ------------------------------------------------------------- materials

        private struct KitMaterials
        {
            public Material Cabinet;    // matte off-white doors and bodies
            public Material Walnut;     // dark wood
            public Material Countertop; // marble
            public Material Metal;      // handles, appliances
            public Material Black;      // plinths, gaps, screens
            public Material Ceramic;    // toilet, basin
            public Material[] Books;
            public Material[] Towels;
            public Material Cardboard;
            public Material Jar;
            public Material Water;
        }

        private static KitMaterials kitMaterials;

        private static void LoadKitMaterials()
        {
            kitMaterials = new KitMaterials
            {
                Cabinet = FlatMaterial("Modern_Cabinet", new Color(0.78f, 0.77f, 0.74f), 0.3f),
                Walnut = FlatMaterial("Modern_Walnut", new Color(0.26f, 0.19f, 0.15f), 0.4f),
                Countertop = SurfaceMaterial("marble_01", 1f, 0.65f, new Color(0.85f, 0.85f, 0.84f)),
                Metal = FlatMaterial("Modern_Metal", new Color(0.62f, 0.62f, 0.63f), 0.75f),
                Black = FlatMaterial("Modern_Black", new Color(0.03f, 0.03f, 0.035f), 0.8f),
                Ceramic = FlatMaterial("Modern_Ceramic", new Color(0.86f, 0.86f, 0.84f), 0.85f),
                Books = new[]
                {
                    FlatMaterial("Book_Red", new Color(0.36f, 0.1f, 0.08f), 0.25f),
                    FlatMaterial("Book_Navy", new Color(0.1f, 0.14f, 0.24f), 0.25f),
                    FlatMaterial("Book_Olive", new Color(0.26f, 0.27f, 0.15f), 0.25f),
                    FlatMaterial("Book_Cream", new Color(0.7f, 0.65f, 0.55f), 0.25f),
                    FlatMaterial("Book_Grey", new Color(0.3f, 0.3f, 0.31f), 0.25f),
                    FlatMaterial("Book_Ochre", new Color(0.55f, 0.38f, 0.14f), 0.25f)
                },
                Towels = new[]
                {
                    FlatMaterial("Towel_White", new Color(0.82f, 0.81f, 0.78f), 0.1f),
                    FlatMaterial("Towel_Grey", new Color(0.42f, 0.42f, 0.43f), 0.1f),
                    FlatMaterial("Towel_Sage", new Color(0.4f, 0.47f, 0.4f), 0.1f),
                    FlatMaterial("Towel_Navy", new Color(0.14f, 0.18f, 0.3f), 0.1f)
                },
                Cardboard = FlatMaterial("Modern_Cardboard", new Color(0.55f, 0.42f, 0.28f), 0.15f),
                Jar = FlatMaterial("Modern_Jar", new Color(0.42f, 0.27f, 0.1f), 0.7f),
                Water = FlatMaterial("Modern_Water", new Color(0.16f, 0.19f, 0.2f), 0.95f)
            };
        }

        // ------------------------------------------------------------------ kits

        /// <summary>Builds a kit under the root (front +z, standing on y 0). Returns its size.</summary>
        private static Vector3 BuildKit(Transform root, Furnish item)
        {
            if (kitMaterials.Cabinet == null)
            {
                LoadKitMaterials();
            }

            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root, false);

            float w = item.Fit.x, h = item.Fit.y, d = item.Fit.z;
            KitMaterials m = kitMaterials;

            switch (item.Kit)
            {
                case KitCounter:
                    Counter(visuals, w, h, d, m.Cabinet, m.Countertop);
                    break;
                case KitVanity:
                    Counter(visuals, w, h, d, m.Walnut, m.Ceramic);
                    Part(visuals, "Basin", new Vector3(0f, h + 0.06f, 0f), new Vector3(0.5f, 0.12f, 0.36f), m.Ceramic);
                    Part(visuals, "Mirror", new Vector3(0f, h + 0.75f, -d * 0.5f + 0.02f), new Vector3(0.7f, 0.85f, 0.02f), m.Metal);
                    break;
                case KitUppers:
                    Cabinets(visuals, w, h, d, m.Cabinet, 0.08f);
                    break;
                case KitFridge:
                    Part(visuals, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), m.Metal);
                    Part(visuals, "Split", new Vector3(0f, h * 0.66f, d * 0.5f + 0.002f), new Vector3(w - 0.02f, 0.008f, 0.004f), m.Black);
                    Part(visuals, "HandleTop", new Vector3(w * 0.5f - 0.07f, h * 0.8f, d * 0.5f + 0.03f), new Vector3(0.025f, 0.4f, 0.03f), m.Black);
                    Part(visuals, "HandleBottom", new Vector3(w * 0.5f - 0.07f, h * 0.45f, d * 0.5f + 0.03f), new Vector3(0.025f, 0.55f, 0.03f), m.Black);
                    break;
                case KitDrawers:
                    Drawers(visuals, w, h, d, m.Walnut, m.Black, m.Metal);
                    break;
                case KitShelf:
                    Shelf(visuals, item.Name, w, h, d, m.Walnut, m.Books);
                    break;
                case KitLinenShelf:
                    Shelf(visuals, item.Name, w, h, d, m.Cabinet, m.Towels, ShelfFill.Linen);
                    break;
                case KitStoreShelf:
                    Shelf(visuals, item.Name, w, h, d, m.Metal, new[] { m.Cardboard, m.Jar, m.Cabinet }, ShelfFill.Storage);
                    break;
                case KitBasket:
                {
                    const float wall = 0.025f;
                    float bh = h * 0.8f;
                    Part(visuals, "Floor", new Vector3(0f, 0.01f, 0f), new Vector3(w, 0.02f, d), m.Ceramic);
                    Part(visuals, "WallF", new Vector3(0f, bh * 0.5f, d * 0.5f - wall * 0.5f), new Vector3(w, bh, wall), m.Ceramic);
                    Part(visuals, "WallB", new Vector3(0f, bh * 0.5f, -d * 0.5f + wall * 0.5f), new Vector3(w, bh, wall), m.Ceramic);
                    Part(visuals, "WallL", new Vector3(-w * 0.5f + wall * 0.5f, bh * 0.5f, 0f), new Vector3(wall, bh, d), m.Ceramic);
                    Part(visuals, "WallR", new Vector3(w * 0.5f - wall * 0.5f, bh * 0.5f, 0f), new Vector3(wall, bh, d), m.Ceramic);
                    // A heap of clothes spilling over the rim.
                    Part(visuals, "ClothA", new Vector3(-0.06f, bh - 0.02f, 0.02f), new Vector3(w * 0.75f, 0.12f, d * 0.7f), m.Towels[3]);
                    Part(visuals, "ClothB", new Vector3(0.08f, bh + 0.05f, -0.03f), new Vector3(w * 0.5f, 0.1f, d * 0.55f), m.Towels[1]);
                    Part(visuals, "ClothC", new Vector3(-0.02f, bh + 0.1f, 0.04f), new Vector3(w * 0.3f, 0.07f, d * 0.4f), m.Towels[0]);
                    break;
                }
                case KitIroning:
                    Part(visuals, "Board", new Vector3(0f, h - 0.02f, 0f), new Vector3(w, 0.03f, d), m.Towels[2]);
                    Part(visuals, "Nose", new Vector3(w * 0.5f + 0.08f, h - 0.02f, 0f), new Vector3(0.16f, 0.03f, d * 0.6f), m.Towels[2]);
                    Part(visuals, "LegA", new Vector3(-w * 0.3f, (h - 0.04f) * 0.5f, 0f), new Vector3(0.03f, h - 0.04f, 0.03f), m.Metal);
                    Part(visuals, "LegB", new Vector3(w * 0.25f, (h - 0.04f) * 0.5f, 0f), new Vector3(0.03f, h - 0.04f, 0.03f), m.Metal);
                    Part(visuals, "FootA", new Vector3(-w * 0.3f, 0.015f, 0f), new Vector3(0.04f, 0.03f, d * 0.9f), m.Metal);
                    Part(visuals, "FootB", new Vector3(w * 0.25f, 0.015f, 0f), new Vector3(0.04f, 0.03f, d * 0.9f), m.Metal);
                    Part(visuals, "Iron", new Vector3(-w * 0.35f, h + 0.06f, 0f), new Vector3(0.26f, 0.1f, 0.12f), m.Ceramic);
                    Part(visuals, "IronHandle", new Vector3(-w * 0.35f, h + 0.14f, 0f), new Vector3(0.18f, 0.03f, 0.03f), m.Black);
                    break;
                case KitBottles:
                    Part(visuals, "BottleA", new Vector3(-w * 0.3f, 0.12f, 0f), new Vector3(0.1f, 0.24f, 0.07f), m.Towels[3]);
                    Part(visuals, "BottleB", new Vector3(-w * 0.08f, 0.1f, 0.02f), new Vector3(0.09f, 0.2f, 0.07f), m.Ceramic);
                    Part(visuals, "BoxA", new Vector3(w * 0.22f, 0.08f, -0.02f), new Vector3(0.18f, 0.16f, 0.1f), m.Jar);
                    Part(visuals, "CapA", new Vector3(-w * 0.3f, 0.255f, 0f), new Vector3(0.04f, 0.03f, 0.04f), m.Black);
                    break;
                case KitTowelRail:
                    Part(visuals, "Rail", new Vector3(0f, h, 0f), new Vector3(w, 0.02f, 0.02f), m.Metal);
                    Part(visuals, "BracketL", new Vector3(-w * 0.5f, h, -0.025f), new Vector3(0.02f, 0.02f, 0.05f), m.Metal);
                    Part(visuals, "BracketR", new Vector3(w * 0.5f, h, -0.025f), new Vector3(0.02f, 0.02f, 0.05f), m.Metal);
                    Part(visuals, "TowelFront", new Vector3(0f, h * 0.55f, 0.015f), new Vector3(w * 0.8f, h * 0.9f, 0.012f), m.Towels[0]);
                    Part(visuals, "TowelBack", new Vector3(0f, h * 0.7f, -0.012f), new Vector3(w * 0.8f, h * 0.6f, 0.012f), m.Towels[0]);
                    break;
                case KitMirror:
                    Part(visuals, "Frame", new Vector3(0f, h * 0.5f, 0f), new Vector3(w + 0.04f, h + 0.04f, d), m.Black);
                    Part(visuals, "Glass", new Vector3(0f, h * 0.5f, d * 0.5f + 0.002f), new Vector3(w, h, 0.004f), m.Water);
                    break;
                case KitBasinTop:
                    Part(visuals, "Basin", new Vector3(0f, 0.06f, 0.03f), new Vector3(w * 0.75f, 0.12f, d * 0.7f), m.Ceramic);
                    Part(visuals, "Bowl", new Vector3(0f, 0.121f, 0.03f), new Vector3(w * 0.55f, 0.004f, d * 0.45f), m.Water);
                    Part(visuals, "Tap", new Vector3(0f, 0.2f, -d * 0.5f + 0.06f), new Vector3(0.03f, 0.16f, 0.03f), m.Metal);
                    Part(visuals, "Spout", new Vector3(0f, 0.27f, -d * 0.5f + 0.12f), new Vector3(0.025f, 0.025f, 0.12f), m.Metal);
                    break;
                case KitTubTop:
                {
                    const float rim = 0.07f;
                    Part(visuals, "RimF", new Vector3(0f, 0.02f, d * 0.5f - rim * 0.5f), new Vector3(w, 0.04f, rim), m.Ceramic);
                    Part(visuals, "RimB", new Vector3(0f, 0.02f, -d * 0.5f + rim * 0.5f), new Vector3(w, 0.04f, rim), m.Ceramic);
                    Part(visuals, "RimL", new Vector3(-w * 0.5f + rim * 0.5f, 0.02f, 0f), new Vector3(rim, 0.04f, d), m.Ceramic);
                    Part(visuals, "RimR", new Vector3(w * 0.5f - rim * 0.5f, 0.02f, 0f), new Vector3(rim, 0.04f, d), m.Ceramic);
                    Part(visuals, "Water", new Vector3(0f, 0.003f, 0f), new Vector3(w - rim * 2f, 0.004f, d - rim * 2f), m.Water);
                    Part(visuals, "Tap", new Vector3(w * 0.5f - 0.12f, 0.12f, -d * 0.5f + 0.04f), new Vector3(0.03f, 0.2f, 0.03f), m.Metal);
                    Part(visuals, "Spout", new Vector3(w * 0.5f - 0.12f, 0.21f, -d * 0.5f + 0.1f), new Vector3(0.03f, 0.03f, 0.12f), m.Metal);
                    Part(visuals, "Towel", new Vector3(-w * 0.3f, 0.05f, d * 0.5f - 0.02f), new Vector3(0.4f, 0.03f, 0.12f), m.Towels[2]);
                    break;
                }
                case KitTvUnit:
                    Cabinets(visuals, w, h, d, m.Walnut, 0.2f);
                    Part(visuals, "TvFoot", new Vector3(0f, h + 0.01f, 0f), new Vector3(0.36f, 0.02f, 0.2f), m.Black);
                    Part(visuals, "TvNeck", new Vector3(0f, h + 0.07f, -0.02f), new Vector3(0.06f, 0.12f, 0.04f), m.Black);
                    Part(visuals, "TvScreen", new Vector3(0f, h + 0.5f, 0f), new Vector3(1.3f, 0.75f, 0.045f), m.Black);
                    break;
                case KitWasher:
                    Part(visuals, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), m.Ceramic);
                    Part(visuals, "Panel", new Vector3(0f, h - 0.05f, d * 0.5f + 0.002f), new Vector3(w - 0.02f, 0.08f, 0.004f), m.Metal);
                    Part(visuals, "Door", new Vector3(0f, h * 0.45f, d * 0.5f + 0.006f), new Vector3(0.38f, 0.38f, 0.012f), m.Black);
                    break;
                case KitToilet:
                    Part(visuals, "Bowl", new Vector3(0f, 0.2f, 0.06f), new Vector3(w * 0.9f, 0.4f, d * 0.75f), m.Ceramic);
                    Part(visuals, "Seat", new Vector3(0f, 0.415f, 0.08f), new Vector3(w * 0.95f, 0.03f, d * 0.7f), m.Ceramic);
                    Part(visuals, "Tank", new Vector3(0f, 0.6f, -d * 0.5f + 0.09f), new Vector3(w, 0.4f, 0.18f), m.Ceramic);
                    break;
                case KitTable:
                    Part(visuals, "Top", new Vector3(0f, h - 0.02f, 0f), new Vector3(w, 0.04f, d), m.Walnut);
                    for (int i = 0; i < 4; i++)
                    {
                        float lx = (i % 2 == 0 ? -1f : 1f) * (w * 0.5f - 0.1f);
                        float lz = (i < 2 ? -1f : 1f) * (d * 0.5f - 0.1f);
                        Part(visuals, "Leg" + i, new Vector3(lx, (h - 0.04f) * 0.5f, lz), new Vector3(0.05f, h - 0.04f, 0.05f), m.Black);
                    }

                    break;
                default:
                    Part(visuals, "Box", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), m.Walnut);
                    break;
            }

            return item.Fit;
        }

        /// <summary>Base cabinets: recessed plinth, door run with gaps and bar handles, a top.</summary>
        private static void Counter(Transform parent, float w, float h, float d, Material body, Material top)
        {
            KitMaterials m = kitMaterials;
            Part(parent, "Plinth", new Vector3(0f, 0.05f, -0.04f), new Vector3(w - 0.02f, 0.1f, d - 0.1f), m.Black);
            Part(parent, "Body", new Vector3(0f, 0.1f + (h - 0.14f) * 0.5f, -0.01f), new Vector3(w, h - 0.14f, d - 0.04f), body);
            Part(parent, "Top", new Vector3(0f, h - 0.02f, 0f), new Vector3(w + 0.02f, 0.04f, d), top);

            float front = d * 0.5f - 0.03f;
            int doors = Mathf.Max(1, Mathf.RoundToInt(w / 0.6f));
            float doorWidth = w / doors;
            for (int i = 0; i < doors; i++)
            {
                float x = -w * 0.5f + doorWidth * (i + 0.5f);
                if (i > 0)
                {
                    Part(parent, "Gap" + i, new Vector3(x - doorWidth * 0.5f, 0.1f + (h - 0.14f) * 0.5f, front + 0.002f),
                        new Vector3(0.006f, h - 0.16f, 0.004f), m.Black);
                }

                Part(parent, "Handle" + i, new Vector3(x, h - 0.2f, front + 0.012f), new Vector3(Mathf.Min(0.3f, doorWidth * 0.5f), 0.012f, 0.02f), m.Metal);
            }
        }

        /// <summary>A plain cabinet block (wall units, TV unit) with door gaps and handles.</summary>
        private static void Cabinets(Transform parent, float w, float h, float d, Material body, float handleHeight)
        {
            KitMaterials m = kitMaterials;
            Part(parent, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), body);
            int doors = Mathf.Max(1, Mathf.RoundToInt(w / 0.6f));
            float doorWidth = w / doors;
            for (int i = 0; i < doors; i++)
            {
                float x = -w * 0.5f + doorWidth * (i + 0.5f);
                if (i > 0)
                {
                    Part(parent, "Gap" + i, new Vector3(x - doorWidth * 0.5f, h * 0.5f, d * 0.5f + 0.002f), new Vector3(0.006f, h - 0.02f, 0.004f), m.Black);
                }

                Part(parent, "Handle" + i, new Vector3(x, handleHeight, d * 0.5f + 0.012f), new Vector3(Mathf.Min(0.24f, doorWidth * 0.4f), 0.012f, 0.02f), m.Metal);
            }
        }

        /// <summary>A chest of drawers on short legs: three rows of drawers, a handle on each.</summary>
        private static void Drawers(Transform parent, float w, float h, float d, Material body, Material legs, Material handle)
        {
            const float legHeight = 0.12f;
            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1f : 1f) * (w * 0.5f - 0.05f);
                float lz = (i < 2 ? -1f : 1f) * (d * 0.5f - 0.05f);
                Part(parent, "Leg" + i, new Vector3(lx, legHeight * 0.5f, lz), new Vector3(0.035f, legHeight, 0.035f), legs);
            }

            float bodyHeight = h - legHeight;
            Part(parent, "Body", new Vector3(0f, legHeight + bodyHeight * 0.5f, 0f), new Vector3(w, bodyHeight, d), body);

            int rows = bodyHeight > 0.6f ? 3 : 2;
            float rowHeight = bodyHeight / rows;
            for (int r = 0; r < rows; r++)
            {
                float y = legHeight + rowHeight * (r + 0.5f);
                if (r > 0)
                {
                    Part(parent, "Gap" + r, new Vector3(0f, legHeight + rowHeight * r, d * 0.5f + 0.002f), new Vector3(w - 0.03f, 0.006f, 0.004f), legs);
                }

                Part(parent, "Handle" + r, new Vector3(0f, y, d * 0.5f + 0.012f), new Vector3(Mathf.Min(0.36f, w * 0.3f), 0.014f, 0.02f), handle);
            }
        }

        /// <summary>
        /// Open shelving filled with books. Books are batched per colour into one mesh each, so
        /// a full bookcase is a handful of draw calls rather than two hundred boxes.
        /// </summary>
        private enum ShelfFill { Books, Linen, Storage }

        private static void Shelf(Transform parent, string seedName, float w, float h, float d, Material wood, Material[] books,
            ShelfFill fill = ShelfFill.Books)
        {
            const float board = 0.03f;
            Part(parent, "SideL", new Vector3(-w * 0.5f + board * 0.5f, h * 0.5f, 0f), new Vector3(board, h, d), wood);
            Part(parent, "SideR", new Vector3(w * 0.5f - board * 0.5f, h * 0.5f, 0f), new Vector3(board, h, d), wood);
            Part(parent, "Back", new Vector3(0f, h * 0.5f, -d * 0.5f + 0.006f), new Vector3(w, h, 0.012f), wood);

            int levels = Mathf.Max(2, Mathf.RoundToInt(h / 0.4f));
            float spacing = (h - board) / levels;
            var perColour = new List<CombineInstance>[books.Length];
            for (int i = 0; i < books.Length; i++)
            {
                perColour[i] = new List<CombineInstance>();
            }

            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var random = new System.Random(seedName.GetHashCode());
            float inner = w - board * 2f;

            for (int level = 0; level <= levels; level++)
            {
                float y = board * 0.5f + spacing * level;
                Part(parent, "Board" + level, new Vector3(0f, y, 0f), new Vector3(inner, board, d - 0.01f), wood);

                if (level == levels)
                {
                    break;
                }

                if (fill != ShelfFill.Books)
                {
                    FillShelfLevel(perColour, cube, random, fill, inner, d, y + board * 0.5f, spacing);
                    continue;
                }

                // Fill most of the shelf, leaving the odd gap and a few books lying flat.
                float x = -inner * 0.5f + 0.02f;
                float end = inner * 0.5f - 0.02f;
                while (x < end - 0.03f)
                {
                    if (random.NextDouble() < 0.08)
                    {
                        x += 0.08f + (float)random.NextDouble() * 0.15f;
                        continue;
                    }

                    float thickness = 0.022f + (float)random.NextDouble() * 0.03f;
                    float height = Mathf.Min(spacing - 0.06f, 0.17f + (float)random.NextDouble() * 0.11f);
                    float depth = d - 0.08f - (float)random.NextDouble() * 0.05f;
                    if (x + thickness > end)
                    {
                        break;
                    }

                    var matrix = Matrix4x4.TRS(
                        new Vector3(x + thickness * 0.5f, y + board * 0.5f + height * 0.5f, 0.01f),
                        Quaternion.identity,
                        new Vector3(thickness, height, depth));
                    perColour[random.Next(books.Length)].Add(new CombineInstance { mesh = cube, transform = matrix });
                    x += thickness + 0.002f;
                }
            }

            for (int i = 0; i < books.Length; i++)
            {
                if (perColour[i].Count == 0)
                {
                    continue;
                }

                var mesh = new Mesh { name = $"Books_{seedName}_{i}" };
                mesh.CombineMeshes(perColour[i].ToArray(), true, true);
                var go = new GameObject("Books_" + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = books[i];
            }
        }

        /// <summary>
        /// One shelf level of folded towels (stacks of flat slabs) or storage (cardboard boxes
        /// with jars in front). Colours index into the shelf's material list, as for books.
        /// </summary>
        private static void FillShelfLevel(List<CombineInstance>[] perColour, Mesh cube, System.Random random,
            ShelfFill fill, float inner, float d, float y, float spacing)
        {
            float x = -inner * 0.5f + 0.03f;
            float end = inner * 0.5f - 0.03f;
            while (x < end - 0.12f)
            {
                if (fill == ShelfFill.Linen)
                {
                    float width = 0.28f + (float)random.NextDouble() * 0.08f;
                    if (x + width > end)
                    {
                        break;
                    }

                    int colour = random.Next(perColour.Length);
                    int folds = Mathf.Min(6, Mathf.FloorToInt((spacing - 0.08f) / 0.055f));
                    folds = Mathf.Max(2, folds - random.Next(3));
                    for (int f = 0; f < folds; f++)
                    {
                        var matrix = Matrix4x4.TRS(
                            new Vector3(x + width * 0.5f + ((float)random.NextDouble() - 0.5f) * 0.02f, y + 0.0275f + f * 0.055f, 0.01f),
                            Quaternion.identity, new Vector3(width, 0.05f, d - 0.1f));
                        perColour[colour].Add(new CombineInstance { mesh = cube, transform = matrix });
                    }

                    x += width + 0.04f;
                    continue;
                }

                if (random.NextDouble() < 0.35)
                {
                    int count = 3 + random.Next(3);
                    for (int j = 0; j < count && x + 0.09f < end; j++)
                    {
                        float jh = 0.12f + (float)random.NextDouble() * 0.06f;
                        var matrix = Matrix4x4.TRS(new Vector3(x + 0.045f, y + jh * 0.5f, d * 0.25f),
                            Quaternion.identity, new Vector3(0.08f, jh, 0.08f));
                        perColour[1].Add(new CombineInstance { mesh = cube, transform = matrix });
                        x += 0.1f;
                    }

                    x += 0.04f;
                    continue;
                }

                float boxWidth = 0.25f + (float)random.NextDouble() * 0.17f;
                float boxHeight = Mathf.Min(spacing - 0.06f, 0.16f + (float)random.NextDouble() * 0.14f);
                if (x + boxWidth > end)
                {
                    break;
                }

                var box = Matrix4x4.TRS(new Vector3(x + boxWidth * 0.5f, y + boxHeight * 0.5f, 0f),
                    Quaternion.Euler(0f, ((float)random.NextDouble() - 0.5f) * 8f, 0f), new Vector3(boxWidth, boxHeight, d - 0.08f));
                perColour[random.NextDouble() < 0.8 ? 0 : 2].Add(new CombineInstance { mesh = cube, transform = box });
                x += boxWidth + 0.03f;
            }
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            AddDecorBox(parent, name, position, size, material);
        }

        // ---------------------------------------------------------- ceiling lamps

        /// <summary>
        /// A modern pendant over every room lamp, so the light has a source. The shade casts no
        /// shadow (the lamp sits inside it).
        /// </summary>
        private static int EnsureCeilingLamps(Transform house, Transform dressing)
        {
            Transform lighting = house.Find("Lighting");
            if (lighting == null || dressing.Find("CeilingLamps") != null)
            {
                return 0;
            }

            var root = new GameObject("CeilingLamps").transform;
            root.SetParent(dressing, false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Models/modern_ceiling_lamp_01/modern_ceiling_lamp_01.fbx");
            if (source == null)
            {
                return 1;
            }

            foreach (Transform lamp in lighting)
            {
                float ceiling = lamp.position.y >= UpperFloor ? UpperCeiling : GroundCeiling;
                var item = new Furnish { Name = lamp.name, Fit = new Vector3(0.42f, 0.42f, 0.42f) };

                var holder = new GameObject("Lamp_" + lamp.name).transform;
                holder.SetParent(root, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Model";
                model.transform.SetParent(holder, false);

                var fitHolder = new GameObject("Visuals").transform;
                fitHolder.SetParent(holder, false);
                model.transform.SetParent(fitHolder, false);
                Vector3 size = FitModel(fitHolder, model.transform, item);
                holder.position = new Vector3(lamp.position.x, ceiling - size.y, lamp.position.z);

                foreach (Renderer renderer in holder.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                foreach (Collider collider in holder.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return 1;
        }

        // ---------------------------------------------------------------- upgrade

        /// <summary>
        /// Scenes dressed before v3 (antique furniture, open-backed frames, skirting across the
        /// stair walls) lose their old dressing once, so the builder can lay the new one.
        /// </summary>
        private const string RoomsMarkerName = "Rooms_v1";

        /// <summary>
        /// Room identity pass: the pantry and linen shelves change from books to storage and
        /// towels. Only those two kits are rebuilt, once; everything else (including pieces the
        /// owner rescaled by hand) is left alone, and new pieces are simply added by name.
        /// </summary>
        private static int UpgradeRoomIdentity(Transform dressing)
        {
            if (dressing.Find(RoomsMarkerName) != null)
            {
                return 0;
            }

            int changed = 0;
            foreach (string name in new[] { "Furn_Pantry_Shelf", "Furn_Linen_Shelf" })
            {
                Transform old = dressing.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    changed++;
                }
            }

            new GameObject(RoomsMarkerName).transform.SetParent(dressing, false);
            return changed + 1;
        }

        private static int UpgradeDressing(Transform dressing)
        {
            if (dressing.Find(DressingVersionName) != null)
            {
                return 0;
            }

            var doomed = new List<GameObject>();
            foreach (Transform child in dressing)
            {
                if ((child.name.StartsWith("Furn_") || child.name == "Skirting" || child.name == "CeilingLamps"
                    || child.name.StartsWith("Rug_") || child.name.StartsWith("Tiles_")
                    || child.name.StartsWith("Dressing_v"))
                    && !HandEditTracker.Protected(child)) // Hand-edited pieces survive a re-lay.
                {
                    doomed.Add(child.gameObject);
                }
            }

            foreach (GameObject go in doomed)
            {
                Object.DestroyImmediate(go);
            }

            new GameObject(DressingVersionName).transform.SetParent(dressing, false);
            return doomed.Count + 1;
        }

        /// <summary>Everything under Dressing is static: batched, never moves.</summary>
        private static void MarkDressingStatic(Transform dressing)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in dressing.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        /// <summary>Walls along the two stair strips get no skirting: the ramp would cut through it.</summary>
        private static bool NearStair(Vector3 centre, Vector3 size)
        {
            foreach (Stair stair in Stairs)
            {
                float x0 = MinX + Cell * stair.Column - 0.3f;
                float x1 = x0 + Cell + 0.6f;
                int lowRow = Mathf.Min(stair.FootRow, stair.TopRow);
                float z0 = MinZ + Cell * lowRow - 0.3f;
                float z1 = z0 + Cell * 2f + 0.6f;

                bool overlapX = centre.x + size.x * 0.5f > x0 && centre.x - size.x * 0.5f < x1;
                bool overlapZ = centre.z + size.z * 0.5f > z0 && centre.z - size.z * 0.5f < z1;
                if (overlapX && overlapZ)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
