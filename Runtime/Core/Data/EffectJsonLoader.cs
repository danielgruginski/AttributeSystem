namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Loads Effects saved as JSON (by the Effect Editor, or <see cref="EffectJson"/>) from Resources/Data/Effects.
    /// </summary>
    public static class EffectJsonLoader
    {
        /// <summary>
        /// The folder, relative to a Resources folder, that holds the effect JSON files.
        /// Example: Assets/Resources/Data/Effects/Spells/Fireball.json has the ID "Spells/Fireball".
        /// </summary>
        public const string ResourcesPath = "Data/Effects";

        /// <summary>
        /// Loads an effect by ID (e.g. "Spells/Fireball"; "Fireball.json" and "Data/Effects/Fireball" work too).
        /// If it can't be loaded, logs an error and returns an effect that does nothing.
        /// </summary>
        public static Effect Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return TryLoad(id, out var effect) ? effect : new Effect { EffectName = NormalizeId(id) };
        }

        /// <summary>Loads an effect by ID. Logs an error and returns false if it can't be loaded.</summary>
        public static bool TryLoad(string id, out Effect effect)
        {
            if (!JsonDataLoader.TryLoad(ResourcesPath, id, json => EffectJson.FromJson(json), nameof(EffectJsonLoader), "Effect", out effect))
            {
                return false;
            }

            // An effect without a name is named after its file in logs.
            if (string.IsNullOrEmpty(effect.EffectName)) effect.EffectName = NormalizeId(id);
            return true;
        }

        /// <summary>The ID in its canonical form ("Fireball.json" and "Data/Effects/Fireball" become "Fireball").</summary>
        internal static string NormalizeId(string id) => JsonDataLoader.NormalizeId(id, ResourcesPath);
    }
}
