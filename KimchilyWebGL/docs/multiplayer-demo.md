# 칠리 아일랜드 — TypeScript 협동 포털 시연

게임별 로직은 서버와 클라이언트의 두 TS 파일에서 작성한다. C# 서버는 방·통신·위치 검증·제한된 JS 실행을 제공하고, C# Unity SDK는 범용 HUD와 엔진 접근을 제공한다.

[판정 수식·동기화·설계 이유·포트폴리오용 상세 설명](portfolio-typescript-multiplayer.md)

## 시연 순서

1. 같은 Wi-Fi에서 게시된 QR로 입장하고 각자 닉네임을 입력한다.
2. Unity 캐릭터·TMP 채팅·머리 위 말풍선으로 서로 접속한 것을 확인한다.
3. 모두 들어오면 TS가 만든 “모두 준비됐어요 · 시작” 버튼을 누른다.
4. 빛나는 서로 다른 발판을 한 명씩 맡는다. 혼자라면 별 발판 하나로 실행할 수 있다.
5. 서버 TS가 정한 유지 시간 동안 자리를 지키면 포털이 열린다.
6. 열린 포털의 중앙을 통과한다. 서버가 진입을 기록하면 빛나는 다리를 걸어 보라색 두 번째 정원으로 이동한다.
7. 별 → 달 → 해 → 잎 순서로 발판을 1.5초씩 밝힌다. 처음에는 시작 인원만큼 서로 다른 친구가 한 번씩 맡고, 그 뒤에는 직전 담당자와 교대한다. 혼자서는 네 발판 모두 진행할 수 있다.
8. 네 번째 발판까지 밝히면 중앙 새싹 기념물의 완주 연출이 모두에게 표시된다.
9. “새 탐험 준비”로 두 미션을 초기화한다. 다리의 바닥은 유지되므로 첫 정원으로 걸어 돌아와 다시 시작한다.

실제 두 휴대폰 플레이와 한 브라우저의 두 탭은 다르다. 숨긴 탭은 위치 전송이 멈출 수 있어 협동 유지 조건이 해제될 수 있다.

## 제작자가 보여줄 핵심

첫 번째는 서버 규칙 변경이다.

    // KimchilyCreator/ServerScripts/chili-portal/PortalRules.ts
    export const config = {
        holdSeconds: 5,
        // 발판 위치·반경·접지/신선도 판정 조건도 이 TS 파일에 있다.
    };

기본 3초를 5초로 바꾸면 같은 C# 호스트가 다른 시간에 성공을 판정한다. 클라이언트 안내와 진행률도 서버가 보낸 holdSeconds에 맞춰 표시된다. 운영자가 승인하는 규칙 번들 배포와 새 콘텐츠 게시를 수행하며, 기존 방을 실행 중에 강제로 교체하지 않는다.

두 번째는 클라이언트 UI와 연출 변경이다.

    import { Room } from "Kimchily.Network";
    import { Hud } from "Kimchily.UI";

    Room.useGame(SCRIPT_ID, SCRIPT_HASH);
    Hud.showPanel({
        eyebrow: "MY GAME",
        title: "친구와 함께 시작!",
        body: "문구와 버튼도 TS로 만듭니다.",
        action: { id: "start", label: "시작", enabled: true }
    });

    // Unity 입력을 TS Update에서 회수한다.
    const action = Hud.takeAction();
    if (action === "start") Room.sendAction("start");

    // 서버의 최종 결과를 읽는다. 상태를 직접 덮어쓰는 API는 없다.
    const room = Room.getState<MyGameState>();

1단계의 “동시에 밟기”와 2단계의 “순서대로 교대하며 밟기”를 같은 C# 호스트에서 실행한다. 확장 과정에서도 서버 TS의 상태 전이와 클라이언트 TS의 안내·연출을 수정했으며 게임별 C# 런타임 분기를 추가하지 않았다. 이동은 같은 씬 안의 실제 다리를 걷는 방식이고, 다른 월드로의 로드나 순간이동은 사용하지 않는다.

모바일 폭 600px 이하 또는 높이 600px 이하에서는 웹 상단 버튼과 Unity Canvas를 별도 행으로 배치한다. 화면 회전 시에도 CSS 버튼 영역과 Unity 채팅 영역이 겹치지 않는다.

## 수정할 파일과 빌드

| 파일 | 담당 |
|---|---|
| [PortalRules.ts](../KimchilyCreator/ServerScripts/chili-portal/PortalRules.ts) | 판정·유지 시간·승리·재시작 |
| [PortalGarden.ts](../KimchilyCreator/Assets/Demos/ChiliIsland/Scripts/PortalGarden.ts) | HUD·버튼·문구·진행률·발판·포털 |
| [PortalRuleIdentity.ts](../KimchilyCreator/Assets/Demos/ChiliIsland/Scripts/PortalRuleIdentity.ts) | 자동 생성되는 서버 규칙 ID/해시 |
| [ChiliIsland.unity](../KimchilyCreator/Assets/Demos/ChiliIsland/Scenes/ChiliIsland.unity) | 모델·배치·TS 오브젝트 바인딩 |

Task의 KimchilyCreator에서 다음 명령을 실행한다.

    .\tools\build_chili_island.ps1 -Publish

이 도구는 서버 TS를 먼저 컴파일·등록한 뒤 별도 클론에서 Unity 콘텐츠를 만든다. 원본 에디터를 닫을 필요가 없다. 서버와 공통 WebGL 실행기는 새 SDK 최초 도입 시 한 번 갱신해야 한다.

원본 프리팹 14종과 OBJ/MTL은 [아트 안내](chili-island-art.md)에 있다. 기존 Unity-Chan 플레이어는 재사용 자산이다.

## 버전과 검증

- TypeScript SDK 0.3.0: 일반 Room API와 Hud API.
- Networking SDK 0.4.0: ID/해시 연결, 상태 버전, 범용 TMP HUD.
- Creator 콘텐츠 SDK 0.1.0.
- .NET 10 C# 서버 + Jint 4.16.2에서 승인된 TS 컴파일 결과 실행.

이전 고정 프리셋 API의 게시 버전은 새 스크립트 API로 다시 게시해야 한다. 최신 결과는 [검증 기록](chili-island-validation.md)에서 확인한다. 이전 iPhone 실행 이력을 이번 TS 협동 게임의 실기기 검증으로 합산하지 않는다.
