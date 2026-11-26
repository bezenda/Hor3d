using UnityEngine;
using AshenWoods.Player;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A wardrobe, locker or gap in the wall the player can step into.
    ///
    /// The spot is only data plus an interaction: it knows where to put the player but not
    /// how the player works. All of that lives in PlayerHidingController, which is why a
    /// second kind of hiding place (a bed, a crate, a crawlspace) needs no new code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingSpot : MonoBehaviour, IInteractable
    {
        [Header("Placement")]
        [Tooltip("Where the player stands while hidden. Falls back to this object. " +
                 "The player root sits at the feet, so put it on the floor.")]
        [SerializeField] private Transform hidePoint;

        [Tooltip("Camera height above the hide point while inside, in metres. 0 keeps the " +
                 "current height (a wardrobe); a bed uses a few centimetres so you peek out " +
                 "from under the mattress.")]
        [SerializeField, Min(0f)] private float eyeHeight;

        [Header("Text")]
        [SerializeField] private string enterPrompt = "Спрятаться";
        [SerializeField] private string exitPrompt = "Вылезти";

        /// <summary>Camera height while hidden; 0 means "leave it as it is".</summary>
        public float EyeHeight => eyeHeight;

        /// <summary>Whoever is currently inside, or null.</summary>
        public PlayerHidingController Occupant { get; private set; }

        public bool IsOccupied => Occupant != null;

        public Transform HidePoint => hidePoint != null ? hidePoint : transform;

        /// <summary>A hiding spot can always be used - to enter it, or to leave it again.</summary>
        public bool IsAvailable => true;

        public string GetPrompt(IInteractionActor actor)
        {
            return IsOccupied ? exitPrompt : enterPrompt;
        }

        public string Interact(IInteractionActor actor)
        {
            // The actor is the player's interactor component, so its GameObject carries the
            // hiding state. Nothing else on the actor needs to know about HidingSpot.
            Component actorComponent = actor as Component;
            if (actorComponent == null)
            {
                return null;
            }

            PlayerHidingController hider = actorComponent.GetComponent<PlayerHidingController>();
            if (hider == null)
            {
                Debug.LogWarning($"{name}: the player has no PlayerHidingController component.", this);
                return null;
            }

            if (hider.IsHidden)
            {
                hider.Leave();
                return null;
            }

            hider.Enter(this);
            return null;
        }

        /// <summary>Called by PlayerHidingController so the prompt can switch to "leave".</summary>
        public void SetOccupant(PlayerHidingController occupant)
        {
            Occupant = occupant;
        }
    }
}
