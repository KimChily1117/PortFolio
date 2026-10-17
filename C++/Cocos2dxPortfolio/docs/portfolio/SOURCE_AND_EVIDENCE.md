# 코드와 검증 근거

[프로젝트 소개](README.md) · [기술 사례](TECHNICAL_CASE_STUDY.md)

## 코드 읽기 순서

경로는 `Cocos2dxPortfolio` 저장소 기준입니다. 이 자료집의 소스 설명은 2026-10-15 검증본을 기준으로 하며, 이후 수정 시 함수명과 검증 결과를 함께 갱신해야 합니다.

| 질문 | 코드 진입점 | 읽을 내용 |
|---|---|---|
| 입력을 기다리고 유효한 행동만 받는가? | `Classes/core/GameModel.*`의 `useSkill`, `validTarget`, `tick` | 모델의 대상·쿨타임 검증, 수동/자동 경로 |
| 타격과 표시를 언제 연결하는가? | `Classes/GameScene.cpp`의 `drainPresentationEvents`, `showCutscene` | 이벤트 그룹, S3 선택과 대체 경로 |
| 평타가 끝까지 재생되는가? | 같은 파일의 `startAction`, `updateAction`, `finishAction` | 행동 시계, 타격 1회, 복귀, 후속 결과 처리 |
| 스킬 데이터는 어떻게 실행되는가? | `Classes/core/SkillTimeline.*`, `Classes/SkillPresentation.*` | 시간표와 시각 노드·카메라·이펙트 바인딩 |
| 포즈와 tint는 어디서 처리하는가? | `Classes/LegacySpinePose.cpp`, `Classes/SpinePreview.cpp`의 `draw` | 버전 호환, 배치 baseColor와 표시 색 합성 |
| 정지 중 늦게 준비된 음원은? | `Classes/ReferenceAudio.cpp`의 `advancePhases` | 비동기 재생 상태와 정지 시계 |
| 업데이트가 저장을 지우지 않는가? | `Classes/SaveStore.cpp` | 오류 구분, 기존 백업 보존, Android 백업 발행 |
| 변환을 재현할 수 있는가? | `tools/import-epicseven-skills.py`, `tools/import-epicseven-audio.py` | 입력·의존성·검증 보고서 |

## 함께 읽을 테스트

- `tests/CoreTests.cpp`: 잘못된 대상 거부, 무입력 대기, 턴·쿨타임·승패·보상.
- `tests/SaveTests.cpp`: v1/v2 이전, 백업 실패, 손상·미래 버전·열기 오류.
- `tools/smoke-reference.ps1`: 실제 Cocos 렌더러와 입력을 사용한 캡처 실행.
- `tools/validate-battle-actions.py`: 타격 전후 HP, 행동 중복, 적 접근, 정지 상태와 전장 복구.

## 이 자료집에 포함한 검증 기록

| 파일 | 쓰임 |
|---|---|
| [검증 요약](evidence/verification-summary.json) | 주요 수치, 지원 범위, 설치본 해시, 알려진 한계 |
| [일반 공격 검사](evidence/basic-attack-validation.json) | 51개 진단 시점과 Auto/S3 OFF 결과 |
| [Android 검사](evidence/device-validation.json) | 설치본·저장·영상·정지·재개·최종 설정의 확인 범위 |
| [APK 자산 검사](evidence/apk-validation.json) | 전체 자산의 경로·크기·CRC 대조 |
| [S3 모델 검사](evidence/skill-skeleton-validation.json) | 런타임 검사 결과 |
| [음원 파일 검사](evidence/audio-validation.json) | 29,003개 Ogg 파일 검사와 17개 S3 음향 단계 |
| [매체 출처](evidence/media-manifest.json) | 선별 캡처·영상의 원본 경로, 크기, SHA-256 |

`device-validation.json`은 이전 실행 전체의 기록입니다. 그 안의 캡처 이름 중 일부는 원래 검증 폴더 기준이며 이 자료집에는 대표 화면만 선별했습니다. 포함한 화면과 원본 파일의 대응은 매체 출처 파일에서 확인할 수 있습니다. 전체 진단과 REA 함수별 Evidence ID는 프로젝트의 `docs/analysis/` 및 기존 로컬 결과물에 보존합니다.

## 수치를 읽는 기준

**수집·탐색 범위:** 애니메이션 세트 488, 초상화 385, WebP 컷인 427, 음원 29,003. 이 수치는 플레이 가능한 영웅 수나 복합 S3 지원 수가 아닙니다.

**실행 연결 범위:** 아카테스·아라민타·클라릿사·도미니엘의 복합 S3 4개. 일반 공격은 현재 편성의 아군·적 리그에 연결했습니다.

**검증 범위:** 21개 S3 의존 모델 런타임 검사, WebP 427개 디코더 검사, Ogg 29,003개 형식·샘플 수 검사. 파일 검사 통과는 전체 화면 일치나 모든 음원을 직접 청취했다는 의미가 아닙니다.

**기기 증거의 구분:** v5에서 모션 영상·4초 메뉴 정지, v6에서 최종 설치·해시·앱 재개·설정·로비를 확인했습니다. Windows는 별도 저장 경로의 시나리오를 사용했습니다. 영상 평균 프레임률과 한 번의 메모리 샘플은 성능 벤치마크로 사용하지 않습니다.
