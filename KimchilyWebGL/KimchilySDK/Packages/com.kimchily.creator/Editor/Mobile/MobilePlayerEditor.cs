using Kimchily.Creator.Mobile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kimchily.Creator.Editor
{
    [CustomEditor(typeof(KimchilyMobilePlayer))]
    public sealed class MobilePlayerEditor : UnityEditor.Editor
    {
        private bool refreshQueued;
        private bool showAnimationSettings;

        private void OnEnable() { Undo.undoRedoPerformed += RefreshAfterUndo; }
        private void OnDisable() { Undo.undoRedoPerformed -= RefreshAfterUndo; }
        private void RefreshAfterUndo()
        {
            if (target != null) QueueAnimationRefresh((KimchilyMobilePlayer)target);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "animationProfile");
            serializedObject.ApplyModifiedProperties();
            var player = (KimchilyMobilePlayer)target;
            DrawModelPreparation(player);
            EditorGUILayout.Space();
            DrawAnimationProfile(player);
            EditorGUILayout.Space();
            DrawPlayDebug(player);
        }

        private void DrawModelPreparation(KimchilyMobilePlayer player)
        {
            if (player.ModelPrefab == null) return;
            string[] unsupported;
            try { unsupported = PublishableModelUtility.GetUnsupportedScripts(player.ModelPrefab); }
            catch (System.ArgumentException error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
                return;
            }
            if (unsupported.Length == 0) return;
            EditorGUILayout.HelpBox("이 모델에는 앱에 없는 C# 스크립트가 포함되어 있습니다:\n" +
                string.Join("\n", unsupported), MessageType.Warning);
            EditorGUILayout.HelpBox("게시용 복사본은 모델·재질·Animator를 유지하고 위 스크립트를 제외합니다. " +
                "스크립트가 담당하는 표정 UI·물리·IK 등의 기능은 빠집니다. 원본 모델은 보존되며 Root Motion은 꺼집니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || AssetDatabase.Contains(player) || PrefabStageUtility.GetCurrentPrefabStage() != null))
                if (GUILayout.Button("게시용 모델 복사본 만들기 · 연결"))
                    EditorApplication.delayCall += () =>
                    {
                        if (player == null) return;
                        try { MobilePlayerModelPreparation.PreparePlayer(player); }
                        catch (System.Exception error) { Debug.LogException(error, player); }
                    };
        }

        private void DrawAnimationProfile(KimchilyMobilePlayer player)
        {
            EditorGUILayout.LabelField("플레이어 애니메이션", EditorStyles.boldLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animationProfile"),
                new GUIContent("Animation Profile", "모델에 맞는 Idle / Walk / Run / Jump / Fall / Land 클립을 연결합니다."));
            if (serializedObject.ApplyModifiedProperties()) QueueAnimationRefresh(player);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                if (GUILayout.Button("애니메이션 Profile 만들기 · 연결"))
                    EditorApplication.delayCall += () => CreateProfile(player);

            var profile = player.AnimationProfile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Profile이 비어 있으면 모델의 기존 Animator Controller를 사용합니다. " +
                    "이동·점프에 맞춰 클립을 바꾸려면 Profile을 만들고 해당 모델의 클립을 연결하세요. " +
                    "애니메이션 없는 Static 모델은 Profile 없이 사용할 수 있습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("FBX를 Project 창에서 펼쳐 내부 AnimationClip을 각 슬롯에 넣으세요. " +
                "Profile 재생 중에는 실행 중 모델의 Controller를 잠시 분리하고 해제 시 복구합니다. " +
                "기존 Controller의 상태·파라미터·레이어 전환은 사용하지 않으며 원본 모델 자산은 변경하지 않습니다.", MessageType.Info);
            using (var profileObject = new SerializedObject(profile))
            {
                profileObject.Update();
                DrawProfileProperty(profileObject, "idle", "Idle · 대기");
                DrawProfileProperty(profileObject, "walk", "Walk · 걷기");
                DrawProfileProperty(profileObject, "run", "Run · 달리기");
                DrawProfileProperty(profileObject, "jump", "Jump · 상승");
                DrawProfileProperty(profileObject, "fall", "Fall · 하강");
                DrawProfileProperty(profileObject, "land", "Land · 착지");
                showAnimationSettings = EditorGUILayout.Foldout(showAnimationSettings, "속도와 전환 설정", true);
                if (showAnimationSettings)
                {
                    DrawProfileProperty(profileObject, "walkSpeed", "걷기 기준 속도 (m/s)");
                    DrawProfileProperty(profileObject, "runSpeed", "달리기 기준 속도 (m/s)");
                    DrawProfileProperty(profileObject, "runThreshold", "달리기 전환 속도 (m/s)");
                    DrawProfileProperty(profileObject, "idleThreshold", "정지 판정 속도 (m/s)");
                    DrawProfileProperty(profileObject, "blendDuration", "전환 시간 (초)");
                    DrawProfileProperty(profileObject, "landingDuration", "착지 유지 시간 (초)");
                }
                if (profileObject.ApplyModifiedProperties()) QueueAnimationRefresh(player);
            }
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Play 중에도 클립 변경을 현재 플레이어에 반영합니다. Profile은 공유 자산이므로 " +
                    "이 자산의 수정은 Play 종료 후에도 남습니다. 씬의 Profile 연결 변경은 Play 종료 시 되돌아갑니다.", MessageType.Info);
            DrawAnimationDiagnostics(player, profile);
        }

        private static void DrawProfileProperty(SerializedObject profile, string name, string label)
        {
            var property = profile.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private static void CreateProfile(KimchilyMobilePlayer player)
        {
            if (player == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            string modelName = player.ModelPrefab == null ? "Player" : player.ModelPrefab.name;
            string path = EditorUtility.SaveFilePanelInProject("플레이어 Animation Profile 만들기", modelName + "Animations",
                "asset", "새 Profile을 저장할 위치를 선택하세요.", "Assets");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
            {
                EditorUtility.DisplayDialog("새 자산 이름 필요", "기존 자산을 덮어쓰지 않습니다. 다른 이름으로 만들어 주세요.", "확인");
                return;
            }
            var profile = CreateInstance<KimchilyPlayerAnimationProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            using (var playerObject = new SerializedObject(player))
            {
                playerObject.FindProperty("animationProfile").objectReferenceValue = profile;
                playerObject.ApplyModifiedProperties();
            }
            EditorGUIUtility.PingObject(profile);
        }

        private void QueueAnimationRefresh(KimchilyMobilePlayer player)
        {
            if (!Application.isPlaying || refreshQueued) return;
            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                if (player != null && Application.isPlaying && player.isActiveAndEnabled) player.RefreshAnimation();
                if (this != null) Repaint();
            };
        }

        private static void DrawAnimationDiagnostics(KimchilyMobilePlayer player, KimchilyPlayerAnimationProfile profile)
        {
            Animator animator = Application.isPlaying ? player.AnimationAnimator :
                player.ModelPrefab != null ? player.ModelPrefab.GetComponentInChildren<Animator>(true) :
                player.VisualRoot != null ? player.VisualRoot.GetComponentInChildren<Animator>(true) : null;
            if (animator == null)
                EditorGUILayout.HelpBox("연결된 모델에 Animator가 없습니다. 애니메이션 모델은 모델 prefab의 본 계층에 맞는 " +
                    "Animator를 준비하세요. 플레이어 루트에 임의로 추가하면 본 경로가 맞지 않을 수 있습니다.", MessageType.Warning);
            else
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("대상 Animator", animator, typeof(Animator), true);
                if (!animator.enabled || !animator.gameObject.activeSelf)
                    EditorGUILayout.HelpBox("대상 Animator 또는 모델 오브젝트가 비활성 상태입니다. 재생할 모델의 활성 상태를 확인하세요.", MessageType.Warning);
                if (animator.avatar != null && !animator.avatar.isValid)
                    EditorGUILayout.HelpBox("Avatar가 유효하지 않습니다. 모델 FBX의 Rig 설정을 확인하세요. Humanoid는 Configure에서 본 매핑과 T-pose를 확인합니다.", MessageType.Warning);
            }

            if (profile.idle == null) EditorGUILayout.HelpBox("Idle이 비어 있어 첫 번째 호환 클립으로 대기합니다. 대기 자세를 지정하려면 Idle 클립을 넣으세요.", MessageType.Warning);
            if (profile.walk == null && profile.run == null)
                EditorGUILayout.HelpBox("Walk와 Run이 모두 비어 있습니다. 플레이어는 이동해도 걷기·달리기 동작이 나오지 않습니다.", MessageType.Warning);
            else if (profile.walk == null)
                EditorGUILayout.HelpBox("Walk가 비어 있어 이동 동작에는 Run 클립을 사용합니다.", MessageType.Info);
            else if (profile.run == null)
                EditorGUILayout.HelpBox("Run이 비어 있어 빠르게 이동할 때에도 Walk 클립을 사용합니다.", MessageType.Info);
            if (profile.jump == null && profile.fall == null)
                EditorGUILayout.HelpBox("Jump와 Fall이 비어 있습니다. 물리 점프는 가능하지만 별도 공중 동작은 없습니다.", MessageType.Info);

            AnimationClip[] clips = { profile.idle, profile.walk, profile.run, profile.jump, profile.fall, profile.land };
            string[] roles = { "Idle", "Walk", "Run", "Jump", "Fall", "Land" };
            int compatible = 0;
            int index = 0;
            foreach (var clip in clips)
            {
                string role = roles[index++];
                if (clip == null) continue;
                string error = KimchilyPlayerAnimationProfile.GetClipCompatibilityError(animator, clip);
                if (error == null) compatible++;
                else if (animator != null || clip.legacy)
                    EditorGUILayout.HelpBox(role + " (" + clip.name + ") — 이 클립을 재생에서 제외합니다: " + error, MessageType.Warning);
            }
            if (compatible == 0)
                EditorGUILayout.HelpBox("재생할 수 있는 Profile 클립이 없습니다. SDK 애니메이션 전환은 시작하지 않으며 모델의 기존 Controller가 유지됩니다.", MessageType.Warning);
            if (profile.land == null)
                EditorGUILayout.HelpBox("Land가 없으면 착지 클립을 건너뛰고 현재 속도의 Idle / Walk / Run으로 돌아갑니다.", MessageType.Info);
            EditorGUILayout.HelpBox("Humanoid는 유효한 Avatar와 리타게팅 결과를 확인하세요. Generic은 모델과 클립의 본 계층·경로가 같아야 합니다. " +
                "클립을 연결하는 것만으로 다른 리그와 자동 호환되지는 않습니다. Root Motion은 SDK가 끄고 CharacterController가 이동합니다.", MessageType.Info);
        }

        private static void DrawPlayDebug(KimchilyMobilePlayer player)
        {
            EditorGUILayout.LabelField("Play 디버그", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Game 창을 클릭한 뒤 WASD / 화살표로 이동, Space로 점프합니다. " +
                "오른쪽 시점 영역에서 우클릭 드래그하면 추적 카메라가 회전합니다. " +
                "화면 왼쪽 조이스틱·오른쪽 점프 버튼은 마우스/터치로도 조작할 수 있습니다.", MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play를 시작하면 실제 이동 속도·접지·애니메이션 상태를 여기서 확인할 수 있습니다.", MessageType.None);
                return;
            }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("목표 애니메이션 상태", player.AnimationState ?? "Unmapped");
                EditorGUILayout.FloatField("수평 속도 (m/s)", player.PlanarSpeed);
                EditorGUILayout.Vector2Field("이동 입력", player.Controls.Move);
                EditorGUILayout.Toggle("접지", player.IsGrounded);
                EditorGUILayout.FloatField("수직 속도 (m/s)", player.VerticalVelocity);
                EditorGUILayout.TextField("충돌", player.LastCollisions.ToString());
                EditorGUILayout.Vector3Field("월드 위치", player.transform.position);
                EditorGUILayout.Toggle("3인칭 추적 사용", player.UseThirdPersonCamera);
                EditorGUILayout.ObjectField("추적 카메라", player.ViewCamera, typeof(Camera), true);
            }
            if (player.AnimationProfile != null && !string.IsNullOrEmpty(player.AnimationError))
                EditorGUILayout.HelpBox(player.AnimationError, MessageType.Warning);
            if (!player.UseThirdPersonCamera)
                EditorGUILayout.HelpBox("추적 카메라가 꺼져 있습니다. 월드의 활성 카메라로 보며 우클릭 드래그는 그 카메라를 회전시키지 않습니다.", MessageType.Info);
            if (player.Controls.Move.sqrMagnitude > .01f && player.PlanarSpeed < .02f)
                EditorGUILayout.HelpBox("입력은 들어오지만 실제 이동량이 작습니다. Pause 여부, CharacterController 활성 상태, " +
                    "주변 벽·바닥 Collider와 시작 위치를 확인하세요. 납작하게 줄인 Cylinder의 CapsuleCollider는 보이는 바닥보다 크게 남을 수 있습니다.", MessageType.Warning);
        }
    }
}
