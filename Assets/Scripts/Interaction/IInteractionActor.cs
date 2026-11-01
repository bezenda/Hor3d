namespace AshenWoods.Interaction
{
    /// <summary>
    /// What an interactable is allowed to ask of whoever is using it (normally the player).
    ///
    /// This is the seam that keeps doors and pickups free of any dependency on the player
    /// classes: a door asks "do you have the iron key?", it does not know about cameras,
    /// CharacterControllers or input. A future enemy or NPC can implement this too.
    /// </summary>
    public interface IInteractionActor
    {
        bool HasKey(string keyId);
        void GiveKey(string keyId);
    }
}
