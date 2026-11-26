using UnityEditor;
using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Props pass 2 (owner, 2026-11-25: "no plain boxes, one style"): the pieces still built from Unity primitives get
    /// modelled looks from Tools/Blender/facility_props2.py (build_props2()). Once (marker Props2_v1): each host gets a
    /// "Model2" child placed in its own frame; the primitive renderers are switched off and their colliders kept, so
    /// interaction, physics and navigation do not change. Hosts with a scaled transform (keypad, notes) get a child
    /// that undoes the scale. Delete the marker and every "Model2" child to re-run.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string Props2Name = "Props2_v1";
        private const string Model2Name = "Model2";

        private static int SetUpProps2()
        {
            if (root == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found == null)
                {
                    return 0;
                }

                root = found.transform;
            }

            if (root.Find(Props2Name) != null)
            {
                return 0;
            }

            ConfigureModels();
            int added = 0;

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.gameObject.scene != SceneManagerActive())
                {
                    continue;
                }

                string n = t.name;
                KeyPickup pickup = t.GetComponent<KeyPickup>();
                if (pickup != null && pickup.IsHeavy && n.StartsWith("FuelCan"))
                {
                    added += Dress(t, "Prop2_FuelCan", Vector3.zero, Quaternion.identity, false, t.Find("Visuals"));
                }
                else if (n.StartsWith("Sample_") && t.Find("Visuals") != null)
                {
                    Transform visuals = t.Find("Visuals");
                    added += Dress(visuals, "Prop2_SampleVial", Vector3.zero, Quaternion.identity, false, visuals);
                }
                else if (n == "Lever" && t.parent != null && t.parent.name.StartsWith("Valve_") && t.Find("Wheel") != null)
                {
                    added += Dress(t, "Prop2_ValveWheel", Vector3.zero, Quaternion.identity, false, t);
                }
                else if (n.StartsWith("Steam_") && t.GetComponent<NoiseLure>() != null)
                {
                    added += Dress(t, "Prop2_SteamVent", Vector3.zero, Quaternion.identity, false,
                        t.Find("Body"), t.Find("Stripe"), t.Find("Lever"), t.Find("Gauge"), t.Find("Nozzle"));
                }
                else if (n == "SecurityTerminal")
                {
                    added += Dress(t, "Prop2_Terminal", Vector3.zero, Quaternion.identity, false, t.Find("Stand"), t.Find("Monitor"));
                }
                else if (n == "Laptop" && t.Find("Base") != null)
                {
                    added += Dress(t, "Prop2_Laptop", Vector3.zero, Quaternion.identity, false, t.Find("Base"), t.Find("Lid"));
                }
                else if (t.GetComponent<Keypad>() != null)
                {
                    added += Dress(t, "Prop2_Keypad", Vector3.zero, Quaternion.identity, true, t.Find("Keys"));
                    HideOwn(t);
                    PlaceKeypadLed(t);
                }
                else if (n == "Keycard" && t.Find("Visuals") != null)
                {
                    added += Dress(t, "Prop2_Keycard", Vector3.zero, Quaternion.Euler(0f, 182f, 0f), false, t.Find("Visuals"));
                }
                else if (n == "DeskLamp")
                {
                    added += Dress(t, "Prop2_DeskLamp", Vector3.zero, Quaternion.identity, false, t.Find("Stand"));
                }
                else if (n == "Pin" && t.parent != null && t.GetComponent<MeshFilter>() != null
                         && t.GetComponent<MeshFilter>().sharedMesh != null && t.GetComponent<MeshFilter>().sharedMesh.name == "Sphere")
                {
                    added += Dress(t.parent, "Prop2_PushPin", t.localPosition, Quaternion.identity, true, "PinModel", t);
                }
                else if (n == "GeneratorPanel")
                {
                    added += Dress(t, "Prop2_GenControl", Vector3.zero, Quaternion.identity, false,
                        t.Find("Post"), t.Find("Housing"), t.Find("Face"), t.Find("Fuel"));
                    Transform led = t.Find("Led");
                    if (led != null)
                    {
                        led.localPosition = new Vector3(-0.1f, 1.392f, 0.03f); // inside the roof lamp's glass
                        Light light = led.GetComponent<Light>();
                        if (light != null)
                        {
                            light.range = Mathf.Min(light.range, 0.8f);
                        }
                    }

                    Transform lever = t.Find("Starter/Lever");
                    if (lever != null)
                    {
                        added += Dress(lever, "Prop2_GenLever", Vector3.zero, Quaternion.identity, false, lever.Find("Handle"), lever.Find("Grip"));
                    }
                }
                else if (n == "Note_Janitor")
                {
                    // The clipboard lies on the desk: the scaled box's bottom face is the desk top.
                    added += Dress(t, "Prop2_Clipboard", new Vector3(0f, -0.5f, 0f), Quaternion.identity, true);
                    HideOwn(t);
                }
            }

            var marker = new GameObject(Props2Name);
            marker.transform.SetParent(root, false);
            marker.SetActive(false);
            return added + 1;
        }

        private const string Props2FixName = "Props2_v2";

        /// <summary>
        /// Owner's look at the new props (2026-11-25), once (marker Props2_v2): the desk lamp stands on the desk (its
        /// foot hung 9 cm above it), the steam-vent signs move off the back of the riser to its front, every 3D label
        /// (TextMesh) is set in Inter SemiBold, and the vents' steam leaves from the new outlet.
        /// </summary>
        private static int SetUpProps2Fix()
        {
            if (root == null || root.Find(Props2FixName) != null)
            {
                return 0;
            }

            int changed = 0;
            GameObject lamp = GameObject.Find("DeskLamp");
            if (lamp != null)
            {
                // The model's foot is 0.425 m below the bulb (the lamp's pivot). The desk has no collider under the foot,
                // so its top is read off the laptop, which stands on the same desk (its pivot is its underside).
                GameObject laptop = GameObject.Find("Laptop");
                if (laptop != null)
                {
                    Vector3 p = lamp.transform.position;
                    lamp.transform.position = new Vector3(p.x, laptop.transform.position.y + 0.425f, p.z);
                    EditorUtility.SetDirty(lamp.transform);
                    changed++;
                }
            }

            foreach (NoiseLure lure in Object.FindObjectsByType<NoiseLure>(FindObjectsInactive.Include))
            {
                Transform t = lure.transform;
                Transform tag = t.Find("Tag");
                Transform text = t.Find("TagText");
                if (tag != null)
                {
                    tag.localPosition = new Vector3(tag.localPosition.x, tag.localPosition.y, 0.028f);
                }

                if (text != null)
                {
                    text.localPosition = new Vector3(text.localPosition.x, text.localPosition.y, 0.036f);
                }

                Transform steam = t.Find("Steam");
                if (steam != null)
                {
                    steam.localPosition = new Vector3(0f, -0.225f, 0.02f);
                    steam.localRotation = Quaternion.Euler(60f, 0f, 0f);
                }

                EditorUtility.SetDirty(t.gameObject);
                changed++;
            }

            Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/Resources/Fonts/Inter-SemiBold.ttf");
            if (bold != null)
            {
                foreach (TextMesh label in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
                {
                    label.font = bold;
                    label.fontStyle = FontStyle.Normal;
                    MeshRenderer renderer = label.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = bold.material;
                    }

                    EditorUtility.SetDirty(label);
                    changed++;
                }
            }

            var marker = new GameObject(Props2FixName);
            marker.transform.SetParent(root, false);
            marker.SetActive(false);
            return changed + 1;
        }

        private static UnityEngine.SceneManagement.Scene SceneManagerActive() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        /// <summary>
        /// Puts the model as a child of <paramref name="host"/> at a local pose and switches off the renderers under
        /// <paramref name="hide"/> (colliders stay). With <paramref name="unscale"/> the child cancels the host's scale.
        /// </summary>
        private static int Dress(Transform host, string model, Vector3 localPosition, Quaternion localRotation, bool unscale,
            params Transform[] hide)
        {
            return Dress(host, model, localPosition, localRotation, unscale, Model2Name, hide);
        }

        private static int Dress(Transform host, string model, Vector3 localPosition, Quaternion localRotation, bool unscale,
            string childName, params Transform[] hide)
        {
            if (host == null)
            {
                return 0;
            }

            GameObject source = Model(model);
            if (source == null)
            {
                Debug.LogWarning("[Facility] Model missing (run build_props2() in Tools/Blender/facility_props2.py): " + model);
                return 0;
            }

            if (host.Find(childName) == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = childName;
                instance.transform.SetParent(host, false);
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;
                if (unscale)
                {
                    Vector3 s = host.lossyScale;
                    instance.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y),
                        1f / Mathf.Max(0.0001f, s.z));
                }

                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c); // looks only: the old colliders stay
                }
            }

            foreach (Transform h in hide ?? new Transform[0])
            {
                if (h == null)
                {
                    continue;
                }

                foreach (Renderer r in h.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform.IsChildOf(host.Find(childName)) || r is ParticleSystemRenderer || r.name == "LedLens"
                        || r.name == "Screen" || r.name.Contains("Text") || r.name == "Letter")
                    {
                        continue;
                    }

                    r.enabled = false;
                }
            }

            EditorUtility.SetDirty(host.gameObject);
            return 1;
        }

        /// <summary>The modelled keypad has keys where the LED sat: the LED goes on the plate under the key grid.</summary>
        private static void PlaceKeypadLed(Transform keypad)
        {
            Transform screen = keypad.Find("Screen");
            Transform lens = keypad.Find("LedLens");
            Transform led = keypad.Find("Led");
            if (screen == null || lens == null)
            {
                return;
            }

            Vector3 face = screen.position - keypad.position;
            face.y = 0f;
            face = face.sqrMagnitude > 0.0001f ? face.normalized : -keypad.right;
            Vector3 side = Vector3.Cross(Vector3.up, face);
            lens.position = keypad.position + face * 0.0255f + Vector3.up * -0.098f - side * 0.04f;
            if (led != null)
            {
                led.position = lens.position + face * 0.015f;
            }
        }

        private static void HideOwn(Transform t)
        {
            Renderer own = t.GetComponent<Renderer>();
            if (own != null)
            {
                own.enabled = false;
            }
        }
    }
}
