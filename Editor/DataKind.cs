using ReactiveSolutions.AttributeSystem.Core.Data;
using System;
using System.Linq;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>A kind of JSON data file: the folder that holds it in a Resources folder, and the window that edits it.</summary>
    internal sealed class DataKind
    {
        public static readonly DataKind EntityProfile =
            new DataKind("Entity Profile", EntityProfileJsonLoader.ResourcesPath, EntityProfileEditorWindow.Open);

        public static readonly DataKind StatBlock = new DataKind("Stat Block", StatBlockJsonLoader.ResourcesPath, StatBlockEditorWindow.Open);

        public static readonly DataKind Effect = new DataKind("Effect", EffectJsonLoader.ResourcesPath, EffectEditorWindow.Open);

        public static readonly DataKind StatusEffect =
            new DataKind("Status Effect", StatusEffectJsonLoader.ResourcesPath, StatusEffectEditorWindow.Open);

        public static readonly DataKind[] All = { EntityProfile, StatBlock, Effect, StatusEffect };

        private readonly Func<JsonDataEditorWindow> _showWindow;

        private DataKind(string label, string resourcesPath, Func<JsonDataEditorWindow> showWindow)
        {
            Label = label;
            ResourcesPath = resourcesPath;
            _showWindow = showWindow;
        }

        /// <summary>The kind's name, e.g. "Entity Profile".</summary>
        public string Label { get; }

        /// <summary>The folder that holds these files in a Resources folder, e.g. "Data/EntityProfiles".</summary>
        public string ResourcesPath { get; }

        /// <summary>Where new files go, unless one is created in another data folder of this kind.</summary>
        public string DefaultFolder => "Assets/Resources/" + ResourcesPath;

        /// <summary>Shows the window that edits this kind.</summary>
        public JsonDataEditorWindow ShowWindow() => _showWindow();

        /// <summary>Opens a file of this kind in its window.</summary>
        public void Open(string assetPath) => ShowWindow().OpenFile(assetPath);

        /// <summary>The kind of a file, from the data folder it is in; null for any other file.</summary>
        public static DataKind Of(string assetPath) => All.FirstOrDefault(kind => DataFilePaths.IdOf(assetPath, kind.ResourcesPath) != null);
    }
}
