using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>
    /// Dust hanging in the air around the player. The motes use a lit particle material, so
    /// in the dark they are invisible and the flashlight beam (or a lamp) picks them out -
    /// which reads as dust floating in the beam.
    ///
    /// The particle system is built in code from a few numbers; only the material comes from
    /// the scene (so the build keeps its shader). Motes live in world space in a box that
    /// follows the camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FlashlightDust : MonoBehaviour
    {
        [Tooltip("Lit, transparent particle material (Simple Lit). Without one nothing is drawn.")]
        [SerializeField] private Material material;

        [Tooltip("Size of the box around the camera that holds the motes, in metres.")]
        [SerializeField] private Vector3 volume = new Vector3(7f, 3.2f, 7f);

        [SerializeField, Min(1)] private int maxMotes = 700;
        [SerializeField, Min(0.1f)] private float lifetime = 9f;
        [SerializeField] private Vector2 moteSize = new Vector2(0.008f, 0.022f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.55f;

        [Tooltip("Slow swirl of the air.")]
        [SerializeField, Min(0f)] private float drift = 0.06f;

        private void Awake()
        {
            Configure(GetComponent<ParticleSystem>());
        }

        private void Configure(ParticleSystem system)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(moteSize.x, moteSize.y);
            main.startColor = new Color(1f, 0.97f, 0.9f, opacity);
            main.gravityModifier = 0.0005f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxMotes;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = maxMotes / lifetime;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = volume;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = drift > 0f;
            noise.strength = drift;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.05f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Fade in and out so motes never pop.
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = material != null;

            system.Play(true);
        }
    }
}
