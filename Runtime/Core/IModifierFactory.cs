using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public interface IModifierFactory
    {
        void Register(string id, ModifierBuilder builder);
        IAttributeModifier Create(string id, AttributeModifierSpec spec);
        IAttributeModifier Create(AttributeModifierSpec spec, AttributeProcessor context);
        IEnumerable<string> GetAvailableTypes();
    }

    /// <summary>
    /// Delegate for creating a modifier instance.
    /// </summary>
    public delegate IAttributeModifier ModifierBuilder(AttributeModifierSpec spec);
}