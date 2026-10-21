# 채팅 작성 후 Enter 전송 수정

## 사용자 증상과 기존 검증의 누락

Enter로 입력창은 열리지만, 내용을 작성한 후 Enter를 다시 누르면 전송되지 않는다고 보고됐다. 이전 검증은 `ChatKey` 호출과 `chatDraft` 직접 대입으로 통신을 확인했으므로 포커스가 잡힌 `GUI.TextField`의 키 이벤트 경로를 검증하지 못했다.

## 수정 내용

- `Update`의 물리 키 입력과 `OnGUI`의 Return/KeypadEnter 이벤트를 같은 입력 상태로 모은다. `GUI.TextField`가 이벤트를 소비하기 전에 Enter 전송 의도를 받는다.
- 한글 조합 중 Enter를 버리지 않고 전송 요청으로 보관한다. 조합 문자열이 비고 한 프레임 동안 안정된 다음 현재 입력 내용을 전송한다. 마지막 글자가 들어오기 전의 문자열을 보내지 않는다.
- 키를 계속 누르거나 Update/OnGUI에서 중복으로 관측해도 한 번만 처리한다. 전송 직후 같은 Enter로 입력창을 다시 열지 않는다.
- 입력창 포커스는 정상 Repaint에서 등록된 컨트롤에 지정한다. ScrollView가 Used 이벤트에서 컨트롤 ID를 다르게 할당하는 경우의 포커스 불일치를 피한다.
- Esc·컨텍스트 변경 시 대기 중 전송을 취소하고, 전송 후 입력창 포커스를 해제한다. 서버 채팅 범위·속도 제한·본문 길이 제한은 유지한다.

Unity 2022.3의 [GUI.cs 원문](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Modules/IMGUI/GUI.cs)을 참고했다. 실제 게임 수정은 `UnityClient/Assets/Scripts/RaidDemoApp.Chat.cs`, 입력 상태는 `ChatEnterInput.cs`에 있다. 서버·DB·프로토콜 변경은 없다.

## 검증 방식과 한계

- `ChatEnterChecks`13검사: 열기/전송, 중복·키 반복, IME 완료 지연, 마지막 글자 반영 대기, 취소. IME 상태는 모의값이다.
- `--chat-auto`는 더 이상 전송 함수를 직접 호출하거나 문자열을 대입하지 않는다. 실제 OnGUI 경로에 Enter/문자/Enter 이벤트를 넣고, `GUI.TextField`에서 편집한 내용이 AWS를 거쳐 온전히 수신되는지 검사한다.
- 첫 이벤트 실행 `Artifacts/aws-flow-4p-20261018-185642`에서는 포커스 문제로 문자 입력과 전송이 확인되지 않았다. 정상 Repaint 포커스 지정과 이벤트 사이의 실제 Repaint를 추가해 수정했다. 실패 기록을 보존했다.
- Windows Computer Use로 실키 입력 검증을 시도했지만 창 캡처가 `FrameArrived timed out`, `window capture timed out`으로 두 차례 실패해 중단했다. OS 키보드와 실제 Windows 한글 조합 시험 완료로 표현하지 않는다.

## 최종 AWS 결과와 재배포

`Artifacts/aws-flow-4p-20261018-185839`: Enter/문자 입력/Enter로 마을·던전 각각 전송했고, 네 클라이언트가 전체 한글 문자열을 동일하게 수신했다. 말풍선 캡처도 확인했다. `chat-enter-verification.json`8/8, `chat-verification.json`9/9 통과. 정상 마을 복귀4/4, Unity오류0.

기존 채팅 검증기는 서버 ObjectId를 전투 슬롯0~3과 같다고 가정해 두 검사를 잘못 실패시켰다. 실제 `Entered ... object=` 로그의 안정된 서버 ID로 채널 수신자와 비교하도록 수정했고 기존 실패 파일도 보존했다. 이는 판정 기준 완화가 아니라 발신자 식별자의 올바른 대조다.

전체 인원/전투 흐름은16/17이다. 동일 전투 입력1건이 `TooLate`로 거절되어 네 클라이언트에 전달됐다(`Logs/chat-enter-aws-diagnostics.log`). 공통 상태91·이벤트256은 일치했다. 이를 해결했다고 주장하지 않는다. 이번 실행은45초 복귀 시험으로 이전90초 전체 전투26검사를 다시 실행한 것이 아니다.

배포 ZIP: `Artifacts/friend-playtest-20261018-185919/ProjectDawn-FriendTest.zip`,144파일, SHA256 `552039ccc9a89ca8d0b5e76aac6074528208df5262a3a5fede29bf6a5cb55d5b`. CRC·파일별해시·현재 시험 Build와 일치·민감파일 제외 확인. ZIP 전체를 풀고 `Start-ProjectDawn.cmd` 실행. 클라이언트만 교체하며 현재 v14 서버·DB는 그대로 호환된다. AWS 서비스 재시작·신규 자원·유료 플랜 전환 없음.
