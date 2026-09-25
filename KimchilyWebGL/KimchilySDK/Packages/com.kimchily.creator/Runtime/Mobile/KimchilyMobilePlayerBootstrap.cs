using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Mobile
{
    /// <summary>Optional scene-scoped default player. Existing authored players always win.</summary>
    public static class KimchilyMobilePlayerBootstrap
    {
        private static Mesh builtinCylinderMesh;

        public static KimchilyMobilePlayer EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded world scene is required.", nameof(scene));
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                KimchilyMobilePlayer existing = root.GetComponentInChildren<KimchilyMobilePlayer>(true);
                if (existing != null) { RepairLegacyStarterFloor(scene); return existing; }
            }
            return CreateForScene(scene);
        }

        public static KimchilyMobilePlayer CreateForScene(Scene scene, GameObject modelPrefab = null)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded world scene is required.", nameof(scene));
            RepairLegacyStarterFloor(scene);
            FindSpawn(scene, out Vector3 position, out Quaternion rotation);
            var root = new GameObject("Kimchily Mobile Player");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(position, rotation);
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .32f; controller.center = new Vector3(0, .9f, 0);
            controller.stepOffset = .3f; controller.slopeLimit = 50; controller.skinWidth = .04f;
            root.AddComponent<KimchilyMobileControls>();
            KimchilyMobilePlayer player = root.AddComponent<KimchilyMobilePlayer>();
            player.ModelPrefab = modelPrefab;
            player.SetSpawnPosition(position);
            if (!Application.isPlaying) player.RefreshVisual();
            root.SetActive(true);
            return player;
        }

        // The first two bundled starter scenes flattened Unity's primitive cylinder
        // but retained its capsule collider. Match their complete generated shape;
        // authored/custom colliders and every edit-mode scene remain untouched.
        private static void RepairLegacyStarterFloor(Scene scene)
        {
            if (!Application.isPlaying) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                float width;
                float thickness;
                if (root.name == "World Platform") { width = 4.5f; thickness = .1f; }
                else if (root.name == "Display Platform") { width = 3; thickness = .08f; }
                else continue;
                Transform target = root.transform;
                if (!root.activeSelf || root.layer != 0 || !root.CompareTag("Untagged") || target.childCount != 0 ||
                    (target.localPosition - new Vector3(0, -thickness, 0)).sqrMagnitude > .00000001f ||
                    (target.localScale - new Vector3(width, thickness, width)).sqrMagnitude > .00000001f ||
                    Quaternion.Angle(target.localRotation, Quaternion.identity) > .001f ||
                    root.GetComponents<Component>().Length != 4) continue;
                CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (capsule == null || filter == null || root.GetComponent<MeshRenderer>() == null ||
                    !capsule.enabled || capsule.isTrigger || capsule.sharedMaterial != null || capsule.attachedRigidbody != null ||
                    capsule.direction != 1 || capsule.center.sqrMagnitude > .00000001f ||
                    Mathf.Abs(capsule.radius - .5f) > .00001f || Mathf.Abs(capsule.height - 2) > .00001f) continue;
                if (builtinCylinderMesh == null)
                {
                    // Obtain the engine's own mesh identity without relying on an
                    // undocumented Resources filename, name or vertex-count guess.
                    GameObject reference = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    reference.SetActive(false);
                    reference.GetComponent<Collider>().enabled = false;
                    reference.hideFlags = HideFlags.HideAndDontSave;
                    SceneManager.MoveGameObjectToScene(reference, scene);
                    builtinCylinderMesh = reference.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.Destroy(reference);
                }
                if (filter.sharedMesh != builtinCylinderMesh) continue;
                capsule.enabled = false;
                UnityEngine.Object.Destroy(capsule);
                MeshCollider replacement = root.AddComponent<MeshCollider>();
                replacement.sharedMesh = filter.sharedMesh;
                replacement.convex = false;
                if (Debug.isDebugBuild) Debug.Log("[Kimchily Player] Applied legacy starter floor compatibility: " + root.name, root);
            }
        }

        /// <summary>Prefer free capsule space on a broad floor, offset from origin props.</summary>
        public static bool FindSpawn(Scene scene, out Vector3 position, out Quaternion rotation)
        {
            Physics.SyncTransforms();
            var floors = new List<Collider>();
            Bounds sceneBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(false))
                {
                    if (!collider.enabled || collider.isTrigger || collider is CharacterController) continue;
                    floors.Add(collider);
                    if (!hasBounds) { sceneBounds = collider.bounds; hasBounds = true; } else sceneBounds.Encapsulate(collider.bounds);
                }
            }
            floors.Sort((a, b) => (b.bounds.size.x * b.bounds.size.z).CompareTo(a.bounds.size.x * a.bounds.size.z));
            Physics.SyncTransforms();
            PhysicsScene physics = scene.GetPhysicsScene();
            var overlaps = new Collider[32];
            Vector2[] samples = { new Vector2(1, -1), new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(.5f, -.5f), new Vector2(-.5f, -.5f), Vector2.zero };
            foreach (Collider floor in floors)
            {
                Bounds bounds = floor.bounds;
                if (bounds.size.x < .8f || bounds.size.z < .8f) continue;
                float step = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .25f, .2f, 6);
                foreach (Vector2 sample in samples)
                {
                    Vector3 start = bounds.center + new Vector3(sample.x * step, 0, sample.y * step);
                    start.y = sceneBounds.max.y + 5;
                    if (!physics.Raycast(start, Vector3.down, out RaycastHit hit, sceneBounds.size.y + 100, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider != floor || Vector3.Dot(hit.normal, Vector3.up) < .75f) continue;
                    Vector3 candidate = hit.point + Vector3.up * .08f;
                    int count = physics.OverlapCapsule(candidate + Vector3.up * .36f, candidate + Vector3.up * 1.44f, .32f, overlaps, ~0, QueryTriggerInteraction.Ignore);
                    if (count > 0) continue;
                    position = candidate;
                    Vector3 facing = Vector3.ProjectOnPlane(sceneBounds.center - candidate, Vector3.up);
                    rotation = facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : Quaternion.identity;
                    return true;
                }
            }
            // An authored scene without ground remains usable for inspection; a fall
            // below 30m returns the player here instead of falling without a bound.
            if (!hasBounds)
            {
                foreach (GameObject root in scene.GetRootGameObjects()) foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!hasBounds) { sceneBounds = renderer.bounds; hasBounds = true; } else sceneBounds.Encapsulate(renderer.bounds);
                }
            }
            position = hasBounds ? new Vector3(sceneBounds.max.x + 2, sceneBounds.max.y + 1, sceneBounds.min.z - 2) : Vector3.up;
            Vector3 fallbackFacing = Vector3.ProjectOnPlane(sceneBounds.center - position, Vector3.up);
            rotation = fallbackFacing.sqrMagnitude > .01f ? Quaternion.LookRotation(fallbackFacing) : Quaternion.identity;
            return false;
        }
    }
}
