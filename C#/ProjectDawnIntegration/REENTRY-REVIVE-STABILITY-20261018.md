# v15 재입장·지연 입력·파티 공용 부활

## 요청과 원인 증거

- 재현: 2인 레이드 실패 → 마을 → 다시 매칭 → 입장 직후 강제 복귀.
- 실제 사용자 Player.log에서 재입장 뒤 `HostProposalOmissions` 종료를 확인했다. AWS 로그의 room/encounter ID는 새 값이었다. **방 ID 재사용이 관측된 원인은 아니다.**
- 기존 서버는 방장 미제안 입력을 전투 전체에서 누적해 3회면 종료했다. 순간적인 제안 지연도 누적되어 정상 진행 중 종료될 수 있었다.
- 기존 입력 시각 허용치는 최소 RTT/2 + 35ms였다. 낮은 RTT의 AWS 연결에서는 약37ms가 되어 개별 TCP/스케줄링 지연에 취약했다. `tick=0` TooLate는 유효 틱 계산 전 시각 허용치 검사에서 거절된 경우다.

## 변경한 계약

| 항목 | v15 동작 |
|---|---|
| 권위 | 방장 입력·Lua 제안 → 서버 C# 검증 → 확정. 미승인 동작을 서버가 임의 승인하지 않음 |
| 시각 검증 | 최소 RTT/2 + 80ms dispatch 여유. 전체 입력 마감 160ms 이내로 제한 |
| 확정 경계 | 제안 추가 예산300ms·최종확정480ms 유지. 확정 피해를 다시 열지 않음 |
| 방장 응답 누락 | 승인된 동작이 이어지면 누락 연속 상태 초기화. 누락3회 이상이며 유효 진행이 2초 이상 없을 때 종료 |
| 재입장 | 새 encounter별 입력 버퍼·예측·이벤트/진단·종료 사유 초기화, 시작 HP와 실제 전투 HP 일치 |
| 오래된 방 콜백 | 현재 소속 방 또는 해당 방으로 진행 중인 이동이 아니면 복귀 요청 무시 |
| 호환성 | 규칙 `dawn-reentry-revive-v15`, snapshot12. 서버와 클라이언트를 함께 교체 |

80ms는 무한 지연 허용이나 지연 입력 전부의 소급 인정이 아니다. 허용치·최종확정 이후 입력, 변조 시각·오래된 encounter는 계속 거절한다. `mappedAgeUs`와 `allowanceUs`를 서버 로그에 추가해 이후 TooLate의 위치를 구분한다.

## 공용 부활 사용법

- 레이드 시작마다 **파티 전체 3회**. 사망한 본인이 **F5** 또는 **부활 버튼**으로 요청한다.
- 서버 확정 시 최대 HP의 **50%**, **3초 무적**. MP·스킬 쿨타임·장비·현재 방은 유지한다.
- 마지막 1회를 동시에 요청하면 서버의 고정 슬롯 순서로 1명만 부활한다. 패킷 재전송은 추가 차감하지 않는다.
- 전원 사망해도 잔여 횟수가 있으면 **10초** 동안 부활할 수 있다. 횟수0, 기한 만료, 제한시간 종료는 패배다.
- 연결 끊김·퇴장 상태에서는 요청할 수 없다. 클라이언트 예측만으로 HP가 회복되지 않는다.
- 전멸 대기 중 살아 있는 표식 대상이 없으면 새 방 메테오 대상 선정은 미룬다. 부활 뒤 30틱의 제안 준비시간을 확보해 재개한다. 기존 장판은 계속 진행한다.
- 보스 HP·피해량은 이번에 변경하지 않았다. 부활 횟수는 전투 상태이므로 DB 스키마 변경은 없다.

## 주요 소스

- `Packages/com.projectdawn.combat/Runtime/ClockMapping.cs`: 측정 시각과 지연 허용치.
- `Packages/com.projectdawn.combat/Runtime/HostProposalHealth.cs`: 방장 제안 진행 감시.
- `Packages/com.projectdawn.combat/Runtime/Content/RaidSimulation.Revive.cs`: 부활 C# 규칙.
- `Packages/com.projectdawn.combat/Runtime/Content/RaidSimulation.Rooms.cs`: 전멸 중 표식 예약 처리.
- `Server/Server/Game/Room/GameRoom_Raid.cs`: 검증 결과·종료·시작 상태.
- `Server/Server/Game/Room/RoomTransferService.cs`: 오래된 방의 복귀 요청 차단.
- `UnityClient/Assets/Scripts/DawnRaidTransport.cs`: 전투별 상태 초기화와 입력 전송.
- `UnityClient/Assets/Scripts/RaidDemoApp.Revive.cs`: 부활 UI.
- `StabilityTests/Program.cs`: 실제 TCP·MySQL 실패/재입장 + 소규모 결정적 규칙 검사.

## 검증 절차

```powershell
. ./Tools/Use-MySqlEnvironment.ps1
dotnet run --project StabilityTests -p:DawnCloud=true
./Build-Unity.ps1
./Play-Flow4.ps1 -Port 28242 -CloudRuntime -DelayMs 100 -JitterMs 10 -ReturnAfter 90
python Verify-Flow4.py <실행폴더>
```

`Cloud/Play-Member4.ps1 -Reentry -PartySize 2`는 QA 전용 입력으로 첫 전투에서 일반 피격·부활·패배를 거친 다음, 같은 프로세스/연결로 재입장한다. 서버 HP를 조작하는 패킷은 없다. 각 전투의 감사 기록은 `round-1`, `round-2`에 보존한다. 별도 TCP 테스트의 패배 fixture는 테스트 프로세스의 내부 상태에만 적용한다.

## 검증 결과

| 검사 | 결과 / 증거 |
|---|---|
| 안정성·부활·실제 TCP 재입장 | 30/30 · `Logs/stability-v15.log` |
| 시각 / 예측 / 콘텐츠 | 34/34 · 64/64 · 86/86 |
| 전투 코어 / AI / 방 / Lua | 26/26 · 22/22 · 72/72 · 50/50 |
| 라이프사이클 / 1~4인·SQL 보상 | 46/46 · 125/125 |
| Unity 빌드 | Simulation/Motion/Frenzy/Thrust/asset/chat 검사 및 Windows 빌드 성공 |
| 실제 Unity 4인, 편도100±10ms | `Artifacts/flow-4p-20261018-204232`: 전투26/26·흐름10/10, 공통상태176·이벤트394 일치, 화면상 안전 점프15/15 서버 확정, 입력 거절0·프로토콜 종료0 |
| 실제 AWS 2인 실패→재입장 | `Artifacts/aws-flow-2p-20261018-204237`: 13/13, 자연 피격 후3회 부활·패배·마을·재매칭·90초 전투·정상 복귀. 새 encounter에서 방장도 변경. 두 라운드 입력 거절0 |
| 실제 AWS 4인90초 | `Artifacts/aws-flow-4p-20261018-204528`: 전투26/26·흐름10/10·인원/채팅17/17, 공통상태181·이벤트395 일치, 점프14/14 확정, 낙하 표시 틱 오차0·프레임 지연0, 입력 거절0·Unity오류0. 이 실행은 빈 방 생략 없이 모든 검사 회차4발 착지 |
| AWS 채팅 회귀 | 위4인 실행의 TextField Enter 전송8/8·채널/파티 격리9/9 |

4인 지연 시험과 AWS 2인 재입장 시험은 한 PC에서 총6개 Unity 프로세스로 동시에 실행했다. 실제 서로 다른 물리 PC/ISP나 OS F5 키 입력까지 검증했다는 뜻은 아니다. 서버 HP 조작·무적 QA 모드는 사용하지 않았다. 자동 플레이는 일반 입력 경로의 제한된 부활을 사용한다.

### 실패 기록과 검사 보정

- `flow-4p-20261018-203625`: 전멸 중 빈 대상 목록에서 Lua가 실패한 수정 전 실행. 방장 InvalidOperationException/HostDisconnected, 완료 감사 누락을 `incomplete-run.json`에 남겼다.
- `flow-4p-20261018-203939`: 추가 수정 후 입력 거절0, 점프12/12 일치였으나 자동 플레이 전멸로 전투 검사24/26. 이 실패를 삭제하지 않았다.
- 최종 지연 실행 `204232`의 최초 검사25/26 기록은 `verification-before-empty-room-evidence.json`에 보존했다. 누락된 첫 낙하의 명령 틱2064에서 방 이동 이벤트가 증명한 전원 위치는 **방1/3/7/3**이었다. 원래 C# 규칙은 보스방4에 살아 있는 대상이 없으면 그 낙하를 생략한다.
- `Tools/MeteorVolleyEvidence.py`는 실제 네 참가자 object ID와 서버 확정 RoomEnter 이벤트로 **전원이 보스방 밖임이 증명된 경우만** 생략을 인정한다. 4개 명령·87틱 간격·명령+72틱 실제 착지 대응을 검사하고, 실제4발이 모두 떨어진 회차도 최소1회 요구한다. 방에 사람이 있거나 ID가 불명확한 누락, 명령 누락, 늦은/추가 착지는 실패한다. 변조 fixture 포함7검사 통과. 전체 검사 기준을 단순 삭제하지 않았다.

## 배포

- 기존 AWS 서비스 교체 전 접속자0 확인. 신규 리소스/유료 플랜 전환/DB 초기화 없음.
- Linux: `Artifacts/cloud-release-20261018-203800/release.zip`, SHA256 `018dd702435242ce5734cfc7b2dc37e12d0d6d2ad978cf386d65bab68b00df16`.
- 이전 서버: `/opt/projectdawn/release-before-reentry-20261018-203800`.
- 관리 도구 `admin-members.py`는 root:root0750 유지. 서비스2개 active.
- 친구 클라이언트: `Artifacts/friend-playtest-20261018-204311/ProjectDawn-FriendTest.zip`, SHA256 `a03775b8df26abfe64ec2a4005b39508e62f5cc4999118ba6f233609ce634817`.
- 클라이언트144파일 CRC·개별SHA·실제빌드 일치·접속주소·금지 확장자 부재 검사 통과. 이전 ZIP과 서버 규칙이 다르므로 전체 교체한다.
- 검증 종료 후 AWS 접속자0·레이드 방0·마을 방2, 두 서비스 active 확인. 운영 서버는 켜둔 상태다.
