using UnityEngine;
using AshenWoods.Audio;

namespace AshenWoods.Player
{
    /// <summary>
    /// The player's last second when caught: the view jerks up at the enemy's face, then the
    /// body drops and the camera hits the floor on its side, still looking up at what caught
    /// you. Purely a camera move - it starts on <see cref="PlayerEnemyTarget.Caught"/> and only
    /// when the catch ends the run (a respawn placeholder catch is left alone).
    ///
    /// It writes the camera's world pose in LateUpdate after look and stance code, on
    /// unscaled time, so it keeps going while the death screen fades in. The scene is
    /// reloaded after a death, so nothing has to be put back.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CaughtCameraFall : MonoBehaviour
    {
        [SerializeField] private PlayerEnemyTarget target;

        [Tooltip("The camera to move. Falls back to the first camera below the player.")]
        [SerializeField] private Transform view;

        [Header("Timing (real seconds)")]
        [Tooltip("Jerking round to look at the enemy's face.")]
        [SerializeField, Min(0f)] private float jerkSeconds = 0.22f;

        [Tooltip("Dropping to the floor.")]
        [SerializeField, Min(0.05f)] private float fallSeconds = 0.55f;

        [Tooltip("The small bounce and settle after hitting the floor.")]
        [SerializeField, Min(0f)] private float settleSeconds = 0.35f;

        [Header("Pose on the floor")]
        [Tooltip("Height of the eye above the player's feet once down.")]
        [SerializeField, Min(0.05f)] private float floorEyeHeight = 0.22f;

        [Tooltip("How far the body falls away from the enemy, in metres.")]
        [SerializeField, Min(0f)] private float fallBack = 0.45f;

        [Tooltip("Camera roll once lying on the side, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float roll = 72f;

        [Tooltip("Height on the enemy the camera stares at from the floor.")]
        [SerializeField, Min(0f)] private float enemyFaceHeight = 1.6f;

        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.55f;

        private bool playing;
        private float elapsed;
        private bool impactPlayed;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Quaternion jerkRotation;
        private Vector3 floorPosition;
        private Quaternion floorRotation;
        private AudioSource impactSource;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<PlayerEnemyTarget>();
            }

            if (view == null)
            {
                Camera camera = GetComponentInChildren<Camera>(true);
                view = camera != null ? camera.transform : null;
            }
        }

        private void OnEnable()
        {
            if (target != null)
            {
                target.Caught += OnCaught;
            }
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.Caught -= OnCaught;
            }
        }

        private void OnCaught(Vector3 enemyPosition)
        {
            // A respawn catch teleports the player home; only a run-ending catch falls.
            if (view == null || target == null || !target.IsOutOfPlay || playing)
            {
                return;
            }

            playing = true;
            elapsed = 0f;
            impactPlayed = false;
            startPosition = view.position;
            startRotation = view.rotation;

            Vector3 feet = transform.position;
            Vector3 face = enemyPosition + Vector3.up * enemyFaceHeight;

            // First: a jerk round to the face that caught you.
            Vector3 toFace = face - startPosition;
            jerkRotation = toFace.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toFace.normalized, Vector3.up)
                : startRotation;

            // Then down, a little away from the enemy, unless a wall is right behind.
            Vector3 away = feet - enemyPosition;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;

            float back = fallBack;
            Vector3 lowOrigin = feet + Vector3.up * 0.3f;
            if (Physics.SphereCast(lowOrigin, 0.15f, away, out RaycastHit hit, fallBack + 0.2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !(hit.collider is CharacterController))
            {
                back = Mathf.Max(0f, hit.distance - 0.2f);
            }

            floorPosition = feet + away * back + Vector3.up * floorEyeHeight;

            Vector3 up = face - floorPosition;
            Quaternion lookUp = up.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(up.normalized, Vector3.up)
                : jerkRotation;

            // Lying on one side; which side is picked by the approach so it is not always the same.
            float side = Vector3.Dot(Vector3.Cross(away, Vector3.up), startRotation * Vector3.forward) >= 0f ? 1f : -1f;
            floorRotation = lookUp * Quaternion.Euler(0f, 0f, roll * side);

            if (impactSource == null)
            {
                impactSource = gameObject.AddComponent<AudioSource>();
                impactSource.playOnAwake = false;
                impactSource.spatialBlend = 0f;
                impactSource.ignoreListenerPause = true;
            }
        }

        private void LateUpdate()
        {
            if (!playing || view == null)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            Vector3 position;
            Quaternion rotation;

            if (elapsed < jerkSeconds)
            {
                float t = Smooth(elapsed / jerkSeconds);
                position = startPosition + Shake(elapsed, 0.025f);
                rotation = Quaternion.Slerp(startRotation, jerkRotation, t);
            }
            else if (elapsed < jerkSeconds + fallSeconds)
            {
                float t = (elapsed - jerkSeconds) / fallSeconds;

                // Gravity for the height, a quicker topple for the turn.
                float drop = t * t;
                float topple = Smooth(Mathf.Clamp01(t * 1.25f));
                position = Vector3.Lerp(startPosition, floorPosition, drop);
                rotation = Quaternion.Slerp(jerkRotation, floorRotation, topple);
            }
            else
            {
                if (!impactPlayed)
                {
                    impactPlayed = true;
                    if (impactSource != null)
                    {
                        impactSource.PlayOneShot(SoundBank.Get(GameSound.Clunk), impactVolume);
                    }
                }

                float s = elapsed - jerkSeconds - fallSeconds;
                float bounce = settleSeconds <= 0f
                    ? 0f
                    : Mathf.Sin(Mathf.Clamp01(s / settleSeconds) * Mathf.PI) * 0.05f * (1f - Mathf.Clamp01(s / settleSeconds));

                // A faint, slowing breath once down.
                float breath = Mathf.Sin(s * 2.2f) * 0.006f * Mathf.Exp(-s * 0.6f);
                position = floorPosition + Vector3.up * (bounce + breath);
                rotation = floorRotation * Quaternion.Euler(-bounce * 40f, 0f, 0f);
            }

            view.SetPositionAndRotation(position, rotation);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static Vector3 Shake(float time, float amount)
        {
            return new Vector3(
                (Mathf.PerlinNoise(time * 30f, 0.1f) - 0.5f) * 2f * amount,
                (Mathf.PerlinNoise(0.7f, time * 30f) - 0.5f) * 2f * amount,
                0f);
        }
    }
}
