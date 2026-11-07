using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// Placeholder feedback: tints the enemy body by state so you can see the AI think.
    ///   grey  = patrolling
    ///   amber = searching
    ///   red   = chasing
    ///
    /// It only reads EnemyBrain.StateChanged, so when a real model arrives this script is
    /// deleted (or swapped for an animator) and nothing else changes. The tint goes through
    /// a MaterialPropertyBlock, so the shared material asset is never modified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyStateVisuals : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyBrain brain;

        [Tooltip("Renderer tinted by state. Falls back to the first Renderer below this object.")]
        [SerializeField] private Renderer body;

        [Tooltip("Tint the body by state. Turn off for a real textured model - the setup menu " +
                 "'Ashen Woods > Enemy > Set Up Enemy Animations' does this for you.")]
        [SerializeField] private bool tintBody = true;

        [Header("Colors")]
        [SerializeField] private Color patrolColor = new Color(0.42f, 0.44f, 0.40f);
        [SerializeField] private Color searchColor = new Color(0.85f, 0.62f, 0.18f);
        [SerializeField] private Color chaseColor = new Color(0.72f, 0.13f, 0.11f);
        [SerializeField] private Color investigateColor = new Color(0.95f, 0.8f, 0.35f);

        private MaterialPropertyBlock block;
        private int baseColorId;
        private int colorId;

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponentInParent<EnemyBrain>();
            }

            if (body == null)
            {
                body = GetComponentInChildren<Renderer>();
            }

            block = new MaterialPropertyBlock();
            baseColorId = Shader.PropertyToID("_BaseColor");
            colorId = Shader.PropertyToID("_Color");
        }

        private void OnEnable()
        {
            if (brain == null)
            {
                return;
            }

            brain.StateChanged += Apply;
            Apply(brain.State);
        }

        private void OnDisable()
        {
            if (brain != null)
            {
                brain.StateChanged -= Apply;
            }
        }

        private void Apply(EnemyState state)
        {
            if (body == null || !tintBody)
            {
                return;
            }

            Color color;
            switch (state)
            {
                case EnemyState.Chase:
                    color = chaseColor;
                    break;
                case EnemyState.Investigate:
                    color = investigateColor;
                    break;
                case EnemyState.Search:
                case EnemyState.Caught:
                    color = searchColor;
                    break;
                default:
                    color = patrolColor;
                    break;
            }

            body.GetPropertyBlock(block);
            block.SetColor(baseColorId, color);
            block.SetColor(colorId, color);
            body.SetPropertyBlock(block);
        }
    }
}
