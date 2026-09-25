# 프로젝트 문서

DirectX 11 프로젝트 분석과 개선 작업은 다음 두 문서를 기준으로 관리한다.

1. [`directx11-project-review.md`](./directx11-project-review.md)
   - 프로젝트 구조와 실행 흐름
   - 심각도별 문제 분석
   - 최초 Debug/Release 빌드 검증 결과

2. [`directx11-remediation-workflow.md`](./directx11-remediation-workflow.md)
   - 문제 간 의존관계
   - `WF-00`부터 `WF-09`까지의 실행 단위
   - 워크플로우별 변경 범위, 테스트 및 완료 조건
   - 현재 진행 상태

3. [`navigation-grid-phase0-analysis.md`](./navigation-grid-phase0-analysis.md)
   - DirectX 클라이언트와 `D3D_Server`의 현재 이동 권위 분석
   - Navigation Grid 에디터 기반 구조와 누락된 기능
   - 공용 Nav Asset, Version/Hash, 좌표 규칙 권장안
   - 서버 A*, 이동 시뮬레이션 및 Phase 1 최소 범위

4. [`navgrid-v1-format.md`](./navgrid-v1-format.md)
   - NavGrid V1 Little-Endian 바이너리 레이아웃
   - XZ 좌표 규칙, SHA-256 정규화, 골든 에셋 명세

5. [`navigation-grid-phase1-implementation.md`](./navigation-grid-phase1-implementation.md)
   - C++/C# 로더와 테스트 구현 결과
   - `C_Move.objectId` 최소 보안 패치
   - 빌드·교차 언어 검증 결과와 다음 Phase 권장
6. [`navigation-grid-phase2-server-runtime.md`](./navigation-grid-phase2-server-runtime.md)
   - 서버 Content Root 및 안전한 상대 경로 정책
   - 맵별 Navigation 설정과 읽기 전용 Registry
   - Room 0 연결, Bounds 검증, 배포 및 테스트 결과

7. [`navigation-grid-phase3-authoritative-move.md`](./navigation-grid-phase3-authoritative-move.md)
   - Session/Room/NavGrid 기반 권위 목적지 검증
   - MoveRequest/Accepted/Rejected 및 NavigationInfo 프로토콜
   - Sequence, Blocked 보정, PlayerMovementState, 빌드·테스트 결과

8. [`navigation-grid-phase4-server-pathfinding.md`](./navigation-grid-phase4-server-pathfinding.md)
   - 서버 결정론적 8방향 A*와 Corner Cutting 금지
   - Walkable/Slow 비용, 탐색 제한, 불변 NavGridPath
   - MovementState 경로 등록, 회귀 및 145×145 성능 측정

코드 수정은 워크플로우 문서의 순서를 따르고, 완료한 작업은 두 문서의 체크리스트와 검증 기록에 반영한다.
