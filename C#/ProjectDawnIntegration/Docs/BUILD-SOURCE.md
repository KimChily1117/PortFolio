# 소스 빌드·검사

## 준비

- .NET 10 SDK: 현재 서버·회원 게이트웨이 빌드.
- .NET 8 런타임 또는 Major roll-forward: net8.0 콘솔 검사 실행.
- Unity 2022.3.16f1: 클라이언트 프로젝트 검토. 전체 화면 실행에는 별도 자원이 필요합니다.
- NuGet 접근 가능 환경. 제3자 바이너리는 Git에 포함하지 않습니다.

저장소 루트에서 PowerShell:

```powershell
Set-Location 'C#/ProjectDawnIntegration'
dotnet build Client/ProjectDawn.RaidClient.csproj -c Release --disable-build-servers -m:1
dotnet build FlowHost/FlowHost.csproj -c Release -p:DawnCloud=true --disable-build-servers -m:1
dotnet build CloudGateway/CloudGateway.csproj -c Release --disable-build-servers -m:1
```

기존 서버의 기본 TargetFramework는 호환 이력을 위해 netcoreapp3.1을 유지합니다. 현재 MySQL 서버는 반드시 `-p:DawnCloud=true`로 빌드합니다. 빌드는 서비스를 실행하거나 AWS를 변경하지 않습니다.

## 무자원·무DB 검사

```powershell
dotnet run --project Tests/ProjectDawn.Combat.Tests.csproj -f net8.0 -c Release --disable-build-servers
dotnet run --project CommandTests/CommandTests.csproj -f net8.0 -c Release --disable-build-servers
dotnet run --project LuaTests/ProjectDawn.LuaTests.csproj -f net8.0 -c Release --disable-build-servers
dotnet run --project RoomTests/ProjectDawn.RoomTests.csproj -f net8.0 -c Release --disable-build-servers
```

.NET 10 런타임만 설치됐다면 이 검사 프로세스에 `DOTNET_ROLL_FORWARD=Major`를 설정할 수 있습니다. 이번 공개본 검사는 net8.0 타깃을 .NET 10.0.13 런타임으로 실행했습니다.

## Unity 의존성

```powershell
./Tools/Restore-UnityDependencies.ps1
```

Google.Protobuf 3.25.1과 MoonSharp 2.0.0 및 의존 DLL을 NuGet에서 복원해 무시된 Plugins 폴더에 배치합니다. 원본 게임 자원은 내려받지 않습니다.

Resources·StreamingAssets·Data·실제 접속 설정은 별도 입력입니다. 자원이 없는 상태에서 기존 `Build-Unity.ps1`, Cloud 배포 패키지 생성, 시연/DB 시나리오 전체가 성공한다고 보장하지 않습니다. 해당 스크립트는 완전한 개발 환경에서 사용한 도구 소스입니다.

## 운영 설정

서버의 DB 연결은 `PROJECT_DAWN_DB_CONNECTION_STRING`, 인증 게이트웨이는 `DAWN_AUTH_DB_CONNECTION_STRING` 등의 실행 환경에서 제공합니다. 실제 값을 문서·Git·클라이언트에 저장하지 않습니다. 스키마는 [MySQL migrations](../Server/Server/Migrations/MySql)에 있으며 스키마와 실제 사용자 데이터는 별개입니다.

[HTML 인수인계](DeveloperHandbook/index.html)는 구현·운영 참고 자료입니다. 공개본에서 화면 이미지·운영 식별자는 제거/예시화했으며, 최신 기능 상태는 [README](../README.md)와 [검증](VALIDATION.md)을 우선합니다.
