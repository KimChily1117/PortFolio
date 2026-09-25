using System;
using UnityEngine;

namespace Kimchily.TypeScript
{
    /// <summary>Compiled, closed module graph produced by the Editor TypeScript importer.</summary>
    public sealed class TypeScriptAsset : ScriptableObject
    {
        public int apiVersion = 1;
        public string entryModule;
        public string className;
        public string sourceHash;
        public string compilerVersion;
        public bool compiledSuccessfully;
        public string[] diagnostics = Array.Empty<string>();
        public TypeScriptModule[] modules = Array.Empty<TypeScriptModule>();
        public TypeScriptField[] fields = Array.Empty<TypeScriptField>();
    }

    [Serializable]
    public sealed class TypeScriptModule
    {
        public string id;
        public string source;
        public string sourceMap;
    }

    [Serializable]
    public sealed class TypeScriptField
    {
        public string name;
        public string kind;
    }

    [Serializable]
    public sealed class TypeScriptFieldBinding
    {
        public string name;
        public string kind;
        public bool useOverride;
        public double numberValue;
        public string stringValue;
        public bool boolValue;
        public GameObject gameObjectValue;
        public Transform transformValue;
        public Vector3 vectorValue;
    }
}
