# 출처와 구현 범위

[프로젝트 소개](README.md)

에픽세븐 설치본에서 얻은 캐릭터·배경·UI·효과·음원은 원작 및 해당 권리자의 자료입니다. 포트폴리오에서 설명하는 프로젝트 구현 범위는 데이터 분석 기록, 호환 계층, 렌더러 연결, 전투 모델, 연출 동기화, 저장 및 검증 코드입니다.

| 구분 | 사용한 기반 |
|---|---|
| 엔진 | Cocos2d-x 4와 외부 라이브러리 |
| 애니메이션·영상 | Spine C 런타임, libwebp |
| 분석 | REA와 Ghidra, 설치본 데이터·의사코드·참조 관계 |
| 구현 지원 | Codex를 활용한 분석·코드 작성·검증 |
| 변환 도구·메타데이터 | CeciliaBot/EpicSevenAssetRipper, Aikiooo/e7codex, Kutinana/E7SpineAssetConverter, NineS11942/SCSP-Spine-Viewer, CeciliaBot 자료 |
| 음원 변환 | vgmstream, FFmpeg/libvorbis |

커뮤니티 도구를 사용한 부분과 프로젝트에서 추가한 호환·실행 코드를 구분합니다. 원작 바이너리의 의사코드는 원본 소스와 동일하지 않으며, 본 프로젝트의 C++ 소스와도 구별합니다. 현재 음원 재생에는 Cocos AudioEngine을 사용합니다.

이 자료집은 문서와 프로젝트 실행 화면·영상으로 구성합니다. 원본 게임 EXE, 추출 자산 원본, APK, 변환 도구 바이너리는 포함하지 않습니다. 도구별 상세 출처·고정 리비전·라이선스 기록은 원래 프로젝트의 `THIRD_PARTY.md`와 로컬 provenance 보고서에 보존합니다.
