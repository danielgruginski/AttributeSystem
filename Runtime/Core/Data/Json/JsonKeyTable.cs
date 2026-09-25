using SemanticKeys;
using System;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    /// <summary>
    /// Names the keys a file uses. The file's "keys" table maps each name to the key's GUID: the name keeps the
    /// file readable, the GUID is the key's identity (keys match by GUID, so the file survives renames).
    /// </summary>
    internal sealed class KeyTableWriter
    {
        private readonly bool _useGuids;
        private readonly Dictionary<string, string> _nameByGuid = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The "keys" table: name -> GUID, in the order the keys were first used.</summary>
        public JsonNode Table { get; } = JsonNode.NewObject();

        /// <param name="useGuids">Name each key by its GUID and keep no table: for comparing values, not for files.</param>
        public KeyTableWriter(bool useGuids = false)
        {
            _useGuids = useGuids;
            Table.Expanded = true;
        }

        /// <summary>
        /// The key's name in this file: its value (e.g. "Health"), with a number added if another key already has
        /// that name ("Poison#2"). A '/' is replaced, as it separates the steps of a path.
        /// </summary>
        public string NameOf(SemanticKey key)
        {
            if (_useGuids) return key.Guid;
            if (_nameByGuid.TryGetValue(key.Guid, out var name)) return name;

            string baseName = key.Value.Trim().Replace('/', '_');
            if (baseName.Length == 0) baseName = key.Guid.Trim().Replace('/', '_');

            name = baseName;
            for (int n = 2; _usedNames.Contains(name); n++) name = baseName + "#" + n;

            _usedNames.Add(name);
            _nameByGuid.Add(key.Guid, name);
            Table.Add(name, JsonNode.From(key.Guid));
            return name;
        }
    }

    /// <summary>Resolves the key names in a file with its "keys" table.</summary>
    internal sealed class KeyTableReader
    {
        private readonly Dictionary<string, string> _guidByName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Func<string, SemanticKey> _findKey;

        /// <param name="table">The file's "keys" table, or null if it has none.</param>
        /// <param name="findKey">Finds keys whose names aren't in the table, or null.</param>
        public KeyTableReader(JsonNode table, Func<string, SemanticKey> findKey)
        {
            _findKey = findKey;
            if (table == null) return;

            if (table.Kind != JsonKind.Object)
            {
                throw DataJsonReader.Error(table, "keys", "expected an object of key names and their GUIDs, e.g. { \"Health\": \"9bdcc19d-...\" }");
            }

            foreach (var entry in table.Properties)
            {
                string name = entry.Key.Trim();
                string path = DataJsonReader.Child("keys", entry.Key);
                if (entry.Value.Kind != JsonKind.String || entry.Value.Text.Length == 0)
                {
                    throw DataJsonReader.Error(entry.Value, path, $"expected the key's GUID (a string), found {entry.Value.Describe()}");
                }
                if (name.Length == 0 || name.Contains("/"))
                {
                    throw DataJsonReader.Error(entry.Value, path, "a key name can't be empty or contain '/'");
                }

                if (_guidByName.TryGetValue(name, out var guid) && guid != entry.Value.Text)
                {
                    throw DataJsonReader.Error(entry.Value, path, $"'{name}' is listed twice, with different GUIDs");
                }
                _guidByName[name] = entry.Value.Text;
            }
        }

        public SemanticKey Resolve(string name, JsonNode at, string path)
        {
            if (_guidByName.TryGetValue(name, out var guid)) return new SemanticKey(guid, name, null);

            if (_findKey != null)
            {
                SemanticKey found;
                try
                {
                    found = _findKey(name);
                }
                catch (Exception e)
                {
                    throw DataJsonReader.Error(at, path, e.Message);
                }
                if (found != SemanticKey.None) return found;
            }

            throw DataJsonReader.Error(at, path,
                $"unknown key '{name}': it isn't in the file's \"keys\" table. Load and save the file in its editor window " +
                $"to look the name up in your KeyDomains, or add \"{name}\": \"<the key's GUID>\" to \"keys\"");
        }
    }
}
