using System.Collections.Generic;
using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// Keeps the house's lamps cheap without letting light leak where it should not:
    ///
    ///  - a lamp on the other storey whose light is cut off by the floor slab is switched off
    ///    (without shadows its light would come straight through the floor);
    ///  - only the few lamps nearest the camera cast shadows (<see cref="ShadowBudget"/>,
    ///    set by the graphics quality); shadowed point lights cost six shadow maps each, and
    ///    twenty of them halved the frame rate.
    ///
    /// Re-evaluated a few times a second. Works with HousePower, which only scales intensity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LampCuller : MonoBehaviour
    {
        [Tooltip("Lamps to manage. Empty: every point and spot light not on the player or an enemy.")]
        [SerializeField] private List<Light> lamps = new List<Light>();

        [Tooltip("How many of the nearest lamps cast shadows. Changed by the graphics settings.")]
        [SerializeField, Range(0, 8)] private int shadowBudget = 2;

        [Tooltip("Seconds between updates.")]
        [SerializeField, Min(0.05f)] private float interval = 0.25f;

        /// <summary>The scene's culler, or null.</summary>
        public static LampCuller Instance { get; private set; }

        /// <summary>How many lamps may cast shadows at once.</summary>
        public int ShadowBudget
        {
            get => shadowBudget;
            set
            {
                shadowBudget = Mathf.Clamp(value, 0, 8);
                timer = 0f;
            }
        }

        private Transform viewer;
        private float timer;
        private readonly List<Light> sorted = new List<Light>();

        private void Awake()
        {
            Instance = this;

            if (lamps.Count == 0)
            {
                foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if (light.type != LightType.Directional && light.GetComponentInParent<Camera>() == null
                        && light.GetComponentInParent<CharacterController>() == null
                        && !AshenWoods.World.IndependentLight.Covers(light))
                    {
                        lamps.Add(light);
                    }
                }
            }

            lamps.RemoveAll(lamp => lamp == null);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
            {
                return;
            }

            timer = interval;

            if (viewer == null)
            {
                viewer = Camera.main != null ? Camera.main.transform : null;
                if (viewer == null)
                {
                    return;
                }
            }

            Vector3 eye = viewer.position;
            int storey = Acoustics.StoreyOf(eye.y);

            sorted.Clear();
            foreach (Light lamp in lamps)
            {
                // A lamp on the other storey only matters while it can actually be seen (stairs).
                // Lamps hang just under their ceiling (2.6 m downstairs is right on the storey
                // split), so classify them by a point a metre lower.
                bool otherStorey = Acoustics.StoreyOf(lamp.transform.position.y - 1f) != storey;
                bool visible = !otherStorey || !Acoustics.IsBlocked(eye, lamp.transform.position);
                lamp.enabled = visible;

                if (visible)
                {
                    sorted.Add(lamp);
                }
                else
                {
                    lamp.shadows = LightShadows.None;
                }
            }

            sorted.Sort((a, b) =>
                (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].shadows = i < shadowBudget ? LightShadows.Soft : LightShadows.None;
            }
        }
    }
}
