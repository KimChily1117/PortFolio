# 원본 전투 UI·배경 자원 분석과 HUD 재구현

2026-10-15. 사용자가 설치한 에픽세븐의 추출 자원을 읽어 전투 화면을 조사했다. 방치 수입·강화 카드가 붙은 기존 화면 대신, 행동 순서와 현재 행동자의 세 가지 스킬을 중심으로 하는 독립적인 HUD를 구현했다. 전투 모델과 저장·진행 규칙은 HUD에서 소유하지 않는다.

## 증거와 해석의 범위

`tools/import-battle-ui.py`는 기존 CSB FlatBuffers 파서를 재사용한다. 실제 노드 트리, 옵션, 위치·크기·앵커, 터치 활성화, 이미지 경로를 읽는다. 해당 파일이 존재한다는 사실만으로 게임의 실제 실행 분기나 네이티브 함수 호출을 복원했다고 주장하지 않는다.

8개 파일의 498개 노드를 파싱했다. 상세 결과와 SHA-256은 `.deps/epicseven-assets/battle-analysis/layouts.json`에 남긴다. 모든 입력 CSB의 version 필드는 `2.1.0.0`이다.

| 원본 파일 | 노드 수 | 확인한 필드 |
|---|---:|---|
| `wnd/inbattle_hud.csb` | 49 | `n_skills=(1280,0)`, 자식 `skill=(-321.004,-0.001)`, `n_skill1=(64,57)`, `n_skill2=(163,57)`, `n_skill3=(262,57)` |
| `wnd/inbattle_hud_0.csb` | 70 | 동일한 우측 하단 스킬 구조, 두 번째·세 번째 슬롯의 x가165/261인 변형 |
| `wnd/inbattle_order.csb` | 156 | 세로 시간 막대 `_game_hud_timebg1.png`, 로컬 위치(25,93), 크기15×516, 하단 배경471×87 |
| `wnd/inbattle_topbar.csb` | 113 | `btn_top_auto`, `btn_speed`, `btn_speed2`, `btn_setting`, pause/auto/speed 원본 이미지 |
| `wnd/inbattle_target_guide.csb` | 52 | 여러 상태의 170×192 대상 안내 버튼과 하위 노드 |
| `wnd/inbattle_state.csb` | 4 | 상태 이름, `buff/stic_blind.png` 아이콘, 남은 턴 숫자 자리 |
| `wnd/inbattle_detail.csb` | 34 | `t_ut` 샘플 값 `84%`, HP bar 위치, 스킬·쿨타임 노드 |
| `wnd/skill_icon.csb` | 20 | 80×80 루트, (43,35)에81×84 스킬 이미지, 강화 frame, 스킬 번호·이름·비용 자리 |

대표 해시:

- `inbattle_hud.csb`: `5dca9cf4c7abfc3cc3c80f9edf72ca9627128f52a21cc416ce45fc4de55fbc07`
- `inbattle_order.csb`: `6d617a091271c5c0df84005c3842adffd5e2f999f948dbcac6f2537013bfdf0d`
- `inbattle_topbar.csb`: `c510f8c9e8d7f6fe4f2887299b1bd5d52496ae172c2781ef78cadc59c966564b`
- `skill_icon.csb`: `dc150405afb0f4f61f3afc7459df8378a778eb4425a080c4df441e1abe1939cd`

스킬 노드의 계층 위치를 합하면 중심은 약 `(1023,57)`, `(1122,57)`, `(1221,57)`이다. HUD가 사용하는 기본 배치다. 다른 노드의 로컬 좌표를 루트 좌표로 단순 오인하지 않는다. 배포판의 어느 HUD 변형을 언제 선택하는지는 자원만으로 확인되지 않는다.

## 실제 이미지 재사용

```powershell
python tools/import-battle-ui.py --evidence-only
python tools/import-battle-ui.py
```

두 번째 명령은 Pillow로 디코딩한 UI·스킬·얼굴과 배경 atlas region을 `Resources/reference/epicseven/battleui/`에 저장한다. 현재 3,363개 이미지다. `sources.json`은 원본 경로·해시·atlas 해시·region 필드를 기록한다. 원본 입력은 수정하지 않는다.

`hero-icons.json`은 아카테스(c1017), 아라민타(c1048), 클라릿사(c1028), 도미니엘(c1037)의 얼굴과 S1/S2/S3 이미지 경로를 검증한다. 예를 들어 `skill/sk_c1017_1.png`은 아카테스의 첫 번째 스킬 아이콘을 식별하는 자원 경로다. 아이콘 존재는 스킬 피해량·대상 규칙·쿨타임 수치까지 입증하지 않는다.

자동전투·속도·메뉴 버튼은 `cm_hud_top_btn_auto.png`, `cm_hud_top_btn_speed.png`, `cm_hud_top_btn_pause.png`를 사용한다. 행동 순서의 막대와 얼굴 틀, HP 색상 strip, 대상 표시 마커도 원본 PNG를 사용한다. 서버 기능, 소울번·신수 등의 미구현 동작을 작동하는 버튼처럼 추가하지 않았다.

## 배경 조립

원본 `bg/forest.sct`, `bg/battle.sct`, `bg/nworld1.sct`의 디코딩 PNG와 각각의 atlas를 조사했다. atlas 전체 이미지는 여러 부품이 포장된 texture이므로 완성 배경처럼 직접 화면에 확대하지 않는다. `xy`, `size`, `orig`, `offset`, `rotate` 필드대로 잘라 복원한다. 회전된 region은 packed 폭·높이를 교환하고 270도 회전하며, trim offset은 원본 캔버스의 아래 기준 값에서 이미지 위 기준 위치로 변환한다.

| 테마 | 실제 사용 region | 관찰한 그림 |
|---|---|---|
| forest | forest0, forest2, forest4, forest12, forest13_6 | 풀밭 바닥, 먼 숲, 관목, 나무 기둥, 잎 가지 |
| battle | battle_0, battle_11, battle_12, battle_16, battle_19 | 원형 문양 바닥, 기둥, 성소 계단, 구름 |
| nworld1 | nworld1_0, nworld1_1, nworld1_5, nworld1_6, nworld1_13 | 붕괴된 땅, 암석, 건물 잔해, 황혼 하늘 |

추가로 `.scsp`를 분석용 폴더에 복사한 후 기존 포맷 파서로 해독했다. `forest_tree.scsp`는 Spine2.1.27, bone17/slot5, forest0_2…6 attachment를 가진다. `forest_light1.scsp`는 bone9/slot8이며 forestlight1/2를 참조한다. `battlespine_1.scsp`는 bone10/slot24와 `idle` animation, battle_0/7/8/9/10 및 candle attachment들을 가진다. 결과 요약은 `.deps/epicseven-assets/battle-analysis/scsp/summary.json`에 남긴다. forest 두 파일의 해독 결과에는 animation이 없었다.

`BattleStageBackground`의 배치·카메라 위치와 5스테이지마다 바뀌는 테마 매핑은 이 프로젝트의 조립 방식이다. **원작 스테이지 스크립트의 배치와 전환을 복원한 결과는 아니다.** 원본 그림을 재사용하면서 1280×720 전투 공간에 맞춘다. `setCameraOffset()`은 먼 배경·중경·바닥에 서로 다른 이동 계수를 적용한다. 가짜 호흡이나 자동 스크롤을 배경 애니메이션이라고 표시하지 않으며, 자체 반복 타이머도 없다.

## C++ 통합 계약

`BattleHud::create(Callbacks)`의 callback은 `onSkill(int)`(0=S1,1=S2,2=S3), `onAuto()`, `onSpeed()`, `onMenu()`다. HUD에는 전투 모델 헤더 의존성이 없다.

| API | 입력 의미 |
|---|---|
| `setStage(string)` | 상단 스테이지 이름 |
| `setActiveHero(HeroView)` | 현재 행동자의 name, facePath, hp, maxHp |
| `setSkills(array<SkillView,3>)` | label, iconPath, cooldown, enabled |
| `setTurnOrder(vector<TurnView>)` | 최대8개. unitId, name, facePath, readiness(0…100), enemy, active. 정렬은 호출자 책임 |
| `setControls(bool,int)` | 자동 여부와 배속 표시 |
| `setStatus(string)` | 대상 선택·대기 안내 |
| `setInputEnabled(bool)` | HUD callback 허용. false이면 자동·속도·메뉴도 차단 |
| `setTargetMarker(Vec2,bool)` | 전투 좌표의 대상 마커 |
| `facePath(id)`, `skillIconPath(id,index)` | 가져온 이미지 경로 생성 |

HP는0…maxHp로 제한하여 표시하고, 스킬은 enabled이면서 cooldown≤0일 때만 callback을 낸다. 쿨타임과 게이지를 HUD 내부에서 임의로 감소시키지 않는다. 아이콘 경로가 바뀔 때만 texture를 변경하며, 행동 순서의8개 노드는 재사용한다. 방치 보상, 시간당 수입, 레벨 강화 카드는 포함하지 않는다.

## 전투 로직에 대한 결론의 한계

자원의 행동 순서 rail, 퍼센트 표시, 스킬3개와 cooldown 노드는 CR·턴·스킬 선택을 중심으로 하는 전투 화면 구성의 근거다. 속도에서 CR을 얼마나 올리는지, 초깃값 난수, 동률 우선순위, 행동 게이지 변경, 실제 damage 식, 스킬별 AI 우선순위는 CSB에서 추출할 수 없다. 이 값들의 구현·테스트·원작 참고 근거는 별도 전투 모델 분석에서 관리한다. 여기서는 화면 snapshot과 버튼 이벤트를 연결하며, 원작 수치나 서버 전투를 그대로 복제했다고 주장하지 않는다.

자원 파싱·이미지 디코딩은 성공했다. 원본 게임 런타임 화면과의 픽셀 비교는 확보되지 않았다. 최종 통합 빌드와 전투 화면 캡처로 실제 배치·입력·배경 가림을 확인해야 한다.
