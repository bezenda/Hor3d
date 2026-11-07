using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A spare battery. Using it tops up whatever <see cref="IBatteryReceiver"/> the actor
    /// carries (the player's flashlight). A full flashlight leaves the battery where it is, so
    /// the player can come back for it later.
    /// </summary>
    public sealed class BatteryPickup : MonoBehaviour, IInteractable
    {
        [Tooltip("Charge added, as a fraction of a full battery.")]
        [SerializeField, Range(0.05f, 1f)] private float charge = 0.45f;

        [SerializeField] private string prompt = "Take battery";
        [SerializeField] private string takenMessage = "Fresh battery. The flashlight will last a while longer.";
        [SerializeField] private string fullMessage = "The flashlight is already fully charged.";
        [SerializeField] private string noReceiverMessage = "Nothing to put a battery in.";

        [Header("Noise")]
        [Tooltip("Loudness reported when the battery is taken. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float pickupNoiseLoudness = 0.12f;

        public bool IsAvailable => true;

        public string GetPrompt(IInteractionActor actor)
        {
            return prompt;
        }

        public string Interact(IInteractionActor actor)
        {
            IBatteryReceiver receiver = (actor as Component)?.GetComponentInParent<IBatteryReceiver>();

            if (receiver == null)
            {
                return noReceiverMessage;
            }

            if (!receiver.AddCharge(charge))
            {
                return fullMessage;
            }

            NoiseSystem.Emit(transform.position, NoiseCategory.Item, pickupNoiseLoudness, this);
            GameLog.Info($"[Battery] Took {name} (+{charge:P0}).", this);
            Destroy(gameObject);
            return takenMessage;
        }
    }
}
