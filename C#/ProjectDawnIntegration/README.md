# Project Dawn — 바칼 협동 레이드

Unity 액션 클라이언트와 C# 서버를 연결한 **1~4인 협동 레이드**의 소스 공개본입니다. 기존 Project Dawn의 계정·채널·매칭 기반과 DNF 액션 프로토타입을 통합했습니다.

[Notion 포트폴리오](https://www.notion.so/3356402ecd9380a4aa4ecf15a672a62b) · [통합 설계·시연](https://www.notion.so/3376402ecd938052b979f38d08fda9a0) · [구조와 코드 읽기](Docs/ARCHITECTURE.md) · [빌드·검사](Docs/BUILD-SOURCE.md) · [검증과 한계](Docs/VALIDATION.md)

## 무엇을 구현했나

- **방장 계산·서버 검증:** Lua 패턴·행동 제안을 서버 C# 공통 코어로 검증. 입력 원장·시각 검증·미확정 구간 재판정·확정 사건과 예측 연출 연결.
- **완결된 흐름:** 로그인·채널·마을·코디 → 1~4인 선택 매칭 → 대기·카운트다운·로딩 → 바칼 2페이즈 → 시네마틱·보상 → MySQL 저장·장착.
- **콘텐츠:** 3×3 방, 2명 표식 메테오·겹치는 불바다, 점프 충격파, 지정 3연속 돌진, 기존 21개 스킬 체계·프렌지 토글·커스텀 커맨드.
- **표현과 운영:** 본인 중심 타격 연출·컷신, 보스/파티 HUD, 마을/던전 채팅, 포션·공용 부활·재접속, 공개 TLS와 AWS 배포 도구.

## 바로 읽을 코드

| 관심 영역 | 진입점 |
|---|---|
| 검증·확정·재판정 | [EncounterEngine.cs](Packages/com.projectdawn.combat/Runtime/EncounterEngine.cs) |
| 공통 전투 상태 | [CombatState.cs](Packages/com.projectdawn.combat/Runtime/CombatState.cs) |
| 예측 이벤트 연결 | [PredictionLedger.cs](Packages/com.projectdawn.combat/Runtime/PredictionLedger.cs) |
| Lua 패턴 | [bakal_patterns.lua](Lua/bakal_patterns.lua) |
| 실제 서버 연결 | [RaidCombatSession.cs](Server/Server/Game/Room/RaidCombatSession.cs) |
| 네트워크 계약 | [Raid.proto](Protocol/Raid.proto) |
| Unity 화면·입력·연출 | [Scripts](UnityClient/Assets/Scripts) |
| 회원·TLS 게이트웨이 | [CloudGateway](CloudGateway) |
| DB 스키마 | [MySQL migrations](Server/Server/Migrations/MySql) |

## 확인한 결과

2026-10-22 AWS 실행에서 한 PC의 독립 Unity 클라이언트 4개로 정상 클리어·보상·마을 복귀·실제 DB 장착까지 기능 **44/44**를 확인했습니다. **TooLate 14건은 남아 있습니다.** 서로 다른 물리 PC/ISP 시험이나 장시간 안정성 보장으로 해석하지 않습니다.

이번 공개본에서도 서버·게이트웨이·클라이언트 라이브러리를 빌드하고, 외부 리소스·실제 DB 없이 전투 26 / 커맨드 30 / Lua 50 / 방 이동·불바다 72개 검사를 통과했습니다. 상세 조건은 [검증 문서](Docs/VALIDATION.md)를 참고하세요.

## 포함하지 않은 것

원본 게임 리소스·스프라이트·음원·폰트·영상·추출 데이터, 실행 빌드, 운영 로그·DB 백업, 실제 계정·비밀번호·초대키·SSH/TLS 개인키·배포 접속 설정은 포함하지 않았습니다. 제3자 DLL은 커밋하지 않고 고정 버전 NuGet으로 복원합니다.

따라서 **클론 직후 원작 화면으로 플레이되는 배포 패키지가 아닙니다.** 서버·공통 코어의 빌드와 코드 검토·무자원 검사는 가능하며, Unity 전체 게임 실행에는 별도로 준비한 적법한 자원과 로컬 데이터/설정이 필요합니다. [제외 범위·출처](Docs/PUBLICATION-SCOPE.md)

## 개발 이력

- 기존 Dawn: [클라이언트](../Project_Dawn) / [서버](../Server)
- 초기 2인 데모: [DnfRaidDemo](../DnfRaidDemo)
- 이 폴더가 최신 통합 코드의 검토 진입점입니다. 기존 폴더의 파일을 통합판으로 간주하지 않습니다.
- 개인 프로젝트이며 Codex·Claude Code의 분석·구현·검증 보조를 활용했습니다. 원작 참고 자원과 자체 통합 구현을 구분합니다.
