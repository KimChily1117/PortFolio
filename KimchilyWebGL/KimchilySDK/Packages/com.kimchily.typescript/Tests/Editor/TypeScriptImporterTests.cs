using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kimchily.TypeScript.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.TypeScript.Tests
{
    public sealed class TypeScriptImporterTests
    {
        string folder;
        const string Header = "import { KimchilyScriptBehaviour } from 'Kimchily.Script';\nimport { GameObject } from 'UnityEngine';\n";

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/__KimchilyTypeScriptTests_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        TypeScriptAsset Import(string name, string source)
        {
            string path = folder + "/" + name + ".ts";
            File.WriteAllText(path, source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(path);
        }

        [Test]
        public void ImportProducesActualClassModuleGraphAndPublicInspectorFields()
        {
            Import("Helper", "export const amount: number = 45;");
            var asset = Import("Main", "import { amount } from './Helper';\n" + Header +
                "export default class Main extends KimchilyScriptBehaviour { public speed = amount; public target: GameObject | null = null; private hidden = new Map<string, number>(); }");
            Assert.NotNull(asset);
            Assert.IsTrue(asset.compiledSuccessfully, string.Join("\n", asset.diagnostics));
            Assert.AreEqual("Main", asset.className);
            Assert.AreEqual(2, asset.modules.Length);
            CollectionAssert.AreEqual(new[] { "speed", "target" }, asset.fields.Select(f => f.name).ToArray());
            CollectionAssert.AreEqual(new[] { "number", "GameObject" }, asset.fields.Select(f => f.kind).ToArray());
            // DependsOnSourceAsset is an importer invalidation dependency, not a
            // serialized Object reference returned by GetDependencies. Verify
            // its actual effect without explicitly reimporting Main.ts.
            string before = asset.sourceHash;
            File.WriteAllText(folder + "/Helper.ts", "export const amount: number = 99;");
            AssetDatabase.ImportAsset(folder + "/Helper.ts", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var refreshed = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(folder + "/Main.ts");
            Assert.IsTrue(refreshed.compiledSuccessfully, string.Join("\n", refreshed.diagnostics));
            Assert.AreNotEqual(before, refreshed.sourceHash, "Changing a source dependency must recompile its entry automatically.");
            StringAssert.Contains("99", refreshed.modules.Single(m => m.id.EndsWith("/Helper")).source);
        }

        [Test]
        public void HelperImportsSuccessfullyButCannotBeAttachedAsBehaviour()
        {
            var helper = Import("Helper", "export const amount = 45;");
            Assert.IsTrue(helper.compiledSuccessfully);
            Assert.IsTrue(string.IsNullOrEmpty(helper.className));
            var owner = new GameObject("TypeScript helper validation");
            owner.SetActive(false);
            try
            {
                var behaviour = owner.AddComponent<KimchilyTypeScriptBehaviour>();
                behaviour.ScriptAsset = helper;
                Assert.IsTrue(behaviour.ValidateContent().Any(error => error.Contains("default-export")));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void FailedReimportClearsPreviouslySuccessfulJavaScript()
        {
            var initial = Import("Main", Header + "export default class Main extends KimchilyScriptBehaviour { public speed: number = 45; }");
            Assert.IsTrue(initial.compiledSuccessfully);
            Assert.IsNotEmpty(initial.modules);
            bool previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true; // The deliberately invalid import reports TS2322 through Unity's importer log.
            try
            {
                var failed = Import("Main", Header + "export default class Main extends KimchilyScriptBehaviour { public speed: number = 'wrong'; }");
                Assert.IsFalse(failed.compiledSuccessfully);
                Assert.IsEmpty(failed.modules);
                Assert.IsEmpty(failed.fields);
                Assert.IsTrue(failed.diagnostics.Any(error => error.Contains("TS2322")));
            }
            finally { LogAssert.ignoreFailingMessages = previous; }
        }

        [Test]
        public void BuildPreflightDetectsAHelperEditedOutsideUnityAndRefreshesTheAsset()
        {
            Import("Helper", "export const amount = 45;");
            var initial = Import("Main", "import { amount } from './Helper';\n" + Header +
                "export default class Main extends KimchilyScriptBehaviour { public speed = amount; }");
            string before = initial.sourceHash;
            File.WriteAllText(folder + "/Helper.ts", "export const amount = 80;");
            var errors = new List<string>();
            TypeScriptBuildValidation.ValidateAssets(new[] { folder + "/Main.ts" }, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            var refreshed = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(folder + "/Main.ts");
            Assert.AreNotEqual(before, refreshed.sourceHash);
            StringAssert.Contains("80", refreshed.modules.Single(m => m.id.EndsWith("/Helper")).source);
        }

        [Test]
        public void UnsupportedPublicTypeProducesAWarningWithoutInventingAFieldBinding()
        {
            var asset = Import("Main", Header + "export default class Main extends KimchilyScriptBehaviour { public collection = new Map<string, number>(); private hidden = new Map<string, number>(); }");
            Assert.IsTrue(asset.compiledSuccessfully);
            Assert.IsEmpty(asset.fields);
            Assert.AreEqual(1, asset.diagnostics.Length);
            StringAssert.Contains("collection", asset.diagnostics[0]);
        }
    }
}
