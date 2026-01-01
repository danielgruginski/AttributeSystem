using SemanticKeys;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Defines a Tag application that targets a specific entity path.
    /// E.g. Apply "Blessed" to "Owner".
    /// </summary>
    [System.Serializable]
    public class TagModifierSpec
    {
        public SemanticKey Tag;
        public List<SemanticKey> TargetPath;
        // SourceId is implicit (the StatBlock applying it)
    }
}