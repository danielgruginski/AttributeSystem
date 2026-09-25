using ReactiveSolutions.AttributeSystem.Core.Data.Json;
using SemanticKeys;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Converts Effects to and from JSON files (the ones in Resources/Data/Effects). The file describes the effect the
    /// way EffectBuilder builds it, e.g. <c>"actions": [{ "target": "Target/Health", "type": "Reduce", "value": 10 }]</c>,
    /// and names keys by name, with a "keys" table of their GUIDs at the end. See Documentation/Effects.md.
    /// </summary>
    public static class EffectJson
    {
        /// <summary>
        /// The effect as JSON. Throws an InvalidOperationException, saying which field, if something can't be saved
        /// (e.g. a path that doesn't start with Source or Target).
        /// </summary>
        public static string ToJson(Effect effect) => DataJsonWriter.WriteEffect(effect);

        /// <summary>
        /// Builds an effect from JSON with EffectBuilder. Throws a <see cref="JsonFormatException"/>, saying where and
        /// what is wrong, if the JSON isn't a valid effect.
        /// </summary>
        /// <param name="findKey">
        /// Optional: finds the keys whose names aren't in the file's "keys" table (e.g. names typed by hand).
        /// Return SemanticKey.None for a name you don't know. The Effect Editor looks them up in your KeyDomains.
        /// </param>
        public static Effect FromJson(string json, Func<string, SemanticKey> findKey = null) =>
            DataJsonReader.ReadEffect(json, findKey);
    }
}
