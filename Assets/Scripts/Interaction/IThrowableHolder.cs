namespace AshenWoods.Interaction
{
    /// <summary>
    /// Something that can carry one <see cref="ThrowableItem"/> (the player's free hand).
    /// The item only talks to this interface, so it never needs to know about the player.
    /// </summary>
    public interface IThrowableHolder
    {
        /// <summary>True while a throwable is already in hand.</summary>
        bool IsHoldingThrowable { get; }

        /// <summary>Takes the item into the hand. False when the hand is full or unavailable.</summary>
        bool TryHold(ThrowableItem item);
    }
}
