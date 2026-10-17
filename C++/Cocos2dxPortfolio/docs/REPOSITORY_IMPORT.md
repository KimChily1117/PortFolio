# 저장소 정리 검증 — 2026-10-17

기존 로컬 `Cocos2dxPortfolio` 작업 폴더의 최신 소스·테스트·문서·선별 시연 자료를 `PortFolio/C++/Cocos2dxPortfolio`로 복사했다. 원본 폴더는 유지했다. 원본 저장소의 HEAD는 `0496b6c67720781ba0ef736bb141139aa3c9ac0b`였지만, 이번에 포함한 내용은 그 이후 미커밋 수정과 신규 파일을 포함한 4인 턴제 버전이다. 이전 ASTER 초기 커밋과 동일한 내용이라는 뜻이 아니다.

Git 추적 파일과 제외 규칙에 해당하지 않는 신규 파일 중 중복 압축본 `docs.zip`을 제외한 114개 파일을 복사하고 SHA-256 일치를 확인했다. 이후 저장소 안내, 제외 규칙, 이 기록을 보완했다. Git 공백 검사에서 지적한 4개 파일의 끝 빈 줄을 정리했으며, 게임 동작이나 테스트 로직은 수정하지 않았다.

## 새 경로에서 실행한 검사

| 검사 | 결과 | 범위 |
|---|---|---|
| CMake `ASTER_CORE_ONLY=ON`, VS 2022, Win32 Release | 구성·빌드 성공 | MSVC 19.38.33133.0, Windows SDK 10.0.26100.0 |
| `ctest --test-dir build-core -C Release --output-on-failure` | 2/2 통과 | `game_rules`, `skill_timeline` |
| `python -m unittest discover -s tests -p test_spine_adapter.py` | 1/1 통과 | 로컬 Python 3.13 및 Pillow 환경 |
| `python tools/test-epicseven-skills.py` | 실행 준비 단계에서 실패 | 제외된 외부 모듈 `E7_Scsp2Json`을 찾지 못함. 검사 본문은 실행되지 않음 |
| 프로젝트 Markdown 상대 링크 | 누락 대상 없음 | 업로드 대상 파일 기준 |

코어 구성·빌드 명령은 프로젝트 README의 저장소 안내에 있다. 스킬 컴파일러는 `.deps/epicseven-assets/tools` 및 하위 변환 도구를 import하므로 별도 로컬 준비가 필요하다. 이번 정리에서는 해당 도구나 원작 리소스를 공개 저장소에 추가하지 않았다.

## 이번에 다시 확인하지 않은 범위

전체 Cocos 렌더러 빌드, 저장·WebP·Spine fixture 검사, Android APK 빌드·설치와 실기기 시연은 이번 경로 정리에서 다시 수행하지 않았다. 기존 검증 결과와 실행 당시 조건은 [2026-10-15 통합 검증](FINAL_VALIDATION.md)에 보존한다.

엔진·외부 도구, 원작 추출/변환 리소스, APK 및 빌드 출력은 Git에 포함하지 않는다. 제출용 화면·영상과 출처 문서는 포함한다. 따라서 이 저장소의 소스 검사 성공과 전체 로컬 실행 환경 준비를 구분해야 한다.
