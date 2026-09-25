using ReactiveSolutions.AttributeSystem.Core.Data.Json;
using SemanticKeys;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Converts StatusEffects to and from JSON files (the ones in Resources/Data/StatusEffects). The file describes the
    /// status the way StatusEffectBuilder builds it, e.g. <c>"duration": 5, "tick": { "every": 1, "effect": "Debuffs/PoisonTick" }</c>,
    /// and names keys by name, with a "keys" table of their GUIDs at the end. See Documentation/Status Effects.md.
    /// </summary>
    public static class StatusEffectJson
    {
        /// <summary>
        /// The status effect as JSON. Throws an InvalidOperationException, saying which field, if something can't be
        /// saved (e.g. a path in its condition that doesn't start with Source or Target).
        /// </summary>
        public static string ToJson(StatusEffect status) => DataJsonWriter.WriteStatusEffect(status);

        /// <summary>
        /// Builds a status effect from JSON with StatusEffectBuilder. Throws a <see cref="JsonFormatException"/>, saying
        /// where and what is wrong, if the JSON isn't a valid status effect.
        /// </summary>
        /// <param name="findKey">
        /// Optional: finds the keys whose names aren't in the file's "keys" table (e.g. names typed by hand).
        /// Return SemanticKey.None for a name you don't know. The Status Effect Editor looks them up in your KeyDomains.
        /// </param>
        public static StatusEffect FromJson(string json, Func<string, SemanticKey> findKey = null) =>
            DataJsonReader.ReadStatusEffect(json, findKey);
    }
}
