namespace AshenWoods.Interaction
{
    /// <summary>
    /// Anything the player can look at and use.
    ///
    /// Implement this on the MonoBehaviour that owns the collider the interaction raycast
    /// hits (or on a parent of it). The interaction system then never has to know what a
    /// door or a key actually is - it only knows the three questions below.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// False while the object cannot be used right now (mid-swing, already taken).
        /// No prompt is shown in that state, so the player never gets a dead button.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Short text shown on screen while the player looks at this object,
        /// for example "Open door" or "Locked (needs an iron key)".
        /// </summary>
        string GetPrompt(IInteractionActor actor);

        /// <summary>
        /// Runs the interaction. Returns a one-off message to show the player
        /// ("Locked. It needs an iron key."), or null when nothing needs saying.
        /// </summary>
        string Interact(IInteractionActor actor);
    }
}
