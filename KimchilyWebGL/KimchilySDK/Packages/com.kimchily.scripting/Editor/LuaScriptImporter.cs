using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Kimchily.Scripting.Editor
{
    [ScriptedImporter(1, "lua")]
    public sealed class LuaScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var asset = new TextAsset(File.ReadAllText(context.assetPath))
            {
                name = Path.GetFileNameWithoutExtension(context.assetPath)
            };
            context.AddObjectToAsset("script", asset);
            context.SetMainObject(asset);
        }

        [MenuItem("Assets/Create/Kimchily/Lua Script", priority = 81)]
        private static void CreateScript()
        {
            const string template = "-- Attach this asset to a Kimchily Lua Behaviour.\n" +
                "function on_start()\n    log(\"World started\")\nend\n\n" +
                "function on_update(dt)\n    self.rotate(0, 45 * dt, 0)\nend\n";
            ProjectWindowUtil.CreateAssetWithContent("WorldBehaviour.lua", template);
        }
    }
}
