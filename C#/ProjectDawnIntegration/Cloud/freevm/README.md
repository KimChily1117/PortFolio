# 단일 무료 플랜 VM · 회원 인증 배포안

Ubuntu 24.04/t3.small에서 실제 회원·Unity4인·DB9테이블 복원·TLS갱신을 검증했다. 첫 실행 제안 지연 실패와 최종 통과 기록은 실제 배포 기록 (`../AWS-DEPLOYED-20261018.md`, 로컬 검증 자료)에 남긴다. 기존 Secrets Manager/RDS용 `ops/launch.py`를 사용하지 않는다. AWS 자원을 생성하거나 유료 플랜으로 전환하는 스크립트는 포함하지 않는다.

## 설치 순서

1. Ubuntu 24.04에 MySQL 8.4, .NET self-contained 실행에 필요한 OS 라이브러리, 인증서 도구를 준비한다. MySQL은 loopback만 바인딩한다. 개발 PC DB를 복제하지 않고 별도 테스트 DB를 사용한다.
2. release.zip SHA-256 확인 후 `/opt/projectdawn/release`에 배치한다. Linux 실행 파일에 실행 권한을 지정한다. 두 OS 서비스 계정 `dawn-server`, `dawn-gateway`를 로그인 불가 계정으로 생성한다.
3. 마이그레이션용 관리자 DB 계정으로 `FlowHost --initialize-empty-database`를 실행해 기본 스키마를 만든 후 `ops/002_member_auth.sql`을 적용한다. 관리자 자격증명은 서비스에 넘기지 않는다.
4. 게임 DML 계정과 인증 DML 계정을 분리한다. 인증 계정은 account의 SELECT/INSERT, accountcredential의 SELECT/INSERT/UPDATE, authregistrationgate의 SELECT/UPDATE만 필요하다. 게임 계정은 기존 player/item/보상/매칭 관련 테이블의 필요한 DML만 허용하고 credential은 허용하지 않는다. DDL/GRANT 권한은 주지 않는다.
5. `/etc/projectdawn/server.env`에 `PROJECT_DAWN_DB_CONNECTION_STRING`만 지정한다. `/etc/projectdawn/gateway.env`에는 `DAWN_AUTH_DB_CONNECTION_STRING`, `DAWN_TLS_PFX`, `DAWN_TLS_PASSWORD`를 지정한다. 모든 값은 서버에서 생성하고 파일은 root 소유 0600으로 둔다. systemd가 읽어 각 프로세스에 전달한다. env 파일을 소스/ZIP/로그로 복사하지 않는다.
6. MySQL 연결은 TLS `SslMode=VerifyFull`과 신뢰 CA/인증서의 호스트 이름을 맞춘다. 로컬 DB 인증서를 구성하지 않았으면 운영 서비스를 시작하지 않는다. TLS 검증을 끄는 우회는 하지 않는다.
7. 공인 IP 인증서는 Certbot 5.4+의 IP/shortlived 방식으로 발급한다. PFX는 게이트웨이 계정만 읽을 수 있게 둔다. 자동 갱신 후 원자적 PFX 교체와 게이트웨이 재시작을 수행하는 deploy hook을 검증한다. 게이트웨이 재시작은 접속 중인 게임을 끊으므로 갱신 운영 시간을 안내한다.
8. 두 unit을 설치하고 시작한다. 외부에는 28443/TLS와 80/ACME, 소유자 IP의 22/SSH만 허용한다. 3306/28182/8090은 공개하지 않는다.
9. 실제 Unity에서 인증서 검증 → 가입 → 캐릭터 → 4인 레이드 → 복귀 → 재로그인을 확인한다. CPU/메모리/디스크와 Free plan 크레딧을 기록한다.

MemoryMax는 서버 768 MiB, 게이트웨이 384 MiB의 초기 제한이다. 첫 4인 종료 직후 전체 VM 사용 724MiB/available1182MiB였으나 지속 부하 보장은 아니며, 제한에 걸리면 자동 유료 증설하지 않는다. 재시작 시 보상 outbox는 StateDirectory에 남는다. backup-member.py로 서비스 정지 상태에서 DB9테이블 별도 복원·전체 행 대조와 outbox state 보존을 확인했다. VM 내부 백업은 외부 재해복구 백업과 다르다.

새 VM 설치는 install-base.sh → configure-member.py → TLS/hook/timer 순서다. configure-member.py는 MySQL을 재시작하며 새 전용 VM 부트스트랩용이다. release/ops에 001_initial.sql과 002_member_auth.sql이 모두 필요하다. 일상 업데이트 때 재실행하지 않는다. 내부 DB TLS의 CA는 AppArmor 허용 /etc/mysql 경로에도 배치하며 Oracle Connector 개인키는 PKCS#1 RSA 형식이다. VerifyFull+REQUIRE SSL이며 상호TLS 보장을 주장하지 않는다.
