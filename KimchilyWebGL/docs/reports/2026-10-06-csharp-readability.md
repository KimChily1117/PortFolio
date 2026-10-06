# 2026-10-06 — UGC C# 가독성 정리

## 요청과 범위

최신 C# 축약 문법과 한 줄에 붙은 실행문을 읽기 어려워하는 사용자 요청에 따라, 직접 작성한 UGC C# 코드를 일반적인 생성자·함수·중괄호 블록 중심으로 정리했다.

`E:\task\KimchilyWebGL`에서 수정·검증했다. 시작 시 대상 C# 97개가 Git 관리본과 같은 내용인지 확인하고 별도 Task 백업을 남겼다. 실제 수정은 C# 91개이며, 이미 같은 형식인 6개는 그대로 유지했다.

| 구분 | 변경한 C# 파일 | 주요 내용 |
|---|---:|---|
| KimchilyServer | 12 | 명시적 Program.Main·생성자·필드, block namespace, 명시적 생성 타입, 방·소켓·작업 큐·VM·검사 코드의 분기와 함수 간격 |
| KimchilySDK | 69 | Creator·Networking·TypeScript·Lua의 한 줄 메서드·프로퍼티·제어문 전개, 필드 분리, 긴 호출·조건식 줄바꿈, 기존 검사 코드 정리 |
| KimchilyUnityRuntime | 8 | 호스트·웹 브리지·빌더·기존 검사의 블록 및 처리 단계 간격 |
| KimchilyCreator | 2 | 씬·아트 생성 도구의 제어문·초기화·함수 간격 |

통신 DTO의 record 값 비교, with 복사, init 및 JSON 필드 계약은 유지했다. 서버 SDK, Unity, 패키지 버전과 TypeScript 게임 규칙은 변경하지 않았다. 외부 라이브러리·원본 Android 프로젝트·Project Dawn·Lyra는 수정 범위에 포함하지 않았다.

편집기 설정은 [`.editorconfig`](../../.editorconfig), 향후 작성 기준은 [C# 스타일 안내](../csharp-style.md)에 기록했다.

## 검증

기존 검사를 다시 실행했다. 새 기능이나 성능 개선을 검증한 결과로 해석하지 않는다.

| 검사 | 결과 | 확인 근거 |
|---|---|---|
| 서버 Release 빌드·회귀 검사 | 경고 0, 오류 0 / 57 passed, 0 failed | `KimchilyServer/Artifacts/checks/20261006-144726/results.json` |
| Creator managed 검사 | 17/17 통과 | `dotnet run --project KimchilySDK/Build/PureTests/Kimchily.PureTests.csproj` |
| TypeScript/Jint VM 검사 | 34개 통과 | `com.kimchily.typescript/Tools~/VmChecks` |
| Lua VM 검사 | 16개 통과 | `com.kimchily.scripting/Tools~/VmChecks` |
| SDK Unity fixture-build | 성공 | `KimchilySDK/Artifacts/fixture-build.log` |
| SDK Unity EditMode / PlayMode | 45 / 57개 통과 | `KimchilySDK/Artifacts/editmode.xml`, `playmode.xml` |
| 공통 실행기 Unity EditMode / PlayMode | 60 / 121개 통과 | `KimchilyUnityRuntime/Artifacts/runtime-editmode.xml`, `runtime-playmode.xml` |
| WebGL 분기 관리 코드 컴파일 | 오류 0, 경고 6 | `KimchilyUnityRuntime/Tools~/WebChecks/WebCompile.csproj` |

공통 실행기 검사는 채팅 입력·TMP·말풍선·원격 위치, 스크립트 상태·HUD, 월드 로딩·정리 등 기존 테스트를 포함한다. 변경한 Unity C#의 Editor/WebGL/Standalone 조건부 분기를 구문 검사하고, 원래 문자열·수치·주석과 임베디드 JS/Lua 코드가 보존되는지도 비교했다. 서버의 생성자 캡처, WebSocket 종료 순서, JSON 제한과 DTO 계약은 변경 전후를 별도로 검토했다.

## 검증 도구 보완

기존 `WebCompile.csproj`에는 Networking 런타임 소스와 UI 참조가 빠져 있어 `Kimchily.Networking` 네임스페이스를 찾지 못했다. 해당 소스, TMP·uGUI 어셈블리 및 WebGL 플랫폼 모듈 참조를 추가해 실제 WebGL 전처리 분기도 컴파일하도록 보완했다.

새 체크아웃에서는 설정된 Unity 버전의 WebGL 모듈을 설치하고 `KimchilyUnityRuntime`을 한 번 임포트해야 한다. 이때 만들어지는 `Library/ScriptAssemblies/Unity.TextMeshPro.dll`, `UnityEngine.UI.dll`을 검사 도구가 사용한다. 캐시 DLL은 Git에 넣지 않는다.

이번에는 게시 WebGL 전체 재빌드·재게시, 운영 서버 재시작과 새 실기기 플레이를 수행하지 않았다. 기존 게시 revision과 데모의 기능 범위는 이전 기록을 유지한다.
