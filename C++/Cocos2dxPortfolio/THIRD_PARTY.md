# 사용 리소스와 라이선스

- Cocos2d-x 4.0: https://github.com/cocos2d/cocos2d-x, MIT 계열 엔진 라이선스. 고정 commit `e3438ed3fd10a304b7ca2cd3dad9b29fead818d2`. 엔진의 `licenses` 디렉터리와 소스 헤더를 보존합니다.
- Cocos2d-x 외부 라이브러리: 공식 `cocos2d-x-3rd-party-libs-bin`의 `metal-support-22`. 개별 라이브러리 라이선스는 내려받은 `.deps/cocos2d-x/external` 및 엔진 `licenses`에 있습니다. 재배포 시 해당 고지를 함께 포함해야 합니다.
- Noto Sans CJK KR Regular: https://github.com/notofonts/noto-cjk, SIL Open Font License 1.1. 전문은 `Resources/fonts/LICENSE.txt`에 있습니다.
- `Resources/art/characters.png` 및 `heroes-animation.png`: 이 프로젝트용으로 built-in imagegen 도구로 생성한 캐릭터 시안. 기존 게임의 리소스를 복사하지 않았습니다. 프롬프트와 도구 출처는 `docs/ART_PROMPTS.md`에 기록합니다.
- 이전 ASTER 시안의 배경 도형과 UI는 Cocos2d-x DrawNode 및 Label로 작성했습니다. 현재 기본 화면의 선술집·버튼·지도·전장·영웅·이펙트·셰이더는 아래 에픽세븐 로컬 자료에 해당합니다.
- libwebp 1.6.0: https://github.com/webmproject/libwebp/tree/v1.6.0, BSD 3-Clause. `tools/bootstrap-webp.py`가 SHA256으로 고정한 공식 소스를 받으며 `COPYING`, `PATENTS`, `AUTHORS`를 보존합니다. 갤러리 컷신의 프레임 디코딩과 합성에 사용합니다.
- Spine C 런타임: Cocos2d-x에 포함된 `cocos/editor-support/spine`의 별도 Spine Runtimes License를 따릅니다. 해당 소스의 라이선스 전문을 보존합니다. Cocos 엔진의 MIT 고지와 구별합니다.
- 로컬 에픽세븐 자료: 사용자가 설치한 STOVE 클라이언트에서 추출한 캐릭터·컷인·UI·배경·사운드 자료이며 프로젝트의 자체 제작 아트에 해당하지 않습니다. 원본과 실행용 변환 파일은 Git에서 제외합니다. 추출 자체가 재배포 권한을 제공하지 않습니다.
- 사운드 변환: 기존 로컬 vgmstream r2117과 FFmpeg 5.0.1의 libvorbis 인코더를 변환 도구로 사용합니다. 도구 바이너리는 APK에 포함하지 않습니다. 28,984개 FMOD 샘플과 설치 영상의 19개 오디오 스트림은 원작 자료에서 파생한 Ogg이며 자체 제작 사운드가 아닙니다. 해시·원본 경로·디코더·샘플 길이·검증 결과는 `docs/analysis/AUDIO_PIPELINE_ANALYSIS.md`와 로컬 변환 보고서에 보존합니다. 프로젝트 재생기는 Cocos AudioEngine이며 원작 FMOD DLL을 SDK로 사용하지 않습니다.
- SCSP/SCT 변환 도구 및 커뮤니티 이름 메타데이터: CeciliaBot/EpicSevenAssetRipper, Aikiooo/e7codex, Kutinana/E7SpineAssetConverter, NineS11942/SCSP-Spine-Viewer, CeciliaBot/CeciliaBot.github.io, CeciliaBot/E7Tools. 각 고정 리비전·SHA256·출처·확인된 라이선스는 로컬 `.deps/epicseven-assets/tools/PROVENANCE.json`, `animated/_tool-provenance/`, `cutscene-analysis/community-metadata/sources.json`에 기록합니다. 상위 저장소에서 라이선스를 찾지 못한 파일은 그 사실을 기록했으며 엔진 라이선스를 적용하지 않습니다.

Windows 빌드에는 공식 엔진의 동적 라이브러리가, Android 빌드에는 엔진 및 외부 정적 라이브러리가 포함됩니다. 공개 배포 전에 대상 플랫폼의 라이선스 고지 패키지와 서명 설정을 별도로 정리합니다.
