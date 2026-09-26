using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>A JSON data file: its ID (e.g. "Templates/Character") and its asset path.</summary>
    internal readonly struct DataFile
    {
        public readonly string Id;
        public readonly string AssetPath;

        public DataFile(string id, string assetPath)
        {
            Id = id;
            AssetPath = assetPath;
        }
    }

    /// <summary>
    /// The project's JSON data files of each kind, in every Resources folder: Assets/Resources, the RPG Starter once it
    /// is imported, and any other. Kept until a JSON file is added, changed, moved or deleted.
    /// </summary>
    internal static class DataFiles
    {
        private static readonly Dictionary<string, List<DataFile>> Cache = new Dictionary<string, List<DataFile>>();

        /// <summary>The files of a kind (e.g. "Data/EntityProfiles"), sorted by ID.</summary>
        public static IReadOnlyList<DataFile> Find(string resourcesPath)
        {
            if (Cache.TryGetValue(resourcesPath, out var cached)) return cached;

            var files = new List<DataFile>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string id = DataFilePaths.IdOf(path, resourcesPath);
                if (id != null) files.Add(new DataFile(id, path));
            }
            files.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id) != 0 ? string.CompareOrdinal(a.Id, b.Id) : string.CompareOrdinal(a.AssetPath, b.AssetPath));

            // Resources.Load returns either of two files with the same ID.
            foreach (var clash in files.GroupBy(file => file.Id).Where(group => group.Count() > 1))
            {
                Debug.LogWarning($"[Attribute System] {clash.Count()} files have the ID '{clash.Key}', and loading it may return any " +
                                 $"of them: {string.Join(", ", clash.Select(file => file.AssetPath))}. Rename or move all but one.");
            }

            Cache[resourcesPath] = files;
            return files;
        }

        /// <summary>The asset path of the file with that ID, or null if there is none.</summary>
        public static string PathOf(string resourcesPath, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var file in Find(resourcesPath))
            {
                if (file.Id == id) return file.AssetPath;
            }
            return null;
        }

        public static void Forget() => Cache.Clear();
    }

    /// <summary>Clears the list of data files whenever a JSON file is imported, deleted or moved.</summary>
    internal sealed class DataFilesWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool IsJson(string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            if (imported.Any(IsJson) || deleted.Any(IsJson) || moved.Any(IsJson) || movedFrom.Any(IsJson)) DataFiles.Forget();
        }
    }
}
