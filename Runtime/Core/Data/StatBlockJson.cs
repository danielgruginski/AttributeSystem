using ReactiveSolutions.AttributeSystem.Core.Data.Json;
using SemanticKeys;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Converts StatBlocks to and from JSON files (the ones in Resources/Data/StatBlocks). The file describes the
    /// block the way StatBlockBuilder builds it, e.g. <c>"modifiers": [{ "target": "Damage", "value": 5 }]</c>, and
    /// names keys by name, with a "keys" table of their GUIDs at the end. See Documentation/JSON Format.md.
    /// </summary>
    public static class StatBlockJson
    {
        /// <summary>
        /// The StatBlock as JSON. Throws an InvalidOperationException, saying which field, if something can't be
        /// saved (e.g. a logic field holding a Unity object).
        /// </summary>
        public static string ToJson(StatBlock block) => DataJsonWriter.WriteStatBlock(block);

        /// <summary>
        /// Builds a StatBlock from JSON with StatBlockBuilder. Throws a <see cref="JsonFormatException"/>, saying
        /// where and what is wrong, if the JSON isn't a valid StatBlock.
        /// </summary>
        /// <param name="findKey">
        /// Optional: finds the keys whose names aren't in the file's "keys" table (e.g. names typed by hand).
        /// Return SemanticKey.None for a name you don't know. The Stat Block Editor looks them up in your KeyDomains.
        /// </param>
        public static StatBlock FromJson(string json, Func<string, SemanticKey> findKey = null) =>
            DataJsonReader.ReadStatBlock(json, findKey);
    }
}
