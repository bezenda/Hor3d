using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The first minute (2026-11-19, "polish the first ten minutes"): the run used to open facing the closet's
    /// doorway, with the janitor's desk - the torch, the guide note, the first objective - dark and behind the
    /// player. Once (marker Opening_v1): the player starts turned to the desk, and a battery desk lamp that
    /// stutters now and then lights the torch and the note. The lamp is not facility wiring (IndependentLight),
    /// so the dead power does not switch it off.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string OpeningName = "Opening_v1";

        private static int SetUpOpening()
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

            if (root.Find(OpeningName) != null)
            {
                return 0;
            }

            KeyPickup torch = null;
            foreach (KeyPickup pickup in Object.FindObjectsByType<KeyPickup>(FindObjectsInactive.Include))
            {
                if (pickup.KeyId == TorchId)
                {
                    torch = pickup;
                }
            }

            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            if (torch == null || player == null)
            {
                return 0;
            }

            Vector3 toDesk = torch.transform.position - player.transform.position;
            toDesk.y = 0f;
            if (toDesk.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(toDesk.normalized, Vector3.up);
                EditorUtility.SetDirty(player.transform);
            }

            Transform group = Group("Opening");
            var lamp = new GameObject("DeskLamp");
            lamp.transform.SetParent(group, false);
            lamp.transform.position = torch.transform.position + new Vector3(-0.35f, 0.45f, 0.15f);
            lamp.AddComponent<IndependentLight>();

            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.name = "Bulb";
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localScale = Vector3.one * 0.07f;
            Renderer bulbRenderer = bulb.GetComponent<Renderer>();
            bulbRenderer.sharedMaterial = AtmosphereAssets.Unlit("Facility_DeskLampBulb", new Color(2.4f, 1.6f, 0.9f), null);
            bulbRenderer.shadowCastingMode = ShadowCastingMode.Off;

            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(stand.GetComponent<Collider>());
            stand.name = "Stand";
            stand.transform.SetParent(lamp.transform, false);
            stand.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            stand.transform.localScale = new Vector3(0.025f, 0.2f, 0.025f);
            stand.GetComponent<Renderer>().sharedMaterial = AtmosphereAssets.Lit("Facility_DeskLampStand", new Color(0.12f, 0.12f, 0.13f), 0.5f);

            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.46f);
            light.intensity = 1.6f;
            light.range = 4f;
            light.shadows = LightShadows.Soft;
            lamp.AddComponent<SceneryLight>().Configure(SceneryLight.Mode.Stutter, 1f, bulbRenderer);

            var marker = new GameObject(OpeningName);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return 2;
        }
    }
}
