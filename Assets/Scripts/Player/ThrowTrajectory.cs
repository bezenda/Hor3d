using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>
    /// Draws the arc a throw would follow while the throw button is held, and a small ring
    /// where it would land. Pure feedback: it reads <see cref="PlayerThrower.GetLaunch"/>
    /// and simulates the same gravity, so the line and the real flight agree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowTrajectory : MonoBehaviour
    {
        [SerializeField] private PlayerThrower thrower;

        [Tooltip("Unlit material for the arc and the landing ring.")]
        [SerializeField] private Material lineMaterial;

        [SerializeField] private Color arcColor = new Color(0.76f, 0.69f, 0.58f, 1f);
        [SerializeField, Min(0.001f)] private float arcWidth = 0.012f;
        [SerializeField, Min(0.05f)] private float ringRadius = 0.22f;
        [SerializeField, Range(8, 120)] private int maxSteps = 70;
        [SerializeField, Min(0.005f)] private float stepSeconds = 0.035f;

        private const int RingSegments = 28;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private LineRenderer arc;
        private LineRenderer ring;
        private Vector3[] points;

        private void Awake()
        {
            if (thrower == null)
            {
                thrower = GetComponent<PlayerThrower>();
            }

            points = new Vector3[maxSteps + 1];
            arc = CreateLine("ThrowArc", false);
            ring = CreateLine("ThrowLanding", true);
            ring.positionCount = RingSegments;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (arc != null)
            {
                Destroy(arc.gameObject);
            }

            if (ring != null)
            {
                Destroy(ring.gameObject);
            }
        }

        private LineRenderer CreateLine(string objectName, bool loop)
        {
            // Not parented to the player: positions are world space and the player moves.
            GameObject go = new GameObject(objectName);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = arcWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            if (lineMaterial != null)
            {
                line.sharedMaterial = lineMaterial;
            }

            line.startColor = arcColor;
            line.endColor = arcColor;
            return line;
        }

        private void SetVisible(bool visible)
        {
            if (arc != null && arc.enabled != visible)
            {
                arc.enabled = visible;
            }

            if (ring != null && ring.enabled != visible)
            {
                ring.enabled = visible;
            }
        }

        private void LateUpdate()
        {
            if (thrower == null || !thrower.IsHoldingThrowable || !thrower.IsCharging)
            {
                SetVisible(false);
                return;
            }

            thrower.GetLaunch(out Vector3 position, out Vector3 velocity);
            Vector3 gravity = Physics.gravity;
            float damping = Mathf.Clamp01(1f - PlayerThrower.PredictedLinearDamping * stepSeconds);

            int count = 1;
            points[0] = position;
            bool landed = false;
            Vector3 landPoint = position;
            Vector3 landNormal = Vector3.up;

            for (int i = 1; i <= maxSteps; i++)
            {
                velocity = (velocity + gravity * stepSeconds) * damping;
                Vector3 next = position + velocity * stepSeconds;
                if (Cast(position, next, out RaycastHit hit))
                {
                    points[count++] = hit.point;
                    landed = true;
                    landPoint = hit.point;
                    landNormal = hit.normal;
                    break;
                }

                points[count++] = next;
                position = next;
            }

            arc.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                arc.SetPosition(i, points[i]);
            }
            arc.enabled = true;

            ring.enabled = landed;
            if (landed)
            {
                DrawRing(landPoint + landNormal * 0.01f, landNormal);
            }

            // Fade the start so the line does not slice through the view.
            Color faint = arcColor;
            faint.a = 0.15f;
            arc.startColor = faint;
            arc.endColor = arcColor;
        }

        private bool Cast(Vector3 from, Vector3 to, out RaycastHit best)
        {
            best = default;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.0001f)
            {
                return false;
            }

            int count = Physics.RaycastNonAlloc(from, delta / length, Hits, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (thrower.IgnoresForPrediction(Hits[i].collider) || Hits[i].distance >= nearest)
                {
                    continue;
                }

                nearest = Hits[i].distance;
                best = Hits[i];
                found = true;
            }

            return found;
        }

        private void DrawRing(Vector3 centre, Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, centre + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * ringRadius);
            }
        }
    }
}
