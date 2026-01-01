using SemanticKeys;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Manages the application of a Tag to a remote processor via a provider path.
    /// </summary>
    public class TagConnection : PathConnection
    {
        private readonly SemanticKey _tag; // Using SemanticKey for consistency

        // Support passing SemanticKey directly if needed
        public TagConnection(AttributeProcessor root, List<SemanticKey> path, SemanticKey tag) : base(root, path)
        {
            _tag = tag;
            Connect();
        }

        protected override void OnApplyToTarget(AttributeProcessor target)
        {
            target.AddTag(_tag);
        }

        protected override void OnRemoveFromTarget(AttributeProcessor target)
        {
            target.RemoveTag(_tag);
        }
    }
}