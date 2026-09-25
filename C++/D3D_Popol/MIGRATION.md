# D3D 클라이언트·서버 작업 위치

2026-09-25에 `E:\task\C++\D3D_Popol`과 `D3D_Server`의 작업 소스와 에셋을
이 저장소의 `C++/D3D_Popol`, `C++/D3D_Server`로 복사했다. 원본과 대상에만 있던
파일은 보존했다. 두 프로젝트는 형제 디렉터리 구조를 유지해야 한다.

## 빌드

Visual Studio 2022 C++ v143 도구와 Windows SDK가 필요하다. 서버·NavGrid 테스트는
.NET Core 3.1, PacketGenerator는 .NET 7, 클라이언트 디렉터리의 C# 회귀 테스트는
.NET 8을 사용한다. `Libraries`의 외부 헤더·LIB, `Binaries`의 Assimp/FMOD DLL,
`Resources`, `Textures`, `Shaders`, `Common/protoc-*/bin`을 함께 유지한다.

PowerShell에서 클라이언트 루트로 이동한 뒤 아래 순서로 빌드한다. 현재 프로젝트에는
명시적 프로젝트 의존성이 없고 중간 출력 경로를 공유하므로 병렬 빌드를 피한다.

```powershell
Set-Location 'E:\GItHub\PortFolio\C++\D3D_Popol'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$solutionDir = (Get-Location).Path + '\'
foreach ($project in 'ServerCore/ServerCore.vcxproj', 'EngineCore/EngineCore.vcxproj', 'AssimpTool/AssimpTool.vcxproj', 'GameCoding2/GameCoding2.vcxproj') {
    & $msbuild $project /t:Rebuild "/p:SolutionDir=$solutionDir" /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
dotnet build '..\D3D_Server\Server\Server\Server\Server.csproj' -c Debug --no-incremental
```

일반 실행은 먼저 서버를 빌드한 뒤 시작하고, 클라이언트의 작업 디렉터리를 `Binaries`로
설정한다. EXE는 이번 소스 복사 대상에서 제외했으므로 이전 대상에 남아 있던 실행 파일을
그대로 사용하지 말고 재빌드한다. 기존 Release 설정 문제는 이주 범위에서 수정하지 않았다.

## 기존 테스트

`Tests/NavGrid/Cpp/NavGridCppTests.vcxproj`와 형제 서버의
`Server/Server/NavGridTests/NavGridTests.csproj`를 빌드한다. 두 실행 파일은
`Tests/NavGrid/Assets/golden-grid-v1.navgrid`, `Tests/NavGrid/coordinate-vectors.csv`,
출력 JSON 경로를 순서대로 받는다. `Tests/NavGrid/compare-results.ps1`로 두 JSON을 비교한다.

서버 Debug 빌드 후 `Tests/AnnieQ`, `Tests/AnnieW`, `Tests/CombatMovement` 각각에서
`dotnet run --project <해당 csproj>`를 실행한다. AssimpTool Debug 빌드 후 클라이언트 루트에서
`python Tests/AnnieQ/test_converter.py`, `python Tests/AnnieW/test_converter.py`를 실행한다.

## 복사에서 제외한 항목

IDE 캐시, 중간 빌드 출력, 프로젝트의 `bin`/`obj`/`x64`, EXE 변형, PDB/EXP,
자체 생성 EngineCore·ServerCore LIB, 재생성 가능한 테스트 결과, 오래된 ZIP/BAK,
임시 패치 staging을 제외했다. `Common`의 protoc EXE와 `Libraries`의 protobuf Debug LIB는 보존했다.
예전 `tools/apply_*_server.py`는 옛 절대 경로와 `tmp` manifest에 의존하는 일회성 패치여서
복사하지 않았다. 재사용 가능한 import 도구와 테스트 fixture는 보존했다.

실제 파일 복사 시 SHA-256을 검증했고, 덮어쓴 대상 파일은 이주 작업의 `work/d3d-backup`에
백업했다. 기존 추적 산출물은 이주 과정에서 삭제하거나 Git 추적을 해제하지 않았다.

## 이주 검증 결과

- C++ ServerCore, EngineCore, AssimpTool, Client와 C++ NavGrid 테스트 프로젝트의 Debug x64 빌드 성공.
- C# Server와 NavGrid 테스트 프로젝트 Debug 빌드 성공. 이 PC의 NuGet 캐시만으로 복원했다.
- C++ NavGrid 279개, C# NavGrid 436개 검사 통과 및 두 결과 JSON의 바이트 단위 일치 확인.
  Annie Q 서버 패킷 직렬화, Annie W 범위 12개,
  CombatMovement 86개 검사와 Annie Q/W 실제 Assimp 변환 회귀 검사 통과.
- C# NavGrid의 오래된 테스트 fixture가 현재 서버의 생존·챔피언 전제조건을 충족하지 않아
  새로 빌드하면 실패하는 문제를 확인했다. 원본 소스를 별도 디렉터리에서 빌드해도 재현됐다.
  목적지의 테스트에만 다음 네 줄을 보정했으며, 원본 `E:\task`와 운영 코드·기존 assertion은 유지했다.
  `MovementPhase3Tests.cs`, `PathfindingPhase4Tests.cs`의 정상 이동 Player에 각각 `Hp = 100`을 지정하고,
  `CombatPhaseTests.cs`에서 Annie/Garen caster의 `ChampType`을 해당 챔피언으로 명시했다.
  이 세 테스트 파일은 의도적으로 원본 복사 해시와 다르며, 보정 후 전체 436개 검사가 통과했다.
- 창을 여는 클라이언트 실행과 서버 상주 프로세스 시작은 수행하지 않았다.
- 검증 중 바뀐 기존 추적 빌드 산출물은 별도 보관 후 저장소 상태로 되돌려 커밋에서 제외했다.
  새 경로에서 실행할 때는 위 Rebuild/--no-incremental 명령으로 클라이언트와 서버를 함께 다시 빌드한다.
