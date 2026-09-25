namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Loads StatBlocks saved as JSON (by the Stat Block Editor, or <see cref="StatBlockJson"/>) from Resources/Data/StatBlocks.
    /// </summary>
    public static class StatBlockJsonLoader
    {
        /// <summary>
        /// The folder, relative to a Resources folder, that holds the StatBlock JSON files.
        /// Example: Assets/Resources/Data/StatBlocks/Block.json has the ID "Block".
        /// </summary>
        public const string ResourcesPath = "Data/StatBlocks";

        /// <summary>
        /// Loads a StatBlock by ID (e.g. "Weapons/IronSword"; "IronSword.json" and "Data/StatBlocks/IronSword" work too).
        /// If it can't be loaded, logs an error and returns an empty block.
        /// </summary>
        public static StatBlock Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return TryLoad(id, out var block) ? block : new StatBlock { BlockName = NormalizeId(id) };
        }

        /// <summary>Loads a StatBlock by ID. Logs an error and returns false if it can't be loaded.</summary>
        internal static bool TryLoad(string id, out StatBlock block)
        {
            if (!JsonDataLoader.TryLoad(ResourcesPath, id, json => StatBlockJson.FromJson(json), nameof(StatBlockJsonLoader), "StatBlock", out block))
            {
                return false;
            }

            // A block without a name is named after its file in logs and in the Attribute Debugger.
            if (string.IsNullOrEmpty(block.BlockName)) block.BlockName = NormalizeId(id);
            return true;
        }

        /// <summary>The ID in its canonical form ("IronSword.json" and "Data/StatBlocks/IronSword" become "IronSword").</summary>
        internal static string NormalizeId(string id) => JsonDataLoader.NormalizeId(id, ResourcesPath);
    }
}