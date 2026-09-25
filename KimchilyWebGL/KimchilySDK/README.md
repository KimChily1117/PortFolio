**Kimchily Creator SDK — TypeScript 제작과 Unity 월드 실행**

현재 제작 흐름은 [KimchilyCreator](../KimchilyCreator/README.md)의 `.ts` 클래스·모델 → 타입 검사 및 JavaScript 모듈 임포트 → Build & Publish → QR/링크 → 네이티브 Android 앱과 Unity 실행기다. TypeScript 5.9.3과 `.d.ts`로 타입 제안을 제공하고, 플레이어에서는 Jint가 생성된 JavaScript를 실행한다. Lua 코드를 생성하는 방식이 아니다.

앱 구성은 **별도 네이티브 Android 앱에서 필요할 때 Unity 월드를 전체 화면으로 실행**하는 방식이다. 게시 서버는 [KimchilyPublish](../KimchilyPublish/README.md), 호스트 앱은 [KimchilyAndroid](../KimchilyAndroid/README.md), 실행기는 [KimchilyUnityRuntime](../KimchilyUnityRuntime/README.md)다. 기존 MoonSharp Lua 패키지는 호환성 유지를 위해 남겨둔다.

| 소스 UPM 패키지 | 역할 |
|---|---|
| `com.kimchily.creator` | 코루틴 스케줄러, 씬 의존성 수집, 콘텐츠 빌드·게시·다운로드 검증 |
| `com.kimchily.typescript` | `.ts` importer, 타입 선언, Inspector 공개 필드, 제한된 Unity API, Jint 실행기 |
| `com.kimchily.scripting` | 기존 MoonSharp Lua Behaviour와 API 호환 경로 |

시작 예제는 [Character.ts](../KimchilyCreator/Assets/World/Character.ts)다. `public beacon`, `speed`, `blinkSeconds`가 Inspector 대상이고 private `Map`은 내부 상태다. **Kimchily → TypeScript → Configure Type Completion** 메뉴와 제작 프로젝트의 VS Code 설정을 사용한다. 구현·설정·API 표·검증 상태는 [2026-09-19 TypeScript 기록](../docs/reports/2026-09-19-typescript-runtime.md)에 정리했다.

TypeScript 컴파일러 **19개**, Jint VM **22개**, JS facade **17개**, strict 타입 정상/오류 사례와 실제 LanguageService 자동완성을 확인했다. Unity PlayMode **54개**·EditMode **12개**, Creator 씬 전환·Android 콘텐츠 빌드·게시, ARM64 IL2CPP 통합 APK 빌드도 통과했다. **SM-G955N / Android 9**에서 TypeScript revision 1→2를 APK 재설치 없이 같은 프로세스로 실행했으며 FBX 회전·이동·generator beacon 전환·퇴장 후 홈/캐시 정리·새 링크 재입장을 확인했다. 실제 ZXing QR 해독은 통과했고 이번 TypeScript QR의 물리 카메라 촬영은 수행하지 않았다. APK SHA·로그·화면과 검사 범위는 [TypeScript 검증 기록](../docs/reports/2026-09-19-typescript-runtime.md)에 있다. 아래 2026-09-18 xLua/Windows·Lua/Android 결과를 이번 TypeScript 검증으로 합산하지 않는다.

**현재 구현**

- 실제 TypeScript 5.9.3 strict 검사, ES2018 CommonJS 출력, 상대 `.ts` 모듈과 source map 임포트.
- `.d.ts` 타입 제안과 공개 number/string/boolean/GameObject/Transform/Vector3 Inspector override.
- Jint 기반 Behaviour lifecycle, 소유 객체·명시 참조만 전달하는 Unity API, generator 코루틴, 로컬 typed Event.
- Coroutine 시작/취소/owner별 취소, 중첩 IEnumerator, Unity yield 전달, 예외 수집 및 Dispose.
- 스케줄러 비활성화/파괴, owner 파괴 시 작업 정리.
- Lua처럼 취소 시 함수 뒤쪽을 실행하지 않는 어댑터를 위한 명시적 cleanup 콜백.
- 저장된 씬에서 프리팹·FBX·메시·재질·텍스처·애니메이션 등의 의존성 수집.
- 기존 assetBundleName 라벨을 바꾸지 않는 명시적 번들 빌드 맵.
- `world.json`에 시작 씬, SDK/Unity/플랫폼/렌더 파이프라인, 필요 C# 타입, 실제 번들 의존성·SHA-256·CRC·크기 기록.
- 다운로드 완료된 로컬 콘텐츠의 호환성·파일 검증과 의존성 순서 로드.
- Unity 메뉴 `Kimchily > Creator SDK`의 Validate / Build Content.
- Creator, World, ToolManager의 manifest에서 동일한 로컬 UPM 패키지 참조.
- ToolManager의 `Assets/Editor/KimchilyCoroutineBindings.cs`에 xLua 바인딩 생성 설정 추가.

**검증 결과**

아래 표는 이름 변경 전에 수행한 xLua/Windows 검증 기록이다. Kimchily 전환 후의 재검증 범위와 결과는 [전환 기록](../docs/reports/kimchily-rebrand-2026-09-18.md)을 참고한다. 이전 실행 로그와 배포 산출물은 원본 그대로 보존한다.

| 검증 | 결과 |
|---|---|
| Runtime/Editor DLL 컴파일 | Unity 2022.3.16f1의 실제 참조 어셈블리로 성공, 오류·경고 0 |
| Unity 테스트·검증 스크립트 컴파일 | dotnet 컴파일 성공, 오류·경고 0 |
| Unity 테스트 실행 합계 | 26/26 통과, 실패·skip·inconclusive 0 |
| Unity 비의존 관리 코드 테스트 | 17/17 통과 |
| Unity EditMode 테스트 | 6/6 통과: 의존성 수집, 기존 라벨·씬 상태 보존, 잘못된 입력 거절 |
| Unity PlayMode 테스트 | 20/20 통과: Coroutine 12, manifest 6, Lua 1, FBX 왕복 1 |
| 실제 FBX 번들 생성·재입장 | Windows64 번들 생성 성공, 메시·본·재질·텍스처·Animator·계층 참조 및 두 번 입퇴장 확인 |
| 실제 Lua/Coroutine 연결 | 기존 xLua DLL로 실행·cleanup·LuaEnv 종료까지 통과 |
| Unity 2021.3 / Android / iOS | 실행·빌드 미검증 |

원래 프로젝트의 Unity 버전은 변경하지 않았다. 패키지는 기존 2021.3 프로젝트를 대상으로 작성했지만 2021.3 Editor 검증은 별도로 필요하다. 검증용 프로젝트만 현재 설치된 2022.3.16f1을 사용한다.

2026-09-18 재실행 결과다. Windows Editor의 batchmode/nographics 검증이며, 독립 Player 빌드·화면 렌더링·모바일·바이너리 전용 UPM 배포의 검증을 의미하지 않는다. 자세한 결과는 [Unity 실행 검증 리포트](../docs/reports/kimchily-sdk-unity-verification-2026-09-18.md)에 기록했다.

**C# Coroutine 스케줄러 사용**

```csharp
var scheduler = gameObject.AddComponent<Kimchily.Creator.CoroutineScheduler>();
var handle = scheduler.StartRoutine(Routine(), gameObject);
// handle.Cancel(); 또는 scheduler.CancelOwnedBy(gameObject)
```

`CoroutineHandle`도 yield할 수 있다. 완료/취소/실패는 `Status`, 예외는 `Exception`에서 확인한다.

C# 스케줄러는 Unity yield를 전달한다: 다음 프레임(null), WaitForSeconds, WaitForSecondsRealtime, WaitForFixedUpdate, WaitForEndOfFrame, WaitUntil/While, AsyncOperation, 다른 Coroutine. 이 목록은 C# API의 범위다. **현재 TypeScript API는 generator가 yield하는 null/undefined와 WaitForSeconds만 지원**하며 Coroutine 핸들 자체를 yield하지 않는다. C# 프레임 진행·실시간 대기·취소·예외 정리는 기존 PlayMode에서 검증했다. 모든 yield의 단계별 타이밍을 비교한 것은 아니며, 특히 WaitForEndOfFrame의 Unity batchmode 제약과 렌더링 단계는 별도 실행환경에서 확인해야 한다.

SDK 정책상 scheduler를 disable하면 모든 작업을 취소한다. owner의 단순 disable은 자동 취소 조건이 아니므로 스크립트 onDisable에서 handle.Cancel을 호출한다. owner 파괴는 다음 Update에서 취소된다. 취소는 대기하던 AsyncOperation 자체를 중단하는 기능이 아니다.

C# IEnumerator의 IDisposable/finally는 정리한다. 기존 xLua cs_generator의 중단은 Lua 함수 뒷부분을 실행하지 않으므로 `CoroutineRoutine.WithCleanup`을 사용하거나 lifecycle에서 정리한다. LuaEnv를 Dispose하기 전에 scheduler.CancelAll을 호출해야 한다.

기존 xLua의 Mono 검증에서는 callback이 실행된 프레임 안에 LuaEnv를 해제하면 남은 C# callback 참조로 종료가 거절됐다. 검증 코드에서 참조를 해제하고 다음 프레임의 GC/Tick 이후 Dispose하여 종료까지 확인했다. 월드 실행기에서도 callback 해제와 VM 종료 순서를 관리해야 한다.

구형 xLua 예제는 패키지 Samples의 `coroutine_example.lua.txt`에 있다. 해당 원본 프로젝트에서는 누락 importer·SDK 복원과 모바일 바인딩 재생성이 별도로 필요하다. 새 제작 프로젝트의 TypeScript는 `com.kimchily.typescript`, 기존 MoonSharp Lua는 `com.kimchily.scripting`을 사용한다. 코어 스케줄러와 각각의 스크립트 어댑터를 구분한다.

**FBX와 씬 호환의 의미**

UPM은 SDK 설치 방식이고, 월드 콘텐츠 전달은 AssetBundle이 담당한다. 실행 앱에서 원본 FBX를 새로 파싱하는 방식이 아니다. Unity가 임포트한 모델·스켈레톤·메시·재질·애니메이션 및 씬/프리팹 참조를 번들에 넣는다.

지원 설계는 다음 조건을 따른다.

- Unity에 정상 임포트되고 씬 또는 Additional Assets로 참조된 자산을 의존성 기준으로 수집한다.
- AnimatorController·LightingDataAsset처럼 Editor에서 제작하지만 플레이어에 필요한 자산도 포함한다.
- 문자열로만 불러오는 Lua require/Resources.Load 등은 정적으로 찾을 수 없으므로 Additional Assets에 명시한다.
- C# 클래스/DLL 구현은 콘텐츠 번들로 전달되지 않는다. manifest의 requiredTypes가 실행 앱에 있어야 한다.
- 빌드 플랫폼·SDK·Unity 버전·렌더 파이프라인을 맞춘다. 첫 버전은 정확히 같은 Unity/SDK 버전을 요구한다.
- 임의의 Shader, 외부 네이티브 플러그인, 플랫폼 전용 컴포넌트의 보편적 호환을 보장하지 않는다.
- 정적 타입 존재 검사는 모든 커스텀 컴포넌트의 직렬화 호환성을 증명하지 않는다.

콘텐츠는 선택한 씬과 별도 공유 자산 번들로 빌드된다. 기존 7개 고정 종류 이름에 의존하지 않는다. 출력은 매번 새 revision 디렉터리를 만들며, 성공했을 때만 `world.json`을 기록한다. 기존 출력이나 원본 자산을 재귀 삭제하지 않는다.

`WorldContentSession.OpenLocal`은 이미 내려받은 콘텐츠용 개발 로더다. 해시 계산·번들 로드가 동기 방식이므로 대형 모바일 콘텐츠용 비동기 다운로드/캐시는 후속 작업이다. 씬을 먼저 unload하고, 직접 생성한 객체를 제거한 뒤 session.Dispose를 호출한다.

**에디터에서 사용**

1. 원래 프로젝트의 누락 SDK/importer와 컴파일 오류를 복원한다. 이번 코어 추가가 기존 오류를 모두 해결한 것은 아니다.
2. `Kimchily > Creator SDK`를 연다.
3. 저장한 시작 씬과 필요한 Additional Assets를 지정한다.
4. Validate로 누락 스크립트·참조와 의존성을 확인한다.
5. Unity의 현재 플랫폼과 Build Target을 일치시킨 뒤 Build Content를 누른다.
6. 출력 revision 폴더의 world.json과 번들 파일을 확인한다.

열린 대상 씬이 dirty면 빌드를 차단한다. 검사에 필요한 닫힌 씬은 additive로 열어 읽고 닫으며, 기존 활성 씬을 복구한다. EditMode 테스트에서 기존 라벨·활성 씬·씬 개수 보존과 dirty 씬을 저장하거나 닫지 않는 동작을 확인했다.

**컴파일 및 검사 재실행**

PowerShell, 작업 폴더 `E:/task/Unity_Project`:

```powershell
dotnet build KimchilySDK/Build/Tests/Kimchily.CompileTests.csproj --configfile KimchilySDK/Build/NuGet.Config
dotnet run --project KimchilySDK/Build/PureTests/Kimchily.PureTests.csproj --configfile KimchilySDK/Build/NuGet.Config
python KimchilySDK/tools/static_audit.py
```

컴파일은 설치된 Unity 2022.3.16f1 어셈블리와 로컬 NUnit/TestRunner를 참조한다. 다른 경로는 `-p:UnityEditorRoot=...`로 지정할 수 있다. NUnit/TestRunner 경로도 설치 버전에 맞춰 확인한다. NuGet 온라인 복원은 필요하지 않다.

검증 DLL은 `Artifacts/Managed/Runtime/Kimchily.Creator.Runtime.dll`, `Artifacts/Managed/Editor/Kimchily.Creator.Editor.dll`에 생성된다. 이미 소스 UPM을 참조하는 프로젝트에 이 DLL을 다시 복사하면 같은 타입이 중복되므로 함께 설치하지 않는다. 바이너리 전용 UPM 배포는 GUID/MonoScript 전환·바인딩·Unity 버전 검증 후 진행한다.

**Unity 실행 검증 재실행**

`tools/prepare_validation.py`는 이 워크스페이스의 기존 FBX·xLua DLL을 검증 프로젝트에 복사한다. 해당 외부 자산은 SDK 배포 패키지에 포함하지 않는다.

```powershell
python KimchilySDK/tools/prepare_validation.py
powershell -ExecutionPolicy Bypass -File KimchilySDK/tools/run_unity_tests.ps1
```

Unity 라이선스가 활성화된 상태에서 실행한다. 스크립트는 FBX가 포함된 샘플 씬을 빌드하고 EditMode/PlayMode 테스트를 수행한다. 원본 세 프로젝트 대신 ValidationProject만 사용한다. `-Stages EditMode` 또는 `-Stages PlayMode`로 단계별 재실행도 가능하다. XML 생성 시각과 Passed 결과, 최소 테스트 개수, skip·inconclusive·실패 여부를 검사하여 이전 결과나 누락된 테스트를 성공으로 처리하지 않는다.

결과는 `Artifacts/fixture-build.log`, `Artifacts/editmode.xml`, `Artifacts/playmode.xml`에 있다. `Artifacts/world-fixture-path.txt`는 마지막으로 빌드한 콘텐츠 revision 경로를 가리킨다. 준비 스크립트는 검증 전용 DLL importer 설정과 기존 DLL에 필요한 UGUI·ParticleSystem 의존성도 구성한다.

**다음 구현 경계**

새 제작·게시·다운로드·QR 경로는 별도 KimchilyCreator/KimchilyPublish/KimchilyAndroid/KimchilyUnityRuntime에 구현했다. 기존 World 앱의 Play 버튼과 구형 manifest는 그대로 유지한다. TypeScript 실행기 도입 시 앱에 새 Runtime·Jint와 의존성을 포함해야 하며, 이후 설치된 API 범위의 스크립트와 모델 변경은 콘텐츠로 게시한다. 로그인·클라우드 운영·제작자 권한·영구 캐시·멀티플레이·아바타/캐릭터 제어 API는 후속 범위다.
