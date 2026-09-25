namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Loads StatBlocks saved as JSON (by the Stat Block Editor) from Resources/Data/StatBlocks.
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

            var block = new StatBlock();
            LoadIntoStatBlock(id, block);
            return block;
        }

        /// <summary>
        /// Overwrites <paramref name="block"/> with the StatBlock JSON <paramref name="id"/>. Logs an error if it can't be loaded.
        /// </summary>
        public static void LoadIntoStatBlock(string id, StatBlock block) => TryLoadInto(id, block);

        internal static bool TryLoadInto(string id, StatBlock block) =>
            JsonDataLoader.TryLoadInto(ResourcesPath, id, block, nameof(StatBlockJsonLoader), "StatBlock");
    }
}