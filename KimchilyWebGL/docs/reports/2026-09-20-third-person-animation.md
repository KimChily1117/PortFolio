# 3인칭 이동 · 애니메이션 매핑 수정 결과

현재 월드의 바닥 충돌체를 수정하고, 캐릭터 이동과 카메라 회전을 분리했다. 모델마다 Animation Profile에 클립을 직접 연결할 수 있으며 Editor Play에서 입력·실제 속도·접지·충돌·목표 동작을 확인할 수 있다. 수정 APK를 연결된 SM-G955N에 설치한 뒤 걷기·달리기·점프의 실제 자세와 위치 이동을 확인했다.

## 원인과 수정

- **이동 방해:** `MyWorld`의 World Platform은 `(9, 0.1, 9)`로 납작해진 원통이지만 기본 CapsuleCollider가 남아 플레이어 시작점과 겹쳤다. 이 씬의 플랫폼만 실제 메시를 사용하는 정적 MeshCollider로 변경하고 플레이어 시작 높이를 `0.08`로 올렸다. 원본 씬은 Artifacts에 백업했다.
- **카메라 회전:** 추적 카메라를 플레이어 자식에서 같은 씬의 독립 오브젝트로 옮겼다. 카메라 기준으로 이동하고 캐릭터만 이동 방향을 바라본다. 추적 모드 해제·플레이어 삭제·씬 종료에서 기존 카메라와 리스너를 복원하고 생성 카메라를 정리한다.
- **애니메이션 없음:** 최초 상태 로그는 Walk/Run이었지만 기존 Animator Controller가 수동 Playables 평가 결과를 같은 프레임의 후반에 덮고 있었다. 유효한 Profile 재생 중 모델 복사본의 Controller를 일시 분리하고 AlwaysAnimate를 사용한다. 종료 시 Controller·Culling Mode·Root Motion 설정을 복원한다. 원본 prefab과 Controller 자산은 변경하지 않는다.

실제 Humanoid 본을 측정한 비교에서 기존 Controller를 연결한 경우 Update 직후 본 변화는 약 **63.117°**, LateUpdate의 자세 변화는 **0°**였다. 수정 후 네 가지 초기 Controller/Culling 조합 모두 프레임 끝의 변화도 **63.117°**, 덮어쓰기 차이는 **0°**였다. 이 검사는 본 자세의 지속을 측정했으며, 별도로 실기기 화면도 확인했다.

## 사용하는 방법

1. 씬의 **Kimchily Mobile Player**를 선택하고 **Model Prefab**을 연결한다.
2. **애니메이션 Profile 만들기 · 연결**을 누르고 FBX 내부 또는 `.anim`의 Idle / Walk / Run / Jump 클립을 슬롯에 넣는다. Fall / Land도 선택적으로 연결할 수 있다.
3. **Play → Game 창 클릭** 후 WASD·Space 또는 화면 조이스틱으로 조작한다. Inspector의 **Play 디버그**에서 입력·실제 속도·접지·목표 애니메이션을 확인한다. 부분 조이스틱 입력으로 Walk, 최대 입력으로 Run을 확인할 수 있다.
4. Play 종료 후 씬을 저장하고 **Build & Publish**한다. Profile 자산의 변경은 Play 종료 후에도 남는다.

현재 예시 월드에는 `Assets/World/PlayerAnimations.asset`을 만들고 WAIT00 / WALK00_F / RUN00_F / JUMP00을 연결했다. 공통 SDK 코드에는 이 모델 이름에 따른 분기가 없다. Humanoid는 유효한 Avatar가 필요하고 Generic은 본 경로가 일치해야 한다. 자세한 절차는 [애니메이션 가이드](../animation-import-guide.md)를 참고한다.

## 검증과 전달물

- 관리 코드 빌드: 경고 0, 오류 0.
- Unity Editor 테스트 **32/32**, PlayMode 테스트 **90/90** 통과. 실패·건너뜀 0. 실제 Update/LateUpdate 이후 포즈 유지와 Controller 복원 회귀 테스트를 포함한다.
- Android 월드 AssetBundle 빌드 및 로컬 서버 게시 성공. 애니메이션 Profile과 클립이 번들 의존성에 포함된다.
- Unity Android 내보내기·IL2CPP APK 빌드 성공. Android 단위 테스트 31개는 Gradle의 기존 통과 캐시를 재사용했다.
- SM-G955N / Android 9: 새 앱 설치·게시 링크 입장·재입장, 걷기 약 **1.28 m/s**, 달리기 약 **3.99 m/s**, 이동 거리 약 **1.96 m / 2.00 m**, 점프→하강→대기 확인. 걷기·달리기·점프의 다른 자세가 보이는 화면을 직접 검사했다. 해당 실행 로그에 Unity 오류·예외 없음.
- QR PNG 응답과 manifest SHA-256을 확인했다. 이번 변경에서 휴대전화 카메라의 QR 촬영을 다시 시험한 것은 아니며, 앱 딥링크로 입장했다.

최종 APK: `KimchilyAndroid/Artifacts/kimchily-unity-debug.apk`, **38,358,736 bytes**.

SHA-256: `1fcf60d536c30366217acc17d918c262cd20a4d63a2f76b53ed18cb1d0545e32`. 기기에 설치된 base.apk의 SHA-256도 동일하다.

[게시 QR 페이지](http://192.168.0.4:8787/w/sample-world/20260919T171354429Z-728ae5ab)

재현 스크립트, 이전/수정 후 Humanoid 측정, 최종 검증 JSON과 실기기 화면은 `KimchilyCreator/Artifacts/third-person-animation-20260920/`에 보관했다. 최신 근거는 `verification.json`, `humanoid-after-fix.json`, `device-probe.log`, `device-walk.png`, `device-run.png`, `device-jump.png`다. 빌드·프로브용 임시 스크립트는 Assets 밖으로 옮겼다.
