using System.Collections;
using System.Collections.Generic;
using Kimchily.Creator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Networking
{
    public sealed class NetworkAvatars : MonoBehaviour
    {
        readonly Dictionary<string, RemoteAvatar> remotes = new Dictionary<string, RemoteAvatar>();
        public int RemoteCount => remotes.Count;
        InGameChat chat;
        KimchilyMobilePlayer local;
        PlayerSpeech localSpeech;
        public void Initialize(InGameChat owner) { chat = owner; chat.ServerEvent += Receive; }
        public void Bind(KimchilyMobilePlayer player)
        {
            Clear(); local = player;
            localSpeech = PlayerSpeech.Create(player.transform, chat.View.settings.name + " · 나", () => local != null ? local.ViewCamera : Camera.main);
        }
        void Receive(ChatEvent message)
        {
            if (local == null) return;
            if (message.type == "joined")
            {
                SpreadInitialSpawn();
                if (localSpeech != null) localSpeech.SetName(chat.View.settings.name + " · 나");
                foreach (var player in chat.View.players) Upsert(player);
            }
            else if (message.type == "playerJoined" || message.type == "state") Upsert(message.player);
            else if (message.type == "playerLeft" && message.player != null && remotes.TryGetValue(message.player.playerId, out var leaving))
            { if (leaving != null) Destroy(leaving.gameObject); remotes.Remove(message.player.playerId); }
            else if (message.type == "chat" && message.chat != null)
            {
                if (message.chat.playerId == chat.View.selfId) localSpeech?.Say(message.chat.text);
                else if (remotes.TryGetValue(message.chat.playerId, out var remote)) remote.Speech?.Say(message.chat.text);
            }
        }
        void SpreadInitialSpawn()
        {
            if (chat.View.players.Length < 2) return;
            Vector3 start = local.transform.position;
            var physics = local.gameObject.scene.GetPhysicsScene();
            var overlaps = new Collider[16];
            for (int i = 0; i < 8; i++)
            {
                float angle = (i + chat.View.players.Length) * Mathf.PI / 4;
                Vector3 from = start + new Vector3(Mathf.Cos(angle) * 1.2f, 3, Mathf.Sin(angle) * 1.2f);
                if (!physics.Raycast(from, Vector3.down, out RaycastHit hit, 5, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < .75f || hit.collider is CharacterController) continue;
                Vector3 candidate = hit.point + Vector3.up * .08f;
                if (physics.OverlapCapsule(candidate + Vector3.up * .36f, candidate + Vector3.up * 1.44f, .32f, overlaps, ~0, QueryTriggerInteraction.Ignore) != 0) continue;
                bool occupied = false;
                foreach (var player in chat.View.players)
                    if (player.playerId != chat.View.selfId && player.state != null && Vector3.Distance(candidate, new Vector3(player.state.x, player.state.y, player.state.z)) < .8f) occupied = true;
                if (occupied) continue;
                local.SetSpawnPosition(candidate); local.Respawn(); return;
            }
        }
        void Upsert(ChatPlayer player)
        {
            if (player == null || player.playerId == chat.View.selfId || string.IsNullOrEmpty(player.playerId)) return;
            if (!remotes.TryGetValue(player.playerId, out var avatar))
            {
                var root = new GameObject("Remote Player · " + player.name);
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, local.gameObject.scene);
                avatar = root.AddComponent<RemoteAvatar>();
                avatar.Initialize(local, player.name); remotes.Add(player.playerId, avatar);
                root.SetActive(true);
                Debug.Log("[Kimchily Network] Remote joined: " + player.name + " count=" + remotes.Count);
            }
            if (player.state != null) avatar.Apply(player.state);
        }
        public void ClearParticipants()
        {
            foreach (var remote in remotes.Values) if (remote != null) Destroy(remote.gameObject);
            remotes.Clear(); if (localSpeech != null) localSpeech.HideSpeech();
        }
        public void Clear()
        {
            ClearParticipants(); if (localSpeech != null) Destroy(localSpeech.gameObject); localSpeech = null; local = null;
        }
        void OnDestroy() { if (chat != null) chat.ServerEvent -= Receive; Clear(); }
    }

    public sealed class RemoteAvatar : MonoBehaviour
    {
        public PlayerSpeech Speech { get; private set; }
        public long LastSequence { get; private set; } = -1;
        GameObject visual;
        KimchilyMobilePlayer source;
        KimchilyPlayerAnimationDriver animation;
        ChatPose target;
        float lastSample;
        bool ready;
        public void Initialize(KimchilyMobilePlayer local, string nickname)
        {
            source = local;
            if (local.VisualRoot != null)
            {
                // Clone visual hierarchy only; never clone local movement, camera or input.
                visual = Instantiate(local.VisualRoot.gameObject, transform, false);
                visual.name = "Remote Visual"; visual.SetActive(false);
                foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) { behaviour.enabled = false; Destroy(behaviour); }
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            }
            Speech = PlayerSpeech.Create(transform, nickname, () => source != null ? source.ViewCamera : Camera.main);
            Speech.gameObject.SetActive(false);
        }
        IEnumerator Start()
        {
            // Let removed authored behaviours finish destruction while the clone is inactive.
            yield return null;
            ready = true;
            if (target != null) ActivateVisual();
        }
        void ActivateVisual()
        {
            if (visual == null || visual.activeSelf) return;
            visual.SetActive(true); Speech.gameObject.SetActive(true);
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null && source != null && source.AnimationProfile != null)
            { animation = new KimchilyPlayerAnimationDriver(); animation.Initialize(animator, source.AnimationProfile); }
        }
        public void Apply(ChatPose pose)
        {
            if (pose == null || pose.sequence <= LastSequence) return;
            bool first = target == null;
            target = pose; LastSequence = pose.sequence; lastSample = Time.unscaledTime;
            if (first || Vector3.Distance(transform.position, new Vector3(pose.x, pose.y, pose.z)) > 8)
                transform.SetPositionAndRotation(new Vector3(pose.x, pose.y, pose.z), Quaternion.Euler(0, pose.yaw, 0));
            if (ready) ActivateVisual();
        }
        void Update()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime, factor = 1 - Mathf.Exp(-15 * dt);
            transform.position = Vector3.Lerp(transform.position, new Vector3(target.x, target.y, target.z), factor);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, target.yaw, 0), factor);
            animation?.Tick(Time.unscaledTime - lastSample > 1 ? 0 : target.speed, target.grounded, target.verticalVelocity, dt);
        }
        void OnDestroy() { animation?.Dispose(); }
    }

    public sealed class PlayerSpeech : MonoBehaviour
    {
        public string CurrentText => speech == null ? "" : speech.text;
        TextMeshProUGUI nameLabel, speech;
        GameObject bubble;
        System.Func<Camera> viewCamera;
        float expires;
        public static PlayerSpeech Create(Transform target, string name, System.Func<Camera> camera)
        {
            var rect = UnityChatPanel.Node("Nickname and Speech Bubble", target);
            rect.localPosition = new Vector3(0, 2.05f, 0); rect.localScale = Vector3.one * .006f;
            rect.sizeDelta = new Vector2(300, 170); rect.pivot = new Vector2(.5f, 0);
            var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var instance = rect.gameObject.AddComponent<PlayerSpeech>(); instance.viewCamera = camera;
            instance.nameLabel = UnityChatPanel.Label("Nickname", rect, name, 24);
            UnityChatPanel.Place(instance.nameLabel.rectTransform, 0, 133, 300, 36); instance.nameLabel.alignment = TextAlignmentOptions.Center;
            var panel = UnityChatPanel.Node("Speech", rect); UnityChatPanel.Place(panel, 0, 0, 300, 126);
            var background = UnityChatPanel.Paint(panel, new Color(.03f, .13f, .14f, .94f)); background.raycastTarget = false;
            instance.speech = UnityChatPanel.Label("Speech Text", panel, "", 22);
            UnityChatPanel.Stretch(instance.speech.rectTransform); instance.speech.rectTransform.offsetMin = new Vector2(12, 10); instance.speech.rectTransform.offsetMax = new Vector2(-12, -10);
            instance.speech.alignment = TextAlignmentOptions.Center;
            instance.bubble = panel.gameObject; instance.bubble.SetActive(false); return instance;
        }
        public void SetName(string name) { nameLabel.text = name; }
        public void Say(string text) { speech.text = text; expires = Time.unscaledTime + 6; bubble.SetActive(true); }
        public void HideSpeech() { if (bubble != null) bubble.SetActive(false); if (speech != null) speech.text = ""; }
        void LateUpdate()
        {
            Camera camera = viewCamera?.Invoke(); if (camera == null) camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
            if (bubble.activeSelf && Time.unscaledTime >= expires) HideSpeech();
        }
    }
}
