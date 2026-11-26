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
        [SerializeField] private string displayName = "железный ключ";

        [Tooltip("Heavy items (fuel cans) stop the player sprinting and slow the walk while carried.")]
        [SerializeField] private bool heavy;

        [Tooltip("Message on pickup; empty = 'Picked up the <name>.'")]
        [SerializeField] private string takenMessage = "";

        public string KeyId => keyId;
        public string DisplayName => displayName;

        // Display names by key id, filled as pickups wake (the satchel shows "Security keycard", not the id "keycard").
        private static readonly System.Collections.Generic.Dictionary<string, string> Names =
            new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>The Russian display name the level gave this key id (translate with Loc.T).</summary>
        public static bool TryDisplayName(string id, out string name) => Names.TryGetValue(id ?? string.Empty, out name);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetNames() => Names.Clear();
        public bool IsHeavy => heavy;

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
            if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(displayName))
            {
                Names[keyId] = displayName;
            }

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
            return AshenWoods.UI.UiTheme.Capitalize(World.Loc.T(displayName)) + World.Loc.Pick(" — взять", " — take");
        }

        public string Interact(IInteractionActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            actor.GiveKey(keyId);
            Component user = actor as Component;
            AshenWoods.Player.PlayerKeyHolder holder = user != null ? user.GetComponentInParent<AshenWoods.Player.PlayerKeyHolder>() : null;
            if (heavy && holder != null)
            {
                holder.MarkHeavy(keyId);
            }

            NoiseSystem.Emit(transform.position, NoiseCategory.Item, pickupNoiseLoudness, this);
            GameLog.Info($"[KeyPickup] Took '{keyId}' ({displayName}) at {transform.position}.", this);
            AshenWoods.World.PickupLedger.Take(gameObject, keyId, heavy);

            string message = string.IsNullOrEmpty(takenMessage) ? World.Loc.Pick("Подобрано: ", "Picked up: ") + World.Loc.T(displayName) + "." : World.Loc.T(takenMessage);
            if (heavy && holder != null)
            {
                // A heavy item is carried, not destroyed: X puts it down again where the player stands.
                HeavyCarry.On(holder.gameObject).Carry(this);
                gameObject.SetActive(false);
                return message + World.Loc.Pick("\nЛКМ — бросить на пол.", "\nLMB — drop it on the floor.");
            }

            Destroy(gameObject);
            return message;
        }
    }
}
