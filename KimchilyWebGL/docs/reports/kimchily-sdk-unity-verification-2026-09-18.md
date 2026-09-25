# Kimchily SDK Unity 실행 검증 결과

2026-09-18, Unity 2022.3.16f1 / Windows Editor / batchmode + nographics.

**EditMode 6개와 PlayMode 20개, 총 26개를 실제 실행하여 모두 통과했다.** 실패·skip·inconclusive는 모두 0이다. 원본 Creator·World·ToolManager 프로젝트 대신 별도 `KimchilySDK/ValidationProject`를 사용했다.

| 항목 | 확인 결과 |
|---|---|
| 콘텐츠 빌드 | 기존 World의 FBX를 포함한 씬을 StandaloneWindows64 AssetBundle로 빌드, world.json 생성 |
| EditMode 6개 | 씬·프리팹·재질·텍스처 의존성 수집, 동적 자산 명시, 잘못된 씬·폴더·출력 위치 거절, 라벨·활성 씬·dirty 상태 보존 |
| Coroutine 12개 | 프레임 재개, 즉시 완료, 중첩 정리, 자기 취소, 예외 수집, owner 파괴·분리, scheduler 비활성화·파괴, timeScale 0에서 실시간 대기 |
| Manifest 6개 | 호환 manifest 허용, SDK·렌더 파이프라인·시작 씬·필수 타입·중복 파일 불일치 거절 |
| 실제 xLua 1개 | 기존 managed/native DLL과 cs_generator로 새 CoroutineScheduler 호출, 대기·재개·명시적 cleanup·LuaEnv 종료 |
| 실제 FBX 1개 | 번들 로드 후 메시·본·재질·텍스처·AnimatorController·클립·오브젝트 위치·SDK 컴포넌트 확인, 두 번 로드/언로드, 열린 씬의 조기 Dispose 거절 |
| DLL 컴파일 | Runtime·Editor·검증 코드 dotnet build 성공, 경고 0·오류 0 |
| 정적 검사 | 패키지 GUID 26개, 테스트 26개, 세 프로젝트의 로컬 UPM 참조 검사 통과 |

검증 과정에서 테스트 프로젝트의 플러그인 importer·명시적 DLL 참조·UGUI/ParticleSystem 의존성을 보완했다. FBX 샘플에 Animator가 없을 때 Unity의 null 판정으로 컴포넌트를 추가하도록 수정했다. EditMode 테스트는 Test Framework가 만드는 초기 씬을 처리하고 복원하도록 수정했다. 기존 xLua의 종료 검증은 callback 참조를 해제한 뒤 다음 프레임에 GC/Tick과 Dispose를 수행한다.

검증 러너는 Unity 종료 코드뿐 아니라 새 XML인지, 테스트가 실제 발견되고 통과했는지, 실패·skip·inconclusive가 없는지도 검사한다. 최종 EditMode 실행 중 한 차례 라이선스 클라이언트 IPC 연결이 실패했으며, 동일 검증을 샌드박스 밖에서 재실행하여 통과했다. 이전 실패·skip 결과를 최종 성공으로 사용하지 않았다.

**검증 범위의 한계**

현재 증거는 한 FBX 샘플과 Built-in Render Pipeline을 사용한 Windows Editor 검증이다. 모든 FBX·셰이더·외부 컴포넌트 호환성을 보장하지 않는다. nographics 실행이므로 실제 화면 품질·셰이더 렌더링을 확인한 결과도 아니다. Unity 2021.3, 독립 Player, Android/iOS IL2CPP, 생성된 AOT 바인딩, 바이너리 전용 UPM의 MonoScript/GUID 전환은 별도 검증이 필요하다. Coroutine의 모든 yield 타이밍, 특히 WaitForEndOfFrame의 렌더링 단계도 아직 검증하지 않았다.

게시 서버·업로드·QR 표시·딥링크·기존 World 앱의 새 manifest 연결은 후속 구현 범위다. 이번 재실행은 Coroutine과 로컬 콘텐츠 빌드/로드 기반의 동작을 확인한 것이다.

재실행 명령은 저장소 루트에서 다음과 같다.

```powershell
python KimchilySDK/tools/prepare_validation.py
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilySDK/tools/run_unity_tests.ps1
```

결과 파일: [EditMode XML](../../KimchilySDK/Artifacts/editmode.xml), [PlayMode XML](../../KimchilySDK/Artifacts/playmode.xml), [콘텐츠 빌드 로그](../../KimchilySDK/Artifacts/fixture-build.log). 마지막 콘텐츠 경로는 [world-fixture-path.txt](../../KimchilySDK/Artifacts/world-fixture-path.txt)에 있다. Artifacts는 로컬 생성 결과이며 배포 패키지에 포함하지 않는다.
