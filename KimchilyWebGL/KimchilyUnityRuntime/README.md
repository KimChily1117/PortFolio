**Kimchily Unity World 실행기 — Android 호스트 통합용**

현재 WebGL 실행기는 Unity 6000.3.24f1을 사용한다. 아래 Unity 2022.3.16f1/Android 검증 내용은 이전 기준본의 이력이다. 네이티브 앱 프로젝트는 [KimchilyAndroid](../KimchilyAndroid/README.md)이며, 원래 `UnityKimchilyWorld`를 직접 업그레이드하거나 교체하지 않았다.

`KimchilyHostBridge`가 Bootstrap에 상주하고, 네이티브 요청으로 내장 `DemoWorld` 또는 게시 서버의 월드를 additive 로드/언로드한다. 게시된 씬은 APK에 미리 포함하지 않는다. 다운로드 manifest와 번들의 SHA-256·크기·CRC·SDK/Unity 버전을 확인한 뒤 로드한다. Coroutine을 취소하고 씬·번들을 정리한 후 `WorldClosed`를 알린다. 제작은 별도 [KimchilyCreator](../KimchilyCreator/README.md) 프로젝트에서 한다.

새 제작 경로는 **TypeScript → 생성된 JavaScript를 담은 TypeScriptAsset → Jint 실행**이다. `com.kimchily.typescript`는 Behaviour별 VM에서 lifecycle·명시적인 Unity 객체 API·generator 코루틴을 제공한다. Node.js와 TypeScript 컴파일러는 제작 PC에서 사용하며 Android에서 TypeScript를 직접 컴파일하지 않는다. 기존 `com.kimchily.scripting` Lua 실행기는 호환성 유지를 위해 남겨둔다.

TypeScript 최초 도입에는 이 패키지와 Jint 의존성을 포함한 새 Unity export·APK가 필요하다. 이후 지원 API 안의 `.ts`·모델 수정은 새 콘텐츠 revision으로 전달한다. 구현과 제작 방법은 [TypeScript 기록](../docs/reports/2026-09-19-typescript-runtime.md), 실제 입력 예제는 [Character.ts](../KimchilyCreator/Assets/World/Character.ts)를 참고한다. TypeScript의 Unity·Android 실행 검증 상태는 기존 Lua 결과와 별도로 기록한다.

**연결 규약**

Android → Unity는 `UnityPlayer.UnitySendMessage("KimchilyHostBridge", "Receive", json)`를 사용한다. C#의 Receive는 명령을 큐에 보관하고 Update에서 Unity 객체를 조작한다.

Unity → Android는 `com.kimchily.app.UnityHostBridge.onUnityEvent(String)`를 호출하며 Android 쪽이 UI 스레드로 전달한다. Editor 테스트는 동일한 이벤트를 `EventRaised`로 관찰한다.

| 방향 | 메시지 |
|---|---|
| Android → Unity | Initialize, OpenWorld, CloseWorld |
| Unity → Android | RuntimeReady, WorldProgress, WorldReady, WorldFailed, WorldClosed, CloseRequested |

JSON 필드는 `protocolVersion=1`, `type`, `requestId`, `worldId`, `revisionId`, `message`, `code`, `progress`다. OpenWorld에 `manifestUrl`·`manifestSha256`이 있으면 게시된 월드를 다운로드한다. 없으면 내장 `demo / builtin-v1`을 연다. Open과 Close는 서로 다른 요청 ID를 사용한다. 다운로드 중 Close는 즉시 요청을 중단하고 임시 파일을 정리한다. 씬 로딩 중에는 취소할 수 없는 Unity 로드를 마친 뒤 unload하며, 이전 Open에 CANCELLED, Close에 WorldClosed를 순서대로 보낸다.

동시에 한 월드만 허용하고 추가 Open은 BUSY로 거절한다. 여러 Close에는 각 요청 ID로 완료를 알린다. 일반 퇴장에는 `Application.Quit`이나 런타임 unload를 호출하지 않는다. 이 버전은 warm runtime 재사용을 우선 검증한다.

TypeScript 컴포넌트의 컴파일 상태·API 버전·모듈·공개 필드 바인딩을 콘텐츠 검증에 연결한다. 월드 초기화 시 스크립트 오류가 있으면 `WorldReady` 이전에 실패를 알리는 경로를 구현했다. Behaviour 비활성화·파괴·재로드·오류 시 소유 코루틴을 취소하고 VM 수명과 정리 순서를 관리한다. 실제 Unity 검증 결과는 아래에서 기존 Lua 실행 기록과 구분한다.

**준비·실행**

워크스페이스 루트에서 다음을 실행한다.

```powershell
python KimchilyUnityRuntime/tools/prepare_runtime.py
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyUnityRuntime/tools/export_android.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyUnityRuntime/tools/test_runtime.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyUnityRuntime/tools/test_runtime.ps1 -Platform EditMode
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/build.ps1 -WithUnity
```

Editor에서도 `Kimchily > Android > Prepare Sample Scenes`와 `Export Unity Library` 메뉴를 제공한다. export는 Android 플랫폼에서 실행한다. 준비 스크립트는 FBX와 로컬 Test Framework를 구성하고, export는 생성 전용 `Assets/Generated`에 Bootstrap/DemoWorld를 만들고 Build Settings에 추가한다. `Assets/Generated`, `Assets/Fixtures`, `Builds`, `Library`는 생성 결과다. 사용자가 만든 씬을 Generated에 저장하지 않는다.

월드가 준비되면 제작자가 배치한 KimchilyMobilePlayer를 사용하고, 없으면 기본 플레이어를 생성한다. 가로·세로 선택은 네이티브 앱이 관리한다. [사용 안내](../docs/mobile-world-guide.md)와 [검증 기록](../docs/reports/2026-09-19-mobile-controls-server.md)을 참고한다.

`Assets/link.xml`에는 기존 SDK·MoonSharp 외에 `Kimchily.TypeScript.Runtime`, `Jint`, `Acornima`, `System.Runtime.CompilerServices.Unsafe` 보존 설정이 필요하다. 패키지의 `Runtime/TypeScriptPreservation.xml.txt`가 병합용 템플릿이다. 패키지 내부 파일만으로 IL2CPP 보존을 완료했다고 간주하지 않는다. 실제 export·통합 빌드·기기 실행으로 확인한다.

원본 모델의 위치는 `UnityKimchilyWorld/Assets/KimchilyCreatorTool/CharactorResources/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Meshes_Humans/HumanMale_Character.fbx`다. 모델이 없는 다른 환경에서는 준비 단계가 명시적으로 실패한다. 이 샘플 모델을 SDK UPM 자체에 넣지는 않았다.

Bootstrap만 Editor Play로 실행하면 네이티브 명령을 기다린다. 샘플 자동 실행은 PlayMode 테스트가 수행한다. 메뉴에서 씬을 준비한 뒤 `Assets/Generated/DemoWorld.unity`를 직접 열면 모델 표시와 Coroutine 동작을 살펴볼 수 있다.

**검증과 범위**

2026-09-19 TypeScript 변경 후 Unity 2022.3.16f1에서 **PlayMode 54/54**, **EditMode 12/12**가 통과했고 실패·skip은 0이다. 결과는 [runtime-playmode.xml](Artifacts/runtime-playmode.xml)과 [runtime-editmode.xml](Artifacts/runtime-editmode.xml)이다. Creator FBX 씬 전환·Android 콘텐츠 게시와 **ARM64 IL2CPP 207개 노드, 실패 0**의 통합 앱 빌드를 완료했다.

새 APK는 **38,284,262바이트**, SHA-256 `B957A39C117B2B2FAB7C8033C3014CF6334029667F4FE2BA74D725FC2FD3C70B`다. **SM-G955N / Android 9(API 28)**에서 설치 후 TypeScript revision 1을 실행하고, APK 재설치 없이 같은 프로세스 `1902`에서 revision 2도 실행했다. [실행 로그](../KimchilyAndroid/Artifacts/device-typescript-revision2.log)에 03:10:23과 03:21:01의 시작 메시지가 남아 있다. FBX 회전·이동, generator beacon 활성/비활성 전환, 두 번째 퇴장까지 홈 복귀·다운로드 캐시 0개와 새 링크를 통한 재입장을 확인했다. 최신 manifest의 실제 다운로드 해시와 QR 기본 decode·Android 링크 필드도 일치했다. 이번 TypeScript QR의 물리 카메라 촬영은 수행하지 않았다. [통합 검증 JSON](../KimchilyAndroid/Artifacts/typescript-verification.json)과 [TypeScript 실행기 기록](../docs/reports/2026-09-19-typescript-runtime.md)에 증거를 기록한다. 아래 Lua 실행 증거는 그대로 보존한다.

프로젝트·네임스페이스·JNI 연결을 Kimchily로 변경했다. 변경 후 검증은 [Kimchily 전환 기록](../docs/reports/kimchily-rebrand-2026-09-18.md)에 별도로 기록한다. 아래 `device-final-*`, `device-camera-investigation.*` 증거는 변경 전 원본 기록이며 다시 쓰지 않았다.

PlayMode 테스트는 기존 브리지·SDK Coroutine/manifest·Lua·다운로드 검사와 새 TypeScript 검사를 포함한다. helper는 PlayMode 최소 54개, EditMode 최소 12개 완료, 실패·skip 0 및 새 결과 파일을 요구한다. TypeScript의 importer/타입 필드 검사는 EditMode에서 실행한다.

2026-09-18 SM-G955N에서 내장 데모뿐 아니라 LAN 게시 서버의 Android 번들을 다운로드하고 FBX 모델·Lua를 실행했다. 같은 APK와 프로세스에서 Lua revision 1 → 2 → 1 전환, 나가기 후 네이티브 홈 복귀, 다운로드 임시 디렉터리 정리를 확인했다. 최종 Lua DLL로 갱신한 APK에서도 revision 1 → 2를 재확인했고 이전 로더 초기화 예외가 사라졌다. 최종 화면과 로그는 `../KimchilyAndroid/Artifacts/device-final-published-world.png`, `device-final-revision-switch.log`에 있다. 실제 카메라 QR 촬영 → revision 2 실행도 확인했고 `device-camera-investigation.log`, `device-after-camera-scan.png`에 기록했다.

기존 수정 Lua DLL로 PlayMode 42개가 통과했다. 당시 `Assets/link.xml`의 SDK·Lua·MoonSharp 보존 설정을 확인했다. MoonSharp는 공식 2.0.0.0 소스에서 기본 로더만 메모리 기반으로 수정해 빌드한다. 재현 방법과 BSD 라이선스는 Lua UPM 패키지에 함께 보존한다. 이 결과는 새 Jint/TypeScript의 IL2CPP 검증을 대신하지 않는다.

첫 게시 대상은 정확히 한 씬, Built-in, Android, Unity 2022.3.16f1이며 최대 64개 번들/256 MiB다. 다운로드는 비동기이고 검증·로컬 번들 열기는 현재 동기 작업이므로 대형 월드는 별도 성능 개선이 필요하다. 개발 빌드만 HTTP를 허용하고 release는 HTTPS를 요구한다. 임시 파일은 월드 퇴장 후 제거하며 영구 다운로드 캐시는 아직 구현하지 않았다.

통합 방식은 Unity as a Library이며, 직접 관리하는 Kotlin/manifest 코드는 Android 프로젝트에 둔다. `Builds/Android`의 생성 코드를 직접 수정하지 않는다. [Unity Android 통합 문서](https://docs.unity3d.com/2022.3/Documentation/Manual/UnityasaLibrary-Android.html)
