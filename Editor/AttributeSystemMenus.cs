using ReactiveSolutions.AttributeSystem.Unity;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// The menus outside the editor windows: Assets > Create > Attribute System, GameObject > Attribute System, and
    /// double-clicking a data file to open it in its window. The windows themselves are under Tools > Attribute System.
    /// </summary>
    public static class AttributeSystemMenus
    {
        private const string CreateMenu = "Assets/Create/Attribute System/";

        [MenuItem(CreateMenu + "Entity Profile", false, 81)]
        private static void CreateEntityProfile() => CreateFile(DataKind.EntityProfile);

        [MenuItem(CreateMenu + "Stat Block", false, 82)]
        private static void CreateStatBlock() => CreateFile(DataKind.StatBlock);

        [MenuItem(CreateMenu + "Effect", false, 83)]
        private static void CreateEffect() => CreateFile(DataKind.Effect);

        [MenuItem(CreateMenu + "Status Effect", false, 84)]
        private static void CreateStatusEffect() => CreateFile(DataKind.StatusEffect);

        /// <summary>
        /// Starts a new file in its window. It is saved in the folder selected in the Project window if that is a data
        /// folder of its kind (e.g. .../Resources/Data/EntityProfiles/Templates), and otherwise in Assets/Resources/Data/....
        /// </summary>
        private static void CreateFile(DataKind kind)
        {
            string folder = kind.DefaultFolder;
            string subfolder = "";
            string selected = SelectedFolder();
            if (selected != null && DataFilePaths.TrySplit(selected + "/", kind.ResourcesPath, out string dataFolder, out string rest))
            {
                folder = dataFolder;
                subfolder = rest;
            }
            kind.ShowWindow().NewFile(folder, subfolder + "New" + kind.Label.Replace(" ", ""));
        }

        /// <summary>The selected folder in the Project window (or the folder of the selected file), or null.</summary>
        private static string SelectedFolder()
        {
            string path = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        /// <summary>Double-clicking a data file (a JSON file in a Resources/Data/... folder) opens it in its window.</summary>
        [OnOpenAsset]
        public static bool OpenDataFile(int instanceId, int line)
        {
            var kind = DataKind.Of(AssetDatabase.GetAssetPath(instanceId));
            if (kind == null) return false;

            kind.Open(AssetDatabase.GetAssetPath(instanceId));
            return true;
        }

        /// <summary>A GameObject with an EntityController: pick its profile in the Inspector.</summary>
        [MenuItem("GameObject/Attribute System/Entity", false, 10)]
        private static void CreateEntity(MenuCommand command)
        {
            var entity = new GameObject("Entity", typeof(EntityController));
            GameObjectUtility.SetParentAndAlign(entity, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(entity, "Create Entity");
            Selection.activeObject = entity;
        }
    }
}
