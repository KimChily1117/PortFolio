using System.Collections;
using System.Collections.Generic;
using Kimchily.Creator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Networking
{
    /// <summary>
    /// 방의 서버 playerId를 현재 Unity 씬의 시각 객체에 연결한다.
    /// 닉네임은 중복될 수 있으므로 객체 식별이나 말풍선 수신 대상을 정하는 키로 사용하지 않는다.
    /// 게임별 판정은 여기서 하지 않는다. 같은 아바타/채팅 표현을 여러 UGC 게임에서 재사용한다.
    /// </summary>
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
                // 실시간 chat만 말풍선으로 보낸다. joined의 과거 대화는 채팅 패널 이력으로만 남는다.
                if (message.chat.playerId == chat.View.selfId) localSpeech?.Say(message.chat.text);
                else if (remotes.TryGetValue(message.chat.playerId, out var remote)) remote.Speech?.Say(message.chat.text);
            }
        }
        void SpreadInitialSpawn()
        {
            // 두 번째 참가자부터 서로 겹치지 않는 주변 바닥을 찾는다. 첫 위치 전송 전에만 실행해
            // 서버가 기억할 최초 스폰과 로컬 낙하 리스폰 위치가 같은 지점이 되도록 한다.
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
                // 원격 객체도 월드 씬에 귀속시켜 씬 퇴장 시 Unity가 함께 정리할 수 있게 한다.
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
                // 로컬의 시각 모델만 복제한다. 이동 컨트롤러·입력·카메라까지 복제하면 한 기기에서
                // 여러 캐릭터가 같은 입력을 받거나 카메라가 경쟁하므로 사용자 Behaviour도 제거한다.
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
            // Destroy는 프레임 끝에 적용된다. 비활성 상태로 한 프레임 기다려 제거 대상 스크립트가
            // 다시 활성화되지 않게 한다. 첫 위치가 오기 전에는 원점에 캐릭터를 노출하지 않는다.
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
            // 서버에서 검증한 순서 번호도 클라이언트에서 다시 확인한다. 오래된 샘플을 적용하면
            // 이미 이동한 캐릭터가 뒤로 되감기는 현상이 생긴다. 게임 상태 version과는 별개 번호다.
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
            // 네트워크는 약 10 Hz, 렌더링은 매 프레임이다. 지수 보간으로 목표 위치에 부드럽게
            // 가까워지되 미래 위치를 예측하지 않는다. 따라서 이 시각 위치를 서버 판정에 재사용하지 않는다.
            float dt = Time.unscaledDeltaTime, factor = 1 - Mathf.Exp(-15 * dt);
            transform.position = Vector3.Lerp(transform.position, new Vector3(target.x, target.y, target.z), factor);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, target.yaw, 0), factor);
            // 샘플이 1초 이상 멎으면 걷기 애니메이션을 멈춘다. 서버의 발판 신선도 판정과 별개인 표현 정책이다.
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
            // DOM 오버레이가 아니라 캐릭터 자식의 World Space Canvas/TMP다.
            // 공통 Label helper가 richText를 끄므로 채팅에 들어온 태그도 그대로 글자로 표시한다.
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
        // 새 메시지는 이전 말풍선을 대체하고 표시 시간을 갱신한다. 게임 시간 배율과 무관하게 약 6초 유지한다.
        public void Say(string text) { speech.text = text; expires = Time.unscaledTime + 6; bubble.SetActive(true); }
        public void HideSpeech() { if (bubble != null) bubble.SetActive(false); if (speech != null) speech.text = ""; }
        void LateUpdate()
        {
            // 각 기기의 관찰 카메라를 향하게 한다. 이 회전은 UI 표현이므로 네트워크로 동기화하지 않는다.
            Camera camera = viewCamera?.Invoke(); if (camera == null) camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
            if (bubble.activeSelf && Time.unscaledTime >= expires) HideSpeech();
        }
    }
}
