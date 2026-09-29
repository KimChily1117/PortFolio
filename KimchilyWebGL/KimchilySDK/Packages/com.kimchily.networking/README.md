# Kimchily Networking 0.1.0

KimchilyServer protocol 1의 게스트 방·채팅 클라이언트입니다.
기존 Creator SDK의 콘텐츠 버전 계약을 변경하지 않는 별도 Unity 패키지입니다.

`InGameChat.Ensure().SetContext(worldId, revisionId)`로 호스트가 패널을 생성하고 컨텍스트를
설정합니다. `SetExpanded("true")`로 펼치며 사용자가 입장을 선택할 때만 접속합니다.
활성 연결의 월드/버전이 바뀌면 이전 소켓을 정리하고 새 컨텍스트로 입장합니다.
호스트 퇴장 과정에서는 `Disconnect("")`를 호출하여 재접속하지 않고 종료합니다.

WebGL에서는 `.jslib`가 브라우저 WebSocket과 Shadow DOM 채팅 패널을 제공하고,
모든 서버 상태 처리는 C# `InGameChat.Update`에서 수행합니다. Editor/네이티브에서는
ClientWebSocket 수신 큐와 IMGUI를 사용합니다. 네트워크 작업 스레드는 Unity API를 호출하지
않습니다. 이전 접속 세대의 이벤트를 무시하므로 퇴장 직후의 늦은 콜백은 다음 방에 반영되지 않습니다.

같은 월드·버전·방 코드의 참가자만 대화합니다. 채팅의 이름과 발신자 ID는 서버가 부여하며,
웹 UI는 `textContent`, Unity UI는 richText가 꺼진 라벨로 렌더링합니다. 열린 패널은 기존
`KimchilyMobileControls`를 잠시 끄고 닫힘/파괴 시 원래 활성 상태만 복원합니다.

Runtime 호스트와 Creator Editor가 패널을 설치하므로 제작 씬에 컴포넌트를 저장하지 않습니다.
기본 접속은 개발용 `ws://127.0.0.1:8790/ws`이며 WebGL은 페이지 호스트에서 주소를 제안합니다.
HTTPS에서는 WSS 주소가 필요합니다. 자동 세션 복구·계정 인증·캐릭터 이동 동기화·TypeScript
네트워크 API는 포함하지 않습니다. 자세한 실행 설정은 `KimchilyServer/README.md`를 참고합니다.
