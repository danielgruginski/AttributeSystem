using UniRx;
using System.Linq;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Encapsulates the logic for managing Entity Tags.
    /// Uses a reference-counting system to handle overlapping tag applications.
    /// This separation prevents AttributeProcessor from becoming bloated with metadata logic.
    /// </summary>
    public class AttributeTagManager
    {
        // Dictionary mapping Tag -> Count. A tag is "active" if count > 0.
        private readonly ReactiveDictionary<SemanticKey, int> _tagCounts = new ReactiveDictionary<SemanticKey, int>();

        /// <summary>
        /// Public read-only stream of currently active tags (count > 0).
        /// Transforming the dictionary change stream into a collection-like stream would require more complex projection,
        /// but for direct observation, observing the dictionary adds/removes is sufficient.
        /// </summary>
        public IReadOnlyReactiveDictionary<SemanticKey, int> Tags => _tagCounts;

        public void AddTag(SemanticKey tag)
        {
            if (_tagCounts.TryGetValue(tag, out int count))
            {
                _tagCounts[tag] = count + 1;
            }
            else
            {
                _tagCounts[tag] = 1;
            }
        }

        public void RemoveTag(SemanticKey tag)
        {
            if (_tagCounts.TryGetValue(tag, out int count))
            {
                if (count > 1)
                {
                    _tagCounts[tag] = count - 1;
                }
                else
                {
                    _tagCounts.Remove(tag);
                }
            }
        }

        public bool HasTag(SemanticKey tag) => _tagCounts.ContainsKey(tag);
    }
}