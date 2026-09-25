using System.Collections.Generic;
using Kimchily.Creator.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Mobile
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(KimchilyMobileControls))]
    [AddComponentMenu("Kimchily/Mobile Player")]
    public sealed class KimchilyMobilePlayer : MonoBehaviour, IWorldContentValidatable
    {
        [SerializeField, Range(0, 20)] private float moveSpeed = 4;
        [SerializeField, Range(0, 5)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20;
        [SerializeField, Range(1, 30)] private float turnSpeed = 12;
        [SerializeField, InspectorName("3인칭 추적 카메라"), Tooltip("캐릭터를 따라가는 카메라를 사용하고 화면 방향을 기준으로 이동합니다.")]
        private bool createFollowCamera = true;
        [SerializeField, Tooltip("Optional FBX or prefab asset. A visual copy is fitted to the controller; the original is unchanged.")]
        private GameObject modelPrefab;
        [SerializeField, Tooltip("Assign Idle / Walk / Run / Jump clips in a reusable animation profile. Empty keeps the model's own Animator Controller.")]
        private KimchilyPlayerAnimationProfile animationProfile;
        [SerializeField, HideInInspector] private Transform visualRoot;
        private CharacterController controller;
        private KimchilyMobileControls controls;
        private KimchilyMobileCamera followCamera;
        private Camera authoredMovementCamera;
        private Vector3 fallbackForward = Vector3.forward;
        private float verticalVelocity;
        private float jumpBuffer;
        private KimchilyPlayerAnimationDriver animationDriver;
        private KimchilyPlayerAnimationProfile appliedAnimationProfile;
        private GameObject appliedModelPrefab;
        private string reportedAnimationState;
        private bool wasMoving;
        private Vector3 movementStart;
        private readonly List<Camera> suspendedCameras = new List<Camera>();
        private readonly List<AudioListener> suspendedListeners = new List<AudioListener>();
        public CharacterController Controller => controller != null ? controller : (controller = GetComponent<CharacterController>());
        public KimchilyMobileControls Controls => controls != null ? controls : (controls = GetComponent<KimchilyMobileControls>());
        public Vector3 SpawnPosition { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public Camera ViewCamera => followCamera == null ? null : followCamera.GetComponent<Camera>();
        public Transform VisualRoot => visualRoot;
        public float PlanarSpeed { get; private set; }
        public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded;
        public Animator AnimationAnimator { get; private set; }
        public string AnimationState => animationProfile == null ? "Unmapped" : animationDriver == null ? "Disabled" : animationDriver.CurrentState;
        public string AnimationError => animationDriver == null ? null : animationDriver.LastError;
        public CollisionFlags LastCollisions { get; private set; }
        public KimchilyPlayerAnimationProfile AnimationProfile
        {
            get => animationProfile;
            set { animationProfile = value; if (Application.isPlaying && isActiveAndEnabled) RefreshAnimation(); }
        }
        public bool UseThirdPersonCamera
        {
            get => createFollowCamera;
            set
            {
                if (createFollowCamera == value) return;
                createFollowCamera = value;
                authoredMovementCamera = null;
                Controls.ClearInput();
                if (!Application.isPlaying || !isActiveAndEnabled) return;
                if (value) ActivateCamera(); else DeactivateCamera();
            }
        }
        public GameObject ModelPrefab
        {
            get => modelPrefab;
            set
            {
                string error = GetModelError(value);
                if (error != null) throw new System.ArgumentException(error, nameof(value));
                modelPrefab = value;
                if (Application.isPlaying && controller != null) RebuildVisual();
            }
        }

        public IEnumerable<string> ValidateContent()
        {
            string error = GetModelError(modelPrefab);
            if (error != null) yield return error;
        }
        private string GetModelError(GameObject model)
        {
            if (model == null) return null;
            if (model.transform.IsChildOf(transform) || transform.IsChildOf(model.transform))
                return "Mobile Player Model Prefab must be a separate model asset, not the player or its own hierarchy.";
            if (model.GetComponentInChildren<KimchilyMobilePlayer>(true) != null)
                return "Mobile Player Model Prefab must contain only a visual model, without a KimchilyMobilePlayer component.";
            return null;
        }

        private void Reset()
        {
            CharacterController value = GetComponent<CharacterController>();
            if (value == null) return;
            value.height = 1.8f; value.radius = .32f; value.center = new Vector3(0, .9f, 0);
            value.stepOffset = .3f; value.slopeLimit = 50; value.skinWidth = .04f;
        }
        private void Awake()
        {
            controller = GetComponent<CharacterController>(); controls = GetComponent<KimchilyMobileControls>(); SpawnPosition = transform.position;
            fallbackForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (fallbackForward.sqrMagnitude < .01f) fallbackForward = Vector3.forward;
            RebuildVisual();
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (controller == null) Awake();
            verticalVelocity = jumpBuffer = 0;
            Controls.enabled = true;
            if (animationDriver == null) RefreshAnimation();
            if (createFollowCamera) ActivateCamera();
        }
        private void Update()
        {
            // Inspector edits bypass property setters; apply them on the next safe frame.
            if (appliedModelPrefab != modelPrefab) RebuildVisual();
            if (appliedAnimationProfile != animationProfile) RefreshAnimation();
            bool cameraActive = followCamera != null && followCamera.gameObject.activeSelf;
            if (createFollowCamera != cameraActive)
            {
                Controls.ClearInput();
                if (createFollowCamera) ActivateCamera(); else DeactivateCamera();
            }
            Vector2 move = Controls.Move;
            bool moving = move.sqrMagnitude > .01f;
            if (moving && !wasMoving) { wasMoving = true; movementStart = transform.position; }
            else if (!moving) ReportMovementStop();
            Simulate(Time.deltaTime, move, Controls.InputState.ConsumeJump());
        }
        /// <summary>Main-thread movement step, also usable by a custom input adapter.</summary>
        public void Simulate(float deltaTime, Vector2 input, bool jump)
        {
            if (!isActiveAndEnabled || !Controller.enabled) return;
            float dt = Mathf.Clamp(deltaTime, 0, .1f);
            if (dt <= 0) { if (jump) jumpBuffer = .15f; PlanarSpeed = 0; return; }
            if (jump) jumpBuffer = .15f;
            else jumpBuffer = Mathf.Max(0, jumpBuffer - dt);
            float acceleration = -Mathf.Max(1, Mathf.Abs(gravity));
            if (Controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2;
            if (Controller.isGrounded && jumpBuffer > 0)
            {
                verticalVelocity = Mathf.Sqrt(Mathf.Max(0, jumpHeight) * -2 * acceleration);
                jumpBuffer = 0;
                if (Debug.isDebugBuild) Debug.Log("[Kimchily Player] Jump position=" + transform.position, this);
            }
            verticalVelocity += acceleration * dt;
            Camera movementCamera = ResolveMovementCamera();
            Vector3 forward = movementCamera == null ? fallbackForward :
                Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f && movementCamera != null)
                forward = Vector3.ProjectOnPlane(movementCamera.transform.up, Vector3.up);
            forward = forward.sqrMagnitude < .0001f ? fallbackForward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 planar = Vector2.ClampMagnitude(input, 1);
            Vector3 direction = forward * planar.y + right * planar.x;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1 - Mathf.Exp(-turnSpeed * dt));
            Vector3 beforeMove = transform.position;
            CollisionFlags collisions = Controller.Move((direction * Mathf.Clamp(moveSpeed, 0, 20) + Vector3.up * verticalVelocity) * dt);
            LastCollisions = collisions;
            PlanarSpeed = dt > .00001f ? Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up).magnitude / dt : 0;
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
            if (transform.position.y < SpawnPosition.y - 30) Respawn();
            animationDriver?.Tick(PlanarSpeed, IsGrounded, verticalVelocity, dt);
            if (animationProfile != null && reportedAnimationState != AnimationState)
            {
                reportedAnimationState = AnimationState;
                if (Debug.isDebugBuild) Debug.Log("[Kimchily Player] Animation=" + AnimationState + " speed=" +
                    PlanarSpeed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " grounded=" + IsGrounded, this);
            }
        }
        public void SetSpawnPosition(Vector3 position) { SpawnPosition = position; }
        private Camera ResolveMovementCamera()
        {
            if (createFollowCamera && ViewCamera != null && ViewCamera.isActiveAndEnabled) return ViewCamera;
            if (authoredMovementCamera != null && authoredMovementCamera.isActiveAndEnabled &&
                authoredMovementCamera.gameObject.scene == gameObject.scene &&
                !authoredMovementCamera.transform.IsChildOf(transform)) return authoredMovementCamera;
            authoredMovementCamera = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(false))
                {
                    if (!camera.isActiveAndEnabled || camera == ViewCamera || camera.transform.IsChildOf(transform)) continue;
                    if (authoredMovementCamera == null) authoredMovementCamera = camera;
                    if (camera.CompareTag("MainCamera")) return authoredMovementCamera = camera;
                }
            return authoredMovementCamera;
        }
        /// <summary>Rebuilds the child visual after assigning a model in an authoring tool.</summary>
        public void RefreshVisual() { RebuildVisual(); }
        private void RebuildVisual()
        {
            string error = GetModelError(modelPrefab);
            if (error != null) throw new System.InvalidOperationException(error);
            appliedModelPrefab = modelPrefab;
            animationDriver?.Dispose();
            animationDriver = null;
            AnimationAnimator = null;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(visualRoot.gameObject); else DestroyImmediate(visualRoot.gameObject);
            }
            var container = new GameObject("Player Visual");
            visualRoot = container.transform;
            visualRoot.SetParent(transform, false);
            GameObject visual;
            if (modelPrefab == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Default Capsule";
                visual.transform.SetParent(visualRoot, false);
                visual.transform.localScale = new Vector3(.64f, .9f, .64f);
                visual.transform.localPosition = new Vector3(0, .9f, 0);
            }
            else
            {
                visual = Instantiate(modelPrefab, visualRoot, false);
                visual.name = modelPrefab.name + " (Player Visual)";
                visual.SetActive(true);
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float scale = Controller.height / Mathf.Max(.01f, bounds.size.y);
                    visualRoot.localScale = Vector3.one * Mathf.Clamp(scale, .001f, 1000);
                    bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Vector3 offset = new Vector3(transform.position.x - bounds.center.x, transform.position.y - bounds.min.y, transform.position.z - bounds.center.z);
                    visualRoot.position += offset;
                }
            }
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            AnimationAnimator = visual.GetComponentInChildren<Animator>(true);
            if (Application.isPlaying && isActiveAndEnabled) RefreshAnimation();
        }
        public void RefreshAnimation()
        {
            animationDriver?.Dispose();
            animationDriver = new KimchilyPlayerAnimationDriver();
            reportedAnimationState = null;
            appliedAnimationProfile = animationProfile;
            AnimationAnimator = visualRoot == null ? null : visualRoot.GetComponentInChildren<Animator>(true);
            if (!Application.isPlaying || !isActiveAndEnabled || animationProfile == null) return;
            animationDriver.Initialize(AnimationAnimator, animationProfile);
            if (!string.IsNullOrEmpty(animationDriver.LastError)) Debug.LogWarning("[Kimchily Player] " + animationDriver.LastError, this);
        }
        public void Respawn()
        {
            bool enabledBefore = Controller.enabled;
            Controller.enabled = false; transform.position = SpawnPosition; Controller.enabled = enabledBefore;
            verticalVelocity = jumpBuffer = 0;
            PlanarSpeed = 0;
            Controls.ClearInput();
            if (followCamera != null) followCamera.SnapToTarget();
        }

        private void ActivateCamera()
        {
            if (followCamera == null)
            {
                var cameraObject = new GameObject("Kimchily Follow Camera", typeof(Camera));
                // Keep the rig in the world scene, independent of the character's facing.
                // A child camera inherits the turn before LateUpdate and corrupts smoothing.
                SceneManager.MoveGameObjectToScene(cameraObject, gameObject.scene);
                followCamera = cameraObject.AddComponent<KimchilyMobileCamera>();
                followCamera.Initialize(this);
                cameraObject.AddComponent<AudioListener>();
            }
            bool copiedBackground = false;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera != ViewCamera && camera.enabled && camera.gameObject.activeInHierarchy)
                    {
                        if (!copiedBackground)
                        {
                            ViewCamera.clearFlags = camera.clearFlags;
                            ViewCamera.backgroundColor = camera.backgroundColor;
                            copiedBackground = true;
                        }
                        suspendedCameras.Add(camera);
                        camera.enabled = false;
                    }
            }
            foreach (AudioListener listener in FindObjectsOfType<AudioListener>())
                if (listener.gameObject != followCamera.gameObject && listener.enabled) { suspendedListeners.Add(listener); listener.enabled = false; }
            followCamera.gameObject.SetActive(true);
            followCamera.SnapToTarget();
            authoredMovementCamera = null;
            if (Debug.isDebugBuild) Debug.Log("[Kimchily Player] ThirdPerson camera enabled; independentRig=" +
                (followCamera.transform.parent == null), this);
        }
        private void OnDisable()
        {
            ReportMovementStop();
            if (controls != null) { controls.ClearInput(); controls.enabled = false; }
            verticalVelocity = jumpBuffer = 0;
            PlanarSpeed = 0;
            animationDriver?.Dispose();
            animationDriver = null;
            DeactivateCamera();
        }
        private void DeactivateCamera()
        {
            if (followCamera != null) followCamera.gameObject.SetActive(false);
            foreach (Camera camera in suspendedCameras) if (camera != null) camera.enabled = true;
            foreach (AudioListener listener in suspendedListeners) if (listener != null) listener.enabled = true;
            suspendedCameras.Clear(); suspendedListeners.Clear();
            authoredMovementCamera = null;
        }
        private void OnDestroy()
        {
            animationDriver?.Dispose();
            animationDriver = null;
            if (followCamera == null) return;
            if (Application.isPlaying) Destroy(followCamera.gameObject);
            else DestroyImmediate(followCamera.gameObject);
            followCamera = null;
        }
        private void OnApplicationPause(bool paused) { if (paused) { Controls.ClearInput(); jumpBuffer = 0; } }
        private void ReportMovementStop()
        {
            if (!wasMoving) return;
            wasMoving = false;
            if (Debug.isDebugBuild) Debug.Log("[Kimchily Player] MoveStop distance=" +
                Vector3.ProjectOnPlane(transform.position - movementStart, Vector3.up).magnitude.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " from=" + movementStart + " to=" + transform.position, this);
        }
    }
}
