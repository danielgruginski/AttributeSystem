using SemanticKeys;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Represents a persistent link that tries to apply a modifier to a target at the end of a path.
    /// </summary>
    public class AttributeConnection : PathConnection
    {
        private readonly SemanticKey _targetAttribute;
        private readonly IAttributeModifier _modifier;
        private readonly string _sourceId;

        public string SourceId => _sourceId;

        public AttributeConnection(
            AttributeProcessor root,
            List<SemanticKey> path,
            SemanticKey targetAttribute,
            IAttributeModifier modifier,
            string sourceId) : base(root, path)
        {
            _targetAttribute = targetAttribute;
            _modifier = modifier;
            _sourceId = sourceId;

            Connect();
        }

        protected override void OnApplyToTarget(AttributeProcessor target)
        {
            // We use the direct local Add (Handle-less overload) because WE are the handle.
            target.GetOrCreateAttribute(_targetAttribute).AddModifier(_modifier);
        }

        protected override void OnRemoveFromTarget(AttributeProcessor target)
        {
            var attr = target.GetAttribute(_targetAttribute);
            attr?.RemoveModifier(_modifier);
        }
    }
}