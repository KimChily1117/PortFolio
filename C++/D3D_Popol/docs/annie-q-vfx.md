# 애니 Q 투사체·피격 효과 적용

2026-09-18. 클라이언트: `E:\task\C++\D3D_Popol`, 서버: `E:\task\C++\D3D_Server`.

애니 Q에 원본 불꽃 메시, 이동 잔상, 연기, 불씨, 피격 섬광·충격파·잔광을 연결했다. 일반 공격은 기존 효과를 사용한다. 가렌 E의 코드·리소스·셰이더 설정은 수정하지 않았다. 피해량과 판정은 계속 서버가 결정한다.

## AssimpTool 사전 변환과 로딩

추출 원본 위치는 `C:\Users\yeop_sun\Desktop\Extract\assets\characters\annie\skins\base\particles`이다. `Annie_Base_Q_mis`, `Annie_Base_Q_tar`가 참조하는 DDS 20개와 SCB 1개를 `Resources/Textures/Annie/Particles/OriginalQ`에 복사했다. `manifest.json`에 원본 파일 SHA-256을 기록했다. 현재 효과는 이 중 DDS 15개를 사용한다.

프로젝트의 **AssimpTool**에 `--convert-scb-vfx` 실행 경로와 SCB 3.2 어댑터를 추가했다. 기존 FBX·애니메이션 변환은 유지한다. Assimp 라이브러리는 SCB를 직접 처리하지 않으므로 이 어댑터가 파일을 읽는다. 실행 시 창·D3D 장치·네트워크를 만들지 않는다.

```powershell
# 프로젝트 루트에서: 리소스 복사 + AssimpTool 실행 + 해시 기록
python tools\import_annie_q.py C:\Users\yeop_sun\Desktop\Extract

# 메시만 재변환하는 경우
& .\Binaries\AssimpTool.exe --convert-scb-vfx `
  'C:\Users\yeop_sun\Desktop\Extract\assets\characters\annie\skins\base\particles\annie_base_q_mis_01.scb' `
  'E:\task\C++\D3D_Popol\Resources\Models\Annie\Vfx\annie_base_q_mis_01.vfxmesh'
```

변환 출력은 `Resources/Models/Annie/Vfx/annie_base_q_mis_01.vfxmesh`이다.

| 항목 | 결과 |
|---|---:|
| 원본 SCB 크기 | 80,776바이트 |
| 변환 파일 크기 | 28,456바이트, 약 65% 감소 |
| 삼각형 정점 참조 수 | 2,280 |
| 위치·UV를 함께 비교해 합친 정점 수 | 439 |
| 인덱스 수 | 2,280 |

원본의 위치 정점 382개와 면별 UV를 결합하므로, UV 경계에서는 정점을 분리한다. 모든 삼각형의 위치와 UV를 원본과 대조하는 검사를 통과했다. 원본 -Y 방향 꼬리를 변환 파일에서는 -Z로 배치한다. 월드 크기는 효과에서 별도로 적용한다.

`VFXM` 버전 1 파일은 20바이트 헤더(magic/version/vertex stride/vertex count/index count), 44바이트 정점 배열, uint32 인덱스 배열 순서다. `Mesh::LoadVfxMesh`는 이를 장면 초기화 때 한 번 읽고 immutable VB/IB를 만든다. 시전 중에는 원본 SCB 해석, 메시 파일 읽기, 메시 정점의 CPU 변환·재업로드가 없다. 여러 Q의 중심 메시가 하나의 버퍼·머티리얼을 공유하고 하드웨어 인스턴싱으로 그려진다. DDS는 기존 ResourceManager 캐시를 사용한다.

## 재생과 비용 제한

- `AnnieQEffect`가 장면별로 초기화되어 12개 스프라이트 배치와 16개 메시 인스턴스를 재사용한다. 입자마다 GameObject나 GPU 버퍼를 생성하지 않는다.
- 중심 메시 1회와 활성 스프라이트 배치별 1회로, 전용 효과의 드로 호출은 최대 13회다. 이 수치는 기존 효과 대비 FPS 향상 측정치가 아니다.
- 비행 효과는 최대 16개, 입자는 최대 2,048개다. 스프라이트 배치별 표시 한도도 적용한다. 비행 한도를 넘으면 해당 투사체는 기존 잔상으로 표시한다.
- 잔상은 0.12 월드 단위로 기록하며 최대 32개 지점을 유지한다. 불꽃 잔상은 0.4초, 연기 잔상은 0.65초에 걸쳐 사라진다.
- `ProjectileScript`의 시각적 도착은 비행 방출만 멈춘다. 피격 효과는 `S_ProjectileHit`에서만 실행한다. 풀 반환 시 효과가 이동 객체와 분리되므로 같은 객체를 재사용해도 남은 잔상이 따라가지 않는다.
- 색상·알파·침식 값은 입자별 정점에 전달한다. 2×2 아틀라스, 연기 침식 텍스처, 불꽃 보조 텍스처, alpha/additive 블렌드를 분리한다.
- 메시 텍스처는 두 UV 위상을 합쳐 원본의 겹치는 불꽃 느낌을 표현하고 스크롤 경계의 절단면을 완화한다.
- `MeshRenderer::SetWorldOverlay(order)`는 신규 효과에만 명시적으로 사용한다. 기존 14/15/16번 패스의 처리와 기본 정렬 순서는 유지한다. LoL의 pass 번호를 엔진 패스 번호로 사용하지 않는다.

이는 원본 리소스와 확인된 수명을 이용한 현재 엔진용 구현이다. LoL BIN의 모든 emitter·확률 분포·곡선을 그대로 실행하는 범용 파티클 인터프리터는 아니다. 방출량과 월드 크기는 이 프로젝트에 맞게 조정했다.

## Q와 일반 공격의 구분

`S_ProjectileSpawn`에 `optional int32 skillId = 7`을 추가했다. 기존 필드 번호와 패킷 ID는 바꾸지 않았다.

| 송신 값 | 애니 클라이언트 표시 |
|---|---|
| 필드 있음, 0 | 기존 일반 공격 효과 |
| 필드 있음, 1 | 새 Q 효과 |
| 필드 없음 | 구형 서버로 판단하고 기존 효과, 로그 1회 |
| 다른 챔피언 또는 다른 스킬 | 기존 처리 |

`ProjectileVisualPolicy.h`에서 챔피언 종류와 패킷의 필드 존재 여부를 함께 확인한다. 투사체 속도나 마지막 애니메이션으로 추측하지 않는다.

서버의 `AnnieSkillHandler`는 평타에서 0, Q에서 1을 송신한다. 서버 프로토콜·C# 생성 코드 두 사본을 갱신했고, 구버전 protoc 3.12의 `GenProto.bat`에 `--experimental_allow_proto3_optional`을 추가했다. 클라이언트 C++ 생성 코드도 재생성했다. 변경 전 서버 파일은 `tmp/annie-q-server/before`, 클라이언트 파일은 `tmp/annie-q-before`에 보관했다.

**빌드된 서버와 클라이언트를 함께 재시작해야 새 효과가 선택된다.** 서버가 구형이면 새 클라이언트는 기존 효과를 유지한다.

실행 파일은 `Binaries/Client.exe`이며, 같은 빌드를 `Binaries/Client_AnnieQOriginal_AssimpV9.exe`로도 보관했다. 기존 비교용 실행 파일들은 유지했다. 실행 시 작업 디렉터리는 `Binaries`를 사용한다.

## 검증

- Debug x64 전체 순차 Rebuild 성공. 이후 클라이언트 변경도 Rebuild 성공. 프로젝트에 기존 경고는 남아 있다.
- C# 서버 빌드 성공, 오류 0개.
- `AnnieQ.fx` FXC 및 실제 D3D 런타임 컴파일 성공.
- AssimpTool 정상 입력·잘린 파일·잘못된 magic/index·NaN 거부, 결정적 출력, 삼각형 위치/UV 보존 검사 통과.
- 실제 서버 어셈블리로 생성한 C# 평타/Q/구형 패킷을 C++에서 파싱해 분기 확인.
- 숨김 DirectX 창에서 실제 비행·명중·잔광을 렌더링하고 캡처. 풀 재사용 후 잔상 종료, 동시 비행 한도, 장면 교체를 포함해 **17개 검사 통과**.

```powershell
python Tests\AnnieQ\test_converter.py
Push-Location Tests\AnnieQ
dotnet run --project ServerWireTests.csproj
Pop-Location
Push-Location Binaries
# Debug 클라이언트 전용, 네트워크 접속 없이 약 2초 테스트
& .\Client.exe --annie-q-smoke
Pop-Location
```

결과: `Tests/AnnieQ/results/report.txt`, `converter/report.json`, `flight.png`, `impact.png`, `afterglow.png`. 실제 경기 맵에서의 크기·색감 확인과 FPS 비교 측정은 별도로 남아 있다.

SCB 형식 검토 자료: [LeagueToolkit StaticMesh](https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMesh.cs), [StaticMeshFace](https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMeshFace.cs). 외부 라이브러리를 클라이언트 런타임에 추가하지 않았다.
