using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A key lying in the world. Using it puts its id into the actor's key list and removes
    /// the object, so a door that requires the same id can then be opened.
    ///
    /// The gentle spin and bob only move the <see cref="visual"/> child, never the collider
    /// or this transform, so aiming stays stable and the placeholder mesh can be replaced.
    /// </summary>
    public sealed class KeyPickup : MonoBehaviour, IInteractable
    {
        [Header("Key")]
        [Tooltip("Must match Door.requiredKeyId on the door this key opens.")]
        [SerializeField] private string keyId = "iron_key";

        [Tooltip("Name shown in the prompt and in the pickup message.")]
        [SerializeField] private string displayName = "iron key";

        [Header("Visual (optional)")]
        [Tooltip("Child that moves. Leave empty to keep the key perfectly still.")]
        [SerializeField] private Transform visual;

        [SerializeField] private float spinDegreesPerSecond = 45f;
        [SerializeField] private float bobHeight = 0.04f;
        [SerializeField] private float bobSpeed = 1.6f;

        [Header("Noise")]
        [Tooltip("Loudness (0-1) reported when the key is picked up. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float pickupNoiseLoudness = 0.31f;

        private Vector3 visualStartPosition;

        private void Awake()
        {
            if (visual != null)
            {
                visualStartPosition = visual.localPosition;
            }
        }

        private void Update()
        {
            if (visual == null)
            {
                return;
            }

            visual.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);

            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            visual.localPosition = visualStartPosition + new Vector3(0f, bob, 0f);
        }

        public bool IsAvailable => true;

        public string GetPrompt(IInteractionActor actor)
        {
            return "Take " + displayName;
        }

        public string Interact(IInteractionActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            actor.GiveKey(keyId);
            NoiseSystem.Emit(transform.position, NoiseCategory.Item, pickupNoiseLoudness, this);
            GameLog.Info($"[KeyPickup] Took '{keyId}' ({displayName}) at {transform.position}.", this);
            Destroy(gameObject);

            return "Picked up the " + displayName + ".";
        }
    }
}
