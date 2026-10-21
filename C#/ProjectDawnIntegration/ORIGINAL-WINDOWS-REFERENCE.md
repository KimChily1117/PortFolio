# 원본 장비·아바타·스킬창 리소스와 통합

## 확인 근거

설치된 DNF의 다음 NPK를 기존 PyDnfEx 디코더로 읽었다. 게임 폴더는 수정하지 않았다. 이전 바칼 보상 조사에서 현재 REA 제공자의 NPK `unsupported_target / file_format_rejected`가 확인되어 동일 형식에 대해 실패 호출을 반복하지 않았다.

| NPK | IMG | 관찰한 원본 부품 |
|---|---|---|
| `sprite_interface_newstyle_windows_inventory.NPK` | `inventory.img` | 장착 슬롯, 선택 강조, 아이템 칸, 구분선, 장비 배치 부품 |
| 위와 같음 | `inventorybackground.img` | 캐릭터 미리보기 배경, 248×179 |
| `sprite_interface2_ui_skillshop2.NPK` | `skillshop2.img` | 795×510 스킬창 바탕, 좌측 스킬 영역/우측 설명 영역, 36×48 스킬 칸, 탭·분류 장식 |

`sprite_interface.NPK`의 `windowcommon.img`도 추가로 추출해 원본 9조각 창 테두리, 파란 제목줄·버튼, 닫기 버튼을 적용했다. 새 자산 67개는 `UnityClient/Assets/Resources/DawnWindows`에 있다.

원본 리소스는 실제 설치본에서 확인했으며, 원작 UI의 배치 스크립트나 클릭 동작을 복원한 것은 아니다. 통합판에서 쓰는 장비 정보/4개 능력치/아이콘 배치/스킬 드래그 동작은 기존 기능에 맞춰 재구성했다. 원본에 있는 SP·TP·스킬 포인트 소비 기능을 구현하지 않은 상태에서 작동하는 것처럼 표시하지 않는다.

## 바뀐 화면

- **장비 인벤토리:** 캐릭터 미리보기 주변 착용 칸, 8열 아이템 칸, 선택한 장비의 정보와 장착 버튼, 서버가 확정한 캐릭터 능력치. 보상이 늘어나도 전체 아이템을 스크롤한다.
- **아바타 인벤토리:** 같은 캐릭터 미리보기/착용 부위 칸, 8열 외형 칸, 부위 선택, 현재 외형 정보, 기본 외형 버튼. 기존 SQL 외형 저장과 마을/던전 공유 동작을 유지한다.
- **스킬창:** 원본 스킬창 배경을 정확한 종횡비로 사용하고 아이콘 격자, 선택 설명 패널, 기존 14개 단축키 드래그/선택 배치를 넣었다. 원본 아이콘과 실제 데모 스킬 정의의 수치를 사용한다.

장비 정보는 카탈로그 설명보다 서버 `ItemInfo.DisplayName/IconPath/PhysicalAttack/PhysicalDefence/MoveSpeedPercent/AttackSpeedPercent`를 우선한다. 장착은 마을에서만 `C_EquipItem` 요청으로 수행하며 클라이언트가 능력치를 직접 고치지 않는다. 아바타도 기존 `Equip → FlowSendAppearance` 흐름을 유지한다.

## 공식 웹 사진과 대조한 수정

사용자가 원본 사진도 받아 대조하기를 요청해 공식 공개 가이드 이미지를 `Artifacts/original-windows-reference/web`에 참고 자료로 보관했다. 게임 런타임의 창 배경에 웹 스크린샷을 붙이지 않는다. 런타임 UI는 설치본에서 디코딩한 실제 부품으로 그린다.

- [공식 아바타 가이드](https://df.nexon.com/guide?no=1280): 캐릭터가 왼쪽, 착용 부위가 오른쪽인 배치를 확인했다.
- [공식 스킬·인벤토리 개선 안내](https://www.dfoneople.com/news/updates/1910/Convenience-Improvement): 세로로 좁은 아바타 창, 아래쪽 8열 슬롯, 파란 제목줄, 스킬창의 왼쪽 트리/오른쪽 설명과 드래그 기능을 확인했다.
- [공식 스킬창 사진](https://static.dfoneople.com/publish/bbs_data_st1_neople/images/pds/16f549f78c/image.png), [공식 아바타 인벤토리 사진](https://static.dfoneople.com/publish/bbs_data_st1_neople/images/pds/daee696b39/image.png).

대조 결과 최초의 넓은 장비 창/중앙 캐릭터 배치가 원본과 달라, **254×526 비율의 메인 패널을 1.25배**로 그리고 캐릭터는 왼쪽·장비 칸은 오른쪽·아래 8열 슬롯으로 바꿨다. 정보와 실제 적용 능력치는 별도의 인접 패널로 옮겼다. 큰 무기가 창을 넘어가는 현상은 미리보기 그룹 클리핑과 배율 조정으로 수정했다.

스킬은 공용·보조/전직/1차/2차/진각성 행으로 정리했다. 원작처럼 선행 스킬을 배워야 한다는 가짜 조건·화살표를 붙이지는 않았다. 스킬의 `물리 공격력 N%`는 통합판 전투에서 사용하는 **데모 계수**이며 원작 공식 계수를 복구했다는 뜻은 아니다.

단축키는 I 장비, K 스킬, **마을에서 U 아바타**다. 던전의 U는 기존 스킬 단축키를 유지하므로 I로 창을 연 뒤 아바타 탭으로 이동한다.

## 재현과 원본 추적

`Tools/ImportOriginalWindows.py`는 원본 부품을 재추출한다. `Tools/original-window-provenance.json`에는 NPK/IMG/PNG SHA-256, 프레임 번호, 원본 오프셋과 캔버스·실제 이미지 크기를 기록한다. 정적인 부품은 투명한 위치 지정 캔버스를 늘리지 않고 **실제 그림 크기**로 저장한다.

접촉 시트 및 원본 프레임은 `Artifacts/original-windows-reference`에 있다. 데이터 없이 모양만 확인하는 실행은 다음과 같다.

```powershell
./Build/ProjectDawnRaid.exe --original-windows-preview --window-preview-dir Artifacts/original-windows-preview --window-preview-exit
```

이 실행은 로컬 카탈로그로 만든 **가상 가방**을 표시하며 실제 서버/SQL에 접속하거나 장착 요청을 하지 않는다. 화면 상단에 이를 표시한다. Unity가 `inventory.png`, `avatar.png`, `skills.png`를 직접 캡처한다. 실제 저장/전투 수치 검증과는 분리해야 한다.
