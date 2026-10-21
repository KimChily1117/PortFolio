# AWS 친구 테스트 서버 · 실제 배포 인수인계

2026-10-18. 현재 기준 문서. 과거 `AWS-FREE-PLAYTEST-20261018.md`의 배포 전/초대 키 내용보다 우선한다.

## 지금 접속하는 방법

- 최신 친구 ZIP: `Artifacts/friend-playtest-20261018-185919/ProjectDawn-FriendTest.zip` (162.7 MiB,144파일). 1~4인 선택·작성 후 Enter 전송 수정·말풍선·비밀번호8자 포함. CRC·파일별 SHA·실제 시험 Build 일치 확인. SHA256 `552039ccc9a89ca8d0b5e76aac6074528208df5262a3a5fede29bf6a5cb55d5b`.
- 현재 실행 Linux ZIP: `Artifacts/cloud-release-20261018-183815/release.zip`, SHA256 `aa1ffd1e6d62ff15227674c1700df32277ac76ca1b1387df0f8a1764212750de`. v14·비밀번호8자 반영. 이후 root전용 계정 관리 도구만 별도 추가했고 서비스 재시작은 하지 않았다.
- 최신4인: 전투26/26·흐름10/10·인원/채팅17/17 통과. **3인 TooLate1건으로16/17 실패**를 보존한다. 전체 결과 (`../PARTY-SIZE-BUBBLES-20261018.md`, 로컬 검증 자료), [아이디·비밀번호 관리](ACCOUNT-MANAGEMENT-KO.md).

친구는 공통 Windows ZIP을 모두 풀고 `Start-ProjectDawn.cmd`를 실행한다. 게임 안에서 ID/비밀번호로 가입 → 로그인 → 캐릭터 생성/선택 → 마을/채널 → 1~4인 선택 매칭을 진행한다. AWS 계정·SSH 키·초대 키·DB 설치는 필요 없다. 계정당 최대 4캐릭터이며 코디/장비는 AWS의 같은 계정으로 재로그인하면 복원된다.

- 게임 주소: `tls://203.0.113.10`, TCP 28443.
- 서버: 서울 `i-00000000000000000`, `projectdawn-free-playtest`, Ubuntu 24.04, t3.small, encrypted gp3 20GB.
- Free plan 유지. 확인 당시 크레딧 $100(집계 지연 가능), 플랜 만료 2027-04-18. 유료 전환 없음. 실행 중 크레딧을 소비하므로 무기한 무료 운영은 아니다.
- CPU credit standard, IMDSv2 required. RDS/NAT/LB/EIP/Secrets Manager를 생성하지 않았다.
- SSH 22는 승인된 소유자 공인 IP/32, 게임 28443과 ACME 80만 공개한다. MySQL 3306, 내부 서버 28182, 모니터 8090은 실제 loopback 바인딩 확인.

## 이전165030 빌드에서 확인한 것

| 검사 | 결과 / 증거 |
|---|---|
| Linux MySQL 회원가입·로그인·캐릭터·장비·재접속 | 22개 통과, `Logs/aws-member-public-probe.log` |
| Unity 공인 TLS 4인 100초 흐름 | `Artifacts/aws-flow-4p-20261018-165030` |
| 전투 검사 | 26개 통과, 공통 확정 상태 201개·이벤트 440개 일치 |
| 화면 점프 회피 | 21/21 서버 확정 일치 |
| 메테오/충격파 표시 | 128개 활성 대조, 계산 틱 오차 0, 관측 0~1틱 |
| 흐름 검사 | 10개 통과(전투 종합 항목 포함), 마을 왕복·장비·코디·본인 컷신·파티원 컷신 억제 |
| 오류/거절 | 최종 실행 Unity 오류 0, 입력/제안 거절 0 |
| 인증서 자동 갱신 시험 | Certbot `renew --dry-run` 성공, `Logs/aws-certificate-renew-test.log` |
| DB 백업/복원 | 9테이블 전체를 별도 `dawn_restore_20261018_075644` DB에 복원하여 정렬된 행 덤프 바이트 일치, `Logs/aws-backup-restore.log` |
| 서비스 재시작 후 영속성 | 기존 계정/캐릭터/장비 재접속 14개 통과, `Logs/aws-member-after-restore-probe.log` |

최종 시험은 한 개발 PC에서 실제 Unity 네 프로세스가 공인 TLS로 AWS 서버에 접속한 것이다. 서로 다른 물리 PC/ISP의 친구 4명 검증이나 장시간 안정성 보장이 아니다. 보상 DB는 로컬 MySQL에서 별도 검증했으며 이번 AWS 일반 전투는 100초 후 복귀 시험으로 실제 정상 난도 보상 클리어를 검증하지 않았다.

첫 AWS 실행 `Artifacts/aws-flow-4p-20261018-164626`은 35.8초경 방장 제어 제안 3건이 `TooLate`로 거절되어 `HostProposalOmissions`로 종료됐다. 서버는 검증을 생략하지 않고 마을로 돌려보냈다. 소스/판정 기준을 바꾸지 않은 재실행은 통과했지만 첫 실패의 원인이 해결됐다고 단정하지 않는다. 방장 처리/GC/네트워크 지연 계측과 장시간 반복 검증을 후속 항목으로 남긴다.

## 운영 파일

- `/opt/projectdawn/release`: 서버/게이트웨이/Data/ops.
- `/etc/projectdawn/server.env`, `gateway.env`: root0600. 비밀번호가 있으므로 출력/공유/Git 금지.
- `/etc/projectdawn/private/database.json`: 서버 생성 DB 비밀. 외부 배포 금지.
- `/var/lib/dawn-member-server/reward-outbox`: 보상 영수증 재시도 state. 배포 시 보존.
- `/etc/projectdawn/private/backups/20261018_075644`: DB 덤프·서비스 state·복원 보고서. VM 내부 백업이며 외부 재해복구 백업은 아님.
- `dawn-member-server.service`, `dawn-member-gateway.service`: 자동 시작, 실패 재시작, OS 사용자 분리. DB 계정도 game/auth DML 권한 분리.
- 실제 MySQL8.4.11, 외부 TLS Certbot5.8.0, .NET10 self-contained.
- 내부 DB TLS는 VerifyFull+CA+SAN, 계정은 REQUIRE SSL. 상호 TLS 검증 완료라고 표현하지 않는다.

## SSH와 키 정리

실제 연결은 `%LOCALAPPDATA%/ProjectDawn/AwsFreePlaytest-20261018/projectdawn-ssh`의 사용자 전용 ED25519 키를 사용한다. AWS key pair 이름은 `projectdawn-local-ed25519-20261018`이다. private key는 소스/문서/친구 ZIP에 포함하지 않는다. known_hosts도 같은 private 폴더에 보존했다.

IAB 다운로드가 전달되지 않아 사용하지 않은 AWS RSA key pair 두 개(`projectdawn-free-playtest-20261018`, `projectdawn-free-playtest-20261018-managed`)는 인스턴스에 연결되지 않은 상태로 남아 있다. 삭제했다고 보고하지 않는다.

## 인증서와 재개

공인 IP 인증서는 2026-10-24 만료 예정이며 약 6일 수명이다. `dawn-cert-renew.timer`가 UTC00/12시+최대30분 분산으로 검사한다. 갱신 hook은 PFX를 원자적으로 교체하고 gateway를 재시작하므로 접속 중 게임이 끊길 수 있다. dry-run은 기본적으로 hook을 생략한다. hook은 실제 인증서 설치 때 별도로 확인했다.

EC2 stop/start 시 공인 IP가 달라질 수 있다. 새 IP 인증서 발급 → 클라이언트 playtest.server.json 수정 → 실제 Unity TLS 확인이 필요하다. 유료 EIP를 자동 추가하지 않는다. SSH 소유자 공인 IP가 바뀌면 22 규칙은 새 /32로만 조정하고 전체 공개하지 않는다.

## 다음 확인

1. 실제 친구 PC4대에서 계정/캐릭터/채널/매칭/보상/재접속 검증.
2. 초기 방장 제안 지연 실패 원인 계측 및 장시간 반복.
3. AWS 정상 난도 클리어 보상 저장/재접속 실증.
4. 자동 비밀번호 복구, 개별 강제 로그아웃, 관리자 도구 실계정 통합 시험, 외부 암호화 백업, 정기 복원 훈련.
5. 테스트 종료 후 운영 일정·EC2 중지 여부·잔여 크레딧 확인.

전체 개발·배포·스킬·효과·DB 설명은 신규 개발자 HTML 인수인계 (`../Docs/DeveloperHandbook/index.html`, 로컬 검증 자료)를 본다.

## 후속: 채팅 배포 (17:14 KST)

접속자0 확인 후 서버·게이트웨이를 `Artifacts/cloud-release-20261018-171158/release.zip`으로 교체했다. SHA256 `9695ece4edbb41d9d1e1ab7160df3adee0a9c0d417f8d4d38bd1a39c244d2ff1` 로컬/업로드 일치. 이전 버전은 `/opt/projectdawn/release-before-chat-20261018`에 보존했으며 두 서비스 active를 확인했다. DB 스키마 변경/EC2 중지 없음.

최신 실행 `Artifacts/aws-flow-4p-20261018-171407`: 마을2채널/레이드파티 채팅9검사 통과, 네 명 정상 복귀. 전투25/26·흐름9/10으로 **전체 회귀 미통과**다. 전투60.7/69.6초에 TooLate 입력6건이 발생해 거절0 조건을 충족하지 못했다. 공통 상태181개·이벤트401개, 점프19/19는 일치. 지연 근본 원인 미확정이며 기존 통과 기록으로 이 실패를 대체하지 않는다. 전체 채팅/검증 기록 (`../CHAT-INTEGRATION-20261018.md`, 로컬 검증 자료), [서버 중지/시작](SERVER-STOP-START-KO.md).


## 후속: v14 1~4인·말풍선·8자 비밀번호

실행 `Artifacts/aws-flow-4p-20261018-184028`: 4인90초 전투와 마을복귀, 공통상태181·이벤트395 일치, 화면점프22/22 확정, 표시96개 계산틱오차0/관측0~1틱, 거절0/Unity오류0. 단일PC4프로세스이며 정상클리어 보상·물리IME·다른PC 검증은 별도다. 3인 실행183533의 TooLate1건과 이전v13 실패는 수정 완료로 처리하지 않는다.

`Cloud/freevm/admin-members.py`를 `/opt/projectdawn/release/ops/freevm/admin-members.py`에 root:root0750으로 별도 설치했다.183815 ZIP에는 없으므로 해당 ZIP 복구 시 함께 설치한다. 향후 Build-Cloud는 자동 포함한다. 읽기 전용 테스트 계정 조회와 단위11검사 통과; 실제 사용자 비밀번호/잠금 변경 없음. 두 게임 서비스 active, EC2중지·신규자원·유료전환 없음.


## 후속: 작성 후 Enter 전송 수정판

수정과 검증 (`../CHAT-ENTER-FIX-20261018.md`, 로컬 검증 자료). 클라이언트만 교체했다. 실제 TextField GUI이벤트→AWS 수신8/8·채널/파티9/9 통과.45초 인원/전투흐름16/17: TooLate1건 보존. 아래 이전4인 전투26/26 기록은184028 실행이며 이번45초 시험과 구분한다. 물리키보드·WindowsIME는 캡처도구 오류로 미검증.

## 후속: v15 재입장 안정화·공용 부활

접속자0 확인 후 기존 VM에 Linux `cloud-release-20261018-203800`을 배포했다. 이전 서버는 `/opt/projectdawn/release-before-reentry-20261018-203800`에 보존. DB 스키마·계정 변경 없음. 친구 최신 ZIP은 `friend-playtest-20261018-204311`이며 서버/클라이언트 규칙은 `dawn-reentry-revive-v15`로 함께 교체한다.

AWS2인 실제 패배→재입장→90초 전투·복귀13/13, AWS4인90초 전투26/26·흐름10/10·채팅17/17, 입력거절0·점프14/14 확정. 편도100±10ms 로컬4인도 전투26/26·흐름10/10·점프15/15 통과. 공용 부활3회, F5/버튼, HP50%·3초 무적·전멸 시10초 대기. 원인·실패 보존·검사 보정·전체 증거 (`../REENTRY-REVIVE-STABILITY-20261018.md`, 로컬 검증 자료).

검증 후 접속자0·레이드방0 확인. 기존 두 게임 서비스 active, 신규 리소스·유료 플랜 전환·EC2 중지 없음. 다른 물리PC/ISP 및 물리키보드/IME는 이 자동 시험에 포함되지 않는다.

## 후속: 2026-10-22 v21 정상 HP 4인 시연 후 중지

사용자 승인으로 기존 인스턴스를 시작하고 Free plan 잔여 크레딧 $99.67을 확인했다. 신규 리소스/보안 그룹 변경/유료 플랜 전환 없이 `cloud-release-20261022-060854/release.zip`을 배포했다. SHA256 `1b5a67e060e2c5b636307aa36083ec1115c087d83f7e8b9d3bde8d26e68b1df9`. DB 스키마 동일, 비공개 DB/서버 상태 백업 후 교체.

촬영 당시 IP `203.0.113.10`, IP 인증서 갱신 및 실제 TLS 접속 확인. 최종 `portfolio-4p-20261022-062155`: 정상 HP 32,500, 1페이즈 약 41초/2페이즈 약 35초, 채팅·클리어·SQL 보상·장착 기능 44/44. 실제 서버 TooLate 14건은 남은 이슈로 보존하여 네트워크 안정성 완료로 처리하지 않는다.

06:28 KST **EC2 중지됨** 확인. 종료 전 접속자 0/레이드방 0/매칭 대기 0. 현재 주소는 해제됐으므로 다음 시작 때 IP/TLS/접속 파일 재확인 필요. 상세 증거·영상·도구는 AWS 4인 시연 기록 (`../PORTFOLIO-AWS-4P-20261022.md`, 로컬 검증 자료).
