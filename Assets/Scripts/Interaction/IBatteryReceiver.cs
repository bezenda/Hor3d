namespace AshenWoods.Interaction
{
    /// <summary>
    /// Anything a battery can be put into. The pickup only knows this interface, so it never
    /// references the player's flashlight directly.
    /// </summary>
    public interface IBatteryReceiver
    {
        /// <summary>Adds charge (0-1 of a full battery). Returns false when already full.</summary>
        bool AddCharge(float amount);
    }
}
