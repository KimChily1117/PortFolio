using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kimchily.Creator.Editor;
using Kimchily.Creator.Mobile;
using Kimchily.TypeScript;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kimchily.Creator.Project
{
    /// <summary>
    /// 에디터에서 원본 메시·재질·프리팹을 제작하는 도구다. 플레이 중에는 실행하지 않는다.
    /// 게임 판정은 ServerScripts의 TS, 클라이언트 UI·연출은 PortalGarden.ts가 담당한다.
    /// 여기의 C#은 콘텐츠 제작을 위한 기반 기능이며 서버의 승리 조건과는 관계없다.
    /// </summary>
    public static class ChiliIslandArt
    {
        public const string Root = "Assets/Demos/ChiliIsland";
        public const string ScenePath = Root + "/Scenes/ChiliIsland.unity";
        static readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();
        static readonly List<GameObject> catalogue = new List<GameObject>();
        static readonly Dictionary<string, Mesh> builtMeshes = new Dictionary<string, Mesh>();
        static System.Random random = new System.Random(9129);
        static int triangles;

        [MenuItem("Kimchily/Demos/Generate Chili Island")]
        public static void Generate()
        {
            // Unity의 Mesh/AssetDatabase API로 저장 가능한 자산을 만든다.
            // 생성된 메시를 씬에서 직접 편집해도 되며, 게임 로직은 이 생성기를 호출하지 않는다.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (File.Exists(ScenePath)
                && !Application.isBatchMode
                && !EditorUtility.DisplayDialog(
                "Chili Island",
                "Regenerate the generated demo scene? Existing MyWorld is untouched.",
                "Regenerate",
                "Cancel"))
            {
                return;
            }

            foreach (string dir in new[]
            {
                "Meshes",
                "Materials",
                "Prefabs",
                "Scenes",
                "Models"
            })
            {
                Directory.CreateDirectory(Root + "/" + dir);
            }

            AssetDatabase.Refresh();
            palette.Clear();
            catalogue.Clear();
            builtMeshes.Clear();
            triangles = 0;
            random = new System.Random(9129);
            Mat("Cream", "#F4E8CA");
            Mat("Sand", "#D8BFA0");
            Mat("Rock", "#8D9B9A");
            Mat("RockLight", "#B6C6BD");
            Mat("Grass", "#9BCCAC");
            Mat("Rim", "#BDE1BC");
            Mat("Mint", "#72C6AE");
            Mat("MintLight", "#B8E4C1");
            Mat("Pine", "#3E857F");
            Mat("Trunk", "#AD8064");
            Mat("Teal", "#276D74");
            Mat("Deep", "#164954");
            Mat("Gold", "#E7B969");
            Mat("Coral", "#EEA68F");
            Mat("Lilac", "#B4A5D2");
            Mat("Butter", "#EFDA92");
            Mat("White", "#FAF6E8");
            Mat("Glow", "#BFFFF0", .7f);
            Mat("Pink", "#ECB7B3");
            Mat("Petal", "#F3E0C9");
            CreateShaderMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#E9ECE2");
            RenderSettings.ambientEquatorColor = Hex("#BBCBD2");
            RenderSettings.ambientGroundColor = Hex("#798C95");
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#C8DFDF");
            RenderSettings.fogStartDistance = 37;
            RenderSettings.fogEndDistance = 110;
            var sun = new GameObject("Warm afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(42, -32, 0);
            sun.color = Hex("#FFF1D5");
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .32f;
            sun.shadowBias = .025f;
            var fill = new GameObject("Soft sky fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(28, 140, 0);
            fill.color = Hex("#C6E8ED");
            fill.intensity = .35f;
            var camera = new GameObject("Diorama Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("#C8DFDF");
            camera.transform.position = new Vector3(21, 23, -27);
            camera.transform.LookAt(new Vector3(0, 0, 3));
            camera.fieldOfView = 40;
            camera.farClipPlane = 180;
            camera.gameObject.AddComponent<AudioListener>();
            QualitySettings.shadowDistance = 45;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            var island = Island();
            SavePrefab(island, "FloatingIsland");
            var portal = Portal();
            portal.transform.position = new Vector3(0, 0, 10);
            SavePrefab(portal, "GardenPortal");
            var pads = new GameObject[4];
            string[] kinds =
            {
                "Star",
                "Moon",
                "Sun",
                "Leaf"
            };
            Vector3[] places =
            {
                new Vector3(-3, 0, 2),
                new Vector3(3, 0, 2),
                new Vector3(-3, 0, 6),
                new Vector3(3, 0, 6)
            };

            for (int i = 0; i < 4; i++)
            {
                pads[i] = Pad(kinds[i], i);
                SavePrefab(pads[i], kinds[i] + "Pad");
                pads[i].transform.position = places[i];
            }

            var treeA = Tree("MintTree", "Mint");
            SavePrefab(treeA, "MintTree");
            treeA.transform.position = new Vector3(-7, 0, 6);
            var treeB = Tree("BlossomTree", "Pink");
            SavePrefab(treeB, "BlossomTree");
            treeB.transform.position = new Vector3(7, 0, 10);
            treeB.transform.localScale = Vector3.one * .85f;
            var shrub = Shrub();
            SavePrefab(shrub, "GardenShrub");
            shrub.transform.position = new Vector3(-6, 0, -4);
            var flower = Flower();
            SavePrefab(flower, "DaisyCluster");
            flower.transform.position = new Vector3(5, 0, -3);
            var stone = Pebbles();
            SavePrefab(stone, "PebbleCluster");
            stone.transform.position = new Vector3(7, 0, 4);
            var lamp = Lantern();
            SavePrefab(lamp, "GardenLantern");
            lamp.transform.position = new Vector3(-2.5f, 0, -4.5f);
            var mascot = Mascot();
            SavePrefab(mascot, "ChiliSprout");
            mascot.transform.position = new Vector3(-5, 0, -1.5f);
            mascot.transform.rotation = Quaternion.Euler(0, 30, 0);
            var sign = Sign();
            SavePrefab(sign, "WelcomeSign");
            sign.transform.position = new Vector3(3.6f, 0, -4.8f);
            sign.transform.rotation = Quaternion.Euler(0, -18, 0);
            Scatter(
                treeA,
                new[]
            {
                new Vector3(-8, 0, 0),
                new Vector3(-6, 0, 11),
                new Vector3(7.5f, 0, -1.5f),
                new Vector3(8, 0, 7),
                new Vector3(-8.8f, 0, 8)
            },
                .62f,
                .92f);
            Scatter(
                treeB,
                new[]
            {
                new Vector3(-7.3f, 0, -4.5f),
                new Vector3(6.8f, 0, -5.5f),
                new Vector3(5, 0, 13),
                new Vector3(-3.8f, 0, 13)
            },
                .6f,
                .82f);
            Scatter(
                shrub,
                new[]
            {
                new Vector3(-5, 0, 4),
                new Vector3(5, 0, 7.5f),
                new Vector3(-5, 0, 8.5f),
                new Vector3(4.6f, 0, -.5f),
                new Vector3(-3, 0, -6.5f)
            },
                .7f,
                1.2f);
            Scatter(
                flower,
                new[]
            {
                new Vector3(-5.6f, 0, 1.5f),
                new Vector3(5.7f, 0, 2),
                new Vector3(-5, 0, 11),
                new Vector3(3.7f, 0, 12),
                new Vector3(2, 0, -5.4f),
                new Vector3(-7, 0, 3)
            },
                .7f,
                1.1f);
            Scatter(
                stone,
                new[]
            {
                new Vector3(-8, 0, 3),
                new Vector3(7.8f, 0, -4),
                new Vector3(-5, 0, -6),
                new Vector3(6, 0, 12.7f)
            },
                .6f,
                1.1f);
            Scatter(
                lamp,
                new[]
            {
                new Vector3(2.5f, 0, -4.5f),
                new Vector3(-5, 0, 7.2f),
                new Vector3(5, 0, 7.2f),
                new Vector3(-3.2f, 0, 10)
            },
                .8f,
                1);
            Path();
            Clouds();
            // 두 번째 정원은 같은 월드 안의 별도 구역이다. 이동은 기존 캐릭터 컨트롤러를 그대로 사용한다.
            var relayPads = RelayGarden(island, pads, treeA, treeB, flower, lamp, mascot, out var bridgeGlow, out var relayReward);
            var spawn = new GameObject("Arrival");
            spawn.transform.position = new Vector3(0, .12f, -5.5f);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/World/PublishedModels/unitychan_dynamic_Publishable.prefab");
            var player = KimchilyMobilePlayerBootstrap.CreateForScene(scene, model);
            player.transform.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);
            player.SetSpawnPosition(spawn.transform.position);
            player.AnimationProfile = AssetDatabase.LoadAssetAtPath<KimchilyPlayerAnimationProfile>("Assets/World/PlayerAnimations.asset");
            var script = AssetDatabase.LoadAssetAtPath<TypeScriptAsset>(Root + "/Scripts/PortalGarden.ts");

            if (script == null || !script.compiledSuccessfully)
            {
                throw new InvalidOperationException("PortalGarden.ts must compile before generating scene.");
            }

            var director = new GameObject("Portal Garden · TypeScript").AddComponent<KimchilyTypeScriptBehaviour>();
            director.ScriptAsset = script;
            var bindings = new List<TypeScriptFieldBinding>();

            for (int i = 0; i < 4; i++)
            {
                bindings.Add(Binding("pad" + i, pads[i].transform.Find("Occupied").gameObject));
                bindings.Add(Binding("idle" + i, pads[i].transform.Find("Idle").gameObject));
            }

            bindings.Add(Binding("portalOpen", portal.transform.Find("Awake").gameObject));
            bindings.Add(Binding("portalClosed", portal.transform.Find("Sleeping").gameObject));
            bindings.Add(Binding("halo", portal.transform.Find("Halo").gameObject));

            for (int i = 0; i < 4; i++)
            {
                bindings.Add(Binding("relay" + i, relayPads[i].transform.Find("Occupied").gameObject));
                bindings.Add(Binding("relayIdle" + i, relayPads[i].transform.Find("Idle").gameObject));
            }

            bindings.Add(Binding("bridgeGlow", bridgeGlow));
            bindings.Add(Binding("relayReward", relayReward));
            director.Fields = bindings.ToArray();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText(
                "Artifacts/chili-island-art.json",
                "{\"meshTriangles\":" + triangles + ",\"prefabs\":" + catalogue.Count + ",\"scene\":\"" + ScenePath + "\",\"source\":\"original procedural geometry\"}");
            Render(camera, "Artifacts/chili-island-overview.png", 1440, 1080);
            camera.transform.position = new Vector3(22, 10, -28);
            camera.transform.LookAt(new Vector3(0, -.6f, 3));
            camera.fieldOfView = 43;
            Render(camera, "Artifacts/chili-island-profile.png", 1440, 1000);
            camera.transform.position = new Vector3(11, 9, -13);
            camera.transform.LookAt(new Vector3(0, 1.6f, 5));
            camera.fieldOfView = 48;
            Render(camera, "Artifacts/chili-island-ground.png", 1440, 900);
            var hero = mascot.transform.position;
            camera.transform.position = hero + new Vector3(2.3f, 1.7f, -3.7f);
            camera.transform.LookAt(hero + Vector3.up * .8f);
            camera.fieldOfView = 34;
            Render(camera, "Artifacts/chili-sprout-model.png", 1000, 1000);
            ExportObj(catalogue);
            AssetDatabase.Refresh();
            AssetDatabase.ExportPackage(
                new[]
            {
                Root + "/Meshes",
                Root + "/Materials",
                Root + "/Prefabs",
                Root + "/Shaders",
                Root + "/Models"
            },
                "Artifacts/ChiliIsland-OriginalAssets.unitypackage",
                ExportPackageOptions.Recurse);
            Debug.Log("KIMCHILY_CHILI_ART_READY triangles=" + triangles + " prefabs=" + catalogue.Count);
        }

        static TypeScriptFieldBinding Binding(string name, GameObject value)
        {
            return new TypeScriptFieldBinding
            {
                name = name,
                kind = "GameObject",
                useOverride = true,
                gameObjectValue = value
            };
        }

        static GameObject[] RelayGarden(
            GameObject island,
            GameObject[] sourcePads,
            GameObject treeA,
            GameObject treeB,
            GameObject flower,
            GameObject lamp,
            GameObject mascot,
            out GameObject bridgeGlow,
            out GameObject reward)
        {
            // 새 판정 코드는 생성기에 넣지 않는다. 아래 좌표는 PortalRules.ts의 relayPads와 맞춘 레벨 배치다.
            var garden = UnityEngine.Object.Instantiate(island);
            garden.name = "Second garden · Lantern relay";
            garden.transform.position = new Vector3(0, 0, 32);
            garden.transform.Find("Soft grass floor").GetComponent<MeshRenderer>().sharedMaterial = palette["Lilac"];
            var pads = new GameObject[4];

            for (int i = 0; i < pads.Length; i++)
            {
                pads[i] = UnityEngine.Object.Instantiate(sourcePads[i]);
                pads[i].name = "Relay " + (i + 1) + " · " + sourcePads[i].name;
                pads[i].transform.position = sourcePads[i].transform.position + new Vector3(0, 0, 32);
                // 표식 옆의 작은 등불도 기존 원본 메시를 공유한다.
                var beacon = UnityEngine.Object.Instantiate(lamp);
                beacon.transform.position = pads[i].transform.position + new Vector3(i % 2 == 0 ? -1.65f : 1.65f, 0, 0);
            }

            Scatter(treeA, new[] { new Vector3(-7, 0, 30), new Vector3(7, 0, 36), new Vector3(-6, 0, 43) }, .65f, .9f);
            Scatter(treeB, new[] { new Vector3(7, 0, 29), new Vector3(-7, 0, 37), new Vector3(5, 0, 44) }, .65f, .9f);
            Scatter(
                flower,
                new[]
            {
                new Vector3(-5, 0, 32),
                new Vector3(5, 0, 33),
                new Vector3(-5, 0, 40),
                new Vector3(4, 0, 42)
            },
                .8f,
                1.1f);
            var path = Node("Relay garden path");

            for (int i = 0; i < 15; i++)
            {
                Disc(
                    "Relay paver",
                    "Cream",
                    path.transform,
                    new Vector3(Mathf.Sin(i) * .16f, .019f, 27 + i * 1.05f),
                    new Vector3(.65f, .07f, .43f));
            }

            var bridge = Node("Portal crossing · permanent return path");
            // 렌더링과 별개인 연속 바닥으로 발판 사이의 작은 틈에 발이 빠지는 일을 막는다.
            BoxCollider(bridge, new Vector3(3.5f, .25f, 14), new Vector3(0, -.085f, 20));

            for (int side = -1; side <= 1; side += 2)
            {
                BoxCollider(bridge, new Vector3(.16f, .85f, 14), new Vector3(side * 1.8f, .425f, 20));

                for (int i = 0; i < 8; i++)
                {
                    Disc(
                        "Bridge post",
                        "Gold",
                        bridge.transform,
                        new Vector3(side * 1.78f, .37f, 13 + i * 2),
                        new Vector3(.09f, .74f, .09f));
                    Orb("Bridge lantern", "Glow", bridge.transform, new Vector3(side * 1.78f, .83f, 13 + i * 2), Vector3.one * .13f);
                }

                Orb("Bridge handrail", "Cream", bridge.transform, new Vector3(side * 1.78f, .78f, 20), new Vector3(.055f, .055f, 7.05f));
            }

            for (int i = 0; i < 18; i++)
            {
                Disc(
                    "Bridge step",
                    i % 2 == 0 ? "Cream" : "Sand",
                    bridge.transform,
                    new Vector3(0, -.06f, 13.2f + i * .8f),
                    new Vector3(1.75f, .2f, .48f));
            }

            bridgeGlow = Node("Portal route lights");

            for (int i = 0; i < 18; i++)
            {
                Orb(
                    "Guiding firefly",
                    "Glow",
                    bridgeGlow.transform,
                    new Vector3(Mathf.Sin(i * 1.6f) * .4f, .35f, 10.5f + i * 1.18f),
                    Vector3.one * .09f);
            }

            bridgeGlow.SetActive(false);
            var monument = UnityEngine.Object.Instantiate(mascot);
            monument.name = "Relay finish · sprout monument";
            monument.transform.position = new Vector3(0, 0, 43);
            monument.transform.rotation = Quaternion.identity;
            monument.transform.localScale = Vector3.one * 1.8f;
            var dais = Disc("Victory terrace", "Cream", null, new Vector3(0, -.03f, 43), new Vector3(2.6f, .15f, 2.1f));
            reward = Node("Relay completion celebration");
            reward.transform.position = new Vector3(0, 2, 43);

            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2 / 12;
                Orb(
                    "Victory light",
                    i % 2 == 0 ? "Gold" : "Glow",
                    reward.transform,
                    new Vector3(Mathf.Cos(angle) * 2.4f, .5f + Mathf.Sin(angle) * 1.8f, 0),
                    Vector3.one * .16f);
            }

            reward.SetActive(false);
            return pads;
        }

        public static void BuildWorld()
        {
            // 메시와 컴파일된 TS 자산을 WebGL용 콘텐츠로 묶는다. 실행기 전체 재빌드와는 별도다.
            if (!File.Exists(ScenePath))
            {
                throw new InvalidOperationException("Generate the art scene first.");
            }

            var result = WorldContentBuilder.Build(new WorldBuildRequest
            {
                worldId = "chili-island",
                entryScene = ScenePath,
                scenes = new[] { ScenePath },
                target = BuildTarget.WebGL,
                requirePortableScripts = true,
                outputRoot = System.IO.Path.GetFullPath("WorldBuilds")
            });
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/last-build.txt", result.Directory);
            Debug.Log("KIMCHILY_CHILI_WORLD_READY " + result.Directory);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static void Mat(string name, string hex, float emission = 0)
        {
            var shader = Shader.Find("Kimchily/Soft Toy");

            if (shader == null)
            {
                throw new InvalidOperationException("Soft Toy shader missing.");
            }

            var m = new Material(shader)
            {
                name = name,
                color = Hex(hex)
            };
            m.SetFloat("_Glow", emission);
            palette[name] = Save(m, Root + "/Materials/" + name + ".mat");
        }

        static void CreateShaderMaterials()
        {
            var shader = Shader.Find("Kimchily/Chili Portal");

            if (shader == null)
            {
                throw new InvalidOperationException("Portal shader missing.");
            }

            var m = new Material(shader)
            {
                name = "PortalLight"
            };
            m.SetColor("_Tint", Hex("#8EE8D4"));
            palette["PortalLight"] = Save(m, Root + "/Materials/PortalLight.mat");
        }

        static T Save<T>(T value, string path)
            where T : UnityEngine.Object
        {
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);

            if (previous != null)
            {
                EditorUtility.CopySerialized(value, previous);
                UnityEngine.Object.DestroyImmediate(value);
                EditorUtility.SetDirty(previous);
                return previous;
            }

            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        static GameObject Node(string name, Transform parent = null)
        {
            var go = new GameObject(name);

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        static GameObject Piece(
            string name,
            Mesh mesh,
            string material,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation = default)
        {
            var go = Node(name, parent);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = palette[material];
            return go;
        }

        static GameObject Orb(string name, string mat, Transform parent, Vector3 pos, Vector3 scale)
        {
            return Piece(name, MeshBank.Orb, mat, parent, pos, scale);
        }

        static GameObject Disc(string name, string mat, Transform parent, Vector3 pos, Vector3 scale)
        {
            return Piece(name, MeshBank.Disc, mat, parent, pos, scale);
        }

        static void BoxCollider(GameObject go, Vector3 size, Vector3 center)
        {
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            c.center = center;
        }

        static void SavePrefab(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + name + ".prefab");
            catalogue.Add(go);
        }

        static void Scatter(GameObject source, Vector3[] positions, float min, float max)
        {
            foreach (var p in positions)
            {
                var go = UnityEngine.Object.Instantiate(source);
                go.name = source.name;
                go.transform.position = p;
                go.transform.localScale = Vector3.one * Mathf.Lerp(min, max, (float)random.NextDouble());
                go.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
            }
        }

        static GameObject Island()
        {
            var root = Node("Floating garden island");
            var bands = new[]
            {
                (-4.6f, .76f),
                (-2.8f, .94f),
                (-1.25f, 1f),
                (-.38f, 1.015f),
                (-.1f, 1.01f),
                (0f, .98f)
            };
            string[] colors =
            {
                "Rock",
                "RockLight",
                "Sand",
                "Cream",
                "Rim"
            };

            for (int layer = 0; layer < bands.Length - 1; layer++)
            {
                var mesh = new Geo();
                int n = 40;

                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2 / n, b = (i + 1) * Mathf.PI * 2 / n;

                    Vector3 P(float t, (float y, float r) band)
                    {
                        float rough = 1 + .035f * Mathf.Sin(t * 7) + .02f * Mathf.Sin(t * 11);
                        return new Vector3(Mathf.Cos(t) * 11 * band.r * rough, band.y, 3 + Mathf.Sin(t) * 14 * band.r * rough);
                    }

                    mesh.Quad(P(a, bands[layer]), P(a, bands[layer + 1]), P(b, bands[layer + 1]), P(b, bands[layer]));
                }

                Piece("Carved cliff " + layer, mesh.Save("IslandBand" + layer), colors[layer], root.transform, Vector3.zero, Vector3.one);
            }

            var top = new Geo();

            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;

                Vector3 P(float t)
                {
                    float r = .98f * (1 + .035f * Mathf.Sin(t * 7) + .02f * Mathf.Sin(t * 11));
                    return new Vector3(Mathf.Cos(t) * 11 * r, 0, 3 + Mathf.Sin(t) * 14 * r);
                }

                top.Tri(new Vector3(0, 0, 3), P(b), P(a));
            }

            var floor = Piece("Soft grass floor", top.Save("IslandGrass"), "Grass", root.transform, Vector3.zero, Vector3.one);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            var bottom = new Geo();

            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;

                Vector3 P(float t)
                {
                    float r = .76f * (1 + .035f * Mathf.Sin(t * 7) + .02f * Mathf.Sin(t * 11));
                    return new Vector3(Mathf.Cos(t) * 11 * r, -4.6f, 3 + Mathf.Sin(t) * 14 * r);
                }

                bottom.Tri(new Vector3(0, -4.6f, 3), P(a), P(b));
            }

            Piece("Underside stone", bottom.Save("IslandUnderside"), "Rock", root.transform, Vector3.zero, Vector3.one);
            return root;
        }

        static GameObject Portal()
        {
            var root = Node("The sleeping garden portal");
            Disc("Stone terrace", "Cream", root.transform, new Vector3(0, -.02f, 0), new Vector3(3.2f, .18f, 2.3f));
            Disc("Inset terrace", "Sand", root.transform, new Vector3(0, .05f, -.02f), new Vector3(2.8f, .13f, 1.9f));
            var arch = TubeEllipse(1.96f, 2.36f, .34f, "PortalArch");
            Piece("Porcelain arch", arch, "Cream", root.transform, new Vector3(0, 2.6f, .04f), Vector3.one);
            Piece(
                "Teal inner bevel",
                TubeEllipse(1.88f, 2.26f, .19f, "PortalTrim"),
                "Teal",
                root.transform,
                new Vector3(0, 2.6f, -.22f),
                Vector3.one);
            Piece(
                "Golden rim",
                TubeEllipse(1.72f, 2.13f, .055f, "PortalGold"),
                "Gold",
                root.transform,
                new Vector3(0, 2.6f, -.34f),
                Vector3.one);

            for (int side = -1; side <= 1; side += 2)
            {
                Disc("Carved foot", "Cream", root.transform, new Vector3(side * 1.7f, .28f, 0), new Vector3(.62f, .5f, .66f));
                Disc("Gold foot cuff", "Gold", root.transform, new Vector3(side * 1.7f, .47f, 0), new Vector3(.5f, .13f, .54f));
                Orb(
                    "Leaf fin",
                    "Mint",
                    root.transform,
                    new Vector3(side * 1.87f, 4.45f, 0),
                    new Vector3(.35f, .7f, .19f)).transform.localRotation = Quaternion.Euler(0, 0, side * -35);
            }

            var halo = Node("Halo", root.transform);
            halo.transform.localPosition = new Vector3(0, 5.12f, 0);
            Piece("Sun crest", Symbol("Sun"), "Gold", halo.transform, Vector3.zero, new Vector3(.48f, .48f, .48f), new Vector3(90, 0, 0));
            Orb("Jade heart", "Mint", halo.transform, new Vector3(0, 0, -.10f), new Vector3(.22f, .22f, .16f));
            var sleeping = Node("Sleeping", root.transform);
            Piece(
                "Quiet glass",
                EllipseFace("PortalFace"),
                "Deep",
                sleeping.transform,
                new Vector3(0, 2.6f, .025f),
                new Vector3(1.67f, 2.08f, 1));

            for (int i = 0; i < 3; i++)
            {
                Disc(
                    "Sleeping stars",
                    "Gold",
                    sleeping.transform,
                    new Vector3((i - 1) * .52f, 2.6f, -.06f),
                    new Vector3(.095f, .025f, .095f),
                    new Vector3(90, 0, 0));
            }

            var awake = Node("Awake", root.transform);
            Piece(
                "Living portal",
                EllipseFace("PortalFace"),
                "PortalLight",
                awake.transform,
                new Vector3(0, 2.6f, -.07f),
                new Vector3(1.67f, 2.08f, 1));

            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9;
                Orb(
                    "Firefly " + i,
                    "Glow",
                    awake.transform,
                    new Vector3(Mathf.Cos(angle) * 2.38f, 2.65f + Mathf.Sin(angle) * 2.65f, -.2f),
                    Vector3.one * .085f);
            }

            awake.SetActive(false);
            BoxCollider(root, new Vector3(.65f, 4, .65f), new Vector3(-1.92f, 2, 0));
            BoxCollider(root, new Vector3(.65f, 4, .65f), new Vector3(1.92f, 2, 0));
            return root;
        }

        // An overload keeps horizontal disc geometry reusable for decorative inlays.
        static GameObject Disc(string name, string mat, Transform parent, Vector3 pos, Vector3 scale, Vector3 rotation)
        {
            return Piece(name, MeshBank.Disc, mat, parent, pos, scale, rotation);
        }

        static GameObject Pad(string symbol, int index)
        {
            string[] colors =
            {
                "Butter",
                "Lilac",
                "Coral",
                "Mint"
            };
            var root = Node(symbol + " cooperation pedestal");
            Disc("Carved base", "Cream", root.transform, new Vector3(0, .04f, 0), new Vector3(1.28f, .16f, 1.28f));
            Disc("Inset brass", "Gold", root.transform, new Vector3(0, .13f, 0), new Vector3(1.11f, .09f, 1.11f));
            Disc("Colored ceramic", colors[index], root.transform, new Vector3(0, .17f, 0), new Vector3(1.03f, .08f, 1.03f));
            Piece("Raised " + symbol, Symbol(symbol), "Cream", root.transform, new Vector3(0, .216f, 0), new Vector3(.56f, .56f, .56f));
            var idle = Node("Idle", root.transform);

            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Orb(
                    "Brass rivet",
                    "Gold",
                    idle.transform,
                    new Vector3(Mathf.Cos(a) * 1.18f, .14f, Mathf.Sin(a) * 1.18f),
                    Vector3.one * .055f);
            }

            var active = Node("Occupied", root.transform);
            Piece(
                "Light ring",
                TubeEllipse(1.24f, 1.24f, .055f, "PadLightRing"),
                "Glow",
                active.transform,
                new Vector3(0, .26f, 0),
                Vector3.one,
                new Vector3(90, 0, 0));

            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2;
                Orb(
                    "Light mote",
                    "Glow",
                    active.transform,
                    new Vector3(Mathf.Cos(a) * 1.14f, .48f, Mathf.Sin(a) * 1.14f),
                    Vector3.one * .065f);
            }

            active.SetActive(false);
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = ScaledMesh(MeshBank.Disc, new Vector3(1.2f, .2f, 1.2f), new Vector3(0, .1f, 0), "PadCollider");
            return root;
        }

        static GameObject Tree(string name, string leaf)
        {
            var root = Node(name);
            Disc("Flared roots", "Trunk", root.transform, new Vector3(0, .2f, 0), new Vector3(.48f, .42f, .48f));
            Piece(
                "Curved trunk",
                TubePath(
                new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(.12f, 1, 0),
                new Vector3(-.07f, 2, 0),
                new Vector3(.08f, 2.8f, 0)
            },
                .21f,
                "TreeTrunk"),
                "Trunk",
                root.transform,
                Vector3.zero,
                Vector3.one);
            Orb("Main crown", leaf, root.transform, new Vector3(0, 3, 0), new Vector3(1.25f, 1.6f, 1.2f));
            Orb("Crown left", leaf, root.transform, new Vector3(-.74f, 2.7f, -.05f), new Vector3(.88f, 1.07f, .88f));
            Orb("Crown right", leaf, root.transform, new Vector3(.75f, 3.12f, .02f), new Vector3(.95f, 1.22f, .9f));
            Orb(
                "Crown highlight",
                leaf == "Pink" ? "Petal" : "MintLight",
                root.transform,
                new Vector3(-.22f, 3.9f, -.15f),
                new Vector3(.76f, .65f, .78f));
            var c = root.AddComponent<CapsuleCollider>();
            c.radius = .25f;
            c.height = 2.5f;
            c.center = new Vector3(0, 1.25f, 0);
            return root;
        }

        static GameObject Shrub()
        {
            var root = Node("Rounded garden foliage");

            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.4f;
                var leaf = Orb(
                    "Wax leaf",
                    i % 2 == 0 ? "Mint" : "Pine",
                    root.transform,
                    new Vector3(Mathf.Cos(a) * .36f, .38f, Mathf.Sin(a) * .36f),
                    new Vector3(.2f, .65f, .28f));
                leaf.transform.rotation = Quaternion.Euler(25 * Mathf.Sin(a), a * Mathf.Rad2Deg, -25 * Mathf.Cos(a));
            }

            Orb("Little berry", "Coral", root.transform, new Vector3(.04f, .5f, -.45f), Vector3.one * .17f);
            return root;
        }

        static GameObject Flower()
        {
            var root = Node("Porcelain daisies");

            for (int j = 0; j < 3; j++)
            {
                var flower = Node("Daisy " + j, root.transform);
                flower.transform.localPosition = new Vector3((j - 1) * .32f, 0, j % 2 * .2f);
                float h = .45f + j * .16f;
                Disc("Stem", "Pine", flower.transform, new Vector3(0, h / 2, 0), new Vector3(.035f, h, .035f));

                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 2 / 5;
                    var p = Orb(
                        "Petal",
                        "Petal",
                        flower.transform,
                        new Vector3(Mathf.Cos(a) * .17f, h, Mathf.Sin(a) * .17f),
                        new Vector3(.13f, .065f, .21f));
                    p.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0);
                }

                Orb("Pollen", "Butter", flower.transform, new Vector3(0, h + .035f, 0), new Vector3(.10f, .07f, .10f));
            }

            return root;
        }

        static GameObject Pebbles()
        {
            var root = Node("Soft cliff pebbles");
            Orb("Large stone", "RockLight", root.transform, new Vector3(0, .22f, 0), new Vector3(.55f, .42f, .55f));
            Orb("Small stone", "Cream", root.transform, new Vector3(.53f, .12f, -.2f), new Vector3(.32f, .23f, .3f));
            Orb("Moss", "MintLight", root.transform, new Vector3(-.08f, .5f, -.04f), new Vector3(.3f, .10f, .29f));
            return root;
        }

        static GameObject Lantern()
        {
            var root = Node("Acorn garden lantern");
            Disc("Foot", "Cream", root.transform, new Vector3(0, .09f, 0), new Vector3(.28f, .18f, .28f));
            Disc("Post", "Teal", root.transform, new Vector3(0, .48f, 0), new Vector3(.075f, .8f, .075f));
            Orb("Honey light", "Butter", root.transform, new Vector3(0, .95f, 0), new Vector3(.23f, .32f, .23f));
            Disc("Cap", "Teal", root.transform, new Vector3(0, 1.2f, 0), new Vector3(.31f, .18f, .31f));
            return root;
        }

        static GameObject Mascot()
        {
            var root = Node("Chili the garden sprout");
            Orb("Soft body", "Cream", root.transform, new Vector3(0, .69f, 0), new Vector3(.56f, .7f, .42f));
            Orb("Left foot", "Teal", root.transform, new Vector3(-.23f, .10f, -.05f), new Vector3(.2f, .13f, .25f));
            Orb("Right foot", "Teal", root.transform, new Vector3(.23f, .10f, -.05f), new Vector3(.2f, .13f, .25f));

            for (int s = -1; s <= 1; s += 2)
            {
                Orb("Eye", "Deep", root.transform, new Vector3(s * .17f, .87f, -.40f), new Vector3(.045f, .065f, .025f));
                Orb("Cheek", "Coral", root.transform, new Vector3(s * .32f, .72f, -.37f), new Vector3(.09f, .04f, .022f));
                var leaf = Orb("Sprout leaf", "Mint", root.transform, new Vector3(s * .22f, 1.54f, 0), new Vector3(.18f, .38f, .09f));
                leaf.transform.localRotation = Quaternion.Euler(0, 0, s * -40);
                Orb("Little arm", "Cream", root.transform, new Vector3(s * .55f, .68f, 0), new Vector3(.15f, .25f, .18f));
            }

            Piece(
                "Smile",
                TubePath(
                new[]
            {
                new Vector3(-.075f, .73f, -.425f),
                new Vector3(0, .69f, -.435f),
                new Vector3(.075f, .73f, -.425f)
            },
                .018f,
                "SproutSmile"),
                "Deep",
                root.transform,
                Vector3.zero,
                Vector3.one);
            BoxCollider(root, new Vector3(.9f, 1.3f, .75f), new Vector3(0, .65f, 0));
            return root;
        }

        static GameObject Sign()
        {
            var root = Node("Welcome garden sign");
            Disc("Stem", "Trunk", root.transform, new Vector3(0, .62f, 0), new Vector3(.11f, 1.2f, .11f));
            Orb("Cream plaque", "Cream", root.transform, new Vector3(0, 1.22f, 0), new Vector3(.84f, .53f, .13f));
            Piece(
                "Leaf emblem",
                Symbol("Leaf"),
                "Mint",
                root.transform,
                new Vector3(-.25f, 1.28f, -.14f),
                new Vector3(.2f, .2f, .2f),
                new Vector3(90, 0, -30));
            // Three raised stepping dots read as a path without language-dependent textures.
            for (int i = 0; i < 3; i++)
            {
                Orb("Path mark", "Gold", root.transform, new Vector3(.05f + i * .18f, 1.2f, -.13f), new Vector3(.048f, .048f, .03f));
            }

            return root;
        }

        static void Path()
        {
            var root = Node("Meandering stepping stones");

            for (int i = 0; i < 15; i++)
            {
                float z = -7 + i * 1.05f;
                var stone = Disc(
                    "Paver " + i,
                    i % 3 == 0 ? "Cream" : "Petal",
                    root.transform,
                    new Vector3(Mathf.Sin(i * 1.4f) * .2f, .019f, z),
                    new Vector3(.64f, .07f, .43f));
                stone.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(i) * 13, 0);
            }

            for (int row = 0; row < 2; row++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int step = 1; step < 3; step++)
                    {
                        Disc(
                            "Side paver",
                            "Petal",
                            root.transform,
                            new Vector3(side * step * .87f, .017f, 2 + row * 4),
                            new Vector3(.36f, .06f, .31f));
                    }
                }
            }
        }

        static void Clouds()
        {
            var root = Node("Cloud archipelago");
            Vector3[] points =
            {
                new Vector3(-19, -1, 12),
                new Vector3(20, 1, 20),
                new Vector3(-17, 4, 35),
                new Vector3(12, 5, 38),
                new Vector3(-23, 2, -8)
            };

            foreach (var p in points)
            {
                var cloud = Node("Marshmallow cloud", root.transform);
                cloud.transform.position = p;

                for (int i = 0; i < 4; i++)
                {
                    Orb(
                        "Cloud puff",
                        "White",
                        cloud.transform,
                        new Vector3((i - 1.5f) * 1.6f, Mathf.Sin(i) * .3f, 0),
                        new Vector3(2, 1.05f, 1.5f));
                }
            }
        }

        static Mesh TubeEllipse(float rx, float ry, float radius, string name)
        {
            var geo = new Geo();
            const int n = 64, sides = 8;

            Vector3 P(int i, int j)
            {
                float a = i * Mathf.PI * 2 / n, b = j * Mathf.PI * 2 / sides;
                return new Vector3(
                    Mathf.Cos(a) * (rx + Mathf.Cos(b) * radius),
                    Mathf.Sin(a) * (ry + Mathf.Cos(b) * radius),
                    Mathf.Sin(b) * radius);
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    geo.Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                }
            }

            return geo.Save(name);
        }

        static Mesh TubePath(Vector3[] path, float radius, string name)
        {
            var g = new Geo();
            const int n = 8;

            Vector3 P(int i, int j)
            {
                float a = j * Mathf.PI * 2 / n;
                return path[i] + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            }

            for (int i = 0; i < path.Length - 1; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    g.Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                }
            }

            return g.Save(name);
        }

        static Mesh EllipseFace(string name)
        {
            var g = new Geo();

            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                g.Tri(Vector3.zero, new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0));
            }

            return g.Save(name);
        }

        static Mesh ScaledMesh(Mesh source, Vector3 scale, Vector3 offset, string name)
        {
            var copy = UnityEngine.Object.Instantiate(source);
            copy.vertices = copy.vertices.Select(v => Vector3.Scale(v, scale) + offset).ToArray();
            copy.RecalculateBounds();
            return Save(copy, Root + "/Meshes/" + name + ".asset");
        }

        static Mesh Symbol(string name)
        {
            var p = new List<Vector2>();

            if (name == "Moon")
            {
                for (int i = 0; i <= 20; i++)
                {
                    float a = Mathf.Lerp(60, 300, i / 20f) * Mathf.Deg2Rad;
                    p.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }

                for (int i = 1; i < 20; i++)
                {
                    float y = Mathf.Lerp(-.866f, .866f, i / 20f);
                    p.Add(new Vector2(-.22f + .72f * Mathf.Pow(y / .866f, 2), y));
                }
            }
            else if (name == "Leaf")
            {
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2 / 24;
                    float t = (1 - Mathf.Cos(a)) / 2;
                    p.Add(new Vector2(Mathf.Sin(a) * (.55f + .25f * t), Mathf.Cos(a)));
                }
            }
            else
            {
                int points = name == "Star" ? 10 : 24;

                for (int i = 0; i < points; i++)
                {
                    float a = i * Mathf.PI * 2 / points + Mathf.PI / 2, r = i % 2 == 0 ? 1 : (name == "Star" ? .46f : .76f);
                    p.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
            }

            // Ear clipping supports concave raised star and crescent silhouettes.
            var g = new Geo();
            var indices = Enumerable.Range(0, p.Count).ToList();

            float Cross(Vector2 a, Vector2 b, Vector2 c)
            {
                return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            }

            float area = 0;

            for (int i = 0; i < p.Count; i++)
            {
                area += p[i].x * p[(i + 1) % p.Count].y - p[(i + 1) % p.Count].x * p[i].y;
            }

            if (area < 0)
            {
                p.Reverse();
            }

            Vector3 V(int i, float h)
            {
                return new Vector3(p[i].x, h, p[i].y);
            }

            int safety = 0;

            while (indices.Count > 2 && safety++ < 2000)
            {
                bool found = false;

                for (int k = 0; k < indices.Count; k++)
                {
                    int a = indices[(k + indices.Count - 1) % indices.Count], b = indices[k], c = indices[(k + 1) % indices.Count];

                    if (Cross(p[a], p[b], p[c]) <= .000001f)
                    {
                        continue;
                    }

                    bool inside = false;

                    foreach (int q in indices)
                    {
                        if (q != a
                            && q != b
                            && q != c
                            && Cross(p[a], p[b], p[q]) >= 0
                            && Cross(p[b], p[c], p[q]) >= 0
                            && Cross(p[c], p[a], p[q]) >= 0)
                        {
                            inside = true;
                            break;
                        }
                    }

                    if (inside)
                    {
                        continue;
                    }

                    g.Tri(V(a, .04f), V(c, .04f), V(b, .04f));
                    g.Tri(V(a, 0), V(b, 0), V(c, 0));
                    indices.RemoveAt(k);
                    found = true;
                    break;
                }

                if (!found)
                {
                    throw new InvalidOperationException("Symbol triangulation failed: " + name);
                }
            }

            for (int i = 0; i < p.Count; i++)
            {
                int j = (i + 1) % p.Count;
                g.Quad(V(i, 0), V(i, .04f), V(j, .04f), V(j, 0));
            }

            return g.Save(name + "Symbol");
        }

        static class MeshBank
        {
            public static Mesh Orb
            {
                get
                {
                    var g = new Geo();
                    const int rings = 10, n = 14;

                    Vector3 P(int r, int s)
                    {
                        float a = s * Mathf.PI * 2 / n, b = r * Mathf.PI / rings;
                        float swell = 1 + .035f * Mathf.Sin(a * 3) * Mathf.Sin(b);
                        return new Vector3(Mathf.Sin(b) * Mathf.Cos(a) * swell, Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a) * swell);
                    }

                    for (int r = 0; r < rings; r++)
                    {
                        for (int s = 0; s < n; s++)
                        {
                            if (r == 0)
                            {
                                g.Tri(P(0, 0), P(1, s + 1), P(1, s));
                            }
                            else if (r == rings - 1)
                            {
                                g.Tri(P(r, s), P(r, s + 1), P(rings, 0));
                            }
                            else
                            {
                                g.Quad(P(r, s), P(r, s + 1), P(r + 1, s + 1), P(r + 1, s));
                            }
                        }
                    }

                    return g.Save("SoftFacetedOrb");
                }
            }

            public static Mesh Disc
            {
                get
                {
                    var g = new Geo();
                    int n = 24;
                    Vector2[] profile =
                    {
                        new Vector2(.85f, -.5f),
                        new Vector2(1, -.3f),
                        new Vector2(1, .3f),
                        new Vector2(.85f, .5f)
                    };

                    Vector3 P(int r, int s)
                    {
                        float a = s * Mathf.PI * 2 / n;
                        return new Vector3(Mathf.Cos(a) * profile[r].x, profile[r].y, Mathf.Sin(a) * profile[r].x);
                    }

                    for (int s = 0; s < n; s++)
                    {
                        g.Tri(new Vector3(0, .5f, 0), P(3, s + 1), P(3, s));
                        g.Tri(new Vector3(0, -.5f, 0), P(0, s), P(0, s + 1));

                        for (int r = 0; r < 3; r++)
                        {
                            g.Quad(P(r, s), P(r + 1, s), P(r + 1, s + 1), P(r, s + 1));
                        }
                    }

                    return g.Save("BeveledDisc");
                }
            }
        }

        sealed class Geo
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int> indices = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                indices.Add(i);
                indices.Add(i + 1);
                indices.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh Save(string name)
            {
                if (builtMeshes.TryGetValue(name, out var previous))
                {
                    return previous;
                }

                var mesh = new Mesh
                {
                    name = name
                };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x * .5f + .5f, v.y * .5f + .5f)).ToList());
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                triangles += indices.Count / 3;
                var result = ChiliIslandArt.Save(mesh, Root + "/Meshes/" + name + ".asset");
                builtMeshes[name] = result;
                return result;
            }
        }

        static void Render(Camera camera, string path, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log("Art preview needs a graphics device.");
                return;
            }

            var rt = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void ExportObj(List<GameObject> objects)
        {
            string F(float value)
            {
                return value.ToString("0.######", CultureInfo.InvariantCulture);
            }

            foreach (var root in objects)
            {
                var obj = new StringBuilder("# Original Kimchily Chili Island mesh\nmtllib ChiliIsland.mtl\n");
                int offset = 1;

                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var mesh = filter.sharedMesh;
                    obj.Append("o ").Append(filter.name.Replace(' ', '_')).Append('\n');
                    var material = filter.GetComponent<MeshRenderer>().sharedMaterial;
                    obj.Append("usemtl ").Append(material.name).Append('\n');

                    foreach (var v in mesh.vertices)
                    {
                        var p = matrix.MultiplyPoint3x4(v);
                        obj.Append("v ").Append(F(-p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
                    }

                    foreach (var v in mesh.normals)
                    {
                        var p = matrix.inverse.transpose.MultiplyVector(v).normalized;
                        obj.Append("vn ").Append(F(-p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
                    }

                    var t = mesh.triangles;

                    for (int i = 0; i < t.Length; i += 3)
                    {
                        obj.Append("f");

                        foreach (int k in new[]
                        {
                            0,
                            2,
                            1
                        })
                        {
                            int v = offset + t[i + k];
                            obj.Append(' ').Append(v).Append("//").Append(v);
                        }

                        obj.Append('\n');
                    }

                    offset += mesh.vertexCount;
                }

                File.WriteAllText(Root + "/Models/" + root.name.Replace(' ', '_').Replace('·', '_') + ".obj", obj.ToString());
            }

            var mtl = new StringBuilder("# Original Kimchily color palette\n");

            foreach (var pair in palette)
            {
                Color c = pair.Value.HasProperty("_Color") ? pair.Value.color : Hex("#8EE8D4");
                mtl.Append("newmtl ").Append(pair.Key).Append("\nKd ").Append(F(c.r)).Append(' ').Append(F(c.g)).Append(' ').Append(F(c.b)).Append("\nKa 0.2 0.2 0.2\nNs 12\n\n");
            }

            File.WriteAllText(Root + "/Models/ChiliIsland.mtl", mtl.ToString());
            AssetDatabase.Refresh();
        }
    }
}
