using UnityEngine;

namespace AshenWoods.EditorTools
{
    public static partial class FacilityBuilder
    {
        private const string SignsV1Name = "Signs_v1";

        /// <summary>Height of a door exit sign's centre: just above the doorway frame.</summary>
        private const float DoorSignCentreY = 3.3f;

        /// <summary>
        /// Playtest 2026-11-11: the exit signs hung high above the doorways (one 0.6 m off the wall),
        /// and the kit's red "ExitLight" quad sat in front of the lettering as a separate red block.
        /// Once: hide the quad, set door signs on the frame and against the wall.
        /// </summary>
        private static int FixExitSigns()
        {
            int lenses = SignLightsV2() + RedBitsV3() + BreakerDoorV1() + Fix2() + Zones() + Rooms();
            if (root.Find(SignsV1Name) != null)
            {
                return lenses;
            }

            int changed = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Exit_") || t.parent == null || t.parent.name != "Props")
                {
                    continue;
                }

                Transform quad = t.Find("ExitLight");
                if (quad != null && quad.TryGetComponent(out Renderer glow))
                {
                    glow.enabled = false;
                }

                Transform model = t.Find("SciFi_exit_snaps001");
                Renderer sign = model != null ? model.GetComponent<Renderer>() : null;
                if (sign == null)
                {
                    continue;
                }

                // Out of the wall: from the pivot towards the sign's centre.
                Vector3 centre = sign.bounds.center;
                Vector3 outward = centre - t.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                outward.Normalize();
                float halfDepth = Mathf.Abs(Vector3.Dot(sign.bounds.extents, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z))));

                Vector3 position = t.position;
                if (t.name != "Exit_Bay")
                {
                    position.y += DoorSignCentreY - centre.y;
                }

                // Back face flush with the wall at the new height.
                Vector3 probe = new Vector3(centre.x, position.y + (centre.y - t.position.y), centre.z) + outward * 1f;
                if (Physics.Raycast(probe, -outward, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float gap = Vector3.Dot(probe - hit.point, outward) - 1f - halfDepth;
                    position -= outward * gap;
                }

                t.position = position;
                changed++;
            }

            var marker = new GameObject(SignsV1Name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return changed;
        }

        private const string SignsV2Name = "Signs_v2";

        /// <summary>
        /// The red emergency lamp next to a door hung 0.4 m off the wall as a red box beside the
        /// sign. Once: hide that box and put its light on the sign, so the sign is the source.
        /// </summary>
        private static int SignLightsV2()
        {
            if (root.Find(SignsV2Name) != null)
            {
                return 0;
            }

            int changed = 0;
            Transform lights = root.Find("Lights");
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Exit_") || t.parent == null || t.parent.name != "Props" || lights == null)
                {
                    continue;
                }

                Transform model = t.Find("SciFi_exit_snaps001");
                Renderer sign = model != null ? model.GetComponent<Renderer>() : null;
                if (sign == null)
                {
                    continue;
                }

                Vector3 centre = sign.bounds.center;
                Vector3 outward = centre - t.position;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;

                foreach (Transform lens in lights)
                {
                    if (!lens.name.StartsWith("Emergency_Lens_") || Vector3.Distance(lens.position, centre) > 1.5f)
                    {
                        continue;
                    }

                    if (lens.TryGetComponent(out Renderer box))
                    {
                        box.enabled = false;
                    }

                    Transform lamp = lights.Find(lens.name.Replace("_Lens", string.Empty));
                    if (lamp != null)
                    {
                        lamp.position = centre + outward * 0.25f;
                    }

                    changed++;
                }
            }

            var marker = new GameObject(SignsV2Name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return changed;
        }

        private const string RedV3Name = "Red_v3";

        /// <summary>
        /// Playtest 2026-11-11: red boxes all over the map (the emergency lamps' lenses hung in mid-air)
        /// and glowing red rings on the pipe risers (the valve wheel used the emissive red). Once: put
        /// each lens and its light on the nearest wall (hide it when there is none), paint the wheels.
        /// </summary>
        private static int RedBitsV3()
        {
            if (root.Find(RedV3Name) != null)
            {
                return 0;
            }

            int changed = 0;
            Transform lights = root.Find("Lights");
            if (lights != null)
            {
                Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                foreach (Transform lens in lights)
                {
                    if (!lens.name.StartsWith("Emergency_Lens_") || !lens.TryGetComponent(out Renderer box) || !box.enabled)
                    {
                        continue;
                    }

                    bool found = false;
                    RaycastHit nearest = default;
                    foreach (Vector3 direction in directions)
                    {
                        if (Physics.Raycast(lens.position, direction, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                            && hit.collider.transform != lens
                            && (!found || hit.distance < nearest.distance))
                        {
                            nearest = hit;
                            found = true;
                        }
                    }

                    Transform lamp = lights.Find(lens.name.Replace("_Lens", string.Empty));
                    if (found)
                    {
                        Vector3 normal = new Vector3(nearest.normal.x, 0f, nearest.normal.z).normalized;
                        lens.position = new Vector3(nearest.point.x, lens.position.y, nearest.point.z) + normal * 0.06f;
                        if (lamp != null)
                        {
                            lamp.position = lens.position + normal * 0.25f;
                        }
                    }
                    else
                    {
                        box.enabled = false;
                    }

                    changed++;
                }
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Riser") && !r.name.Contains("PipeRiser"))
                {
                    continue;
                }

                Material[] materials = r.sharedMaterials;
                bool swapped = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == WarningRed)
                    {
                        materials[i] = PaintRed;
                        swapped = true;
                    }
                }

                if (swapped)
                {
                    r.sharedMaterials = materials;
                    changed++;
                }
            }

            var marker = new GameObject(RedV3Name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return changed;
        }

        private const string DoorV1Name = "BreakerDoor_v1";

        /// <summary>
        /// The breaker room door swung out over the catwalk. Once: it opens into the room, and the box
        /// leaf gets the modelled door (Tools/Blender/facility_horror.py, breaker_door()). The box keeps
        /// its collider for the interaction ray.
        /// </summary>
        private static int BreakerDoorV1()
        {
            if (root.Find(DoorV1Name) != null)
            {
                return 0;
            }

            GameObject door = GameObject.Find("Door_BreakerRoom");
            GameObject model = Model("Door_Breaker");
            if (door == null || model == null)
            {
                return 0;
            }

            Transform hinge = door.transform.Find("Hinge");
            var settings = new UnityEditor.SerializedObject(door.GetComponent<AshenWoods.Interaction.Door>());
            settings.FindProperty("openAngle").floatValue = -100f;
            settings.ApplyModifiedPropertiesWithoutUndo();

            foreach (string part in new[] { "Panel", "Handle" })
            {
                Transform t = hinge.Find(part);
                if (t != null && t.TryGetComponent(out Renderer r))
                {
                    r.enabled = false;
                }
            }

            if (hinge.Find("Model") == null)
            {
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model);
                instance.name = "Model";
                instance.transform.SetParent(hinge, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                foreach (Collider c in instance.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(c);
                }
            }

            var marker = new GameObject(DoorV1Name);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return 1;
        }
    }
}
