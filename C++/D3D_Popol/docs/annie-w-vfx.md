# 애니 Q 색상 조정 · W 원본 효과 적용

2026-09-18. 클라이언트: `E:\task\C++\D3D_Popol`.

Q의 투사체, 꼬리 불꽃, 피격광과 충격파에 붉은 기운을 더했다. 일반 공격/Q 구분과 투사체 이동·피격 처리는 기존 구현을 유지한다. W에는 원본 부채꼴 메시를 이용한 전방 불꽃, 가장자리 화염, 불티, 바닥 잔열을 적용했다. V12에서는 Q 피격 폭발을 40% 확대하고 W 사거리·효과·cone을 함께 확대했으며, Q 원형 사거리 표시를 추가했다. 가렌 E 및 공용 ParticleSystem 코드는 수정하지 않았다.

## W 재생과 서버 판정

`ClientPacketHandler::Handle_S_SkillResult`에서 애니의 W(`skillId=2`)를 확인한 후 `AnnieWEffect::Play`를 한 번 호출한다. 로컬/원격 모두 패킷의 `castOrigin`, `castDirection`을 사용한다. 기존 중복 패킷 억제를 거치며, 캐릭터가 이동하거나 회전해도 효과는 시전 지점에 남는다. 기존 컨트롤러의 `PARTICLE->Play("AnnieW")` 호출은 제거했다.

- 전방 화염은 약 0.24초에 걸쳐 펼쳐지고 약 2.4초까지 유지·감쇠한다. 이후 불티와 잔열이 남으며 3.4초에 모두 종료한다. 최초 V10의 본 불꽃 0.85초, 전체 2.15초에서 연장했다.
- V12의 W 사거리는 5.6(기존 4에서 40% 증가), 전체 각도는 기존 50도다. 200ms 간격 10회, 회당 3의 피해는 유지한다. 셰이더가 동일한 부채꼴 경계에서 불꽃 표면을 부드럽게 감쇠한다. 서버 `AnnieSkillHandler.cs`의 W 사거리 상수도 5.6으로 변경했고 프로토콜은 유지했다.
- 서버의 XZ 좌표·방향은 고정한다. 현재 평면 Terrain의 높이만 시전 시 한 번 반영하여, 캐릭터 루트 높이 1.6과 지면 높이 2의 차이 때문에 바닥 효과가 묻히지 않게 했다. 높이가 변하는 지형에 대한 투영은 별도 구현이 필요하다.
- 로컬에서 직전 공격 애니메이션이 끝나지 않았어도 승인된 W 효과·소리·쿨다운은 처리한다. 원격 애니메이션도 이전 공격 대상 대신 W 시전 방향을 유지한다.
- `S_Damage`에 W 시전 식별 정보가 없으므로 개별 대상에 별도 W 피격 효과를 추정하여 붙이지 않는다.

## AssimpTool 사전 변환

원본은 `C:\Users\yeop_sun\Desktop\Extract\assets\characters\annie\skins\base\particles`에 있다. `Annie_Base_W_cas_Right`에서 참조한 메시와 텍스처를 사용하며, 이 엔진에 맞게 크기·색상·수명과 방출량을 조정했다. 원본 BIN의 모든 emitter/곡선을 실행하는 범용 재생기는 아니다.

AssimpTool의 SCB 변환에 `--keep-axes` 옵션을 추가하고 BGRA 정점 색상/알파를 보존했다. W는 지면 기준 원래 축을 사용하며, 원본의 가장자리 투명도가 유지된다. 위치·UV·색상을 함께 비교해 정점을 합친다. Q는 기존 축 변환을 계속 사용하며, 출력 파일이 이전과 바이트 단위로 동일함을 검사했다.

| 변환 메시 | 정점 | 인덱스 | 출력 바이트 |
|---|---:|---:|---:|
| annie_base_w_cone_1 | 441 | 2,400 | 29,024 |
| annie_base_w_cone_1_edge | 105 | 480 | 6,560 |
| annie_base_w_cone_1_edge2 | 210 | 1,080 | 13,580 |
| annie_base_w_cone_2 | 150 | 756 | 9,644 |
| 합계 | 906 | 4,716 | 58,808 |

원본 SCB 합계는 172,464바이트로, 변환 후 약 66% 줄었다. 실행 중에는 `VFXM`을 장면 초기화에서 한 번 읽어 immutable VB/IB로 만든다. DDS 6개도 ResourceManager 캐시를 사용한다. 파일 목록과 SHA-256은 `Resources/Textures/Annie/Particles/OriginalW/manifest.json`에 있다.

동시 W는 최대 4회로 제한하며 메시와 텍스처를 공유한다. 시전당 표면 6개를 풀에서 재사용하고, 모든 불티는 고정 크기 동적 버퍼 한 개로 묶는다. 시전 중 SCB 해석, 메시 파일 읽기, 불티마다 GameObject/VB 생성은 없다. W의 최대 드로 호출은 25회(표면 24 + 불티 1)다. 동시 5번째 W는 표시를 생략하며 서버 판정에는 영향을 주지 않는다. 이 수치는 구조상 상한이며 실전 FPS 개선 측정값은 아니다.

```powershell
# 프로젝트 루트에서 원본 가져오기 + AssimpTool 변환
python tools\import_annie_w.py C:\Users\yeop_sun\Desktop\Extract

# 단일 메시 변환 예시
& .\Binaries\AssimpTool.exe --convert-scb-vfx `
  '.\Resources\Textures\Annie\Particles\OriginalW\annie_base_w_cone_1.scb' `
  '.\Resources\Models\Annie\Vfx\annie_base_w_cone_1.vfxmesh' --keep-axes
```

SCB 형식 참고: [LeagueToolkit StaticMesh](https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMesh.cs), [StaticMeshFace](https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMeshFace.cs). 실제 변환은 프로젝트의 `AssimpTool/ScbVfxConverter.cpp`에서 수행한다.

## 검증과 실행

- AssimpTool 및 클라이언트 Debug x64 Rebuild 성공. 마지막 수정 후 클라이언트 증분 빌드 성공. 기존 프로젝트 경고는 남아 있다.
- W 셰이더 FXC 및 실제 DirectX 런타임 컴파일 성공.
- 원본 W의 모든 삼각형 위치/UV/RGBA, 축 유지, 투명도, 결정적 변환을 검증했다. Q 변환 회귀 검사도 통과했다.
- 숨겨진 DirectX 창에서 실제 렌더러로 Q/W를 재생했다. 기존 Q/평타 패킷 구분, 밝은 배경, 지속 시간, Q/W 가이드 선택·전환·취소·이동·쿨다운·사망과 GPU 캡처를 포함한 46개 검사 통과.
- 변경된 서버 어셈블리로 W 범위/각도 경계, 확대된 구간의 10회 피해, 아군 제외, 원점 고정, 클라이언트가 보낸 범위값 무시 등을 검증했다. 서버 검사 12개 통과.
- W 네 방향·동시 4회·종료·장면 교체, 실제 패킷 처리·중복 억제·이동 후 위치 고정·지면 높이 보정·이전 공격 대상과 방향 분리를 검사했다. 가렌 W 패킷이 애니 효과를 생성하지 않는 것도 확인했다.
- 로그인 후 실제 전투 화면에서의 외형과 FPS 측정은 수행하지 않았다.

```powershell
python Tests\AnnieQ\test_converter.py
python Tests\AnnieW\test_converter.py
Push-Location Tests\AnnieW
dotnet run --project ServerRangeTests.csproj
Pop-Location
Push-Location Binaries
.\Client.exe --annie-q-smoke  # Debug 전용. 이제 W 검사도 함께 실행한다.
Pop-Location
```

결과 보고서는 `Tests/AnnieQ/results/report.txt`, GPU 캡처는 같은 폴더의 `flight.png`, `impact.png`, `w-burst.png`, `w-peak.png`, `w-ground.png`, `w-four-directions.png`다. V11에서는 `flight-bright.png`, `w-peak-bright.png`, `impact-extended.png`, `w-sustain.png`도 확인했다. W 변환 보고서는 `Tests/AnnieW/results/converter-report.json`에 있다.

게임 실행은 작업 디렉터리를 `Binaries`로 두고 `Client.exe`를 실행한다. 현재 동일 빌드는 `Client_AnnieQW_AssimpV12.exe`다. 이전 실행 파일도 남겨 두었지만 셰이더/리소스는 공용 경로를 사용하므로 정확한 이전 버전 비교에는 백업한 소스·셰이더가 필요하다. **W 실제 판정도 확대하려면 이번에 빌드한 서버와 클라이언트를 재실행해야 한다.** 실행 중인 서버 프로세스는 자동으로 종료하거나 재시작하지 않았다.

이번 작업 전 기존 파일은 `tmp/annie-w-before`에 보관했고, 해당 기존 파일들의 변경 diff는 `tmp/annie-w-changes.patch`에 있다. 신규 W 구현은 `GameCoding2/AnnieWEffect.h/.cpp`, `Shaders/AnnieW.fx`, `tools/import_annie_w.py`다.

## V11: 지속 시간과 불투명도 조정

사용자 피드백에 따라 Q의 불꽃 잔상을 0.4초에서 0.8초, 연기 잔상을 0.65초에서 1.2초로 늘렸다. 피격 불꽃은 0.5초에서 1.4초, 충격파는 0.75초에서 1.6초, 연기는 0.75초에서 1.8초로 연장했다. 초반 알파를 유지한 뒤 부드럽게 감쇠하며, 침식 시작도 늦춰 지정 수명보다 일찍 사라지는 현상을 완화했다. 투사체 비행 속도·서버 판정은 변경하지 않았다.

Q 중심 메시·꼬리·피격 불꽃과 W 불꽃 표면에는 premultiplied alpha 합성을 사용한다. 알파가 충분한 중심부는 배경을 가리며 가장자리에는 원본 투명도를 유지한다. Q는 알파 보강 계수 1.6, W는 1.9를 사용하고, 광채·불티는 additive 합성을 유지한다. W의 UV 흐름과 침식 진행률도 분리해 연장된 시간 동안 불꽃이 유지되게 했다.

AssimpTool 변환 데이터, 풀 크기, 메시·텍스처 공유, 드로 호출 상한은 유지한다. 효과가 더 오래 보이므로 활성 프레임 수는 늘어난다. 조정 전 파일과 캡처는 `tmp/annie-qw-opacity-before`, 이번 변경 diff는 `tmp/annie-qw-opacity-changes.patch`에 보관했다.

## V12: Q 피격 확대 · W 범위 확대 · Q 사거리 표시

- Q 피격 시 생성되는 플래시·광채·불꽃·연기·충격파·불티의 시작/종료 크기와 퍼지는 속도에 1.4를 곱한다. 비행 중인 투사체 크기와 속도, 피해량은 유지한다.
- W의 **반경/길이를 4 → 5.6**으로 확대한다. 전체 각도 50도를 유지하므로 폭도 같은 비율로 증가한다. 면적 40% 증가라는 의미는 아니다. VFX의 메시, 바닥 UV, 불티 분포, 조준 표시, 시전 거리, 자동 접근 정지 거리, 요청 패킷, 서버 피해 탐색 범위를 함께 맞췄다.
- `AnnieSkillTuning.h`에서 클라이언트 Q 사거리, W 사거리/각도, Q 피격 배율을 공유한다. 서버의 W 상수는 별도 언어의 코드이므로 경계/피해 통합 검사로 일치 여부를 확인한다.
- Q의 기존 실제 시전 검사값은 8이었지만 `skillTable`에는 6이 남아 있었다. 기존 사거리 8을 유지하고 설정표도 8로 통일했다. Q 선택 시 반경 8의 원이 캐릭터를 따라 움직인다. 서버 Q 판정 정책에는 이번에 변경을 가하지 않았다.
- Q를 누른 뒤 적을 좌클릭하면 기존 타깃 스킬 흐름으로 시전한다. `Esc`·우클릭·다른 스킬 선택·시전·사망 때 원이 사라진다. 쿨다운 중에는 표시하지 않는다. 빈 바닥 클릭은 타깃 선택을 유지한다. 가렌에는 애니 범위 표시를 적용하지 않는다.
- `SkillRange.fx`는 원과 부채꼴을 수학적으로 그린다. W 가이드 끝을 반경 5.6의 원호로 잘라 실제 원형 부채꼴 판정에 맞췄다. 한 개의 기존 Quad와 미리 만든 Material을 재사용하며 시전 시 파일을 다시 읽지 않는다.

V12 가이드 캡처: `Tests/AnnieQ/results/q-range.png`, `w-range.png`, `w-range-overlay.png`. 서버 검증 결과: `Tests/AnnieW/results/server-range-report.txt`.

클라이언트 변경 전 파일/캡처는 `tmp/annie-range-before`, 변경 diff는 `tmp/annie-range-changes.patch`에 있다. 서버 한 줄 변경의 원본/적용본/해시는 `tmp/annie-w-range-server`에 보관했다. `tools/apply_annie_w_range_server.py`는 원본 해시와 정확한 변경 내용을 확인한 뒤 해당 서버 파일 한 개에만 적용한다.


## V13: Combat movement lock

Current executable: `Binaries/Client_AnnieQW_AssimpV13.exe` (same as `Client.exe`). See [combat-movement-lock.md](combat-movement-lock.md) for attack/skill movement restrictions and the Garen E exception. Q/W visual tuning remains V12.
