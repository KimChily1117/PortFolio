# DirectX 11 프로젝트 분석 및 개선 계획

> 작성일: 2026-07-26  
> 대상 솔루션: `GameCoding2.sln`  
> 목적: 현재 엔진 구조와 확인된 위험 요소, 수정 우선순위 및 검증 결과를 이후 작업의 기준으로 유지한다.

## 추가 분석: 마우스 클릭 피킹과 이동 부하

2026-07-26 기준 우클릭 이동 경로는
`PlayerController::Update → Scene::Pick → Collider/Terrain::Pick` 순서다.

기존 병목:

- `Scene::Pick`이 Collider마다 같은 화면 좌표의 월드 Ray를 다시 계산했다.
- 두 `Pick` 오버로드가 거의 같은 구현을 중복 보유했다.
- Collider 순회 후 Terrain을 찾기 위해 전체 Scene Object를 다시 순회했다.
- `Terrain::Pick`은 145×145 셀의 삼각형 두 개를 모두 검사하여, 클릭 한 번에 최대 42,050회의 Ray-Triangle 교차 검사를 수행했다.
- 이동 좌표가 이미 결정된 뒤 `GetTileCorrectedPosition`을 중복 호출했다.
- 클릭 커서 `Model`과 `Material`을 우클릭할 때마다 디스크에서 다시 읽었다.

적용한 개선:

- 화면 좌표에서 월드 Ray를 클릭당 한 번만 생성한다.
- 두 `Scene::Pick` 오버로드를 단일 구현 경로로 통합한다.
- 캐시된 `_terrain`을 직접 사용하고 Collider와 Terrain 중 가까운 결과를 선택한다.
- 현재 Terrain이 높이 변화가 없는 Grid라는 전제에서, Ray를 Terrain 로컬 공간으로 옮겨 `y=0` 평면과 한 번 교차한 뒤 범위만 검사한다.
- 클릭 커서 모델은 `PlayerController::Start`에서 한 번 로드해 재사용한다.
- 피킹 결과를 이동 보정 좌표로 재사용해 중복 보정을 제거한다.

제약과 후속 판단:

- 현재 방식은 평면 Terrain에 최적화되어 있다. 높이맵이나 변형 Terrain을 도입하면 Grid DDA 또는 Terrain 전용 BVH로 교체해야 한다.
- Collider는 여전히 Scene의 모든 Collider를 선형 순회한다. 동적 객체 수가 커질 때 Spatial Hash, Quadtree 또는 BVH를 다음 단계로 검토한다.
- 클릭 이펙트 `GameObject` 자체는 클릭마다 생성된다. 모델 I/O는 제거했지만 클릭 빈도가 높아 GC/할당 흔적이 보이면 Object Pool을 적용한다.

## 1. 프로젝트 개요

이 저장소는 다음 네 개의 주요 프로젝트로 구성된다.

| 프로젝트 | 역할 |
| --- | --- |
| `EngineCore` | DirectX 11 초기화, 렌더링, 씬, 컴포넌트, 리소스, 애니메이션, UI, 입력, 사운드 및 네트워크 관리자 |
| `GameCoding2` (`Client`) | WinMain 엔트리포인트, 실제 게임 씬과 플레이어/스킬 로직 |
| `AssimpTool` | Assimp 기반 모델, 재질, 애니메이션 변환 도구 |
| `ServerCore` | IOCP 기반 클라이언트 네트워크 기반 코드 |

현재 실행 흐름은 다음과 같다.

1. `GameCoding2/Main.cpp`에서 `GameDesc`를 설정한다.
2. `Game::Run`이 Win32 창과 DirectX 11 장치를 초기화한다.
3. 입력, 시간, ImGui, 리소스, 네트워크, FMOD, UI 관리자를 초기화한다.
4. `SceneExcuter`가 `TestScene`을 현재 씬으로 설정한다.
5. 매 프레임 `Time → Input → Network → Sound → Graphics Begin → Scene/UI/Particle → Present` 순서로 실행한다.

## 2. 긍정적인 부분

- 엔진, 게임 클라이언트, 에셋 도구, 네트워크 코어의 책임이 분리되어 있다.
- 대부분의 Direct3D COM 객체가 `ComPtr`로 관리된다.
- `GameObject + Component + MonoBehaviour` 구조가 게임 로직 작성에 적합하다.
- Mesh, Model, Animation 렌더링을 인스턴싱 단위로 묶는 구조를 갖추고 있다.
- 셰이더 리플렉션을 이용해 입력 레이아웃을 생성하므로 정점 포맷 확장이 비교적 쉽다.
- 리소스 캐시, Assimp 변환기, 애니메이션 텍스처, UI 및 파티클 등 포트폴리오로 보여줄 구현 범위가 넓다.

## 3. 확인된 주요 문제

### 3.1 [Critical] CPU와 HLSL 애니메이션 데이터 레이아웃 불일치

관련 위치:

- `EngineCore/BindShaderDesc.h:49`
- `EngineCore/BindShaderDesc.h:65`
- `EngineCore/BindShaderDesc.h:107`
- `EngineCore/ConstantBuffer.h:27`
- `EngineCore/InstancingManager.cpp:128`
- `EngineCore/InstancingManager.cpp:138`
- `Shaders/00. Render.fx:75`
- `Shaders/00. Render.fx:86`
- `Shaders/00. Render.fx:96`

CPU의 `KeyframeDesc`에는 `loop`와 `Vec3 padding`이 포함되지만 HLSL의 `KeyframeDesc`에는 같은 필드가 없다. 이 때문에 CPU 구조체의 `next` 필드와 HLSL이 읽는 `next` 필드의 시작 오프셋이 일치하지 않는다.

CPU의 `TweenDesc`에는 애니메이션 이벤트를 위한 `std::map<float, std::function<void()>>`도 포함된다. 현재 `ConstantBuffer<T>::CopyData`는 `sizeof(T)` 전체를 `memcpy`하므로 STL 컨테이너의 내부 포인터까지 GPU 버퍼에 복사한다. GPU에는 의미가 없는 데이터이며 HLSL 구조체와도 호환되지 않는다.

애니메이션 인스턴싱 배열은 `MAX_MODEL_INSTANCE == 500`으로 고정되어 있지만 `InstancingManager`는 그룹 크기를 검사하지 않고 `tweens[i]`에 기록한다. 동일 모델/셰이더 애니메이터가 500개를 넘으면 CPU 메모리 범위를 벗어난다.

수정 방향:

- CPU 애니메이션 상태와 GPU 전송 데이터를 별도 타입으로 분리한다.
- GPU 구조체에는 고정 크기 숫자 타입만 둔다.
- `alignas(16)`, `std::is_trivially_copyable_v<T>`, `sizeof`, `offsetof` 정적 검증을 추가한다.
- HLSL과 C++ 양쪽에 동일한 필드 순서와 패딩을 명시한다.
- 500개를 초과하는 인스턴스 그룹은 여러 Draw 호출로 분할한다.
- 애니메이션 이벤트 컨테이너는 CPU 전용 상태로 유지한다.

### 3.2 [Critical] `Release|x64` 빌드 실패

검증 결과:

- `Debug|x64`: 빌드 성공, `Binaries/Client.exe` 생성
- `Release|x64`: 빌드 실패

주요 실패 원인:

- Release에 C++17 이상 언어 표준 설정이 없어 inline 변수와 `std::filesystem` 컴파일 실패
- `Libraries/Include` 경로 누락으로 DirectXTex와 EngineCore 헤더 검색 실패
- 라이브러리 경로와 출력 경로 설정이 Debug x64에만 집중됨
- Release FX 컴파일 설정 누락으로 `main` 엔트리포인트 오류 발생

관련 위치:

- `EngineCore/EngineCore.vcxproj:111`
- `EngineCore/EngineCore.vcxproj:139`
- `GameCoding2/GameCoding2.vcxproj:106`
- `GameCoding2/GameCoding2.vcxproj:123`

수정 방향:

- 공통 컴파일러/include/lib 설정을 `.props` 파일로 분리한다.
- Debug/Release 및 Win32/x64가 동일한 C++ 표준과 기본 include 경로를 사용하게 한다.
- FX 파일의 `ShaderType=Effect`, `ShaderModel=5.0`, 빈 엔트리포인트 설정을 모든 구성에 적용한다.
- 최소한 `Debug|x64`와 `Release|x64`를 CI 또는 로컬 검증 대상으로 유지한다.

### 3.3 [High] Release에서 DirectX 오류 검사 제거

관련 위치:

- `EngineCore/Define.h:33`
- `EngineCore/Shader.cpp:459`
- `EngineCore/Shader.cpp:471`

`CHECK`가 `assert(SUCCEEDED(...))`로만 정의되어 있어 Release에서는 검사 자체가 사라진다. 장치, 버퍼, 텍스처, 셰이더 생성 실패 후에도 실행이 계속되어 널 포인터 접근이나 원인 파악이 어려운 크래시로 이어질 수 있다.

셰이더 컴파일 실패도 `assert(false)` 후 계속 진행하므로 Release에서는 비어 있는 `blob`을 역참조할 수 있다.

수정 방향:

- `HRESULT`를 항상 평가하는 함수 또는 매크로로 교체한다.
- 실패한 HRESULT, 파일, 줄 번호와 DirectX 오류 문자열을 로그에 기록한다.
- 초기화 함수는 `bool`, `HRESULT` 또는 예외로 실패를 상위 호출자에게 전달한다.
- 셰이더 컴파일 실패 시 오류 메시지를 남기고 즉시 로드를 중단한다.

### 3.4 [High] 창 크기 변경 미지원

관련 위치:

- `EngineCore/Game.cpp:89`
- `EngineCore/Game.cpp:96`
- `EngineCore/Graphics.cpp`
- `EngineCore/Camera.cpp:64`

`WM_SIZE` 처리부가 비어 있어 창 크기가 변해도 스왑체인 버퍼, RTV, DSV, 뷰포트와 카메라 종횡비가 갱신되지 않는다.

수정 방향:

- 최소화 상태에서는 렌더링을 일시 중단한다.
- 기존 RTV/DSV를 언바인드하고 해제한다.
- `IDXGISwapChain::ResizeBuffers`를 호출한다.
- 백버퍼 RTV와 깊이 스텐실 텍스처/DSV를 재생성한다.
- 뷰포트와 모든 카메라의 width/height를 갱신한다.

### 3.5 [High] 설정값이 실제 스왑체인 동작에 반영되지 않음

관련 위치:

- `GameCoding2/Main.cpp:11`
- `EngineCore/Game.h:11`
- `EngineCore/Graphics.cpp:29`
- `EngineCore/Graphics.cpp:49`

`GameDesc.vsync`는 `false`로 지정되지만 `Present(1, 0)`으로 고정되어 항상 VSync가 활성화된다. `GameDesc.windowed`도 스왑체인의 `Windowed=TRUE` 고정값 때문에 사용되지 않는다.

수정 방향:

- `Present(desc.vsync ? 1 : 0, 0)`처럼 설정을 반영한다.
- 전체 화면 지원이 필요 없다면 사용되지 않는 `windowed` 필드를 제거한다.
- 지원한다면 최신 flip-model 스왑체인과 전체 화면 전환 정책을 명확히 구현한다.

### 3.6 [High] `WndProc` 반환값 누락

관련 위치:

- `EngineCore/Game.cpp:89`

`WM_SIZE`, `WM_CLOSE`, `WM_DESTROY` 처리 후 `LRESULT`가 반환되지 않는다. 반환형 함수의 일부 경로가 값을 반환하지 않으므로 정의되지 않은 동작이다.

수정 방향:

- 직접 처리한 메시지는 명시적으로 `return 0` 한다.
- `WM_CLOSE`에서는 필요에 따라 `DestroyWindow`를 호출한다.
- 처리하지 않은 메시지만 `DefWindowProc`로 전달한다.

### 3.7 [Medium] 블렌드 상태 COM 참조 누수

관련 위치:

- `EngineCore/Graphics.cpp:116`
- `EngineCore/Graphics.cpp:129`

`CreateAlphaBlending`이 raw `ID3D11BlendState*`를 생성하고 해제하지 않는다. 생성 결과의 `HRESULT`도 확인하지 않는다.

수정 방향:

- `ComPtr<ID3D11BlendState>` 멤버로 보관한다.
- 생성 결과를 검사한다.
- 전역으로 항상 알파 블렌딩을 켜기보다 불투명/알파/가산 상태를 캐시하고 렌더 패스에 따라 선택한다.

### 3.8 [Medium] Transform 부모-자식 강한 참조 순환

관련 위치:

- `EngineCore/Transform.h:42`
- `EngineCore/Transform.h:43`
- `EngineCore/Transform.h:64`
- `EngineCore/Transform.h:65`
- `EngineCore/Scene.cpp:116`

부모가 자식을 `shared_ptr`로, 자식도 부모를 `shared_ptr`로 보유해 참조 순환이 생긴다. 씬에서 제거해도 계층 객체가 해제되지 않을 수 있다. `SetParent`에는 이전 부모 제거, 중복 자식 방지, `nullptr`, 자기 자신 및 순환 계층 검사도 없다.

수정 방향:

- 부모 참조를 `weak_ptr<Transform>`으로 변경한다.
- 재부모화 시 이전 부모의 자식 목록에서 제거한다.
- `SetParent(nullptr)`를 지원한다.
- 중복 등록과 순환 계층을 검사한다.

### 3.9 [Medium] 초기화에 대응하는 종료 수명주기 부재

관련 위치:

- `EngineCore/Game.cpp:7`
- `EngineCore/ImGuiManager.cpp`
- `EngineCore/SoundManager.cpp:24`
- `EngineCore/NetworkManager.cpp`
- `ServerCore/SocketUtils.cpp:26`

Graphics, ImGui, Network, FMOD, UI 등을 초기화하지만 메시지 루프 종료 후 명시적인 종료 처리가 없다.

필요한 종료 작업:

- `ImGui_ImplDX11_Shutdown`
- `ImGui_ImplWin32_Shutdown`
- `ImGui::DestroyContext`
- FMOD sound/system 해제
- 네트워크 서비스 종료
- `SocketUtils::Clear`
- Direct3D context의 `ClearState` 및 디버그 빌드 live object 점검

### 3.10 [Medium] GPU 버퍼 템플릿의 안전성 부족

관련 위치:

- `EngineCore/ConstantBuffer.h:20`
- `EngineCore/ConstantBuffer.h:32`
- `EngineCore/VertexBuffer.h:28`
- `EngineCore/VertexBuffer.h:59`
- `EngineCore/InstancingBuffer.cpp:40`

문제:

- 상수 버퍼 크기가 16바이트 배수인지 검사하지 않는다.
- `Map`의 HRESULT를 검사하지 않는다.
- `VertexBuffer::Update`가 생성된 용량보다 큰 데이터를 복사할 수 있다.
- 빈 vector로 immutable 버퍼를 만들 때 `ByteWidth == 0` 또는 빈 `pSysMem` 문제가 생길 수 있다.

수정 방향:

- 상수 버퍼 타입에 `static_assert(sizeof(T) % 16 == 0)`를 적용한다.
- GPU 전송 타입에 `std::is_trivially_copyable` 검사를 적용한다.
- 모든 `Map/CreateBuffer` 결과를 검사한다.
- 버퍼 용량을 추적하고 초과 시 재생성하거나 실패 처리한다.

### 3.11 [Medium] 프로젝트별 중간 디렉터리 충돌

Debug 빌드에서 `MSB8028` 경고가 발생했다. EngineCore, Client, AssimpTool이 `Intermediate/Debug`를 공유한다. 병렬 빌드나 Clean/Rebuild 시 다른 프로젝트의 산출물을 잘못 삭제하거나 재사용할 수 있다.

수정 방향:

```text
Intermediate/$(ProjectName)/$(Platform)/$(Configuration)/
```

형태로 `IntDir`을 분리한다.

### 3.12 [Low] 프레임 루프 디버그 출력

관련 위치:

- `EngineCore/ImGuiManager.cpp:32`

매 프레임 `std::cout`으로 GUI 렌더 호출 횟수를 출력한다. 성능과 로그 가독성에 불필요한 영향을 준다. 제거하거나 조건부 프로파일링 카운터로 교체한다.

### 3.13 [Low] `ResourceType`의 unsigned 기반 `None = -1`

관련 위치:

- `EngineCore/ResourceBase.h:3`
- `EngineCore/ResourceBase.h:5`
- `EngineCore/ResourceManager.h:51`

`ResourceType`의 기반 타입은 `uint8`인데 `None=-1`을 사용하여 컴파일러가 값 잘림과 래핑 경고를 출력한다. 지원하지 않는 템플릿 타입이 `None`으로 매핑되면 `255`를 리소스 배열 인덱스로 사용할 위험이 있다.

수정 방향:

- `None`을 0으로 두고 나머지 값을 순차 배치하거나 signed 기반 타입을 사용한다.
- `GetResourceType<T>`의 기본 경로는 런타임 `None` 반환보다 `static_assert`로 컴파일을 막는다.

## 4. 권장 수정 순서

### 1단계: 렌더링 정확성과 메모리 안전성

- [ ] CPU/GPU 애니메이션 구조체 분리
- [ ] HLSL/C++ 레이아웃 정적 검증
- [ ] 500개 초과 애니메이션 인스턴스 배치 분할
- [ ] 상수/정점/인스턴스 버퍼의 크기와 `Map` 결과 검사

완료 조건:

- 단일 및 다중 애니메이션 인스턴스가 동일하게 정상 재생된다.
- 500개 및 501개 이상 테스트에서 범위 초과가 없다.
- Direct3D 디버그 레이어에서 상수 버퍼 관련 경고가 없다.

### 2단계: 모든 빌드 구성 정상화

- [ ] 공통 `.props` 파일 도입
- [ ] `Release|x64`의 C++20/include/lib/FX 설정 수정
- [ ] 프로젝트별 `IntDir` 분리
- [ ] Debug/Release x64 빌드 검증

완료 조건:

- `Debug|x64`, `Release|x64`가 모두 클린 빌드된다.
- `MSB8028` 경고가 사라진다.

### 3단계: 오류 처리 강화

- [ ] `CHECK(assert)` 교체
- [ ] 셰이더 컴파일 실패 전파
- [ ] 장치/리소스 생성 실패 로그 추가
- [ ] 네트워크 및 사운드 초기화 실패 처리

완료 조건:

- 누락된 셰이더/텍스처/서버 연결 같은 실패가 크래시 대신 명확한 진단으로 표시된다.
- Release에서도 오류 검사가 유지된다.

### 4단계: 창 및 스왑체인 수명주기

- [ ] `WM_SIZE` 리사이즈 구현
- [ ] 최소화 처리
- [ ] 카메라 종횡비 갱신
- [ ] VSync 설정 반영
- [ ] `WndProc` 반환 경로 수정

완료 조건:

- 창을 반복해서 확대/축소해도 화면과 피킹 좌표가 정상이다.
- VSync on/off 설정이 실제 프레임 출력에 반영된다.

### 5단계: 리소스 정리와 객체 수명

- [ ] Graphics/ImGui/Network/Sound 종료 경로 추가
- [ ] 블렌드 상태를 `ComPtr`로 전환
- [ ] Transform 부모를 `weak_ptr`로 전환
- [ ] 씬 제거 및 재부모화 테스트 추가

완료 조건:

- Direct3D live object 보고에서 엔진 소유 리소스 누수가 없다.
- 부모-자식 객체를 씬에서 제거하면 정상 소멸한다.

### 6단계: 품질 및 렌더링 구조 개선

- [ ] 매 프레임 콘솔 출력 제거
- [ ] 불투명/투명/UI 렌더 큐와 상태 분리
- [ ] 프러스텀 컬링 연결
- [ ] 문자열 인코딩 및 수치 변환 경고 정리
- [ ] `ResourceType` 경고와 지원하지 않는 타입 처리 개선

## 5. 검증 기록

### Debug x64

실행 명령:

```powershell
MSBuild.exe GameCoding2.sln /m:1 /nr:false /t:Build /p:Configuration=Debug /p:Platform=x64 /v:minimal
```

결과:

- 빌드 성공
- `Binaries/Client.exe` 생성
- 프로젝트 중간 디렉터리 공유 경고 발생
- `ResourceType` 값 잘림/래핑 경고 발생
- 다수의 `size_t → uint32`, `float → int32`, `wchar_t → char` 변환 경고 발생
- Effects 11 사용 중단 예정 경고 및 일부 HLSL 벡터 암시적 절단 경고 발생

### Release x64

실행 명령:

```powershell
MSBuild.exe GameCoding2.sln /m:1 /nr:false /t:Build /p:Configuration=Release /p:Platform=x64 /v:minimal
```

결과:

- 빌드 실패
- C++ 언어 표준 누락
- include 경로 누락
- DirectXTex 및 EngineCore 헤더 검색 실패
- FX 엔트리포인트 설정 오류

## 6. 작업 시 유지할 원칙

- 이 문서를 현재 프로젝트 개선 작업의 우선순위 기준으로 사용한다.
- 수정 시 체크박스와 검증 기록을 함께 갱신한다.
- CPU/GPU 공유 데이터는 이름이 같다는 이유만으로 호환된다고 가정하지 않는다.
- Debug 성공만으로 완료 처리하지 않고 Release x64도 검증한다.
- 렌더링 수정은 Direct3D 디버그 레이어 경고와 창 리사이즈까지 확인한다.
- 기존 동작을 변경하는 큰 리팩터링은 단계별로 빌드 가능한 상태를 유지한다.
