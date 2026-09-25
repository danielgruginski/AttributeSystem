namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Loads EntityProfiles saved as JSON (by the Entity Profile Editor, or <see cref="EntityProfileJson"/>) from
    /// Resources/Data/EntityProfiles.
    /// </summary>
    public static class EntityProfileJsonLoader
    {
        /// <summary>The folder, relative to a Resources folder, that holds the profile JSON files.</summary>
        public const string ResourcesPath = "Data/EntityProfiles";

        /// <summary>
        /// Loads a profile by ID: its path under Resources/Data/EntityProfiles without the extension
        /// (e.g. "Monsters/Goblin"). Returns null, and logs an error, if it can't be loaded.
        /// </summary>
        public static EntityProfile Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (!JsonDataLoader.TryLoad(ResourcesPath, id, json => EntityProfileJson.FromJson(json), nameof(EntityProfileJsonLoader),
                    "EntityProfile", out var profile))
            {
                return null;
            }

            // Lets Entity.ApplyProfile recognize this profile when a nested entry refers to it by ID.
            profile.JsonId = NormalizeId(id);
            return profile;
        }

        /// <summary>The ID in its canonical form ("Goblin.json" and "Data/EntityProfiles/Goblin" become "Goblin").</summary>
        internal static string NormalizeId(string id) => JsonDataLoader.NormalizeId(id, ResourcesPath);
    }
}