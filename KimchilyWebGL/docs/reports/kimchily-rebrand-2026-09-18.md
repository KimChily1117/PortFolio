# Kimchily 이름 변경 기록

요청에 따라 워크스페이스의 서비스명을 Kimchily로 통일했다. 실제 작업 폴더, UPM 이름, C# 어셈블리·네임스페이스, xLua 바인딩, Android 패키지·Activity·JNI, Unity 메뉴, QR 링크, 빌드 도구와 문서를 함께 전환했다.

## 프로젝트와 규약

현재 프로젝트는 `KimchilyCreator`, `KimchilySDK`, `KimchilyUnityRuntime`, `KimchilyAndroid`, `KimchilyPublish`, `UnityKimchilyCreator`, `UnityKimchilyWorld`, `UnityToolManager`다. 다른 게임 프로젝트에는 변경 대상 서비스명이 없었다.

- 새 제작 UPM: `com.kimchily.creator`, `com.kimchily.scripting`.
- 기존 도구 UPM: `com.kimchily.xlua`, `kimchily.creator.tool`.
- Android 앱 ID: `com.kimchily.app`.
- Unity bridge: `KimchilyHostBridge` ↔ `com.kimchily.app.UnityHostBridge`.
- QR의 앱 링크: `kimchily://world?manifest=…&sha256=…`.
- Unity 메뉴: `Kimchily → Creator SDK`, `Kimchily → Publish World`.

중앙 이행 대상은 작성된 텍스트 186개와 경로 143개였다. CP949 파일은 인코딩을 보존해 수정했으며 `.meta` GUID는 유지했다. DLL 타입명 변경으로 달라지는 MonoScript fileID도 별도로 검증·변경했다. `com.unity3d.player` 등 Unity 자체 규약과 MoonSharp 등 외부 라이브러리의 이름·라이선스는 유지했다. 캐시·과거 산출물·백업을 제외한 작성된 텍스트의 이전 브랜드 검색 결과는 0건이다.

## 새 제작·실행 경로 검증

| 항목 | 결과 |
| --- | --- |
| Core Runtime/Editor/검증 코드 및 Lua 관리 코드 컴파일 | 오류·경고 0 |
| Core 순수 관리 테스트 | 17/17 통과 |
| Lua VM 검증 | 18개 통과, 공개 API 1,744개 유지 |
| 새 Unity 실행기 PlayMode | 42/42 통과, 실패·skip 0 |
| 별도 SDK 검증 프로젝트 | FBX 번들 생성 성공, EditMode 6/6 및 PlayMode 21/21 통과 |
| Android 단독·Unity 통합 구성 | 각 26/26 통과 |
| 게시 서버 HTTP 테스트 | 13/13 통과 |
| Unity Android 실행기 export | 성공 |
| Android 통합 APK | 빌드 성공, 패키지·Activity·JNI·내장 어셈블리 이름 확인 |
| Creator Android 번들 빌드 | 성공 |
| Unity Editor의 게시 클라이언트 | 업로드·QR 이미지 다운로드 성공 |

제작 프로젝트와 SDK 검증 프로젝트의 이전 Unity 캐시에 남은 UPM/DLL 경로 때문에 첫 번들 빌드가 실패했다. 두 `Library`를 백업으로 옮긴 뒤 새 캐시로 빌드해 해결했다. 통합 APK에서도 이전 절대 경로를 가진 IL2CPP 캐시만 격리해 다시 빌드했다. 원본 Assets·씬·모델·Lua는 유지했다.

최종 게시 revision은 `20260918T143533635Z-e5604d16`이며 manifest SHA-256은 `4eaa639e4b5bbb52fc696913b37454ce92b56714d47c3555a77b5501b063c187`다. manifest의 두 필요 타입은 `Kimchily.Creator.CoroutineScheduler`, `Kimchily.Scripting.KimchilyLuaBehaviour`다. 서버에서 다시 받은 manifest와 번들 두 개의 크기·SHA도 일치했다.

[게시 페이지](http://192.168.0.4:8787/w/my-first-world/20260918T143533635Z-e5604d16), [Editor 게시 결과](../../KimchilyCreator/Artifacts/publish-result.json), [Unity 테스트 결과](../../KimchilyUnityRuntime/Artifacts/runtime-playmode.xml).

## QR와 실제 Android 실행

링크 이름 변경 후 일부 QR을 ZXing 3.5.3이 놓치는 회귀가 발견됐다. 데이터 영역의 가짜 finder 후보를 선택하는 문제였으며, 검출 테스트를 완화하지 않고 게시 서버와 fixture의 QR 생성 설정을 오류정정 M / mask 0으로 맞췄다. 15개 payload × 3크기 × 4방향 180개가 통과했다. 최종 새 revision도 실제 서버 PNG로 12/12 검출과 링크 전체·WorldLink 필드 일치를 확인했다. 모든 QR·기기·촬영 조건에 대한 보장은 아니다.

최종 APK는 [kimchily-unity-debug.apk](../../KimchilyAndroid/Artifacts/kimchily-unity-debug.apk), 29,610,920 bytes, SHA-256 `0d9e0e02fb31054a11002967e02aa1499c796f35fbd05e5c54e3e4ab415bb366`다.

SM-G955N / Android 9에 새 앱을 설치하고 `kimchily://` 링크로 최종 게시 월드에 입장했다. 실제 FBX 표시, Lua 시작, 받은 manifest SHA 일치, 퇴장 후 Kimchily 네이티브 홈 복귀와 다운로드 임시 폴더 비움을 확인했다. 새 앱의 물리 카메라 촬영은 재실행하지 않았으며, 이전 앱의 사용자 촬영 성공 기록과 구분한다.

[기기 검증 결과](../../KimchilyAndroid/Artifacts/device-kimchily-verification.json), [월드 화면](../../KimchilyAndroid/Artifacts/device-kimchily-world.png), [앱 실행 로그](../../KimchilyAndroid/Artifacts/device-kimchily-world.log), [QR 검증 결과](../../KimchilyAndroid/Artifacts/qr-decode-kimchily-final.json).

## 기존 DLL·씬 참조

기존 DLL 4개는 각 원본을 독립적으로 이행했다. Manager와 World의 서로 다른 타입·메서드 구성을 유지하고 API·IL의 이름 치환 외 변경이 없음을 검사했다. Windows 파일 속성과 런타임 메타데이터에도 이전 이름이 남지 않도록 처리했다. `.meta` 바이트는 원본과 동일하며 씬·프리팹 3개에서 DLL fileID 6개만 갱신했다. 별도 검토에서도 원본·결과 해시와 변경 범위가 일치했다.

기존 DLL 소스 프로젝트 2개와 World C# 스크립트도 컴파일했다. 기존 프레임워크 경고 3개와 World 경고 1개가 남아 있다. 기존 프로젝트의 누락된 GUID/fileID나 생성되지 않은 번들까지 복구한 것은 아니다. 구 프로젝트 전체의 Unity 2021 실행은 확인하지 않았다. 상세 범위는 [기존 프로젝트 이행 기록](../../UnityToolManager/LEGACY_REBRAND.md)에 있다.

## 보존한 자료와 사용 시 주의점

변경 전 APK·QR PNG·별도 기기 증거·배포 manifest와 번들은 역사적 검증 자료로 보존했다. 불변 revision의 내용을 문자열 치환하지 않았다. 공통 최신 빌드 로그와 테스트 결과 파일은 이번 실행 결과로 갱신했다. 작성된 소스의 원본, 경로 계획과 캐시 백업은 `.kimchily-migration`에 있다. 생성 캐시와 과거 산출물에는 이전 이름이 남을 수 있다.

이름 변경은 앱 ID와 SDK 타입에 영향을 주므로 새 앱과 새로 빌드·게시한 월드를 함께 사용한다. 이전 앱의 사용자 데이터는 삭제하지 않았다. Unity Hub에서 새 경로 `KimchilyCreator`를 추가해 연다.

기존 Unity 2021 프로젝트를 2022로 강제 업그레이드하지 않았다. 현재 제작 경로의 Unity 2022 검증과 기존 프로젝트의 버전별 실행 검증은 별개다.
