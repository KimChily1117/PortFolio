# 외부 모델 C# 의존성 게시 오류 수정

스크린샷의 오류는 서버 연결 문제가 아니라 `unitychan_dynamic.prefab`에 포함된 C# 코드가 Android 앱에 없어서 발생했다. 월드 AssetBundle은 모델·재질·애니메이션을 전달하지만 새 C# 구현을 앱에 설치하지 않는다.

## 적용한 수정

- 게시 검사에서 첫 미지원 타입만 표시하던 것을 모든 미지원 타입을 표시하도록 변경했다. 검증을 우회하거나 외부 C#을 무조건 허용하지 않는다.
- 게시 창에 **플레이어 모델 게시용 복사본 만들기**, Mobile Player Inspector에 **게시용 모델 복사본 만들기 · 연결**, `Kimchily > World > Prepare Mobile Player for Publish` 메뉴를 추가했다.
- 독립 프리팹을 `<씬 폴더>/PublishedModels`에 만들고 Model Prefab과 기존 시각 미리보기를 교체한다. 중첩 프리팹 연결도 해제하므로 원본의 미지원 스크립트 의존성이 다시 따라오지 않는다.
- 모델·재질·스킨 본·Animator·Avatar는 유지하고 외부 C# 컴포넌트만 복사본에서 제외한다. Root Motion은 꺼서 CharacterController의 이동과 겹치지 않도록 한다.
- 원본과 기존 생성 파일은 덮어쓰지 않는다. 플레이어 변경은 Undo에 등록하며 씬은 사용자가 저장한다.
- 외부 StateMachineBehaviour 등 제거 대상 외의 코드 의존성이 남으면 명확히 실패하고 해당 생성본을 보관하지 않는다.

도구는 모델명·UnityChan 클래스명에 따라 분기하지 않는다. Unity가 임포트한 모델 에셋과 프리팹을 공통 입력으로 받아 계층과 컴포넌트 타입을 검사한다. Humanoid와 Generic의 리그 차이는 각 Avatar·클립 설정으로 처리하며, Animator가 없는 정적 모델도 복사할 수 있다. UnityChan은 이번 오류의 재현 사례다. 임의의 외부 C#·셰이더·리그를 무조건 자동 호환한다는 의미는 아니다.

이번 프리팹에서 제외되는 것은 `FaceUpdate`, `IdleChanger`, `IKCtrlRightHand`, `RandomWind`, `SpringManager`, `SpringBone`, `SpringCollider`의 **7개 타입, 58개 컴포넌트**다. 표정 선택 UI·별도 IK·spring bone 움직임 등의 기능도 빠진다. 이 기능까지 필요하면 SDK에 정식으로 구현하고 앱을 갱신해야 한다.

## 확인 결과

- 관리형 Runtime·Editor·테스트 컴파일: 경고 0, 오류 0.
- 실제 Unity EditMode: **32/32 통과**, 실패·건너뜀 0. 중첩 프리팹/원본 보존, Generic 스킨·애니메이션 참조 보존, RequireComponent 삭제 순서, 덮어쓰기 거절, 남은 C# 의존성 거절, 기존 플레이어 미리보기 교체 등을 검사했다. 추가로 특정 캐릭터 이름을 사용하지 않는 정적 MeshRenderer 모델과 프리팹을 거치지 않은 직접 FBX 입력을 검증했다.
- 실제 UnityChan 에셋을 별도 ValidationProject에 복사해 FaceUpdate 등 기존 7개 타입 오류를 재현했다. 변환 후 게시 검사 오류는 0개였다.
- **Android AssetBundle 빌드 성공**: revision `20260919T163735330Z-e9e67ecf`. 원본/변환본 렌더러 모두 23개, Animator Controller·Avatar 참조 유지, Root Motion 해제, 원본 프리팹 바이트 불변을 확인했다.
- 제작 프로젝트 원본 prefab·FBX·Character.ts 해시도 작업 전후 일치했다. 열려 있는 MyWorld 씬은 자동 변경하거나 저장하지 않았다.

증거: [EditMode 결과](../../KimchilySDK/Artifacts/editmode.xml), [실제 모델 검증 결과](../../KimchilySDK/Artifacts/unitychan-publish-probe-20260920.json), [Android 빌드 manifest](../../KimchilySDK/Artifacts/unitychan-publish-probe-build/20260919T163735330Z-e9e67ecf/world.json), [빌드 로그](../../KimchilySDK/Artifacts/unitychan-publish-probe-20260920.log).

실제 모델 검증은 별도 테스트 씬에서 수행했다. 이번 턴에는 사용자 씬을 게시하거나 새 QR로 기기 입장을 수행하지 않았다. 테스트용 소스와 생성 모델은 `KimchilySDK/Artifacts/unitychan-probe-source-20260920`에 보관했다.

## 사용자가 현재 씬에 적용하는 순서

1. Unity로 돌아가 코드 컴파일이 끝나기를 기다린다.
2. **Kimchily > World > Prepare Mobile Player for Publish**를 실행한다. 또는 게시 창에서 Build & Publish 검사를 다시 실행해 표시되는 **플레이어 모델 게시용 복사본 만들기**를 누른다.
3. 씬을 저장한 뒤 **Build & Publish**한다.
4. 새 QR/링크로 입장한다. 이번 변경은 Editor 도구이므로 현재 앱이 지원하는 모델 콘텐츠만 바꾸는 경우 APK 재설치는 필요 없다.

애니메이션은 [임포트·연결 가이드](../animation-import-guide.md)를 참고한다. 현재는 Animator Controller 자산을 모델에 연결할 수 있으며, Idle/Walk/Run/Jump 클립 슬롯과 조이스틱 상태 전환 adapter는 후속 SDK 제안이다.
