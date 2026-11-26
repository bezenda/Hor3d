using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// House dressing: textures on the shell and real furniture instead of grey boxes. Purely
    /// visual on top of the plan in HouseBuilder.Layout.cs - colliders, navigation and every
    /// gameplay object stay where the plan puts them.
    ///
    ///  - Surfaces: CC0 Poly Haven textures (Assets/Art/PolyHaven/Textures) on URP Lit
    ///    materials. A stretched unit cube cannot carry a texture, so every shell box gets a
    ///    mesh whose UVs are in metres (cached per size in Assets/Art/Generated/BoxMeshes.asset);
    ///    the top face is its own sub-mesh, so a slab is plank floor above and plaster below.
    ///  - Furniture: CC0 Poly Haven models (Assets/Art/PolyHaven/Models/&lt;id&gt;/&lt;id&gt;.fbx),
    ///    each scaled to fit a box, set on the floor and given a box collider of that size.
    ///    Without the art a textured box stands in, so the builder works in a bare checkout.
    ///  - Decor (lamps, vases, frames) has no collider and is set down on whatever is below.
    ///  - Floor coverings (tiles, rugs) are 1 cm, collider-less overlays.
    ///
    /// Navigation: furniture footprints are known up front (FurnitureList), and a cell centre
    /// inside a footprint grown by 0.5 m is not a node - see BuildNavNodes.
    /// </summary>
    public static partial class HouseBuilder
    {
        private const string ArtRoot = "Assets/Art/PolyHaven";
        private const string GeneratedFolder = "Assets/Art/Generated";
        private const string BoxMeshAssetPath = GeneratedFolder + "/BoxMeshes.asset";
        private const string DressingRootName = "Dressing";

        // -------------------------------------------------------------- furniture

        private struct Furnish
        {
            public string Name;
            public string Model;      // Poly Haven id; null = fallback box only
            public Vector3 Base;      // centre of the footprint, at the bottom
            public float Yaw;         // 0: front faces +z
            public Vector3 Fit;       // width (local x), height, depth (local z)
            public bool Solid;        // collider + navigation footprint
            public bool Settle;       // drop onto whatever is below (decor on furniture)
            public float ModelYaw;    // extra turn when a model's own front is not +z
            public string Kit;        // built from boxes (HouseBuilder.Furnishing.cs) instead of a model
        }

        private static Furnish F(string name, string model, float x, float y, float z, float yaw,
            float w, float h, float d, float modelYaw = 0f)
        {
            return new Furnish
            {
                Name = name, Model = model, Base = new Vector3(x, y, z), Yaw = yaw,
                Fit = new Vector3(w, h, d), Solid = true, ModelYaw = modelYaw
            };
        }

        /// <summary>Decor: no collider, no navigation footprint; optionally set down on what is below.</summary>
        private static Furnish D(string name, string model, float x, float y, float z, float yaw,
            float w, float h, float d, bool settle = true, float modelYaw = 0f)
        {
            return new Furnish
            {
                Name = name, Model = model, Base = new Vector3(x, y, z), Yaw = yaw,
                Fit = new Vector3(w, h, d), Solid = false, Settle = settle, ModelYaw = modelYaw
            };
        }

        private const float U = UpperFloor;


        /// <summary>The prop boxes that furniture models replaced; removed from older scenes.</summary>
        private static readonly string[] ReplacedPropBoxes =
        {
            "House_Living_Sofa", "House_Living_Bookcase", "House_Living_Crate", "House_Foyer_Sideboard",
            "House_Kitchen_Counter", "House_Kitchen_Stove", "House_Kitchen_Table", "House_Pantry_Shelf",
            "House_Dining_Table", "House_Dining_Sideboard", "House_Utility_Washer", "House_Library_ShelfWest",
            "House_Library_ShelfEast", "House_Library_Table", "House_Linen_Shelf", "House_Study_Desk",
            "House_Store_BoxA", "House_Store_BoxB"
        };

        /// <summary>True when a navigation node at this point would stand inside furniture.</summary>
        private static bool IsUnderFurniture(Vector3 point)
        {
            const float clearance = 0.5f;

            foreach (Furnish item in FurnitureList)
            {
                if (!item.Solid || Mathf.Abs(item.Base.y - point.y) > 0.5f)
                {
                    continue;
                }

                bool sideways = Mathf.Abs(Mathf.Repeat(item.Yaw, 180f) - 90f) < 45f;
                float halfX = (sideways ? item.Fit.z : item.Fit.x) * 0.5f + clearance;
                float halfZ = (sideways ? item.Fit.x : item.Fit.z) * 0.5f + clearance;

                if (Mathf.Abs(point.x - item.Base.x) < halfX && Mathf.Abs(point.z - item.Base.z) < halfZ)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- dressing

        /// <summary>Textures, furniture, floor coverings. Safe to run again.</summary>
        private static int EnsureDressing(Transform house)
        {
            Stage1PlaygroundBuilder.EnsureFolder("Assets", "Art");
            Stage1PlaygroundBuilder.EnsureFolder("Assets/Art", "Generated");

            // A present-day house: painted plaster, parquet downstairs, laminate upstairs, white
            // tiles in kitchen and bathrooms, grey rugs. Slightly dirty tints - it is lived in,
            // not a show home.
            Material wall = SurfaceMaterial("painted_plaster_wall", 2f, 0.08f, new Color(0.8f, 0.78f, 0.74f));
            Material floor = SurfaceMaterial("herringbone_parquet", 1.6f, 0.3f, new Color(0.62f, 0.52f, 0.43f));
            Material upperFloor = SurfaceMaterial("laminate_floor_02", 2f, 0.3f, new Color(0.62f, 0.55f, 0.48f));
            Material ceiling = SurfaceMaterial("plaster_grey_04", 2f, 0.05f, new Color(0.74f, 0.73f, 0.7f));
            Material stairs = SurfaceMaterial("dark_wooden_planks", 1.5f, 0.2f, new Color(0.7f, 0.62f, 0.55f));
            // Smooth walnut veneer (kitchen_wood is a panelled-door texture and made every
            // wardrobe look carved).
            Material darkWood = FlatMaterial("Modern_Walnut", new Color(0.26f, 0.19f, 0.15f), 0.4f);
            Material kitchenTiles = SurfaceMaterial("long_white_tiles", 1.2f, 0.55f, new Color(0.78f, 0.78f, 0.76f));
            Material bathTiles = SurfaceMaterial("long_white_tiles", 1.2f, 0.55f, new Color(0.78f, 0.78f, 0.76f));
            Material concrete = SurfaceMaterial("painted_concrete", 2f, 0.2f, new Color(0.6f, 0.6f, 0.6f));
            Material carpet = SurfaceMaterial("dirty_carpet", 1.5f, 0.02f, new Color(0.38f, 0.37f, 0.38f));
            Material ceramic = FlatMaterial("House_Ceramic", new Color(0.86f, 0.86f, 0.84f), 0.85f);

            int changed = 0;

            // Shell.
            foreach (Transform child in house)
            {
                string name = child.name;
                if (name == "House_Floor_Ground")
                {
                    changed += DressBox(child, floor, floor);
                }
                else if (name.StartsWith("House_Floor_Upper"))
                {
                    changed += DressBox(child, upperFloor, ceiling);
                }
                else if (name == "House_Ceiling")
                {
                    changed += DressBox(child, ceiling, ceiling);
                }
                else if (name.StartsWith("House_G_Wall") || name.StartsWith("House_U_Wall"))
                {
                    changed += DressBox(child, wall, wall);
                }
                else if (name.StartsWith("House_Stair_"))
                {
                    changed += DressBox(child, stairs, stairs);
                }
                else if (name == "House_Living_CrouchRoof" || name == "House_Play_ToyChest")
                {
                    changed += DressBox(child, stairs, stairs);
                }
                else if (name == "House_Living_CrouchSupport" && child.gameObject.activeSelf)
                {
                    // The full-depth support panel made the low table read as a solid block.
                    // Legs replace it (below); the roof alone keeps the enemy out.
                    child.gameObject.SetActive(false);
                    changed++;
                }
                else if (name == "House_Bath_Tub" || name == "House_Toilet_Sink")
                {
                    changed += DressBox(child, ceramic, ceramic);
                }
            }

            // Old prop boxes the models replace.
            foreach (string old in ReplacedPropBoxes)
            {
                Transform box = house.Find(old);
                if (box != null)
                {
                    Object.DestroyImmediate(box.gameObject);
                    changed++;
                }
            }

            Transform dressing = house.Find(DressingRootName);
            if (dressing == null)
            {
                dressing = new GameObject(DressingRootName).transform;
                dressing.SetParent(house, false);
                changed++;
            }

            // v3 replaces the antique dressing once (furniture, rugs, skirting).
            changed += UpgradeDressing(dressing);
            changed += UpgradeRoomIdentity(dressing);

            // Floor coverings: 1 cm, no colliders.
            changed += EnsureOverlay(dressing, "Tiles_Kitchen", kitchenTiles, new Rect(-11.9f, -7.9f, 7.8f, 5.8f), 0f);
            changed += EnsureOverlay(dressing, "Tiles_Utility", concrete, new Rect(8.1f, -7.9f, 3.8f, 5.8f), 0f);
            changed += EnsureOverlay(dressing, "Tiles_Toilet", bathTiles, new Rect(8.1f, 4.1f, 3.8f, 3.8f), 0f);
            changed += EnsureOverlay(dressing, "Tiles_BathroomA", bathTiles, new Rect(4.1f, 0.1f, 7.8f, 3.8f), U);
            changed += EnsureOverlay(dressing, "Tiles_BathroomB", bathTiles, new Rect(4.1f, 4f, 3.8f, 3.9f), U);
            changed += EnsureOverlay(dressing, "Rug_Living", carpet, new Rect(-7.6f, 2.4f, 3.2f, 3.2f), 0f);
            changed += EnsureOverlay(dressing, "Rug_Library", carpet, new Rect(9.2f, 0.9f, 2.0f, 2.2f), 0f);
            changed += EnsureOverlay(dressing, "Rug_Bath", carpet, new Rect(10.5f, 1.3f, 0.6f, 1.4f), U);
            changed += EnsureOverlay(dressing, "Rug_Master", carpet, new Rect(-7.4f, -6.4f, 2.8f, 2.2f), U);
            changed += EnsureOverlay(dressing, "Rug_Guest", carpet, new Rect(-9.4f, 4.4f, 2.8f, 2.2f), U);
            changed += EnsureOverlay(dressing, "Rug_Play", carpet, new Rect(-1.4f, 1.9f, 2.8f, 2.2f), U);

            changed += EnsureSkirting(house, dressing, darkWood);
            changed += EnsureCeilingLamps(house, dressing);

            // Legs of the low table over the iron key (roof x -11.9..-9.5, z 0.1..1.5, 1.1 m up).
            foreach (Vector2 leg in new[] { new Vector2(-11.82f, 0.18f), new Vector2(-9.58f, 0.18f),
                                            new Vector2(-11.82f, 1.42f), new Vector2(-9.58f, 1.42f) })
            {
                string legName = $"CrouchTable_Leg_{leg.x:0.0}_{leg.y:0.0}";
                if (dressing.Find(legName) == null)
                {
                    AddDecorBox(dressing, legName, new Vector3(leg.x, 0.55f, leg.y), new Vector3(0.09f, 1.1f, 0.09f), stairs);
                    changed++;
                }
            }

            // Furniture first (they carry colliders), then decor that settles onto it.
            foreach (Furnish item in FurnitureList)
            {
                if (item.Solid)
                {
                    changed += EnsureFurniture(dressing, item, darkWood);
                }
            }

            Physics.SyncTransforms();

            foreach (Furnish item in FurnitureList)
            {
                if (!item.Solid)
                {
                    changed += EnsureFurniture(dressing, item, darkWood);
                }
            }

            Physics.SyncTransforms();
            changed += SettleItems();
            // Plain walnut, not carved planks: the wardrobes stand in a modern house.
            DressHidingPrefabs(darkWood, carpet);
            DressSharedMaterials(darkWood);
            changed += RefitFurnitureColliders(dressing);
            Physics.SyncTransforms();
            changed += SwitchOffNodesInFurniture();
            MarkDressingStatic(dressing);

            return changed;
        }

        // -------------------------------------------------------------- skirting

        private const float SkirtHeight = 0.12f;
        private const float SkirtDepth = 0.02f;

        /// <summary>
        /// A dark wooden skirting board along both faces of every full-height wall piece, so
        /// floor and wall no longer meet in a bare paper-thin seam. Collider-less and visual
        /// only; door openings are separate wall pieces, so boards stop at every doorway.
        /// Delete House/Dressing/Skirting to rebuild them.
        /// </summary>
        private static int EnsureSkirting(Transform house, Transform dressing, Material wood)
        {
            Transform skirting = dressing.Find("Skirting");
            if (skirting != null)
            {
                return 0;
            }

            skirting = new GameObject("Skirting").transform;
            skirting.SetParent(dressing, false);
            int added = 1;

            foreach (Transform wall in house)
            {
                string name = wall.name;
                if (!(name.StartsWith("House_G_Wall") || name.StartsWith("House_U_Wall")) || name.EndsWith("_Lintel"))
                {
                    continue;
                }

                Vector3 size = wall.localScale;
                Vector3 centre = wall.localPosition;
                float bottom = centre.y - size.y * 0.5f;

                // Only pieces that stand on a floor (not lintels, not stair walls hanging mid-air).
                bool onGround = Mathf.Abs(bottom) < 0.25f;
                bool onUpper = Mathf.Abs(bottom - UpperFloor) < 0.25f || Mathf.Abs(bottom - GroundCeiling) < 0.25f;
                if (size.y < 1.5f || !(onGround || onUpper) || NearStair(centre, size))
                {
                    continue;
                }

                float floorY = onGround ? 0f : UpperFloor;
                float y = floorY + SkirtHeight * 0.5f;
                bool alongX = size.x >= size.z;
                float thickness = alongX ? size.z : size.x;
                float length = alongX ? size.x : size.z;
                if (length < 0.3f)
                {
                    continue;
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    float offset = side * (thickness * 0.5f + SkirtDepth * 0.5f);
                    Vector3 position = alongX
                        ? new Vector3(centre.x, y, centre.z + offset)
                        : new Vector3(centre.x + offset, y, centre.z);
                    Vector3 boardSize = alongX
                        ? new Vector3(length, SkirtHeight, SkirtDepth)
                        : new Vector3(SkirtDepth, SkirtHeight, length);

                    AddDecorBox(skirting, $"Skirt_{name}_{(side < 0 ? "A" : "B")}", position, boardSize, wood);
                    added++;
                }
            }

            return added;
        }

        // ------------------------------------------------------------- furniture

        /// <summary>
        /// Model furniture someone rescaled by hand (its Visuals or Model) keeps a collider of
        /// the old size, so the player walks into it or bumps into air. Every build fits each
        /// model piece's box collider to what is drawn. Kits keep theirs on purpose (a TV or a
        /// mirror above a cabinet is not something to stand on).
        /// </summary>
        private static int RefitFurnitureColliders(Transform dressing)
        {
            var models = new HashSet<string>();
            foreach (Furnish item in FurnitureList)
            {
                if (item.Solid && item.Kit == null)
                {
                    models.Add("Furn_" + item.Name);
                }
            }

            int changed = 0;
            foreach (Transform root in dressing)
            {
                BoxCollider collider = root.GetComponent<BoxCollider>();
                if (collider == null || !models.Contains(root.name))
                {
                    continue;
                }

                bool any = false;
                var local = new Bounds();
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    // Mesh-space corners, so a model turned 135 degrees is not boxed by its
                    // loose world-space bounds.
                    Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                        : renderer.GetComponent<MeshFilter>() != null ? renderer.GetComponent<MeshFilter>().sharedMesh : null;
                    if (mesh == null)
                    {
                        continue;
                    }

                    Bounds meshBounds = mesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = new Vector3(
                            (i & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                            (i & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                            (i & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                        Vector3 point = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                        if (!any)
                        {
                            local = new Bounds(point, Vector3.zero);
                            any = true;
                        }
                        else
                        {
                            local.Encapsulate(point);
                        }
                    }
                }

                if (!any)
                {
                    continue;
                }

                Vector3 size = local.size;
                if ((collider.size - size).sqrMagnitude > 0.0004f || (collider.center - local.center).sqrMagnitude > 0.0004f)
                {
                    collider.size = size;
                    collider.center = local.center;
                    EditorUtility.SetDirty(collider);
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// A navigation node that furniture now covers (someone moved or enlarged a piece) is
        /// switched off - EnemyNavigation skips inactive nodes - and switched back on once it is
        /// clear again. Nodes are never deleted: their names are indices other scenes rely on.
        /// </summary>
        private static int SwitchOffNodesInFurniture()
        {
            GameObject navigation = GameObject.Find("Enemy_Navigation");
            if (navigation == null)
            {
                return 0;
            }

            int changed = 0;
            foreach (Transform node in navigation.GetComponentsInChildren<Transform>(true))
            {
                if (!node.name.StartsWith("Node_"))
                {
                    continue;
                }

                bool blocked = false;
                foreach (Collider hit in Physics.OverlapSphere(node.position + Vector3.up * 0.4f, 0.25f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.name.StartsWith("Furn_"))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (node.gameObject.activeSelf == blocked)
                {
                    node.gameObject.SetActive(!blocked);
                    changed++;
                    if (blocked)
                    {
                        Debug.LogWarning($"[House] {node.name} at {node.position} is inside furniture now; switched off.");
                    }
                }
            }

            return changed;
        }

        private static int EnsureFurniture(Transform parent, Furnish item, Material fallback)
        {
            string rootName = "Furn_" + item.Name;
            if (parent.Find(rootName) != null || HandEditTracker.WasDeleted(parent, rootName))
            {
                return 0; // Exists, or the owner deleted it by hand.
            }

            var root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            root.transform.position = item.Base;
            root.transform.rotation = Quaternion.Euler(0f, item.Yaw, 0f);
            root.isStatic = item.Solid;

            GameObject source = item.Model != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Models/{item.Model}/{item.Model}.fbx")
                : null;

            Vector3 fitted;

            if (item.Kit != null)
            {
                fitted = BuildKit(root.transform, item);
            }
            else if (source != null)
            {
                // An unrotated holder takes the (slightly non-uniform) fit scale along the
                // room's axes; the model inside keeps its own FBX axis rotation.
                var holder = new GameObject("Visuals");
                holder.transform.SetParent(root.transform, false);
                holder.isStatic = item.Solid;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Model";
                model.transform.SetParent(holder.transform, false);
                fitted = FitModel(holder.transform, model.transform, item);

                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider);
                }

                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.gameObject.isStatic = item.Solid;

                    // Picture-frame glass imports as an opaque white sheet over the painting.
                    // Swap that slot for clear glass (switching the renderer off used to hide
                    // whole frames whose glass shared a mesh with the painting).
                    Material[] materials = renderer.sharedMaterials;
                    bool swapped = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null && materials[i].name.ToLowerInvariant().Contains("glass"))
                        {
                            materials[i] = ClearGlass();
                            swapped = true;
                        }
                    }

                    if (swapped)
                    {
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
            else
            {
                // No art in this checkout: a textured box of the same size keeps the room usable.
                GameObject box = Stage1PlaygroundBuilder.CreateBox("Visuals", root.transform,
                    new Vector3(0f, item.Fit.y * 0.5f, 0f), item.Fit, fallback);
                Object.DestroyImmediate(box.GetComponent<Collider>());
                DressBox(box.transform, fallback, fallback);
                fitted = item.Fit;
            }

            if (item.Solid)
            {
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = fitted;
                collider.center = new Vector3(0f, fitted.y * 0.5f, 0f);
            }

            if (item.Settle)
            {
                SetDown(root.transform, item.Base.y - 3f);
            }

            return 1;
        }

        /// <summary>Most a model may be stretched on one axis relative to another before it looks wrong.</summary>
        private const float MaxFurnitureStretch = 1.4f;

        /// <summary>How far past its box a model may grow sideways (the nav footprint has 0.5 m slack).</summary>
        private const float FurnitureOverflow = 1.12f;

        /// <summary>
        /// Fits a model to the item's box: turns it so its long side runs along the box's long
        /// side, gives it the box's height, and brings width and depth as close to the box as a
        /// modest stretch (MaxFurnitureStretch) allows. Scaling uniformly to fit inside the box
        /// used to shrink slim or deep models to doll's-house size (a 0.43 m kitchen table,
        /// 1.2 m bookcases). The scale goes on the unrotated holder; the model is centred and
        /// stood on the root. Returns the fitted size in the root's space.
        /// </summary>
        private static Vector3 FitModel(Transform holder, Transform model, Furnish item)
        {
            // Blender FBX roots carry their own axis rotation (-90 about x); turning the model
            // must keep it, or tables and chairs end up lying on their side.
            Quaternion authored = model.localRotation;
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;
            model.localRotation = Quaternion.Euler(0f, item.ModelYaw, 0f) * authored;

            Bounds bounds = LocalBounds(model);
            bool modelLongX = bounds.size.x >= bounds.size.z;
            bool boxLongX = item.Fit.x >= item.Fit.z;
            if (modelLongX != boxLongX && Mathf.Abs(bounds.size.x - bounds.size.z) > 0.05f * Mathf.Max(bounds.size.x, bounds.size.z))
            {
                model.localRotation = Quaternion.Euler(0f, item.ModelYaw + 90f, 0f) * authored;
                bounds = LocalBounds(model);
            }

            float boxX = item.Fit.x / Mathf.Max(0.001f, bounds.size.x);
            float boxY = item.Fit.y / Mathf.Max(0.001f, bounds.size.y);
            float boxZ = item.Fit.z / Mathf.Max(0.001f, bounds.size.z);

            // Height leads; width and depth follow it within the stretch limit...
            float sy = boxY;
            float sx = Mathf.Clamp(boxX, sy / MaxFurnitureStretch, sy * MaxFurnitureStretch);
            float sz = Mathf.Clamp(boxZ, sy / MaxFurnitureStretch, sy * MaxFurnitureStretch);

            // ...but never far past the box's footprint. When that cap bites, give up a little
            // height (at most 20 %) rather than stretch the model out of shape.
            sx = Mathf.Min(sx, boxX * FurnitureOverflow);
            sz = Mathf.Min(sz, boxZ * FurnitureOverflow);
            float slimmest = Mathf.Min(sx, sz);
            if (sy > slimmest * MaxFurnitureStretch)
            {
                sy = Mathf.Max(boxY * 0.8f, slimmest * MaxFurnitureStretch);
            }

            holder.localScale = new Vector3(sx, sy, sz);

            // Centre on the root and stand on it (bounds are in the holder's unscaled space).
            model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return Vector3.Scale(bounds.size, holder.localScale);
        }

        /// <summary>Renderer bounds of a model in its parent's space.</summary>
        private static Bounds LocalBounds(Transform model)
        {
            Transform parent = model.parent;
            bool any = false;
            var bounds = new Bounds();

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>() != null ? renderer.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                Bounds local = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 world = renderer.transform.TransformPoint(corner);
                    Vector3 inParent = parent != null ? parent.InverseTransformPoint(world) : world;

                    if (!any)
                    {
                        bounds = new Bounds(inParent, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(inParent);
                    }
                }
            }

            return bounds;
        }

        /// <summary>Lowers an object until its lowest point rests on the first surface below.</summary>
        private static bool SetDown(Transform target, float lowest)
        {
            Bounds bounds = WorldBounds(target);
            if (bounds.size == Vector3.zero)
            {
                return false;
            }

            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, origin.y - lowest, ~0, QueryTriggerInteraction.Ignore);

            float best = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(target) || hit.collider.attachedRigidbody != null)
                {
                    continue;
                }

                best = Mathf.Min(best, hit.distance);
            }

            if (float.IsPositiveInfinity(best))
            {
                return false;
            }

            float drop = best - 0.02f - 0.002f;
            if (Mathf.Abs(drop) < 0.002f)
            {
                return false;
            }

            target.position += Vector3.down * drop;
            EditorUtility.SetDirty(target);
            return true;
        }

        private static Bounds WorldBounds(Transform target)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        /// <summary>
        /// Batteries and throwables were placed on the old box props. They now sit wherever the
        /// new furniture's top is (or on the floor), so nothing floats or hides inside a model.
        /// </summary>
        private static int SettleItems()
        {
            GameObject items = GameObject.Find("Items");
            if (items == null)
            {
                return 0;
            }

            int moved = 0;
            foreach (Transform item in items.transform)
            {
                if (!item.name.StartsWith("Battery_") && !item.name.StartsWith("Throwable_"))
                {
                    continue;
                }

                // Lift above any surface first, then set down: a battery that ended up inside a
                // taller model comes out on top of it.
                float floorY = item.position.y >= UpperFloor - 0.1f ? UpperFloor : 0f;
                Vector3 start = item.position;
                item.position = new Vector3(start.x, floorY + 2.2f, start.z);
                Physics.SyncTransforms();

                if (SetDown(item, floorY - 0.5f) || Vector3.Distance(start, item.position) > 0.001f)
                {
                    if (Vector3.Distance(start, item.position) > 0.005f)
                    {
                        moved++;
                    }
                }
            }

            return moved;
        }

        // -------------------------------------------------------------- overlays

        private static int EnsureOverlay(Transform parent, string name, Material material, Rect area, float floorY)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            GameObject overlay = Stage1PlaygroundBuilder.CreateBox(name, parent,
                new Vector3(area.center.x, floorY + 0.005f, area.center.y), new Vector3(area.width, 0.01f, area.height), material);
            Object.DestroyImmediate(overlay.GetComponent<Collider>());
            DressBox(overlay.transform, material, material);
            return 1;
        }

        // ------------------------------------------------------------- materials

        /// <summary>
        /// A URP Lit material over a Poly Haven texture set, tiled once every
        /// <paramref name="tileMeters"/> (the box meshes carry UVs in metres). Falls back to a
        /// flat colour when the texture is not in the project.
        /// </summary>
        private static Material SurfaceMaterial(string textureId, float tileMeters, float smoothness, Color tint)
        {
            string path = $"{GeneratedFolder}/House_{textureId}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            string folder = $"{ArtRoot}/Textures/{textureId}";
            Texture2D albedo = LoadTexture($"{folder}/{textureId}_diff.jpg", false);
            Texture2D normal = LoadTexture($"{folder}/{textureId}_nor_gl.jpg", true);

            material.SetColor("_BaseColor", albedo != null ? tint : tint * 0.6f);
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", Vector2.one / tileMeters);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A barely-there transparent URP Lit for picture and lamp glass.</summary>
        private static Material ClearGlass()
        {
            const string path = GeneratedFolder + "/House_ClearGlass.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.95f);
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.06f));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material FlatMaterial(string name, Color color, float smoothness)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            bool changed = false;
            if (normalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }

            if (importer.maxTextureSize > 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------- box meshes

        private static Dictionary<string, Mesh> boxMeshes;

        /// <summary>
        /// Gives a box the metre-UV mesh for its size: sub-mesh 0 is the top face, 1 the rest.
        /// The BoxCollider is untouched (it is unit-sized in local space either way).
        /// </summary>
        private static int DressBox(Transform box, Material top, Material sides)
        {
            MeshFilter filter = box.GetComponent<MeshFilter>();
            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                return 0;
            }

            Mesh mesh = BoxMesh(box.localScale);
            bool changed = filter.sharedMesh != mesh;
            filter.sharedMesh = mesh;

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length != 2 || materials[0] != top || materials[1] != sides)
            {
                renderer.sharedMaterials = new[] { top, sides };
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(renderer);
            }

            return changed ? 1 : 0;
        }

        private static Mesh BoxMesh(Vector3 size)
        {
            string key = $"Box_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}";

            if (boxMeshes == null)
            {
                boxMeshes = new Dictionary<string, Mesh>();
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(BoxMeshAssetPath))
                {
                    if (asset is Mesh existing)
                    {
                        boxMeshes[existing.name] = existing;
                    }
                }
            }

            if (boxMeshes.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            Mesh mesh = BuildBoxMesh(size);
            mesh.name = key;

            if (AssetDatabase.LoadMainAssetAtPath(BoxMeshAssetPath) == null)
            {
                AssetDatabase.CreateAsset(mesh, BoxMeshAssetPath);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(mesh, BoxMeshAssetPath);
            }

            boxMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>A unit cube (like the primitive) whose UVs measure the scaled box in metres.</summary>
        private static Mesh BuildBoxMesh(Vector3 size)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var rest = new List<int>();

            // normal, u axis, v axis
            Vector3[][] faces =
            {
                new[] { Vector3.up, Vector3.right, Vector3.forward },
                new[] { Vector3.down, Vector3.right, Vector3.back },
                new[] { Vector3.forward, Vector3.left, Vector3.up },
                new[] { Vector3.back, Vector3.right, Vector3.up },
                new[] { Vector3.right, Vector3.forward, Vector3.up },
                new[] { Vector3.left, Vector3.back, Vector3.up },
            };

            foreach (Vector3[] face in faces)
            {
                Vector3 n = face[0], uAxis = face[1], vAxis = face[2];
                float uLength = Mathf.Abs(Vector3.Dot(uAxis, size));
                float vLength = Mathf.Abs(Vector3.Dot(vAxis, size));
                int first = vertices.Count;

                for (int i = 0; i < 4; i++)
                {
                    float su = (i == 1 || i == 2) ? 0.5f : -0.5f;
                    float sv = (i >= 2) ? 0.5f : -0.5f;
                    vertices.Add(n * 0.5f + uAxis * su + vAxis * sv);
                    normals.Add(n);
                    uvs.Add(new Vector2((su + 0.5f) * uLength, (sv + 0.5f) * vLength));
                }

                List<int> target = n == Vector3.up ? top : rest;
                // Unity's front face has normal Cross(b - a, c - a); for (0,1,2) that is
                // Cross(u, v), so keep that order when it points along n and reverse it otherwise.
                bool alongNormal = Vector3.Dot(Vector3.Cross(uAxis, vAxis), n) > 0f;
                if (alongNormal)
                {
                    target.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
                else
                {
                    target.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(rest, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // --------------------------------------------------------- hiding spots

        /// <summary>
        /// Wardrobes get dark panelled wood, a plinth, a cornice and two doors swung open (no
        /// colliders, so the way in stays clear); hiding beds get a wooden frame, a pillow and
        /// a fabric blanket. Edits the two prefabs once; later runs only refresh materials.
        /// </summary>
        private const float WardrobeDoorCrack = 7f;

        private static void DressHidingPrefabs(Material wood, Material fabric)
        {
            Material pillow = FlatMaterial("House_Pillow", new Color(0.72f, 0.69f, 0.62f), 0.1f);

            string wardrobePath = AssetDatabase.GetAssetPath(Stage3EnemyBuilder.WardrobePrefab());
            GameObject root = PrefabUtility.LoadPrefabContents(wardrobePath);
            try
            {
                Transform visuals = root.transform.Find("Visuals");
                foreach (Transform part in visuals)
                {
                    DressBox(part, wood, wood);
                }

                AddDecorBox(visuals, "Cornice", new Vector3(0f, 2.1f, 0f), new Vector3(1.17f, 0.08f, 0.78f), wood);
                AddDecorBox(visuals, "Plinth", new Vector3(0f, 0.04f, -0.02f), new Vector3(1.1f, 0.08f, 0.7f), wood);

                // Doors hinged on the front edges, standing almost closed: a 7 degree crack is
                // all a hidden player sees of the room (they used to stand wide open, so hiding
                // felt like standing in the open). No colliders - the HidingSpot moves the player
                // in and out, so the doors never block anything.
                for (int side = -1; side <= 1; side += 2)
                {
                    string doorName = side < 0 ? "Door_Left" : "Door_Right";
                    Transform hinge = visuals.Find(doorName);
                    if (hinge == null)
                    {
                        hinge = new GameObject(doorName).transform;
                        hinge.SetParent(visuals, false);
                        hinge.localPosition = new Vector3(side * 0.535f, 0f, 0.35f);
                        AddDecorBox(hinge, "Panel", new Vector3(-side * 0.25f, 1.02f, 0.015f), new Vector3(0.5f, 1.95f, 0.03f), wood);
                        AddDecorBox(hinge, "Handle", new Vector3(-side * 0.44f, 1.05f, 0.045f), new Vector3(0.025f, 0.14f, 0.025f), pillow);
                    }

                    // Positive side * angle swings a door forward and out.
                    hinge.localRotation = Quaternion.Euler(0f, side * WardrobeDoorCrack, 0f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, wardrobePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            string bedPath = AssetDatabase.GetAssetPath(Stage3EnemyBuilder.BedPrefab());
            root = PrefabUtility.LoadPrefabContents(bedPath);
            try
            {
                Transform visuals = root.transform.Find("Visuals");
                foreach (Transform part in visuals)
                {
                    if (part.name == "Blanket")
                    {
                        DressBox(part, fabric, fabric);
                    }
                    else if (part.name == "Mattress")
                    {
                        DressBox(part, pillow, wood);
                    }
                    else if (part.GetComponent<MeshFilter>() != null)
                    {
                        DressBox(part, wood, wood);
                    }
                }

                AddDecorBox(visuals, "Pillow", new Vector3(0f, 0.6f, -0.72f), new Vector3(0.7f, 0.1f, 0.35f), pillow);

                // Present-day bed: no turned posts or footboard, a tall upholstered headboard.
                foreach (string old in new[] { "Footboard", "Post_HL", "Post_HR" })
                {
                    Transform part = visuals.Find(old);
                    if (part != null)
                    {
                        Object.DestroyImmediate(part.gameObject);
                    }
                }

                AddDecorBox(visuals, "Headboard_Soft", new Vector3(0f, 0.62f, -0.97f), new Vector3(1.16f, 1.05f, 0.09f), fabric);

                PrefabUtility.SaveAsPrefabAsset(root, bedPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddDecorBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                DressBox(existing, material, material);
                return;
            }

            GameObject box = Stage1PlaygroundBuilder.CreateBox(name, parent, position, size, material);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.isStatic = false;
            DressBox(box.transform, material, material);
        }

        /// <summary>Doors and other stage props share placeholder materials: give them wood.</summary>
        private static void DressSharedMaterials(Material wood)
        {
            foreach (string name in new[] { "Placeholder_Door", "Placeholder_DoorLocked" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");
                if (material == null || wood.GetTexture("_BaseMap") == null)
                {
                    continue;
                }

                material.SetTexture("_BaseMap", wood.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseMap", new Vector2(0.8f, 1.2f));
                material.SetTexture("_BumpMap", wood.GetTexture("_BumpMap"));
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
            }
        }
    }
}
