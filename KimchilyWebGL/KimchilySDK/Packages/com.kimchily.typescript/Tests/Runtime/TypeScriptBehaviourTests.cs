using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.TypeScript.Tests
{
    public sealed class TypeScriptBehaviourTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private const string Imports = "const {KimchilyScriptBehaviour}=require('Kimchily.Script'); const {Vector3,WaitForSeconds}=require('UnityEngine');";

        private KimchilyTypeScriptBehaviour Make(string members, TypeScriptField[] declarations = null, TypeScriptFieldBinding[] bindings = null)
        {
            var asset = ScriptableObject.CreateInstance<TypeScriptAsset>();
            asset.name = "TypeScriptTest";
            asset.compiledSuccessfully = true;
            asset.entryModule = "Assets/Test";
            asset.className = "Test";
            asset.fields = declarations ?? Array.Empty<TypeScriptField>();
            asset.modules = new[] { new TypeScriptModule { id = asset.entryModule, source = Imports + "exports.default=class Test extends KimchilyScriptBehaviour{" + members + "};" } };
            created.Add(asset);
            var go = new GameObject("TypeScript owner");
            go.SetActive(false);
            created.Add(go);
            var behaviour = go.AddComponent<KimchilyTypeScriptBehaviour>();
            behaviour.ScriptAsset = asset;
            behaviour.Fields = bindings ?? Array.Empty<TypeScriptFieldBinding>();
            return behaviour;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // Destroy behaviours first so their bounded cleanup can still read the asset.
            for (int i=created.Count-1;i>=0;i--) if (created[i] is GameObject) UnityEngine.Object.Destroy(created[i]);
            yield return null;
            foreach (var item in created) if (item != null) UnityEngine.Object.Destroy(item);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifecycleAndInspectorOverridesUseRealJavaScriptClass()
        {
            var behaviour = Make("constructor(){super();this.speed=2;this.label='default';} Awake(){this.gameObject.name=this.label;} OnEnable(){this.transform.localScale=new Vector3(2,2,2);} Start(){this.transform.position=new Vector3(this.speed,0,0);} Update(dt){this.transform.Translate(0,dt,0);}",
                new[]{new TypeScriptField{name="speed",kind="number"},new TypeScriptField{name="label",kind="string"}},
                new[]{new TypeScriptFieldBinding{name="speed",kind="number",useOverride=true,numberValue=7},new TypeScriptFieldBinding{name="label",kind="string",useOverride=false,stringValue="ignored"}});
            behaviour.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.IsTrue(behaviour.HasStarted);
            Assert.IsFalse(behaviour.IsFaulted, behaviour.LastError);
            Assert.AreEqual("default",behaviour.name);
            Assert.AreEqual(7,behaviour.transform.position.x);
            Assert.Greater(behaviour.transform.position.y,0);
            Assert.AreEqual(new Vector3(2,2,2),behaviour.transform.localScale);
        }

        [UnityTest]
        public IEnumerator GeneratorWaitResumesAndDisableRunsFinallyOnce()
        {
            var behaviour=Make("Start(){this.StartCoroutine(this.work());} *work(){try{this.transform.position=new Vector3(1,0,0);yield new WaitForSeconds(.02);this.transform.position=new Vector3(2,0,0);yield new WaitForSeconds(60);}finally{this.transform.position=new Vector3(3,0,0);}}");
            behaviour.gameObject.SetActive(true);
            yield return new WaitForSeconds(.08f);
            Assert.AreEqual(2,behaviour.transform.position.x);
            behaviour.enabled=false;
            Assert.AreEqual(3,behaviour.transform.position.x);
            yield return null;
            Assert.IsFalse(behaviour.IsFaulted,behaviour.LastError);
            Assert.AreEqual(0,behaviour.GetComponent<Kimchily.Creator.CoroutineScheduler>().ActiveCount);
        }

        [UnityTest]
        public IEnumerator ScriptCanDisableItselfWithoutReentrantInterpreterCall()
        {
            var behaviour=Make("Start(){this.StartCoroutine(this.work());} OnDisable(){this.transform.localScale=new Vector3(3,3,3);} *work(){try{this.gameObject.SetActive(false);yield null;}finally{this.transform.position=new Vector3(4,0,0);}}");
            behaviour.gameObject.SetActive(true);
            yield return null;
            Assert.IsFalse(behaviour.gameObject.activeSelf);
            Assert.IsFalse(behaviour.IsFaulted,behaviour.LastError);
            Assert.AreEqual(4,behaviour.transform.position.x);
            Assert.AreEqual(new Vector3(3,3,3),behaviour.transform.localScale);
        }

        [UnityTest]
        public IEnumerator AotProfileClassMapGeneratorAndRelativeModule()
        {
            var behaviour=Make("Start(){this.StartCoroutine(this.work());}*work(){const values=new Map([['x',require('./Helper').value]]);yield null;this.transform.position=new Vector3(values.get('x'),0,0);}");
            behaviour.ScriptAsset.modules=new[]{behaviour.ScriptAsset.modules[0],new TypeScriptModule{id="Assets/Helper",source="exports.value=13;"}};
            behaviour.gameObject.SetActive(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(behaviour.HasStarted);
            Assert.IsFalse(behaviour.IsFaulted,behaviour.LastError);
            Assert.AreEqual(13,behaviour.transform.position.x);
        }

        [UnityTest]
        public IEnumerator NamedSceneReferenceHasExplicitFacade()
        {
            var target=new GameObject("target");created.Add(target);
            var behaviour=Make("Start(){this.target.SetActive(false);}",new[]{new TypeScriptField{name="target",kind="GameObject"}},new[]{new TypeScriptFieldBinding{name="target",kind="GameObject",useOverride=true,gameObjectValue=target}});
            behaviour.gameObject.SetActive(true);
            yield return null;
            Assert.IsFalse(target.activeSelf);
            Assert.IsFalse(behaviour.IsFaulted,behaviour.LastError);
        }

        [UnityTest]
        public IEnumerator FailedStartIsReportedAndNextBehaviourStillRuns()
        {
            var bad=Make("Start(){throw new Error('intentional start fault');}");
            var good=Make("Start(){this.gameObject.name='healthy';}");
            LogAssert.Expect(LogType.Error,new Regex("\\[Kimchily TypeScript\\].*intentional start fault"));
            bad.gameObject.SetActive(true);good.gameObject.SetActive(true);
            yield return null;
            Assert.IsTrue(bad.HasStarted);Assert.IsTrue(bad.IsFaulted);Assert.IsTrue(good.HasStarted);Assert.IsFalse(good.IsFaulted);Assert.AreEqual("healthy",good.name);
        }

        [UnityTest]
        public IEnumerator SerializedUnlimitedBudgetCannotBypassLoopProtection()
        {
            var behaviour=Make("Start(){while(true){}}");
            JsonUtility.FromJsonOverwrite("{\"instructionBudget\":2147483647}",behaviour);
            Assert.AreEqual(100000,behaviour.InstructionBudget);
            LogAssert.Expect(LogType.Error,new Regex("\\[Kimchily TypeScript\\].*(budget|statement|time|Statements|Timeout)",RegexOptions.IgnoreCase));
            behaviour.gameObject.SetActive(true);
            yield return null;
            Assert.IsTrue(behaviour.IsFaulted);Assert.IsTrue(behaviour.HasStarted);
        }

        [UnityTest]
        public IEnumerator ApiMismatchFaultsBeforeAnyModuleExecutes()
        {
            var behaviour=Make("Start(){this.gameObject.name='should not run';}");
            behaviour.ScriptAsset.apiVersion=99;
            LogAssert.Expect(LogType.Error,new Regex("Unsupported TypeScript SDK API version"));
            behaviour.gameObject.SetActive(true);
            yield return null;
            Assert.IsTrue(behaviour.IsFaulted);Assert.AreNotEqual("should not run",behaviour.name);
        }

        [Test]
        public void ContentValidationRejectsStaleFieldsAndDuplicateModules()
        {
            var behaviour=Make("",bindings:new[]{new TypeScriptFieldBinding{name="removed",kind="number",useOverride=true}});
            var module=behaviour.ScriptAsset.modules[0];behaviour.ScriptAsset.modules=new[]{module,module};
            string[] errors=behaviour.ValidateContent().ToArray();
            Assert.IsTrue(errors.Any(e=>e.Contains("Duplicate TypeScript module")));
            Assert.IsTrue(errors.Any(e=>e.Contains("does not match")));
        }

        [Test]
        public void IntentionalNullReferenceOverrideIsValid()
        {
            var behaviour=Make("",new[]{new TypeScriptField{name="target",kind="GameObject"}},new[]{new TypeScriptFieldBinding{name="target",kind="GameObject",useOverride=true,gameObjectValue=null}});
            Assert.IsEmpty(behaviour.ValidateContent());
        }
    }
}
