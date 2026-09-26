using System;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Where JSON data files are, and their IDs. A file of a kind (e.g. "Data/EntityProfiles") lives in that folder of any
    /// Resources folder: "Assets/Game/Resources/Data/EntityProfiles/Templates/Character.json" has the ID
    /// "Templates/Character", which is what Resources.Load, and so the loaders, expect.
    /// </summary>
    internal static class DataFilePaths
    {
        private const string Extension = ".json";

        /// <summary>The ID of the JSON file at <paramref name="path"/>, or null if it isn't a data file of that kind.</summary>
        public static string IdOf(string path, string resourcesPath)
        {
            if (!TrySplit(path, resourcesPath, out _, out string rest)) return null;
            if (!rest.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || rest.Length == Extension.Length) return null;
            return rest.Substring(0, rest.Length - Extension.Length);
        }

        /// <summary>
        /// Splits a path inside a data folder into that folder and the rest:
        /// "Assets/Game/Resources/Data/StatBlocks/Weapons/Sword.json" is "Assets/Game/Resources/Data/StatBlocks" and
        /// "Weapons/Sword.json". False if the path isn't inside a folder of that kind.
        /// </summary>
        public static bool TrySplit(string path, string resourcesPath, out string folder, out string rest)
        {
            folder = null;
            rest = null;
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(resourcesPath)) return false;

            string normalized = path.Replace('\\', '/');
            string marker = "/Resources/" + resourcesPath.Trim('/') + "/";
            int index = normalized.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return false;

            folder = normalized.Substring(0, index + marker.Length - 1);
            rest = normalized.Substring(index + marker.Length);
            return true;
        }
    }
}
