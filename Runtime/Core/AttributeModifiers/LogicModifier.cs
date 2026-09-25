using System;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    /// <summary>
    /// A modifier whose value comes from a <see cref="ModifierLogic"/>. StatBlocks create one for each application
    /// of each modifier spec. To add logic from code:
    /// <c>entity.AddModifier("Ring", new LogicModifier(new ValueLogic(5f)), Stats.Damage)</c>.
    /// </summary>
    public sealed class LogicModifier : IAttributeModifier
    {
        public ModifierLogic Logic { get; }
        public ModifierType Type { get; }
        public int Priority { get; }
        public string SourceId { get; }

        /// <summary>
        /// The entity the logic's inputs resolve against (for a StatBlock, the entity it is applied to).
        /// Null means the entity whose attribute is modified.
        /// </summary>
        public Entity Context { get; }

        public LogicModifier(ModifierLogic logic, ModifierType type = ModifierType.Additive, int priority = 0,
                             string sourceId = null, Entity context = null)
        {
            Logic = logic ?? throw new ArgumentNullException(nameof(logic));
            Type = type;
            Priority = priority;
            SourceId = sourceId;
            Context = context;
        }

        public IObservable<float> GetMagnitude(Entity processor) => Logic.Observe(Context ?? processor);
    }
}