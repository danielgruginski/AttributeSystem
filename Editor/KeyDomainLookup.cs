using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// The keys of the project's KeyDomains, for the JSON editor windows: finds the keys that a file names but
    /// doesn't list in its key table (names typed by hand), and refreshes the names of keys renamed since the file
    /// was saved.
    /// </summary>
    internal static class KeyDomainLookup
    {
        /// <summary>
        /// The key named <paramref name="name"/> ("Health", or "Stats.Health" for the one in the Stats domain), or
        /// SemanticKey.None if there is none. Throws if keys in several KeyDomains have that name.
        /// </summary>
        public static SemanticKey FindByName(string name)
        {
            var domains = LoadDomains();
            var matches = Matches(domains, name, null);

            int dot = name.IndexOf('.');
            if (matches.Count == 0 && dot > 0) matches = Matches(domains, name.Substring(dot + 1), name.Substring(0, dot));

            if (matches.Count == 0) return SemanticKey.None;
            if (matches.Count == 1) return new SemanticKey(matches[0].key.Guid, matches[0].key.Name, matches[0].domain.Guid);

            var qualified = matches.Select(m => $"\"{m.domain.DomainName}.{m.key.Name}\"");
            throw new ArgumentException($"'{name}' is a key in more than one KeyDomain: write {string.Join(" or ", qualified)}");
        }

        /// <summary>
        /// Gives the keys in <paramref name="serializedObject"/> the current names (and domains) of the KeyDomain keys
        /// with the same GUIDs, so that saving a file writes the new names of renamed keys.
        /// </summary>
        public static void RefreshKeys(SerializedObject serializedObject)
        {
            var byGuid = new Dictionary<string, (string name, string domainGuid)>();
            foreach (var domain in LoadDomains())
            {
                foreach (var key in domain.Keys)
                {
                    if (key != null && !string.IsNullOrEmpty(key.Guid) && !byGuid.ContainsKey(key.Guid))
                    {
                        byGuid.Add(key.Guid, (key.Name, domain.Guid));
                    }
                }
            }

            var property = serializedObject.GetIterator();
            while (property.Next(true))
            {
                if (property.type != nameof(SemanticKey)) continue;

                var guid = property.FindPropertyRelative("_guid");
                if (guid == null || string.IsNullOrEmpty(guid.stringValue) || !byGuid.TryGetValue(guid.stringValue, out var current)) continue;

                property.FindPropertyRelative("_value").stringValue = current.name;
                property.FindPropertyRelative("_domainGuid").stringValue = current.domainGuid;
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<KeyDomain> LoadDomains() =>
            AssetDatabase.FindAssets("t:KeyDomain")
                .Select(guid => AssetDatabase.LoadAssetAtPath<KeyDomain>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(domain => domain != null)
                .ToList();

        private static List<(KeyDomain domain, KeyDomain.KeyDefinition key)> Matches(List<KeyDomain> domains, string keyName, string domainName) =>
            domains
                .Where(domain => domainName == null || domain.DomainName == domainName)
                .SelectMany(domain => domain.Keys
                    .Where(key => key != null && key.Name == keyName)
                    .Select(key => (domain, key)))
                .ToList();
    }
}
