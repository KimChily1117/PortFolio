**Kimchily Creator SDK 0.1.0**

Coroutine 실행과 씬 의존성 기반 콘텐츠 빌드·게시를 제공하는 개발용 UPM 패키지다.

- Runtime: 언어 독립 CoroutineScheduler / CoroutineHandle / CoroutineRoutine.
- Content: manifest·번들 다운로드, 버전·플랫폼·타입·해시 검증, 로컬 번들 세션과 정리.
- Editor: `Kimchily > Creator SDK`의 Validate / Build Content / Build & Publish / Publish Last Build. 게시 완료 후 QR·앱 링크 표시.
- Samples: Coroutine C# 예제와 기존 xLua 연결 템플릿. 현재 게시용 Lua 컴포넌트는 별도 [com.kimchily.scripting](../com.kimchily.scripting/README.md) 패키지가 제공한다.

FBX는 Unity에서 임포트한 모델과 씬에 참조된 메시·머티리얼·텍스처·애니메이션·프리팹 의존성을 함께 빌드한다. 현재 QR 게시 대상은 Unity **2022.3.16f1**, Android, Built-in Render Pipeline, 단일 진입 씬이며 최대 64개 번들·256 MiB를 지원한다. 개별 셰이더와 렌더링 품질은 실제 Android 기기에서 확인해야 한다.

SDK와 콘텐츠 AssetBundle의 역할은 구분된다. SDK C# 코드는 APK에 미리 포함되어야 하며 번들로 새 C# 구현을 배포하지 않는다. 지원하는 Lua API와 기존 SDK 컴포넌트 범위 안에서 모델·씬·Lua를 변경하면 같은 APK로 새 revision을 실행할 수 있다. 새 C# 컴포넌트나 SDK API는 실행기 업데이트가 필요하다. 일반 Validate는 콘텐츠 의존성을 검사하고, Build & Publish는 게시 대상 플랫폼·렌더 파이프라인·허용 C# 타입도 검사한다.

`Additional Assets`는 씬에서 직접 참조하지 않지만 **기존 실행기에 포함된 C# 로더**가 사용할 리소스를 빌드에 추가하는 목록이다. 번들에 포함하는 것만으로 `Resources.Load` 등에서 자동으로 검색되지는 않는다. Lua 0.1에는 `require`, `Resources.Load`, 임의 파일·네트워크 로딩 API가 없다. Lua 파일은 `KimchilyLuaBehaviour.ScriptAsset`에, 조작할 오브젝트는 `References`에 연결한다.

2026-09-18 기준 Unity Runtime 프로젝트의 PlayMode 테스트 42개가 실패·skip 없이 통과했다. Coroutine·manifest·Lua 생명주기·실행 예산·취소·원격 다운로드 실패와 정리를 포함한다. Android IL2CPP export 생성과 실제 기기에서 게시 월드·Lua를 실행하는 검증은 별도로 구분한다. 최신 실행 결과는 [Runtime 안내](../../../KimchilyUnityRuntime/README.md), 제작·게시 절차는 [Creator 안내](../../../KimchilyCreator/README.md)를 참고한다. 기존 xLua/Windows 검증 기록은 [SDK 안내](../../README.md)에 남아 있다.
