# Project Dawn / UGC 원격 플레이

갱신일: **2026-10-01**. 공유기 WireGuard VPN과 서버 자동 실행을 구성했다. 아래 순서는 **다른 장소의 Windows 노트북**에서 진행한다.

## 다른 노트북에서 시작하기

1. 이 저장소를 최신화한다.

   ```powershell
   git pull --ff-only origin main
   ```

2. [WireGuard 공식 사이트](https://www.wireguard.com/install/)에서 Windows 클라이언트를 설치한다.
3. 저장소 루트에서 아래 명령을 실행하고, 작업 대화에서 별도로 전달한 **패키지 복호화 암호**를 입력한다.

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\RemotePlay\VpnProfile.ps1"
   ```

   암호 입력은 화면에 표시되지 않으며 몇 초 걸릴 수 있다. 성공하면 Windows 다운로드 폴더에 `Kimchily-RemotePC1.conf`가 생성된다. 기존 파일이 있으면 덮어쓰지 않는다. 다른 위치를 원하면 `-OutputPath 'C:\원하는경로\Kimchily-RemotePC1.conf'`를 추가한다.

4. WireGuard에서 **Import tunnel(s) from file**로 위 파일을 가져오고 **Activate**를 누른다.
5. 아래 UGC 링크를 열거나 Dawn 클라이언트를 실행한다.

### UGC

[Chili Island 입장](http://192.168.0.4:8788/w/chili-island/webgl-20260930T014824479Z-2d905a55) → 닉네임 입력 → 입장.

여러 참가자는 같은 revision과 방으로 접속한다. 채팅·이동 동기화와 포털 이후 두 번째 협동 미션이 포함된다.

### Project Dawn

노트북의 게임 실행 파일이 있는 폴더에서:

```powershell
.\Project_Dawn1.exe -serverHost=192.168.0.4 -tcpPort=8080 -udpPort=8081
```

실행 파일 이름이 다르면 실제 이름으로 바꾼다. Unity 프로젝트만 있는 경우 에디터 버전은 `2021.3.11f1`이며, 에디터를 시작할 때 같은 서버 옵션을 전달한다.

```powershell
& '<Unity.exe 경로>' -projectPath '<Project_Dawn 경로>' -serverHost=192.168.0.4 -tcpPort=8080 -udpPort=8081
```

Dawn의 채널 목록 UI는 아직 `127.0.0.1:8090` 고정이다. 원격 채널 목록 조회는 별도 수정 대상이며, 이번 VPN 설정에서 클라이언트 코드는 변경하지 않았다.

## PC가 꺼져 있을 때

[기존 공유기 관리 화면](http://kimchily.iptime.org:9239/ui/)에 로그인해 **특수 기능 → WOL 기능**에서 기존 서버 PC의 **PC 켜기**를 누른다. 공유기는 계속 켜져 있어야 한다.

이 PC에는 `Kimchily-RemotePlay-Servers`라는 Windows 작업을 등록했다. 부팅 후 30초 지연으로 기존 Windows 사용자/LocalDB 소유자 계정에서 서버 세 개를 시작하고, 60초마다 상태를 확인한다. Windows 자동 로그인이나 비밀번호 저장은 설정하지 않았다.

실제 Windows 재부팅·전원 종료 후 WOL 전체 흐름은 아직 시험하지 않았다. 작업 스케줄러의 비대화형 사용자 환경에서 서버 세 개를 새로 실행하고 DB·게임 접속을 확인한 상태다.

## 적용한 접속 구성

| 항목 | 값 |
|---|---|
| 공유기 | ipTIME AX3000SM / 관리도구 15.01.8 |
| WireGuard 서버 | 실행 / VPN 내부 통신 NAT 켜짐 |
| VPN Endpoint | `kimchily.iptime.org:52771` / UDP |
| VPN 인터페이스 | `10.188.43.1/24` |
| 노트북 피어 | `Kimchily-RemotePC1` / `10.188.43.2` |
| 서버 PC | `192.168.0.4` / 실제 유선 MAC으로 DHCP 예약 완료 |
| WOL | 기존 등록 유지 / 실제 유선 MAC 일치 |
| Dawn | TCP 8080 / UDP 8081 |
| UGC 게시 / 실시간 | TCP 8788 / TCP 8790 |

클라이언트 프로필의 `AllowedIPs`는 `192.168.0.4/32, 10.188.43.1/32`다. 게임 서버와 VPN 공유기 주소만 VPN으로 보내며 DNS 설정은 추가하지 않았다. 일반 인터넷 트래픽은 노트북의 기존 연결을 사용한다. 이 설정은 클라이언트의 경로 선택이며 공유기에서 다른 내부 주소를 차단하는 ACL을 의미하지 않는다.

이 프로필은 **외부 PC 한 대용**이다. 다른 기기도 동시에 접속하려면 별도 피어를 만든다. 노트북 자신의 LAN IP가 서버와 같은 `192.168.0.4`이면 주소가 충돌하므로 다른 Wi-Fi/핫스팟으로 시험하거나 주소 구성을 조정한다.

## 암호화 파일과 개인정보 취급

이 저장소는 공개 저장소이므로 원본 WireGuard 파일의 `PrivateKey`·`PresharedKey`는 커밋하지 않는다.

- `profiles/Kimchily-RemotePC1.encrypted.json`: 암호화된 실제 프로필. Git으로 전달한다.
- `VpnProfile.ps1`: Windows PowerShell 5.1에서 복호화한다. 기본 출력 위치는 저장소 밖의 다운로드 폴더다.
- 복호화 암호: 192비트 난수로 생성하고 **작업 대화에서 별도 전달**한다. Git에 포함하지 않는다.
- 형식: PBKDF2-HMAC-SHA256 600,000회, 무작위 salt 32바이트, AES-256-CBC/PKCS7, 무작위 IV 16바이트, 별도 키의 HMAC-SHA256. 메타데이터와 암호문을 인증한 뒤 복호화한다.
- `.gitignore`는 `.local`, `Artifacts`, 원본 `.conf`, 키 파일과 QR 이미지를 제외한다.
- 접속 자격을 폐기할 때는 공유기의 해당 피어를 삭제한다. Git에서 파일을 지우는 것만으로 기존 VPN 키가 폐기되지는 않는다.

## 검증 결과와 남은 확인

2026-10-01 확인:

- 공유기 WireGuard 실행·NAT 켜짐, 외부 PC 피어 1개 등록, 서버 주소 DHCP 예약을 관리 화면에서 확인했다.
- 공유기에서 생성한 피어 QR을 로컬에서 읽어 원본 설정을 보관했다. 내장 브라우저의 다운로드 이벤트가 확인되지 않아 같은 설정을 제공하는 QR 경로를 사용했다.
- 접속 파일의 개인키로 계산한 X25519 공개키가 실제 등록된 피어 공개키와 일치했다.
- Windows PowerShell **5.1.19041.7725**에서 암호화본 복호화 후 원본 SHA-256 일치.
- 잘못된 암호·변조된 암호문 거부, 기존 출력 파일 보호 확인. 실패 시 평문 출력 없음.
- 별도 Python cryptography 구현으로 HMAC 검증·복호화 일치 확인.
- 서버 자동 실행 작업으로 세 서비스를 새로 시작했다. Dawn 2개 클라이언트 로그인·마을 입장 2/2, 이동 송신 48/수신 45, 실패 0.
- UGC WebSocket 두 연결의 입장·채팅·위치 전달·승인된 TypeScript 게임 초기화 통과.

**다른 장소에서 실제 VPN handshake, Unity UDP 플레이, 종료된 PC의 WOL 부팅은 아직 확인 전**이다. 첫 외부 접속 후 WireGuard의 최근 handshake와 전송/수신량을 확인한다.

```powershell
Test-NetConnection 192.168.0.4 -Port 8080
Test-NetConnection 192.168.0.4 -Port 8788
Test-NetConnection 192.168.0.4 -Port 8790
```

이 검사는 TCP 접근만 확인한다. UDP 8081과 실제 Unity 플레이는 클라이언트로 확인한다.

## 서버 PC 운영 파일

서버 PC에서 수정·실행하는 위치는 `E:\task\RemotePlay`, Git 관리본은 `E:\GItHub\PortFolio\RemotePlay`다.

- `Start-GameServers.ps1`: 현재 서버 PC 전용 경로로 Dawn·게시·실시간 서버를 시작하고 상태를 확인한다. 다른 프로세스가 포트를 차지하면 종료하지 않고 오류를 기록한다.
- `Register-ServerStartup.ps1`: 현재 서버 PC의 LocalDB 소유자 계정으로 시작 작업을 등록한다. 외부 노트북에서는 위의 VPN 복호화·WireGuard 연결 절차를 사용한다.
- 로컬 상태·로그: `E:\task\RemotePlay\.local\status.json`, `supervisor.log`, 서비스별 로그.
- Dawn 소스: `E:\task\Server`, UGC 소스: `E:\task\KimchilyWebGL`.
- Dawn 빌드 산출물: `%LOCALAPPDATA%\ProjectDawn\local-server\server\Server.dll`. 소스 갱신 후 서버를 중지하고 빌드를 갱신해야 한다.

```powershell
Get-ScheduledTask -TaskName 'Kimchily-RemotePlay-Servers'
Get-Content 'E:\task\RemotePlay\.local\status.json'
```

자동 시작을 비활성화하려면 `Disable-ScheduledTask -TaskName 'Kimchily-RemotePlay-Servers'`를 사용한다. 이 명령은 이미 실행 중인 서버를 종료하지 않는다. 다시 사용할 때는 `Enable-ScheduledTask`와 `Start-ScheduledTask`를 같은 이름으로 실행한다.

참고: [ipTIME WireGuard 설정](https://www.iptime.com/support/faq/25905), [Windows 작업 실행 계정](https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/new-scheduledtaskprincipal).