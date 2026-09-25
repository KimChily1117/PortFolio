# DirectX 11 개선 워크플로우

> 기준 문서: [`directx11-project-review.md`](./directx11-project-review.md)  
> 목적: 리뷰의 문제를 충돌이 적고 검증 가능한 작업 단위로 나누어 순차적으로 해결한다.

## 전체 흐름

```text
WF-00 기준선 고정
   ├─ WF-01 애니메이션 GPU ABI 수정 ── WF-02 인스턴싱/버퍼 경계 강화
   └─ WF-03 빌드 구성 정상화
                 │
                 v
          WF-04 오류 처리 체계
              ┌──┴────────┐
              v           v
       WF-05 창/스왑체인  WF-06 종료/COM 수명
                              │
                              v
                       WF-07 Transform 수명
                              │
                              v
                       WF-08 품질/렌더 구조
                              │
                              v
                       WF-09 최종 회귀 검증
```

`WF-01`과 `WF-03`은 독립적으로 진행할 수 있다. 이후 단계는 앞선 변경을 통합한 상태에서 순차 검증한다.

## 공통 작업 규칙

각 워크플로우는 아래 순서를 따른다.

1. 범위에 포함된 파일과 기존 동작을 확인한다.
2. 재현 또는 검증 기준을 먼저 정한다.
3. 해당 워크플로우 목적에 필요한 코드만 수정한다.
4. `Debug|x64`를 빌드한다.
5. `WF-03` 이후에는 `Release|x64`도 함께 빌드한다.
6. 가능한 기능은 런타임에서 확인한다.
7. 이 문서와 리뷰 문서의 체크박스 및 검증 기록을 갱신한다.
8. 하나의 워크플로우를 독립적인 커밋 경계로 마감한다.

공통 완료 조건:

- 새 컴파일 오류가 없다.
- 변경 범위와 직접 관련된 새 경고가 없다.
- 실패 경로가 조용히 무시되지 않는다.
- Debug 전용 동작에 의존하지 않는다.
- CPU/GPU 공유 타입 변경 시 C++과 HLSL을 함께 수정한다.

## WF-00: 기준선 고정

목표:

- 수정 전 빌드와 런타임 상태를 비교 가능한 기준으로 남긴다.

작업:

- [x] `Debug|x64` 전체 빌드 결과 기록
- [x] `Release|x64` 전체 빌드 결과 기록
- [ ] Direct3D 디버그 레이어 실행 기준 확보
- [ ] 로컬 서버가 필요한 런타임 조건 기록

완료 기준:

- Debug 성공과 Release 실패가 기존 리뷰 기록과 일치한다.
- 이후 변경과 비교할 오류/경고 기준이 존재한다.

## WF-01: 애니메이션 CPU/GPU ABI 수정

목표:

- CPU 애니메이션 상태와 GPU 상수 버퍼 레이아웃을 분리하고 정확히 일치시킨다.

주요 파일:

- `EngineCore/BindShaderDesc.h`
- `EngineCore/ModelAnimator.h`
- `EngineCore/ModelAnimator.cpp`
- `EngineCore/Shader.h`
- `EngineCore/Shader.cpp`
- `Shaders/00. Render.fx`

작업:

- [ ] CPU 전용 애니메이션 상태 타입 정의
- [ ] 이벤트 `std::map/std::function`을 GPU 전송 타입에서 제거
- [ ] `GpuKeyframeDesc`와 `GpuTweenDesc` 정의
- [ ] C++과 HLSL 필드 순서 및 패딩 통일
- [ ] GPU 타입을 `alignas(16)`로 정렬
- [ ] `std::is_trivially_copyable_v<T>` 검사
- [ ] `sizeof`와 주요 `offsetof` 검사
- [ ] CPU 상태에서 GPU 스냅샷 생성
- [ ] 루프, 비루프, 블렌딩, 종료 콜백 검증

완료 기준:

- GPU 전송 구조체에 STL 컨테이너나 포인터가 없다.
- HLSL 배열 stride와 C++ `sizeof`가 일치한다.
- 현재/다음 애니메이션과 종료 콜백이 정상 동작한다.

권장 커밋:

```text
fix(animation): separate CPU state from GPU tween data
```

## WF-02: 인스턴싱 및 GPU 버퍼 경계 강화

선행 조건: `WF-01`

주요 파일:

- `EngineCore/InstancingManager.cpp`
- `EngineCore/InstancingBuffer.*`
- `EngineCore/ConstantBuffer.h`
- `EngineCore/VertexBuffer.h`

작업:

- [ ] 애니메이션 인스턴스를 최대 500개 단위로 분할
- [ ] 배치별 `SV_InstanceID` 대응 검증
- [ ] 빈 인스턴스의 `Map/memcpy` 방지
- [ ] 모든 `Map` HRESULT 검사
- [ ] VertexBuffer 업데이트 용량 검사
- [ ] 상수 버퍼 16바이트 정렬 검사
- [ ] 빈 immutable 버퍼 생성 방지

경계 테스트:

- [ ] 0개
- [ ] 1개
- [ ] 499개
- [ ] 500개
- [ ] 501개
- [ ] 1000개 이상

완료 기준:

- 모든 경계에서 CPU 범위 초과가 없다.
- D3D 디버그 레이어에 버퍼 관련 경고가 없다.
- 501개 이상에서도 모든 인스턴스가 렌더링된다.

## WF-03: 빌드 구성 정상화

목표:

- Debug와 Release가 동일한 필수 컴파일/링크 설정을 사용하게 한다.

주요 파일:

- 네 프로젝트의 `.vcxproj`
- 새 공통 `.props`

작업:

- [ ] 공통 C++ 표준 설정 분리
- [ ] 공통 include/lib 경로 분리
- [ ] Release x64 Effect 설정 적용
- [ ] 프로젝트별 `IntDir` 분리
- [ ] 출력 디렉터리 규칙 통일
- [ ] 헤더 복사 PreBuild 단계 정리
- [ ] 존재하지 않는 외부 proto 경로 정리

권장 중간 디렉터리:

```text
Intermediate/$(ProjectName)/$(Platform)/$(Configuration)/
```

완료 기준:

- `Debug|x64`, `Release|x64` 클린 빌드 성공
- `MSB8028` 제거
- Effect 파일이 두 구성에서 동일하게 컴파일된다.

## WF-04: 오류 처리 체계 정비

선행 조건: `WF-03` 권장

작업:

- [ ] `CHECK(assert)`를 Release에서도 동작하는 처리로 교체
- [ ] HRESULT와 호출 위치 로그 추가
- [ ] 셰이더 오류 blob 안전 처리
- [ ] 컴파일 실패 후 즉시 반환
- [ ] `CloneEffect`, `Map`, 상태 객체 생성 결과 검사
- [ ] Graphics 초기화 실패를 `Game::Run`까지 전파
- [ ] Network/Sound 실패 정책 분리

실패 테스트:

- [ ] 존재하지 않는 셰이더
- [ ] 잘못된 HLSL 문법
- [ ] 존재하지 않는 텍스처
- [ ] 서버 미실행
- [ ] FMOD 초기화 실패

완료 기준:

- Debug와 Release의 실패 처리 결과가 일치한다.
- 원인이 파일과 HRESULT를 포함한 메시지로 확인된다.
- 실패한 객체가 이후 코드에서 역참조되지 않는다.

## WF-05: 창, 스왑체인 및 카메라 리사이즈

선행 조건: `WF-04`

작업:

- [ ] `WndProc`의 모든 처리 경로에서 값 반환
- [ ] `WM_SIZE`에서 client 크기 반영
- [ ] 최소화 시 렌더 중단
- [ ] RTV/DSV 언바인드 및 재생성
- [ ] `ResizeBuffers` 호출
- [ ] 뷰포트와 카메라 투영 갱신
- [ ] 피킹/UI 좌표 검증
- [ ] `GameDesc.vsync` 반영
- [ ] `windowed` 구현 또는 제거 결정

테스트:

- [ ] 반복 리사이즈
- [ ] 최대화/복원
- [ ] 최소화/복원
- [ ] 리사이즈 후 3D 피킹과 UI 클릭
- [ ] VSync on/off

완료 기준:

- 화면, 깊이 버퍼, 피킹 및 UI 좌표가 새 크기와 일치한다.
- 최소화 상태에서 0 크기 리소스를 만들지 않는다.

## WF-06: 종료 경로와 COM 리소스 수명

선행 조건: `WF-04`

작업:

- [ ] 시스템별 `Shutdown/Release` 인터페이스 통일
- [ ] ImGui backend/context 종료
- [ ] FMOD 리소스 종료
- [ ] 네트워크 서비스와 Winsock 종료
- [ ] D3D context `ClearState/Flush`
- [ ] 블렌드 상태를 `ComPtr`로 전환
- [ ] Debug live object 보고 추가

권장 종료 순서:

```text
App/Scene
→ UI/Particle/Resources
→ Sound/Network
→ ImGui
→ Graphics Context/Device
→ Win32 Window
```

완료 기준:

- 정상 종료 시 엔진 소유 D3D 리소스 누수가 없다.
- 초기화 중 실패해도 생성된 시스템까지만 안전하게 정리된다.

## WF-07: Transform 계층 수명과 재부모화

선행 조건: `WF-06` 권장

작업:

- [ ] `_parent`를 `weak_ptr<Transform>`으로 변경
- [ ] 이전 부모 자식 목록에서 제거
- [ ] `SetParent(nullptr)` 지원
- [ ] 중복 등록 방지
- [ ] 자기 부모 지정 방지
- [ ] 순환 계층 방지
- [ ] 씬 제거 시 자식 처리 정책 확정

테스트:

- [ ] 부모 지정/변경/해제
- [ ] 부모 및 자식 제거
- [ ] 자기 부모와 순환 계층 거부

완료 기준:

- 계층 객체가 씬 제거 후 정상 소멸한다.
- 재부모화 후 월드/로컬 Transform 계산이 정상이다.

## WF-08: 품질과 렌더 구조 개선

선행 조건: `WF-01`부터 `WF-07`까지의 필수 수정

### WF-08A: 경고와 로그

- [ ] 매 프레임 `std::cout` 제거
- [ ] `wchar_t → char` 변환 교체
- [ ] 수치 축소 변환 범위 검사
- [ ] UTF-8 소스 인코딩 설정
- [ ] 로그 레벨과 출력 채널 정리

### WF-08B: 리소스 타입 안전성

- [ ] `ResourceType::None` 표현 수정
- [ ] 지원하지 않는 리소스 타입을 `static_assert`로 차단
- [ ] 배열 인덱스 범위 검사

### WF-08C: 렌더 큐와 상태

- [ ] 불투명/투명/UI 큐 분리
- [ ] 렌더 상태 캐시
- [ ] 투명 객체 정렬 기준 정의
- [ ] 프러스텀 컬링 연결
- [ ] Effects 11 유지 또는 전환 계획 결정

## WF-09: 최종 회귀 검증

빌드 매트릭스:

| 구성 | 요구 결과 |
| --- | --- |
| Debug x64 | 성공 |
| Release x64 | 성공 |
| Debug Win32 | 지원 시 성공, 미지원 시 제거 |
| Release Win32 | 지원 시 성공, 미지원 시 제거 |

런타임 매트릭스:

- [ ] 서버 실행 상태 접속
- [ ] 서버 미실행 상태 안전한 실패
- [ ] 정적 메시
- [ ] 단일/다중 애니메이션과 블렌딩
- [ ] 501개 이상 애니메이션 인스턴스
- [ ] 파티클/UI/사운드
- [ ] 리사이즈/최소화/복원
- [ ] 피킹/UI 클릭
- [ ] 정상 종료와 D3D live object 확인

문서 마감:

- [ ] 리뷰 문서 체크박스 갱신
- [ ] 워크플로우 상태 갱신
- [ ] 보류한 기술 부채 기록

## 권장 실행 단위

| 묶음 | 워크플로우 | 성격 |
| --- | --- | --- |
| A | `WF-00` | 기준선 |
| B | `WF-01` | 애니메이션 ABI |
| C | `WF-02` | 인스턴싱/버퍼 |
| D | `WF-03` | 빌드 시스템 |
| E | `WF-04` | 오류 처리 |
| F | `WF-05` | Win32/DXGI |
| G | `WF-06` | 종료/리소스 수명 |
| H | `WF-07` | 씬 계층 |
| I | `WF-08A`, `WF-08B` | 경고/타입 안전성 |
| J | `WF-08C` | 렌더 구조 |
| K | `WF-09` | 회귀 검증 |

한 번에 하나의 묶음을 완료한다. `B`와 `D`는 병렬 진행할 수 있지만 `E` 이후는 통합 상태에서 순차 검증한다.

## 추가 작업: PICK-01 마우스 피킹 1차 최적화

상태: 코드 적용 및 Debug x64 프로젝트별 재빌드 완료

- [x] 클릭당 Picking Ray 한 번 생성
- [x] `Scene::Pick` 오버로드 구현 통합
- [x] Terrain 탐색을 `_terrain` 캐시 직접 접근으로 변경
- [x] 평면 Terrain의 전체 삼각형 순회를 로컬 평면 교차 1회로 변경
- [x] 클릭 커서 Model/Material을 `Start`에서 캐싱
- [x] 파생 PlayerController의 부모 `Start` 미호출로 발생한 null 모델 예외 수정
- [x] 클릭 시 캐시가 비어 있으면 한 번만 지연 로드하는 방어 경로 추가
- [x] 이동 좌표의 중복 `GetTileCorrectedPosition` 호출 제거
- [x] `EngineCore` Debug x64 재빌드 성공
- [x] `GameCoding2` Debug x64 재빌드 성공 및 `Binaries/Client.exe` 생성
- [ ] 실행 중 지형 모서리, 적 Collider 중첩, 연속 우클릭 동작 확인
- [ ] 필요 시 클릭 이펙트 Object Pool 적용
- [ ] 높이맵 Terrain 도입 시 Grid DDA/BVH로 교체

런타임 수정 기록:

- 증상: 우클릭 이펙트 생성 시 `ModelRenderer::SetModel → Model::GetMaterials`에서 읽기 액세스 위반
- 원인: `GarenPlayerController::Start`와 `AnniePlayerController::Start`가 부모 `PlayerController::Start`를 호출하지 않아 `_clickEffectModel`이 null로 유지됨
- 조치: 두 파생 클래스에서 `Super::Start()` 호출, `EnsureClickEffectModel()`을 통한 멱등 초기화와 클릭 시 지연 초기화 추가
- 검증: `GameCoding2 Debug x64` 재빌드 성공 및 `Binaries/Client.exe` 재생성

빌드 메모:

- 솔루션 전체 빌드는 프로젝트들이 `Intermediate/Debug`와 PDB를 공유해 `C1041`, `MSB8028`이 발생한다.
- 이번 검증은 `MultiProcessorCompilation=false`로 `EngineCore`와 `GameCoding2`를 순차 재빌드했다.
- 이 빌드 구조 문제의 근본 수정은 `WF-03`에서 프로젝트별 `IntDir` 분리로 처리한다.

## 현재 상태

| 워크플로우 | 상태 | 비고 |
| --- | --- | --- |
| `PICK-01` | 코드 완료 | 런타임 클릭 회귀 확인 필요 |
| `WF-00` | 부분 완료 | Debug/Release x64 기록 완료 |
| `WF-01` | 대기 | 첫 코드 수정 대상 |
| `WF-02` | 대기 | WF-01 이후 |
| `WF-03` | 대기 | WF-01과 독립 가능 |
| `WF-04` | 대기 | WF-03 이후 권장 |
| `WF-05` | 대기 | WF-04 이후 |
| `WF-06` | 대기 | WF-04 이후 |
| `WF-07` | 대기 | WF-06 이후 권장 |
| `WF-08` | 대기 | 안정성 작업 이후 |
| `WF-09` | 대기 | 최종 검증 |

핵심 개선 순서의 다음 작업은 `WF-01: 애니메이션 CPU/GPU ABI 수정`이다.
