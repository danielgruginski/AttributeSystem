using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public interface IModifierFactory
    {
        void Register(string id, ModifierBuilder builder);
        IAttributeModifier Create(string id, ModifierArgs args);
        IEnumerable<string> GetAvailableTypes();
    }

    /// <summary>
    /// Delegate for creating a modifier instance.
    /// </summary>
    public delegate IAttributeModifier ModifierBuilder(ModifierArgs args);

    /// <summary>
    /// Unified arguments structure passed to builders.
    /// </summary>
    public struct ModifierArgs
    {
        public string SourceId;
        public ModifierType Type;
        public int Priority;
        public IList<ValueSource> Arguments;

        public ModifierArgs(string sourceId, ModifierType type, int priority, IList<ValueSource> arguments)
        {
            SourceId = sourceId;
            Type = type;
            Priority = priority;
            Arguments = arguments;
        }
    }
}