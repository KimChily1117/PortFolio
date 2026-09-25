using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kimchily.TypeScript.Editor
{
    [CustomEditor(typeof(KimchilyTypeScriptBehaviour))]
    public sealed class TypeScriptBehaviourEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var behaviour = (KimchilyTypeScriptBehaviour)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scriptAsset"), new GUIContent("TypeScript Script"));
            serializedObject.ApplyModifiedProperties();
            var asset = behaviour.ScriptAsset;
            if (asset == null) { EditorGUILayout.HelpBox("Assign a .ts asset with a default class extending KimchilyScriptBehaviour.", MessageType.Info); return; }
            DrawDiagnostics(asset);
            if (!asset.compiledSuccessfully) return;
            if (string.IsNullOrEmpty(asset.className))
            {
                EditorGUILayout.HelpBox("This file is a helper module. A behaviour needs a default-exported class extending KimchilyScriptBehaviour.", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField("Class", asset.className);
            if (GUILayout.Button("Open TypeScript Source")) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetDatabase.GetAssetPath(asset)));
            SynchronizeFields(behaviour);
            serializedObject.Update();
            var bindings = serializedObject.FindProperty("fields");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                string name = binding.FindPropertyRelative("name").stringValue;
                string kind = binding.FindPropertyRelative("kind").stringValue;
                var use = binding.FindPropertyRelative("useOverride");
                EditorGUILayout.Space(3);
                use.boolValue = EditorGUILayout.ToggleLeft(name + " : " + kind + " — Inspector override", use.boolValue);
                if (!use.boolValue) { EditorGUILayout.LabelField("", "Uses the class initializer"); continue; }
                string value = kind == "number" ? "numberValue" : kind == "string" ? "stringValue" : kind == "boolean" ? "boolValue" :
                    kind == "GameObject" ? "gameObjectValue" : kind == "Transform" ? "transformValue" : kind == "Vector3" ? "vectorValue" : null;
                if (value == null) EditorGUILayout.HelpBox("Unsupported field type: " + kind, MessageType.Error);
                else EditorGUILayout.PropertyField(binding.FindPropertyRelative(value), new GUIContent(name));
            }
            serializedObject.ApplyModifiedProperties();
            try { foreach (string error in behaviour.ValidateContent()) EditorGUILayout.HelpBox(error, MessageType.Error); }
            catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
        }

        internal static void SynchronizeFields(KimchilyTypeScriptBehaviour behaviour)
        {
            var schema = behaviour.ScriptAsset.fields ?? Array.Empty<TypeScriptField>();
            var old = behaviour.Fields ?? Array.Empty<TypeScriptFieldBinding>();
            if (old.Length == schema.Length && old.Select((b, i) => b != null && b.name == schema[i].name && b.kind == schema[i].kind).All(x => x)) return;
            Undo.RecordObject(behaviour, "Update TypeScript Inspector fields");
            behaviour.Fields = schema.Select(field => old.FirstOrDefault(b => b != null && b.name == field.name && b.kind == field.kind)
                ?? new TypeScriptFieldBinding { name = field.name, kind = field.kind, useOverride = false }).ToArray();
            EditorUtility.SetDirty(behaviour);
            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
        }

        internal static void DrawDiagnostics(TypeScriptAsset asset)
        {
            foreach (string diagnostic in asset.diagnostics ?? Array.Empty<string>())
                EditorGUILayout.HelpBox(diagnostic, asset.compiledSuccessfully ? MessageType.Warning : MessageType.Error);
        }
    }

    [CustomEditor(typeof(TypeScriptAsset))]
    public sealed class TypeScriptAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (TypeScriptAsset)target;
            TypeScriptBehaviourEditor.DrawDiagnostics(asset);
            EditorGUILayout.LabelField("TypeScript", asset.compilerVersion ?? "not compiled");
            EditorGUILayout.LabelField("Status", asset.compiledSuccessfully ? "Compiled" : "Compilation failed");
            EditorGUILayout.LabelField("Entry", asset.entryModule ?? string.Empty);
            EditorGUILayout.LabelField("Class", string.IsNullOrEmpty(asset.className) ? "Helper module" : asset.className);
            EditorGUILayout.LabelField("Bundled modules", (asset.modules?.Length ?? 0).ToString());
            foreach (var field in asset.fields ?? Array.Empty<TypeScriptField>()) EditorGUILayout.LabelField(field.name, field.kind);
            if (GUILayout.Button("Open TypeScript Source")) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetDatabase.GetAssetPath(asset)));
        }
    }
}
