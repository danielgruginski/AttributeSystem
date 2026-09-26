namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Loads StatusEffects saved as JSON (by the Status Effect Editor, or <see cref="StatusEffectJson"/>) from
    /// Resources/Data/StatusEffects.
    /// </summary>
    public static class StatusEffectJsonLoader
    {
        /// <summary>
        /// The folder, relative to a Resources folder, that holds the status effect JSON files.
        /// Example: Assets/Resources/Data/StatusEffects/Debuffs/Poison.json has the ID "Debuffs/Poison".
        /// </summary>
        public const string ResourcesPath = "Data/StatusEffects";

        /// <summary>
        /// Loads a status effect by ID (e.g. "Debuffs/Poison"; "Poison.json" and "Data/StatusEffects/Poison" work too).
        /// Returns null, and logs an error, if it can't be loaded. Statuses loaded from the same file are the same
        /// status: applying one to an entity that has the other stacks (see StatusEffect.Stacking).
        /// </summary>
        public static StatusEffect Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (!JsonDataLoader.TryLoad(ResourcesPath, id, json => StatusEffectJson.FromJson(json), nameof(StatusEffectJsonLoader),
                    "StatusEffect", out var status))
            {
                return null;
            }

            status.JsonId = NormalizeId(id);
            // A status without a name is named after its file in logs and UI.
            if (string.IsNullOrEmpty(status.StatusName)) status.StatusName = status.JsonId;
            return status;
        }

        /// <summary>The ID in its canonical form ("Poison.json" and "Data/StatusEffects/Poison" become "Poison").</summary>
        internal static string NormalizeId(string id) => JsonDataLoader.NormalizeId(id, ResourcesPath);
    }
}
