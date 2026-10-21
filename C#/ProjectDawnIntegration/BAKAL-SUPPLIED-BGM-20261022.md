# 제공 영상의 BGM 적용과 실제 게임 오디오 녹화

## 음원과 재생 규칙

사용자가 제공한 `C:/Users/YOUR_USER/Downloads/[DNF] 던전앤파이터OST _ 바칼 테마 (Bakal Theme) ▶NePLi.mp4`에서 오디오를 추출했다. 이전 DNF Music 폴더의 OGG를 그대로 쓴 것이 아니다. 원본 영상 길이는 약 7분 23.32초이며 AAC 스테레오 44.1kHz다.

- `Artifacts/bakal-supplied-bgm-20261022/bakal-theme-full.m4a`: 원본 AAC 스트림을 재인코딩 없이 추출한 전체 음원.
- 1페이즈: 요청한 경계인 0:00~2:14, 134초.
- 2페이즈: 2:14~영상 끝. 따라서 영상 후반의 엔딩 구간도 포함한다.
- 기존 OGG 두 파일은 같은 산출물 폴더의 `previous-*.ogg`로 보존했다.
- 출처 파일 경로·SHA-256·분할 위치·결과 해시는 `provenance.json`에 기록했다.

Unity `Resources/Sound/bgm_bakal_dragon_king_1phase.ogg`, `...2phase.ogg`를 교체했다. 조우 시 1페이즈 곡을 시작하고, 2페이즈 전장이 활성화될 때 두 번째 곡으로 전환한다. 조우 종료 때 1페이즈 곡을 처음부터 재시작하지 않는다. 네트워크는 기존 전투 상태만 전달하며 음원·오디오 샘플을 전송하지 않는다. AWS 작업은 없다.

## 재현

```powershell
cd E:\task\ProjectDawnIntegration
python Tools/ImportBakalBgm.py 'C:/Users/YOUR_USER/Downloads/[DNF] 던전앤파이터OST _ 바칼 테마 (Bakal Theme) ▶NePLi.mp4' --ffmpeg E:/task/ProjectDawnTools/media/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe
.\Build-Unity.ps1
.\Start-LocalGame.ps1
```

## 녹음 구조

`--record-four` 시 1P(내부 슬롯 0)의 AudioListener에서 실제 혼합된 BGM·타격음·음성을 WAV로 저장한다. 마이크·PC 전체 소리·다른 클라이언트 소리는 수집하지 않는다. 일반 플레이에는 녹음 컴포넌트를 만들지 않는다. `--record-audio`는 별도 화면 검증용 녹음 옵션이다.

DSP 콜백에서 버퍼를 복사하고 파일 쓰기는 메인 스레드에서 한다. 종료 시 WAV 헤더와 샘플 수·레벨·버퍼 누락을 기록한다. `Tools/MuxFourAudio.py`는 첫 DSP 콜백의 UTC와 화면 캡처 UTC를 맞추고, 필요한 앞부분 자르기/무음 패딩 및 클리핑 방지를 위한 전체 볼륨 조절만 수행한다. 음악을 영상에 사후 덧입히는 방식이 아니다. 정렬에는 오디오 버퍼에 따른 작은 오차가 있을 수 있다.

```powershell
.\Play-Flow4.ps1 -Port 28412 -CloudRuntime -DelayMs 100 -JitterMs 10 -ReturnAfter 105 -Record
# 출력된 run 폴더를 아래 인수로 지정
python Verify-Flow4.py <run>
python Tools/VerifyPhaseTwo4.py <run>
python Tools/BuildFourRecording.py <run> --ffmpeg E:/task/ProjectDawnTools/media/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe
python Tools/MuxFourAudio.py <run> --ffmpeg E:/task/ProjectDawnTools/media/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe
```

영상은 한 PC의 네 Unity 프로세스에서 자동 입력으로 촬영한다. 서로 다른 네 PC의 수동 접속 테스트와 구분한다.

## 검증 기록

최초 촬영은 저장 중 파일 크기가 0으로 표시되어 완료 검증 없이 중단했다. 파일 크기만으로 콜백 실패를 확정할 수 없으므로 이 촬영은 오디오 증거로 사용하지 않는다. 녹음 컴포넌트를 AudioListener보다 먼저 생성하도록 보강하고 종료 시 콜백과의 경쟁도 차단했다.

- 최종 빌드: `Logs/build-v20-audio-capture-final.log`, Unity 빌드 성공.
- 짧은 실제 실행: `Artifacts/bgm-audio-short-20261022/recording/game-audio.json`. 48kHz 스테레오, 1,591,296개 샘플 값(16.576초), peak 0.9224, RMS 0.1648, 누락 0. 조우 12장면, Unity 오류 0. 원본 AAC 추출물도 실제 디코딩해 무음이 아님을 확인했다.

### 최종 4인 촬영

산출물: `Artifacts/flow-4p-20261022-052026/video/index.html`.

- 전체: `four-player-with-audio.mp4`, 104.833초, 1920×1264, 12fps. 가까운 UTC 캡처를 정렬한 실제 네 클라이언트 화면.
- 진각성 비교: `awakening-with-audio.mp4`, 13초. 시전자는 컷신, 같은 방의 파티원은 스킬 FX, 다른 방의 파티원은 자기 방을 계속 표시한다.
- 전투 26/26, 로그인→마을→레이드→마을 10/10, 2페이즈 전환 7/7, 컷신·액션 표시 10/10 검사 통과. 지연은 방향별 100±10ms, Unity 오류 0.
- BGM 확인 4/4: 제공 음원의 리소스 해시 일치, 전원 조우 중 1페이즈 시작, 1인당 두 번의 트랙 시작만 존재, 2페이즈 전환 모두 서버 tick3016. 근거 `bgm-verification.json`.
- 1P AudioListener 원본 녹음은 105.067초, 48kHz 스테레오, 누락 0. 화면 시작에 맞춰 0.0718723초를 잘랐다. 오디오 피크가 1을 넘는 혼합 구간 때문에 영상 음량만 0.663854배로 조정했다. 효과음이나 음악을 새로 덧입히지 않았다.
- 최종 MP4를 끝까지 오디오 디코딩했다. 평균 -18.7dB, 최대 -0.4dB. `video/audio-verification.json`, `video/audio-decode-check.log`에 기록.
- 임시 로컬 테스트 서버와 지연 프록시는 종료했다. AWS 및 원격 서버는 조작하지 않았다.

파일 변환 결과와 소리 재생·저장을 검증했지만, 사람이 헤드폰으로 듣는 최종 음량·타격감 평가는 별도다. 영상 정렬은 DSP 버퍼 단위의 오차를 포함한다.
