
namespace algumacoisaqq.AttributeSystem
{
    /// <summary>
    /// A generic interface for any component that can provide attribute modifiers to an AttributeController.
    /// This allows for a unified management of stats from weapons, armor, buffs, etc.
    /// </summary>
    public interface IStatProvider
    {
        /// <summary>
        /// Initializes the provider and applies its stats to the owner.
        /// </summary>
        void Initialize(AttributeController ownerController);

        /// <summary>
        /// Shuts down the provider and removes its stats from the owner.
        /// </summary>
        void Shutdown();
        void TryRegister();
    }
}