# 서버 끄기·켜기와 소스 위치

## AWS 컴퓨터 전체를 끄기 — 사용량을 줄일 때

1. 플레이 중인 파티가 없는지 확인한다. 중지하면 진행 중 전투는 복구되지 않는다.
2. AWS 콘솔에서 **서울 → EC2 → 인스턴스**를 연다.
3. **projectdawn-free-playtest / i-00000000000000000**을 선택한다.
4. **인스턴스 상태 → 인스턴스 중지(Stop instance) → 중지**를 누른다.
5. 상태가 **중지됨(Stopped)**인지 확인한다. 종료/삭제(Terminate)는 사용하지 않는다.

다시 켜려면 같은 곳에서 **인스턴스 시작(Start instance)**을 선택한다. 현재 설정의 EBS에 있는 DB와 코드는 유지되지만 자동 할당 공인 IP는 바뀔 수 있다. 새 IP로 TLS 인증서를 다시 발급하고 친구 클라이언트의 `playtest.server.json`을 갱신해야 한다.

중지하면 EC2 실행 사용량은 멈추지만 EBS 저장공간은 남아 사용량이 발생할 수 있다. 현재 Free plan/크레딧 적용과 잔액은 Billing에서 확인한다. 중지 자체가 모든 AWS 자원 비용을 0으로 만드는 것은 아니다.

공식 근거: [EC2 중지/시작](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/Stop_Start.html), [상태·IP·스토리지·비용](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/how-ec2-instance-stop-start-works.html).

## 게임 프로그램만 끄기 — 잠깐 점검할 때

Windows PowerShell에서 SSH로 접속한다. 아래는 현재 IP 기준이다.

```powershell
$keyRoot = Join-Path $env:LOCALAPPDATA 'ProjectDawn/AwsFreePlaytest-20261018'
ssh -i "$keyRoot/projectdawn-ssh" -o "UserKnownHostsFile=$keyRoot/known_hosts" ubuntu@203.0.113.10
```

SSH 접속 후 실행:

```bash
# 게임/TLS 서비스만 정지. EC2와 MySQL은 계속 켜져 있음.
sudo systemctl stop dawn-member-gateway dawn-member-server
sudo systemctl is-active dawn-member-gateway dawn-member-server
# 두 서비스 모두 inactive면 정지 완료. is-active의 비정상 종료코드는 이 경우 정상.

# 다시 시작
sudo systemctl start dawn-member-server dawn-member-gateway
sudo systemctl is-active dawn-member-server dawn-member-gateway
# 둘 다 active인지 확인
```

수동으로 stop한 서비스는 자동 실패 재시작 정책 때문에 즉시 되살아나지 않는다. 다만 서버 재부팅 시에는 enabled 서비스가 다시 시작된다. 인증서 갱신 hook도 inactive인 gateway는 시작하지 않는다(try-restart). 서비스만 끄는 것은 EC2 실행 사용량 절감 방법이 아니다.

## 서버 코드 읽기

- 진입점: `FlowHost/Program.cs`
- 기존 세션/매칭/방/DB: `Server/Server/`
- 최종 전투 판정: `Packages/com.projectdawn.combat/Runtime/`
- TLS/회원 인증: `CloudGateway/`
- 패킷 계약: `Protocol/`
- 방장 Lua: `Lua/bakal_patterns.lua` (서버 실행 Lua가 아님)
- 채팅 검증: `Server/Server/Session/ClientSession_Chat.cs`
- 채팅 수신 범위: `Server/Server/Game/Room/GameRoom_Chat.cs`

전체 설명과 주요 코드 원문은 HTML 인수인계 (`../Docs/DeveloperHandbook/index.html`, 로컬 검증 자료)의 서버 소스 장에 있다. 소스 수정 후 서버 빌드는 `pwsh -File Cloud/Build-Cloud.ps1`, Unity 빌드는 `pwsh -File Build-Unity.ps1`이다.
