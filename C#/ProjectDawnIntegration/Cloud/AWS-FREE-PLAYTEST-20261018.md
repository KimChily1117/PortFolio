# AWS 무료 플랜 확인 및 친구 테스트 준비

> **배포 전 확인 이력입니다.** 승인 후 실제 EC2·회원 인증·Unity4인·백업·갱신을 검증했습니다. 현재 운영은 [AWS-DEPLOYED-20261018.md](AWS-DEPLOYED-20261018.md)를 따릅니다. 아래 미생성/초대키 배포 절차는 현재 지시가 아닙니다.


확인일: 2026-10-18 (Asia/Seoul). 실제 로그인된 AWS Billing 및 서울 EC2 콘솔을 읽어 확인했다.

## 계정 확인 결과

| 항목 | 콘솔 확인 결과 |
|---|---|
| 현재 플랜 | Free plan. 무료 플랜에는 요금이 청구되지 않는다고 표시 |
| 활성 크레딧 | AWS Free Tier, USD 100.00 |
| 사용된 크레딧 | USD 0.00 (표시는 지연될 수 있음) |
| 무료 플랜 종료일 | 2027-04-18 (EC2 대시보드에 표시된 날짜) |
| 남은 기간 | Billing 183일 / EC2 182일. 화면별 반올림 차이가 있어 날짜를 기준으로 함 |
| 크레딧 시작/만료 | 2026-10-18 / 2027-10-18 |
| 서울 기존 EC2 | 인스턴스 0, EBS 볼륨 0, Elastic IP 0 |

**크레딧의 1년 유효기간이 무료 플랜의 6개월 기간을 늘려주지는 않는다.** 무료 플랜은 기간 만료 또는 크레딧 소진 중 먼저 발생하는 시점에 종료된다. 유료 플랜으로 전환하지 않는다. Organizations 가입 등 자동 유료 전환 동작도 하지 않는다. 비용 알림은 지출 상한 장치가 아니므로 무료 플랜 자체를 유지한다.

공식 근거: [AWS 플랜 정책](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier-plans.html).

## 이번에 완료한 로컬 준비

**후속 변경:** 사용자 요청으로 개인 초대 키 배포를 게임 회원가입/로그인으로 교체했다. 현재 인증/캐릭터 구현과 최신 테스트는 회원 계정 개발 기록 (`../MEMBER-ACCOUNTS-20261018.md`, 로컬 검증 자료)을 기준으로 한다. 아래 기존 산출물은 회원가입 추가 전 기록이며 최종 회원 빌드로 교체해야 한다. 생성해 둔 개인 초대 키는 이번 배포에 사용하지 않는다.

- [x] Linux x64 .NET 10 서버/게이트웨이 최신 퍼블리시
  - `Artifacts/cloud-release-20261018-161221/release.zip`
  - `manifest.json`에 SHA-256 기록
  - 고지연 점프 보정 v13 전투 코어 포함
- [x] TLS/초대 인증 소켓 테스트 10개 통과
  - `Logs/aws-freeplan-cloud-tests.log`
  - 잘못된 키, 다른 계정 사칭, 중복 로그인, 호스트 이름 불일치, 신뢰되지 않는 인증서, 평문 키 전송 거절 포함
- [x] 친구 배포용 Windows 클라이언트 ZIP 생성
  - `Artifacts/friend-playtest-20261018-161404/ProjectDawn-FriendTest.zip`
  - Unity 실행에 필요한 폴더만 허용 목록으로 포함
  - 개발 소스, Build/CombatCore의 빌드 중간 파일, DB/서버 설정, 개인 초대 키 제외
  - `files.json`과 `manifest.json`에 파일별/ZIP SHA-256 기록
- [x] `dawn_friend01` ~ `dawn_friend04` 초대 키를 서로 다르게 생성
  - `%LOCALAPPDATA%/ProjectDawn/AwsFreePlaytest-20261018`
  - 현재 Windows 사용자 전용 ACL, 토큰은 로그/채팅에 출력하지 않음
  - 서버에는 `invites.hashes.json`만 전송하고 각 친구에게는 본인 `.invite`만 별도 전달
- [x] 친구용 접속/테스트 안내 작성: `Cloud/FRIEND-PLAYTEST-KO.md`

현재 AWS 인스턴스 시작/키 페어 생성/업로드/인증서 발급은 실행하지 않았다. 인터넷 접속 주소는 아직 없다.

## 최소 서버 구성 초안

기존 `aws-playtest.json`은 RDS·Secrets Manager·Elastic IP·S3를 포함하는 별도 배포안이다. 이번 무료 테스트에 그대로 실행하지 않는다.

| 항목 | 이번 초안 |
|---|---|
| 리전 | 서울 ap-northeast-2 |
| 이름 | projectdawn-free-playtest |
| 인스턴스 | t3.small 1대, x86_64, 2 vCPU / 2 GiB |
| OS | Canonical Ubuntu Server 24.04 LTS, Pro/SQL 번들 제외 |
| 확인 AMI | ami-00000000000000000. 이후 다시 사용할 때 소유자/날짜 재확인 |
| 저장소 | gp3 20 GiB, 기본 3000 IOPS / 125 MiB/s, 암호화, 종료 시 삭제 |
| CPU 크레딧 | Standard로 제한할 예정. Unlimited 추가 크레딧 사용하지 않음 |
| 관리 접근 | 새 전용 SSH 키, TCP 22는 소유자의 현재 공인 IP /32만 허용 |
| 게임 접근 | TCP 28443. TLS + 게임 아이디/비밀번호 인증 |
| 인증서 검증 | TCP 80. ACME HTTP-01 검증용, DB/관리 API를 서비스하지 않음 |
| 내부 서비스 | FlowHost 127.0.0.1:28182, MySQL 8.4 loopback, 관리 HTTP loopback |
| 공개 주소 | 인스턴스 자동 공인 IPv4 + 무료 IP 인증서. 유료 도메인/고정 IP 구매 안 함 |
| 미사용 | RDS, NAT Gateway, Load Balancer, 유료 Marketplace, 별도 고객 관리 KMS 키 |

EC2 콘솔에서 t3.small과 gp3가 무료 대상이라고 표시된 것을 확인했다. t3.small의 표시 기준 Linux 요금은 시간당 USD 0.026이며, 무료 플랜에서는 사용량이 크레딧에서 차감된다. EBS·IPv4·전송량도 크레딧 사용량에 포함될 수 있으므로 **영구 무료 서버나 크레딧 미소모를 뜻하지 않는다**. 계정의 카드 청구를 허용하는 유료 전환은 하지 않는다.

2 GiB 사양에서 4인 전투와 MySQL을 함께 돌리는 실제 메모리/CPU 여유는 아직 측정하지 않았다. 첫 실행에서 확인하고 부족하면 자동 증설하지 않고 중단/조정한다.

## 도메인을 구매하지 않는 TLS 방식

Let's Encrypt의 IP 인증서는 일반 제공되고 Certbot 5.4+로 발급 가능하다. `shortlived` 프로파일은 6일 유효하므로 자동 갱신 및 게이트웨이 인증서 재로드가 필요하다.

참고: [Let's Encrypt 공식 Certbot 안내](https://letsencrypt.org/2026/03/11/shorter-certs-certbot).

- 클라이언트 주소: `tls://실제-공인-IP`, 별도 포트 28443.
- 인증서 SAN의 IP, 공개 CA 체인, 만료를 검증한다. 검증을 우회하지 않는다.
- 운영 IP 인증서로 Unity Mono 클라이언트까지 검증해야 배포 완료로 본다. 현재 소켓 테스트는 .NET에서 수행했다.
- EC2 중지 후 재시작 시 자동 공인 IP가 바뀔 수 있다. 실제 IP 재확인 → 해당 IP 인증서 발급 → 새 접속 주소 전달 순서가 필요하다.
- 첫 발급은 ACME 약관과 발급 대상 IP를 확인한 후 진행한다. staging 인증서는 친구 빌드에서 신뢰되지 않으므로 운영 발급까지 확인해야 한다.

## 다음 실행 순서와 완료 기준

1. EC2 시작 전 무료 플랜 상태 재확인, 새 SSH 키와 인바운드 공개 범위 확인.
2. 서울 EC2 1대 생성. IMDSv2 필수, Standard CPU 크레딧, 암호화 EBS, 추가 유료 옵션 없음 확인.
3. 기존 개발 DB를 업로드하지 않고 별도의 빈 MySQL 8.4 테스트 DB 설치. 스키마 초기화용 계정과 실행용 DML 계정 분리. DB는 loopback만 바인딩.
4. 서버 ZIP SHA-256 검증 후 배치. 기존 `ops/launch.py` 대신 [단일 VM 회원용 unit](freevm/README.md)을 사용한다. 보상 outbox는 `/var/lib/dawn-member-server/reward-outbox`에 영속 저장한다. 기본 게임 스키마 다음 `002_member_auth.sql`을 적용한다.
5. 게임 DML 계정과 회원 인증 DML 계정을 분리하고 서버 전용 접속 정보를 root 소유 0600 env 파일에 저장한다. DB 정보·게임 비밀번호·AWS 키는 클라이언트 ZIP/로그에 포함하지 않는다. 개인 초대 키는 배포하지 않는다.
6. IP 인증서 발급/갱신/재로드 확인 후 게이트웨이 실행. 22 / 80 / 28443 이외 공개 포트 없음 확인.
7. 실제 Unity 4개 클라이언트로 AWS TLS 로그인→채널→마을→4인 매칭→레이드→보상→복귀 검증. 서버 거절/지연/점프 판정 로그 확인.
8. 서로 다른 인터넷 회선의 친구 PC로 접속 테스트. 실제 외부 회선 테스트 전에는 '친구 접속 가능 확인 완료'로 표시하지 않음.
9. 테스트 종료 시 DB 백업과 크레딧 잔액 확인. 세션이 끝나면 인스턴스를 중지한다. 중지 중에도 EBS는 크레딧을 사용할 수 있으므로 남은 리소스를 기록한다. 데이터가 있는 인스턴스/디스크를 임의 삭제하지 않는다.

## 기존 검증과 구분

로컬 4인 100±10ms 단방향 지연 검증에서 점프 회피/상태 동기화는 통과했지만, 이는 AWS 실측 결과가 아니다. 80ms 별도 실행의 '대상 없는 방에서 메테오 4회' 검증기 실패는 기존 `HIGH-LATENCY-JUMP-20261018.md`에 남아 있다. 이번 인증 테스트 통과로 그 미해결 항목을 완료 처리하지 않는다.

방장 Lua 계산 → 서버 C# 검증·확정의 전투 권위는 변경하지 않았다.
