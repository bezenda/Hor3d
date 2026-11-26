using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>How a sound gets from where it is made to whoever hears it.</summary>
    public enum SoundPath
    {
        /// <summary>Nothing solid in between (same room, open doorway, open stairwell).</summary>
        Clear,

        /// <summary>A wall or closed door in between, on the same storey.</summary>
        Wall,

        /// <summary>A floor slab in between: the sound comes from the other storey.</summary>
        Floor
    }

    /// <summary>
    /// The one rule for how sound travels through the house, shared by the enemy's ears, the
    /// sounds the player hears and the threat cues, so what you hear always matches what the
    /// enemy can hear:
    ///
    ///   clear - full strength;
    ///   wall  - quieter and muffled;
    ///   floor - much quieter and heavily muffled (a thump from above, not a step beside you).
    ///
    /// Characters (CharacterControllers, the player's layer) never count as obstacles.
    /// </summary>
    public static class Acoustics
    {
        /// <summary>Height of the upper floor's walking surface. Matches HouseBuilder.UpperFloor.</summary>
        public const float UpperFloorHeight = 3.2f;

        /// <summary>The house's storey split: anything at or above this height is upstairs there.</summary>
        public const float HouseStoreySplit = UpperFloorHeight - 0.6f;

        /// <summary>
        /// Anything at or above this height belongs to the upper storey. Set by the scene's
        /// NoiseSystem when it wakes: a level of tall single-storey halls has no split at all.
        /// </summary>
        public static float StoreySplit { get; set; } = HouseStoreySplit;

        // Volume multipliers and low-pass cut-offs (Hz) for what the player hears.
        public const float WallGain = 0.55f;
        public const float FloorGain = 0.3f;
        public const float ClearCutoff = 22000f;
        public const float WallCutoff = 1800f;
        public const float FloorCutoff = 650f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static int playerLayer = -2;

        /// <summary>0 for the ground floor, 1 for the upper floor.</summary>
        public static int StoreyOf(float y)
        {
            return y >= StoreySplit ? 1 : 0;
        }

        /// <summary>Classifies the path between two points.</summary>
        public static SoundPath PathBetween(Vector3 from, Vector3 to, int mask = ~0)
        {
            if (!IsBlocked(from, to, mask))
            {
                return SoundPath.Clear;
            }

            return StoreyOf(from.y) != StoreyOf(to.y) ? SoundPath.Floor : SoundPath.Wall;
        }

        /// <summary>Volume multiplier the player hears for a path.</summary>
        public static float Gain(SoundPath path)
        {
            switch (path)
            {
                case SoundPath.Floor: return FloorGain;
                case SoundPath.Wall: return WallGain;
                default: return 1f;
            }
        }

        /// <summary>Low-pass cut-off for a path: the more in the way, the duller the sound.</summary>
        public static float Cutoff(SoundPath path)
        {
            switch (path)
            {
                case SoundPath.Floor: return FloorCutoff;
                case SoundPath.Wall: return WallCutoff;
                default: return ClearCutoff;
            }
        }

        /// <summary>True when something solid that is not a character sits between the points.</summary>
        public static bool IsBlocked(Vector3 from, Vector3 to, int mask = ~0)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f)
            {
                return false;
            }

            if (playerLayer == -2)
            {
                playerLayer = LayerMask.NameToLayer("Player");
            }

            int count = Physics.RaycastNonAlloc(from, delta / distance, Hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i].collider;
                if (hit == null || hit is CharacterController)
                {
                    continue;
                }

                if (playerLayer >= 0 && hit.gameObject.layer == playerLayer)
                {
                    continue;
                }

                // Loose physics props (a thrown can) are not walls.
                Rigidbody body = hit.attachedRigidbody;
                if (body != null && !body.isKinematic)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>Where the player's ears are: the scene's AudioListener, else the main camera.</summary>
        public static bool TryGetListener(out Vector3 position)
        {
            if (listener == null)
            {
                AudioListener found = Object.FindAnyObjectByType<AudioListener>();
                listener = found != null ? found.transform : Camera.main != null ? Camera.main.transform : null;
            }

            position = listener != null ? listener.position : Vector3.zero;
            return listener != null;
        }

        private static Transform listener;
    }
}
