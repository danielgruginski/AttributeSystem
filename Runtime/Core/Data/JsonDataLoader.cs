using System;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Loads JSON data files (StatBlocks, EntityProfiles) from a folder under Resources, by ID.
    /// An ID is the file's path in that folder without the extension (e.g. "Weapons/IronSword").
    /// </summary>
    internal static class JsonDataLoader
    {
        /// <summary>
        /// Reads the JSON at a Resources path (without extension), or returns null if there is none.
        /// Tests replace it to serve JSON without a Resources folder.
        /// </summary>
        internal static Func<string, string> ReadText = DefaultReadText;

        internal static string DefaultReadText(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            return textAsset != null ? textAsset.text : null;
        }

        /// <summary>
        /// "IronSword", "IronSword.json" and "{folder}/IronSword" all name the same file.
        /// </summary>
        internal static string NormalizeId(string id, string folder)
        {
            string cleanId = id ?? string.Empty;
            if (cleanId.EndsWith(".json"))
            {
                cleanId = cleanId.Substring(0, cleanId.Length - ".json".Length);
            }

            string prefix = folder + "/";
            if (cleanId.StartsWith(prefix))
            {
                cleanId = cleanId.Substring(prefix.Length);
            }
            return cleanId;
        }

        /// <summary>
        /// Overwrites <paramref name="target"/> with the JSON file <paramref name="id"/> in <paramref name="folder"/>.
        /// Logs an error and returns false if the file is missing or can't be parsed.
        /// </summary>
        internal static bool TryLoadInto(string folder, string id, object target, string loaderName, string dataName)
        {
            string resourcePath = folder + "/" + NormalizeId(id, folder);
            string json = ReadText(resourcePath);

            if (json == null)
            {
                Debug.LogError($"[{loaderName}] Could not load {dataName} with ID: {id} (Attempted path: {resourcePath})");
                return false;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(json, target);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{loaderName}] Failed to parse JSON for {id}: {e.Message}");
                return false;
            }
        }
    }
}