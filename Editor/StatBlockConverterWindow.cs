using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;
/*
public class StatBlockConverterWindow : EditorWindow
{
    private const string OLD_PATH = "Resources/Data/StatBlocks";
    private const string NEW_PATH = "Resources/Data/StatBlocks_Converted";

    [MenuItem("Window/Attributes/StatBlock Converter")]
    public static void ShowWindow()
    {
        GetWindow<StatBlockConverterWindow>("Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Legacy to Unified JSON Converter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"Reads from: {OLD_PATH}\nSaves to: {NEW_PATH}", MessageType.Info);

        EditorGUILayout.Space();
        GUILayout.Label("Status Check:", EditorStyles.label);
        // Visual check to ensure the script sees both types
        GUILayout.Label($"- New Type Detected: {typeof(AttributeModifierSpec).Name}");
        GUILayout.Label($"- Legacy Type Detected: {typeof(AttributeModifierSpecLegacy).Name}");

        if (GUILayout.Button("Convert All Files"))
        {
            ConvertFiles();
        }
    }

    private void ConvertFiles()
    {
        string fullOldPath = Path.Combine(Application.dataPath, OLD_PATH);
        string fullNewPath = Path.Combine(Application.dataPath, NEW_PATH);

        if (!Directory.Exists(fullOldPath))
        {
            Debug.LogError($"Old path not found: {fullOldPath}");
            return;
        }

        if (!Directory.Exists(fullNewPath)) Directory.CreateDirectory(fullNewPath);

        string[] files = Directory.GetFiles(fullOldPath, "*.json");
        int count = 0;

        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);

                // 1. Deserialize using a Wrapper that points to your Renamed Legacy Class
                LegacyStatBlockWrapper oldData = JsonUtility.FromJson<LegacyStatBlockWrapper>(json);

                if (oldData == null || oldData.Modifiers == null)
                {
                    Debug.LogWarning($"Skipped {Path.GetFileName(file)}: Could not parse JSON structure.");
                    continue;
                }

                // 2. Convert to NEW structure
                StatBlock newBlock = ConvertBlock(oldData);

                // 3. Save
                string fileName = Path.GetFileName(file);
                string newJson = JsonUtility.ToJson(newBlock, true);
                File.WriteAllText(Path.Combine(fullNewPath, fileName), newJson);

                count++;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to convert {Path.GetFileName(file)}: {e.Message}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"Conversion Complete! Converted {count} files to {NEW_PATH}");
    }

    private StatBlock ConvertBlock(LegacyStatBlockWrapper old)
    {
        StatBlock newBlock = new StatBlock();
        newBlock.BlockName = old.BlockName;
        newBlock.Modifiers = new List<AttributeModifierSpec>();

        foreach (var oldMod in old.Modifiers)
        {
            AttributeModifierSpec newMod = new AttributeModifierSpec();
            newMod.AttributeName = oldMod.AttributeName;
            newMod.Priority = oldMod.Priority;
            newMod.Params = new List<ModifierParam>();

            // --- MAPPING LOGIC (Adapts your Legacy class fields to the New Params) ---

            // IMPORTANT: This switch assumes your AttributeModifierSpecLegacy still uses the old AttributeOp enum.
            // If you renamed AttributeOp, update the cases below.

            // 1. Handle Transformations
            if (oldMod.LogicType.ToString() == "Clamp") // String check is safer if enums changed
            {
                newMod.OperationType = "Clamp";
                newMod.MergeMode = AttributeMergeMode.Override;
                AddParam(newMod.Params, "Min", oldMod.Clamp.Min);
                AddParam(newMod.Params, "Max", oldMod.Clamp.Max);
            }
            else if (oldMod.LogicType.ToString().Contains("DiminishingReturns"))
            {
                newMod.OperationType = "DiminishingReturns";
                newMod.MergeMode = AttributeMergeMode.Add;
                AddParam(newMod.Params, "SoftCap", oldMod.Linear.Source.ConstantValue);
                AddParam(newMod.Params, "MaxBonus", 100f); // Legacy didn't have MaxBonus, defaulting to 100
            }
            // 2. Handle Formulas (Linear)
            else if (oldMod.LogicType == AttributeLogicType.Linear)
            {
                newMod.OperationType = "Linear";
                newMod.MergeMode = MapOpToMerge(oldMod.MergeMode);

                // Check if your legacy class exposed Formula as a struct or fields
                // Adjust these accessors to match your exact AttributeModifierSpecLegacy fields
                AddParam(newMod.Params, "Input", oldMod.Linear.Source.);
                AddParam(newMod.Params, "Coefficient", oldMod.Formula.Coefficient);
                AddParam(newMod.Params, "Addend", oldMod.Formula.Addend);
            }
            // 3. Handle Standard Flat Modifiers
            else
            {
                newMod.OperationType = "Constant";
                newMod.MergeMode = MapOpToMerge(oldMod.MergeMode);
                AddParam(newMod.Params, "Value", oldMod.Amount);
            }

            newBlock.Modifiers.Add(newMod);
        }

        return newBlock;
    }

    private AttributeMergeMode MapOpToMerge(object oldOpEnum)
    {
        // Flexible mapping that works even if enum types differ slightly
        string opName = oldOpEnum.ToString();
        if (opName == "Add") return AttributeMergeMode.Add;
        if (opName == "Multiply") return AttributeMergeMode.Multiply;
        if (opName == "Override") return AttributeMergeMode.Override;
        return AttributeMergeMode.Add;
    }

    private void AddParam(List<ModifierParam> list, string name, float val)
    {
        list.Add(new ModifierParam
        {
            Name = name,
            Value = new ValueSourceSpec { Type = ValueSource.SourceType.Constant, ConstantValue = val }
        });
    }

    // Overload for your legacy ValueSourceSpec (assuming it exists in the legacy file)
    private void AddParam(List<ModifierParam> list, string name, ValueSourceSpec oldSource)
    {
        list.Add(new ModifierParam
        {
            Name = name,
            Value = new ValueSourceSpec
            {
                // We assume the Enum int values (0=Constant, 1=Attribute) match. 
                // If not, cast explicitly.
                Type = oldSource.Type,
                ConstantValue = oldSource.ConstantValue,
                AttributeName = oldSource.AttributeName
            }
        });
    }

    // --- WRAPPER FOR DESERIALIZATION ---
    // This tricks JsonUtility into reading the "Modifiers" list into your RENAMED class.
    [Serializable]
    private class LegacyStatBlockWrapper
    {
        public string BlockName;
        // This is the magic link: It tells Unity "The objects in the 'Modifiers' list are of type AttributeModifierSpecLegacy"
        public List<AttributeModifierSpecLegacy> Modifiers;
    }
}*/