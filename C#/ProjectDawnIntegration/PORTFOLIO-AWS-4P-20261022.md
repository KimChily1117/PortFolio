# AWS 4인 시연 · 2026-10-22

최종 실행: `Artifacts/portfolio-4p-20261022-062155`.

영상 플레이어 (`Artifacts/portfolio-4p-20261022-062155/video/index.html`, 로컬 검증 자료)에서 1P 확대/4인 동시 화면, 구간 이동, 채팅 발췌 영상을 볼 수 있다. 전체 260.75초(4분 20.75초), 채팅 발췌 약 46초다. 정확한 길이는 `video/recording-verification.json`을 따른다.

## 실제로 수행한 흐름

같은 마을 채널에 4명 접속 → Enter·문자·Enter 채팅/말풍선 → 순서대로 4인 매칭 → 대기 슬롯/닉네임 → 3·2·1·작전 시작 → 로딩 → 바칼 조우 → 1페이즈 약 41초 → 용 변신/전장 전환 → 2페이즈 약 35초 → 처치 → 제공 시네마틱 자연 종료 → Liberation/보상 카드 순차 공개 → 마을 복귀 → 실제 MySQL 보상 아이템 조회 → 일반 장착 요청 → 같은 아이템의 Equipped=1 재조회.

방장 Lua 제안 18개가 서버에서 확정됐다. 호스트 계산·서버 C# 검증 구조, 보스 최대 HP 32,500, 기존 피해량을 유지했다. 자동 입력의 공격 간격만 조절해 두 페이즈의 패턴을 충분히 보여주었다. 서버 HP 변경·무적·강제 클리어 fixture는 사용하지 않았다.

## 증거와 검증 범위

- `portfolio-verification.json`: 기능 흐름 44/44. 네 클라이언트 Unity 오류 0, 동일 encounter, 정상 클리어·마을 복귀·외형 일치·보상·장착 확인.
- `aws-live-status.json`: AWS에 실제 4명/레이드방 1개.
- `aws-after-status.json`: 촬영 종료 후 접속자 0/레이드방 0/매칭 대기 0.
- `aws-db-before-equip.json`, `aws-db-after-equip.json`: 보상 기록과 실제 item 행을 JOIN한 읽기 전용 조회. 아이템 89~92, 소유 캐릭터 9~12, 장착 전 0 → 장착 후 1. encounter `639282140586084654`.
- `p1`~`p4/flow-audit.txt`: 마을 메시지 각 4개, 던전 메시지 각 5개 수신. 양쪽 범위에서 말풍선 렌더링. 던전 방을 이동해도 파티 채팅 수신.
- `p1`~`p4/recording/frames.jsonl`: 실제 화면 JPEG의 UTC 시각. 출력은 12fps이며 해당 시각의 마지막 실제 프레임을 유지한다. 초기 과거 보상 확인 창을 닫은 뒤부터 시작한다.
- `p1/recording/game-audio.json`: 실제 1P AudioListener BGM/효과음, 누락 블록 0. 마이크/다른 앱 오디오는 수집하지 않았다. 피크에 맞춰 볼륨만 낮췄다. DSP 버퍼에 따른 오디오 동기 오차는 가능하다.

한 Windows PC에서 4개 Unity 프로세스를 실행해 공개 TLS로 AWS에 접속했다. 다른 물리 PC 4대/서로 다른 ISP/고지연 부하 검증으로 표현하지 않는다. 채팅은 실제 IMGUI 입력창에 Enter·문자·Enter 이벤트를 넣은 자동화다. Windows 실제 키 입력 검증은 화면 캡처 도구의 timeout으로 수행하지 못했으므로 물리 키보드·한글 IME 검증과 구분한다.

## 확인된 잔여 이슈

**TooLate 14건이 실제 AWS 서버 로그에 남았다.** 4개 클라이언트에 동일한 거절 결과가 전달되어 각각 14로 집계되므로 총 56건으로 더하지 않는다. `aws-server.log`의 `[RAID_INPUT_REJECT]`가 근거다. 측정된 입력 나이는 허용치 약 82ms보다 0.075~5.649ms 높았다. 원인이 녹화 부하인지, 서버 큐 지연인지, 경계 계산인지 이번 기록만으로 확정하지 않는다.

기능 시연 44개 통과와 네트워크 거절 0건은 별개다. `networkClean=false`를 보존했다. 다음 개발에서는 같은 조건으로 녹화 유무를 비교하고, 입력 채취→송신→수신→큐→시각 매핑 경계를 계측한다. 이미 확정된 전투 결과를 되돌리거나 시간 검증을 무조건 완화해 숨기지 않는다.

## 이번에 추가한 촬영 도구

- `RaidDemoApp.Portfolio.cs`: `--portfolio-demo`일 때만 동작하는 일반 입력 자동화/전체 씬 캡처/SQL 증거 표시/보상 장비 선택·장착. 일반 플레이와 서버 규칙은 변경하지 않는다.
- `Cloud/Play-Portfolio4.ps1`: 공개 TLS 엔드포인트·비공개 QA 계정 파일로 네 창 실행, 공통 UTC 일정으로 채팅 후 순차 매칭.
- `Tools/QueryPortfolioReceipt.py`: 기존 VM에서 root socket으로 특정 encounter의 보상/아이템 행만 읽는다. 회원 비밀번호·DB 연결 문자열을 읽거나 출력하지 않는다.
- `Tools/WatchPortfolioDb.ps1`: SQL 사전 조회 → Unity의 일반 장착 요청 → SQL 사후 조회. Unity 프로세스에 DB 자격 증명을 전달하지 않는다.
- `Tools/BuildPortfolioRecording.py`: UTC 정렬, 실제 게임 오디오 합성, 구간 이동 HTML, 채팅 2구간 발췌.
- `Tools/VerifyPortfolio.py`: 실제 결과 파일 검증 및 TooLate 잔여 이슈 보고.

QA 계정 파일은 프로젝트 외부의 사용자 LocalAppData/ProjectDawn 아래에 보관했다. 실행 인자·영상·공개 배포 ZIP에 비밀번호나 SSH 개인키를 넣지 않는다.

## AWS 최종 상태

기존 인스턴스만 06:08에 시작하고 06:28에 정상 중지했다. **현재 중지됨**이며 서버/DB 파일은 보존된다. 중지 화면 (`Artifacts/portfolio-aws-20261022/aws-stopped.png`, 로컬 검증 자료). 신규 VM/볼륨/공개 포트/유료 플랜 전환 없음. 시작 전 Free plan 잔여 크레딧 (비공개 운영 금액) 확인. 기존 구성의 실행에 크레딧이 사용되므로 사용량 자체가 0이라고 표현하지 않는다.

촬영 당시 IP는 `203.0.113.10`이었으나 중지 후 해제되었다. 다음 시작 때 새 IP와 TLS 인증서를 다시 확인하고 접속 파일을 갱신해야 한다. 과거 IP 파일을 현재 작동하는 주소로 배포하지 않는다.

서버 배포 ZIP `Artifacts/cloud-release-20261022-060854/release.zip`, SHA256 `1b5a67e060e2c5b636307aa36083ec1115c087d83f7e8b9d3bde8d26e68b1df9`, 규칙 `dawn-raid-countdown-v21`. DB 마이그레이션 SQL이 이전 배포와 동일함을 확인했다. 기존 서버 `/opt/projectdawn/release-before-portfolio-20261021_211036`, 비공개 DB/상태 백업 `/etc/projectdawn/private/backups/portfolio_20261021_211036`.

예비 실행 `061539`는 클리어·DB 장착까지 성공했지만 전투가 짧고 채팅이 없어 최종 영상으로 채택하지 않았다. `061441`(한 클라이언트 초기 로그인 미완료)와 `062045`(촬영 자동화 조정 중단)는 성공 시연으로 집계하지 않았다.

## 전달 파일 확인

세 MP4 전체 디코딩 성공, HTML의 로컬 링크 존재·구간 시간 순서 검사 완료. 브라우저의 file URL 정책이 미리보기를 차단하여 브라우저 재생/버튼 클릭 검증은 하지 못했다. 우회 서버나 다른 브라우저로 정책을 회피하지 않았다. 로컬 MP4와 HTML 파일 자체를 전달한다. `video/media-validation.json` 참고.
