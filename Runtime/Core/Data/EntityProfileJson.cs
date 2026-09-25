using ReactiveSolutions.AttributeSystem.Core.Data.Json;
using SemanticKeys;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Converts EntityProfiles to and from JSON files (the ones in Resources/Data/EntityProfiles). The file describes
    /// the profile the way ProfileBuilder builds it, e.g. <c>"baseAttributes": { "Health": 40 }</c>, and names keys
    /// by name, with a "keys" table of their GUIDs at the end. See Documentation/JSON Format.md.
    /// </summary>
    public static class EntityProfileJson
    {
        /// <summary>
        /// The profile as JSON. Nested profiles built in code are written in full, nested profiles referenced by ID
        /// as their ID. Throws an InvalidOperationException, saying which field, if something can't be saved.
        /// </summary>
        public static string ToJson(EntityProfile profile) => DataJsonWriter.WriteProfile(profile);

        /// <summary>
        /// Builds a profile from JSON with ProfileBuilder. Throws a <see cref="JsonFormatException"/>, saying where
        /// and what is wrong, if the JSON isn't a valid profile.
        /// </summary>
        /// <param name="findKey">
        /// Optional: finds the keys whose names aren't in the file's "keys" table (e.g. names typed by hand).
        /// Return SemanticKey.None for a name you don't know. The Entity Profile Editor looks them up in your KeyDomains.
        /// </param>
        public static EntityProfile FromJson(string json, Func<string, SemanticKey> findKey = null) =>
            DataJsonReader.ReadProfile(json, findKey);
    }
}
