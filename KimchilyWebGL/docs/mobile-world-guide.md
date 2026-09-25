# 서버 실행과 모바일 월드 제작

## 가장 쉬운 게시 순서

1. PC와 휴대폰을 같은 Wi-Fi에 연결한다.
2. Unity에서 `Kimchily > Publish World`를 연다.
3. **서버 켜기 · 연결**을 누른다. PC의 LAN 주소를 선택해 서버를 시작하고, 에디터용 주소와 토큰을 자동으로 연결한다.
4. 씬을 저장하고 **Build & Publish**를 누른다. 프로젝트와 창의 Build Target은 Android여야 한다.
5. 휴대폰 앱의 **월드 QR 스캔**으로 새 게시 QR을 읽는다.

`상태 확인`은 서버를 켜거나 끄지 않고 현재 상태를 갱신한다. `서버 끄기`는 이 게시 서버로 확인된 프로세스만 종료한다. 게시한 월드 파일은 남아 있으며, 같은 설정으로 다시 켜면 기존 QR을 사용할 수 있다. 서버를 끈 동안에는 QR 페이지 열기와 새 월드 다운로드가 불가능하다.

Unity 창이나 에디터를 닫아도 서버는 계속 실행된다. 종료하려면 **서버 끄기**를 사용한다. PC를 재부팅한 뒤에는 서버를 다시 켠다. 서버 시작·종료 명령 중에 창을 닫았다면 다시 열어 **상태 확인**으로 결과를 확인한다.

자동으로 폴더를 찾지 못하면 **서버 폴더 선택**에서 `KimchilyPublish`를 선택한다. 토큰은 에디터 세션 메모리에만 보관한다. 다른 Server URL을 입력하면 기존 토큰을 비우므로 해당 서버의 토큰을 따로 입력한다.

## PowerShell로 켜기·확인·끄기

워크스페이스 루트 `E:\task\Unity_Project`에서 실행한다.

```powershell
# 켜기: PC의 LAN IPv4를 자동 선택한다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyPublish\tools\start.ps1

# 확인: 켜짐, 꺼짐, 연결 오류를 구분한다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyPublish\tools\status.ps1

# 끄기: 이 서버로 확인된 프로세스만 종료한다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyPublish\tools\stop.ps1
```

같은 설정으로 `start.ps1`을 다시 실행하면 기존 서버를 재사용한다. 이미 꺼진 서버의 `stop.ps1`도 정상 종료한다. 네트워크 카드가 여러 개라 자동 선택이 불가능한 경우 `start.ps1 -LanAddress <PC의 IPv4>`로 선택한다. 다른 바인딩 주소·포트·QR용 주소는 `-BindAddress`, `-Port`, `-PublicBaseUrl` 옵션으로 지정한다. 이 안내에서 검증한 구성은 같은 LAN의 개발용 HTTP 서버다. `-PublicBaseUrl`은 QR에 기록할 주소를 정하며, HTTPS 서버나 인터넷 공개 환경을 자동 구성하지 않는다.

Unity의 `http://127.0.0.1:8787`은 PC 내부 게시 주소다. 휴대폰 접속 주소는 `http://<PC LAN 주소>:8787`이다. 휴대폰에서 그 주소 뒤에 `/health`를 붙였을 때 응답이 보이면 서버까지 연결된 것이다. PC의 IP가 바뀌면 서버를 끄고 다시 켠 뒤 새 주소의 QR을 사용해야 한다. 가장 간단한 방법은 **Build & Publish**로 새 QR을 발급받는 것이다.

포트가 다른 프로그램에 점유됐을 때는 오류에 표시된 프로세스와 포트를 확인한다. 도구는 ADB나 다른 서버를 임의로 종료하지 않는다. 자세한 설정과 로그 위치는 [게시 서버 설명](../KimchilyPublish/README.md)을 참고한다.

## 가로·세로 화면

처음 월드에 입장할 때는 **가로**로 열린다. 상단의 **화면: 가로** 버튼에서 **세로 화면**, **가로 화면**, **자동 회전**을 고른다. 버튼은 현재 선택을 표시하며, 저장된 선택은 다음 입장에도 유지된다. 방향만 바꿀 때는 현재 월드와 플레이어를 유지하며 다시 다운로드하지 않는다.

네이티브 홈은 기기 방향에 따라 회전한다. 월드 화면의 아래쪽은 이동·점프 조작에 사용한다.

## 기본 플레이어와 원하는 모델

기존 게시 월드에는 플레이어가 없으면 앱이 기본 플레이어를 생성한다. 제작 씬에서 위치와 모델을 정하려면 다음 순서를 사용한다.

1. `Kimchily > World > Add Mobile Player`를 선택한다.
2. 생성된 `Kimchily Mobile Player`를 바닥 위 원하는 시작 위치에 배치한다. 바닥과 벽에는 Collider가 필요하다.
3. `Kimchily Mobile Player` 컴포넌트의 **Model Prefab**에 Project 창의 FBX 또는 모델 프리팹을 연결한다. 비워 두면 기본 캡슐을 쓴다.
4. 또는 Project 창에서 모델을 선택하고 `Kimchily > World > Use Selected Model for Mobile Player`를 실행한다.
5. 씬을 저장한 후 **Build & Publish**한다.

모델은 플레이어 자식으로 복제하고 높이를 맞춘다. 원본 FBX·프리팹은 수정하지 않는다. Model Prefab에는 플레이어 자체나 `KimchilyMobilePlayer`를 포함한 프리팹을 넣지 않는다. 플레이어 이동과 충돌은 바깥쪽 CharacterController가 처리하며, 모델 내부 Collider는 복제본에서 비활성화한다. 이동 속도와 점프 높이는 플레이어 컴포넌트에서 조절한다. 기존 씬 카메라는 플레이어 카메라가 활성화된 동안 일시 중지되고, 추적 카메라는 첫 활성 카메라의 배경 설정을 이어받는다.

### 모델의 외부 C# 때문에 게시가 막힐 때

`UnityChan.FaceUpdate`처럼 `This C# component is not installed...`가 나오면 모델과 함께 가져온 C#이 Android 앱에 없다는 뜻이다. 프리팹의 체크박스를 끄는 것만으로는 코드 의존성이 없어지지 않는다.

1. 게시 창의 **플레이어 모델 게시용 복사본 만들기**를 누른다. 또는 플레이어 Inspector의 **게시용 모델 복사본 만들기 · 연결**, `Kimchily > World > Prepare Mobile Player for Publish` 메뉴를 사용한다.
2. 모델·재질·스킨·Animator·Avatar를 유지한 독립 프리팹을 `<씬 폴더>/PublishedModels`에 만들고 플레이어에 연결한다. 원본 프리팹은 보존하며 기존 파일을 덮어쓰지 않는다.
3. 씬을 저장하고 **Build & Publish**를 다시 실행한다.

이 복사본은 앱에 없는 C# 컴포넌트를 제외하므로 해당 스크립트가 구현한 표정 UI·머리카락 물리·IK 등의 기능도 빠진다. 플레이어 이동은 CharacterController가 담당하므로 모델의 Root Motion도 끈다. 해당 C# 기능이 필요하다면 SDK에 구현해 앱을 다시 빌드하거나 지원되는 TypeScript API로 옮겨야 한다. 외부 C#에 의존하는 Animator StateMachineBehaviour 등은 자동 삭제하지 않고 오류로 안내한다. 이 기능은 플레이어의 Model Prefab을 대상으로 하며, 다른 월드 오브젝트의 스크립트를 일괄 제거하지 않는다.

플레이어 Inspector에서 **애니메이션 Profile 만들기 · 연결**을 누른 뒤 모델의 Idle / Walk / Run / Jump 클립을 직접 넣는다. 실제 이동 속도와 접지 상태에 따라 클립을 전환하며, Play 디버그에서 입력·속도·충돌·애니메이션 상태를 확인할 수 있다. 자세한 임포트·연결 절차는 [애니메이션 가져오기 안내](animation-import-guide.md)를 참고한다.

바닥의 Collider가 눈에 보이는 표면과 맞는지도 확인한다. 특히 Cylinder를 납작하게 줄인 뒤 기본 CapsuleCollider를 남기면 플레이어가 떠 보일 수 있다. 직접 만든 바닥에는 모양에 맞는 BoxCollider나 정적인 MeshCollider를 설정한다. 구형 기본 월드의 특정 원통 바닥은 앱이 실행 중 보정하지만, 임의로 제작한 Collider까지 자동 보정하지는 않는다.

| 입력 | 동작 |
|---|---|
| 왼쪽 아래 조이스틱 | 카메라 기준 이동 |
| 오른쪽 아래 JUMP | 바닥에서 점프 |
| 오른쪽 빈 화면 드래그 | 카메라 방향 변경 |
| 에디터 W/A/S/D 또는 방향키 | 이동 |
| 에디터 Space | 점프 |
| 에디터 오른쪽 빈 화면에서 마우스 오른쪽 드래그 | 카메라 방향 변경 |

**3인칭 추적 카메라**를 켜면 캐릭터가 이동 방향을 바라보고 카메라는 별도로 따라간다. 조이스틱을 조금 기울이면 걷고 크게 기울이면 달리며, Profile의 기준 속도와 전환 속도로 조절한다. Profile이 없으면 기존 Animator Controller를 유지하며 자동 동작 전환은 하지 않는다. FBX의 리그·클립·재질·셰이더는 모델에 맞게 준비해야 한다. 네트워크 멀티플레이는 아직 제공하지 않으며, 배포 앱에 없는 새 C# 컴포넌트는 콘텐츠 번들만으로 추가되지 않는다.

최종 APK, 자동 검사 결과와 SM-G955N 실기기의 이동·점프·화면 전환 증거는 [구현·검증 보고서](reports/2026-09-19-mobile-controls-server.md)에 정리했다.
