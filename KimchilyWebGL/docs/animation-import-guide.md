# 모델 애니메이션 연결과 Play 디버깅

기준: Unity 2022.3 / Kimchily Creator SDK, 2026-09-20.

**모델 prefab과 Animation Profile을 연결한 뒤, Idle / Walk / Run / Jump / Fall / Land에 실제 AnimationClip을 넣는다.** SDK가 플레이어의 실제 이동 속도·접지·수직 속도를 읽어 클립을 전환한다. 클립 이름이나 모델 이름은 자유이며 Animator Controller의 파라미터 이름을 맞출 필요가 없다.

프로필은 전신 이동 애니메이션용이다. Humanoid와 Generic의 리그 호환 조건은 여전히 필요하며, 임의 모델·파일 포맷·셰이더·표정·물리 스크립트까지 자동으로 호환시키는 기능은 아니다. Static 모델은 Profile 없이 사용할 수 있다.

## 1. Inspector에서 연결하기

1. Play Mode 밖에서 씬의 **Kimchily Mobile Player**를 선택한다. 플레이어가 없다면 **Kimchily → World → Add Mobile Player**로 만든다.
2. **Model Prefab**에 Project 창의 모델 GameObject/FBX 또는 prefab 자산을 넣는다. 씬의 Player 자신이나 AnimationClip을 이 필드에 넣지 않는다.
3. 모델에 앱에서 지원하지 않는 C# 경고가 있으면 **게시용 모델 복사본 만들기 · 연결**을 누른다. 원본은 보존하고 `<씬 폴더>/PublishedModels/<모델 이름>_Publishable.prefab`을 새로 만든다. 메시·재질·본·Avatar·Animator/Controller 참조를 유지하고, 미지원 스크립트와 그 기능을 제외한다. 씬을 저장한다.
4. Inspector의 **플레이어 애니메이션 → 애니메이션 Profile 만들기 · 연결**을 누른다. `Assets` 아래에 새 `.asset`을 저장하면 해당 플레이어의 **Animation Profile**에 연결된다. 기존 Profile을 드래그해도 된다. Project 창의 **Create → Kimchily → Player Animation Profile**에서도 자산을 만들 수 있다.
5. FBX 왼쪽 펼침 화살표를 눌러 **내부 AnimationClip**을 선택한 뒤 아래 슬롯에 넣는다. FBX 모델 자체나 `.controller`를 넣는 칸이 아니다. 별도 `.anim` 자산도 연결할 수 있다.
6. **Play**로 들어가 Game 창을 클릭하고 이동·점프를 시험한다. 아래의 **Play 디버그**에서 상태와 실제 속도를 확인한다.
7. Play를 끝내고 씬을 저장한 뒤 Android **Build & Publish**로 새 revision을 게시한다.

| Inspector 슬롯 | 연결할 동작 | 재생 방식 |
|---|---|---|
| Idle · 대기 | 정지 자세/대기 동작 | 반복 |
| Walk · 걷기 | 저속 이동 동작 | 실제 이동 속도에 맞춰 반복 재생 속도 조절 |
| Run · 달리기 | 고속 이동 동작 | Walk와 혼합하며 실제 이동 속도에 맞춰 반복 |
| Jump · 상승 | 도약·상승 동작 | 처음부터 재생하고 끝 자세 유지 |
| Fall · 하강 | 낙하/공중 자세 | 반복 |
| Land · 착지 | 착지 전용 동작 | 착지 유지 시간에 맞춰 한 번 재생한 뒤 이동 상태로 전환 |

모델에는 **본 계층에 맞는 Animator**가 있어야 한다. SDK는 시각 모델의 자식에서 첫 Animator를 사용하므로, 여러 Animator를 가진 모델은 이동을 담당할 Animator가 먼저 선택되는지 Play의 **대상 Animator**를 확인한다. 바깥 Player 루트에 임의로 Animator를 추가하면 클립의 본 경로가 맞지 않을 수 있다.

호환되는 Profile 클립이 하나 이상 있으면 SDK의 Playables가 그 Animator에 직접 포즈를 출력한다. **Profile이 활성화된 실행 중 모델 복사본에서는 기존 Controller를 저장한 뒤 잠시 분리한다.** Animator Controller의 자동 평가가 Profile의 포즈를 덮어쓰지 않도록 하는 처리이며, 기존 Controller의 상태·파라미터·레이어/표정 전환을 Profile에 합성하지 않는다. 재생 중에는 Animator의 Culling Mode도 Always Animate로 설정한다. Profile 재생을 해제하면 저장해 둔 Controller와 Culling Mode를 복구한다. 이는 Controller의 이전 재생 상태·시간까지 복원한다는 의미는 아니다.

**원본 모델 prefab·FBX·Controller 자산은 변경하지 않는다.** Profile을 비우거나 호환 클립이 전혀 없으면 SDK 애니메이션 전환을 시작하지 않고 기존 Controller를 사용한다. 새 Controller를 만들거나 Speed/Jump 파라미터를 작성할 필요가 없다. 원본과 게시용 모델 자산에는 Controller 참조가 남으므로 Controller에만 들어 있는 외부 C# 의존성도 여전히 게시 검증 대상이다.

## 2. Play 모드에서 확인하기

Inspector의 **Play 디버그**는 Play 중 계속 갱신된다. 조작은 **Game 창에 포커스**가 있어야 한다.

| 입력 | 동작 |
|---|---|
| WASD / 화살표 | 화면 방향 기준 이동 |
| Space | 접지 상태에서 점프 |
| 화면 오른쪽 시점 영역에서 우클릭 드래그 | 3인칭 추적 카메라 회전 |
| 왼쪽 조이스틱을 마우스/터치로 드래그 | 이동. 조금 움직이면 낮은 속도로 이동 |
| 오른쪽 점프 버튼 | 점프 |

**3인칭 추적 카메라**를 끄면 월드의 활성 카메라로 본다. 이때 우클릭 드래그로 월드 카메라를 회전시키지는 않는다. 추적 카메라 필드가 비어 있다는 것만으로 월드에 카메라가 없다는 뜻은 아니다.

| 디버그 항목 | 읽는 방법 |
|---|---|
| 목표 애니메이션 상태 | Idle / Walk / Run / Jump / Fall / Land. 물리 상태에 따른 목표이며, 혼합 중에는 이전 포즈가 남고 누락 슬롯은 대체 클립을 사용할 수 있음 |
| 수평 속도 | 입력값이 아니라 CharacterController가 실제 이동한 결과의 m/s |
| 이동 입력 | WASD/조이스틱 입력. 입력은 있는데 속도가 0에 가까우면 충돌·Pause·컨트롤러 상태 확인 |
| 접지 / 수직 속도 | 점프 시작 가능 여부와 상승·하강 상태 확인 |
| 충돌 / 월드 위치 | 벽·천장·바닥 접촉과 실제 이동 확인 |
| 3인칭 추적 사용 / 추적 카메라 | 현재 카메라 모드와 생성된 추적 카메라 확인 |

Profile의 클립과 속도 설정은 Play 중 인라인으로 수정하면 **현재 선택한 플레이어에 다시 적용**된다. **Profile은 자산이므로 수정 내용은 Play를 종료해도 남는다.** 같은 Profile을 쓰는 다른 모델에도 자산 변경이 영향을 준다. 반면 Play 중 씬 오브젝트의 Profile 연결·위치 등을 바꾼 것은 일반적인 Unity Play 편집처럼 종료 시 되돌아간다. 다른 Profile을 시험하려면 원본을 먼저 복제한다.

WASD는 최대 입력이므로 기본 Move Speed 4 m/s에서는 Run 목표 상태에 들어갈 수 있다. Walk를 시험하려면 조이스틱을 조금 움직이거나 Move Speed를 낮춘다. Profile의 걷기/달리기 기준 속도는 **애니메이션 재생·혼합의 기준**이며 플레이어의 실제 최대 이동 속도를 바꾸는 설정은 아니다.

바닥을 납작한 Cylinder로 만들었다면 기본 CapsuleCollider의 크기도 확인한다. 메시의 높이만 줄였어도 캡슐 충돌체가 보이는 바닥보다 크게 남아 플레이어가 내부에서 시작할 수 있다. 정적 플랫폼의 실제 메시 형상과 맞는 Collider를 사용하고 시작점을 바닥 위에 둔다.

## 3. 속도와 전환 설정

Inspector의 **속도와 전환 설정**을 펼쳐 조절한다.

| 설정 | 기본값 | 의미 |
|---|---|---|
| 걷기 기준 속도 | 2 m/s | 이 속도에서 Walk가 기본 재생 속도. 그 아래에서는 Idle과 Walk를 혼합 |
| 달리기 기준 속도 | 5 m/s | 이 속도에서 Run이 기본 재생 속도이며 완전히 Run으로 혼합 |
| 달리기 전환 속도 | 3 m/s | 이 속도를 넘으면 Walk에서 Run으로 혼합 시작 |
| 정지 판정 속도 | 0.08 m/s | 이 이하에서는 Idle |
| 전환 시간 | 0.15초 | 목표 혼합값에 약 95% 도달하는 시간. 0이면 즉시 전환 |
| 착지 유지 시간 | 0.15초 | Land 클립 전체를 이 시간에 맞춰 한 번 재생. 0이면 생략, 런타임 상한 2초 |

걷기 기준 속도 < 달리기 기준 속도로 설정하고, 달리기 전환 속도는 그 사이에 둔다. 긴 Land 클립을 0.15초에 압축하면 부자연스러울 수 있으므로 착지 구간을 잘라 쓰거나 유지 시간을 조절한다.

슬롯이 비었거나 호환성 검사에서 제외됐을 때의 실제 순서는 다음과 같다.

| 필요한 동작 | 대체 순서 |
|---|---|
| Idle | 첫 번째 호환 클립 |
| Walk | Run → Idle → 첫 번째 호환 클립 |
| Run | Walk → Idle → 첫 번째 호환 클립 |
| Jump | Fall → Idle → 첫 번째 호환 클립 |
| Fall | Jump → Idle → 첫 번째 호환 클립 |
| Land | 해당 상태 생략 후 현재 속도의 Idle / Walk / Run |

첫 번째 호환 클립은 Idle, Walk, Run, Jump, Fall, Land 순서로 고른다. Fall이 없어 Jump를 대신 쓰면 Jump의 마지막 자세를 유지할 수 있다. 대체 재생은 누락 슬롯에서 멈추지 않도록 하는 동작이며, 걷기나 착지 클립을 자동 생성하지는 않는다. 가능한 한 모델에 맞는 Idle·Walk·Run·Jump부터 연결하고 Fall/Land는 실제 구간을 확인한 뒤 추가한다.

SDK는 Idle/Walk/Run/Fall을 반복하고 Jump/Land를 한 번 재생하도록 시간을 제어한다. 원본 FBX의 Loop 설정을 수정하지 않는다. 그래도 반복 시작·끝 자세가 연결되는 클립을 사용해야 하며, Importer Preview의 Loop Time·Loop Pose로 반복 품질을 확인하는 것이 좋다.

## 4. Humanoid와 Generic의 차이

| 모델 종류 | 필요한 조건 | 한계 |
|---|---|---|
| Static | 표시 가능한 메시·재질 | Profile 없이 위치 이동·점프 가능. 본 애니메이션이 생기지는 않음 |
| Humanoid | 대상 모델의 유효한 Humanoid Avatar, 본 매핑·T-pose, Humanoid 클립 | 인체 리타게팅 결과를 모델별로 확인. 유효한 Avatar만으로 모든 포즈 품질을 보장하지 않음 |
| Generic | 클립과 모델의 본 이름·경로·계층 및 Root node가 맞음 | Humanoid식 자동 리타게팅 없음. 다른 동물·로봇 리그의 클립을 임의로 재사용하지 않음 |
| BlendShape | 메시의 BlendShape 이름, SkinnedMeshRenderer와 애니메이션 곡선 경로 일치 | 별도 얼굴 레이어·립싱크·표정 UI를 이 전신 Profile이 제공하지 않음 |

Legacy 클립, 잘못된 Avatar, Humanoid/Generic 불일치는 Inspector와 런타임에서 경고하고 해당 클립을 제외한다. **Generic의 모든 본 경로가 실제 모델에 맞는지는 런타임이 완전히 검사하지 못한다.** 경고가 없더라도 Preview와 Play에서 확인한다. 표정 전용 일반 `.anim`을 Humanoid 전신 슬롯에 넣으면 Humanoid 모션이 없는 클립으로 제외될 수 있다.

### 모델과 클립이 같은 FBX 안에 있을 때

FBX를 선택해 **Rig** 설정을 맞추고 **Animation → Import Animation**을 켠다. Clips 목록과 Preview에서 동작·프레임 범위를 확인하고 Apply한다. FBX를 펼쳐 나온 AnimationClip을 Profile에 넣는다. 별도 `.anim`으로 추출해야만 사용할 수 있는 것은 아니다. [Unity 2022.3 AnimationClip 임포트](https://docs.unity3d.com/2022.3/Documentation/Manual/class-AnimationClip.html)

### 같은 본 구조의 별도 Humanoid 애니메이션 FBX일 때

모델 FBX는 **Rig → Humanoid → Create From This Model**로 유효한 Avatar를 만든다. 동일한 본 구조로 내보낸 별도 애니메이션 FBX는 **Humanoid → Copy From Other Avatar**를 선택하고 그 모델의 Avatar를 Source에 넣어 설정을 재사용할 수 있다. Source는 prefab이나 Controller가 아니다. [Unity 2022.3 Avatar 설정](https://docs.unity3d.com/2022.3/Documentation/Manual/ConfiguringtheAvatar.html)

본 구조가 다른 캐릭터의 Humanoid 동작은 **동작 쪽 리그에 맞는 유효한 Avatar**와 대상 모델의 유효한 Avatar를 각각 준비해 리타게팅한다. 다른 리그에 대상 Avatar를 무조건 복사하는 방법과 다르다. [Unity 2022.3 Humanoid 리타게팅](https://docs.unity3d.com/2022.3/Documentation/Manual/Retargeting.html)

### Generic 또는 외부 모델 형식일 때

Generic은 모델과 애니메이션의 본 계층·경로를 일치시키고 적절한 Root node를 지정한다. [Unity 2022.3 Generic 애니메이션](https://docs.unity3d.com/2022.3/Documentation/Manual/GenericAnimations.html)

FBX 외의 형식은 해당 포맷용 Importer나 변환 과정을 통해 먼저 Unity 모델 GameObject/prefab 자산으로 만들어야 한다. 확장자만 바꾸거나 전용 플러그인의 C# 기능이 APK에 자동 포함된다고 가정하지 않는다. Built-in/URP, Toon/투명도 셰이더, spring·IK·표정 스크립트도 별도로 호환성을 확인한다. 게시용 모델 복사는 지원되지 않는 코드를 제거하며 그 기능을 새 코드로 변환하지 않는다.

원본을 보존하려면 **FBX의 Rig·클립 범위를 수정하기 전에 FBX도 별도 제작 폴더에 복제**한다. prefab만 복사하면 원본 FBX·공유 재질·Avatar의 임포트 설정까지 독립되지는 않는다.

## 5. 현재 프로젝트 예시: Unity-chan

아래 이름은 예시이며 필수 이름이 아니다. 공통 폴더는 `Assets/unity-chan!/Unity-chan! Model/`이다.

- 모델: `Prefabs/unitychan_dynamic.prefab`. 외부 스크립트 경고가 있으면 게시용 복사본을 만든다.
- 모델 Avatar: `Art/Models/unitychan.fbx`의 Avatar. 원본 모델의 Import Animation이 꺼져 있어도 별도 애니메이션 FBX의 클립을 사용할 수 있다.
- 클립: 아래 FBX를 Project 창에서 펼쳐 **내부 AnimationClip**을 Profile에 드래그한다.

| Profile 슬롯 | `Art/Animations` 파일 → 내부 클립 |
|---|---|
| Idle | `unitychan_WAIT00.fbx` → `WAIT00` |
| Walk | `unitychan_WALK00_F.fbx` → `WALK00_F` |
| Run | `unitychan_RUN00_F.fbx` → `RUN00_F` |
| Jump | `unitychan_JUMP00.fbx` → `JUMP00` |
| Fall / Land | 전용 클립의 실제 동작 구간을 확인한 뒤 선택. 처음에는 비워도 됨 |

`JUMP00B`가 있다는 이유만으로 Land라고 지정하지 않는다. Preview에서 도약·공중·착지 구간을 확인해야 한다. 긴 점프 전체 클립을 Jump에 넣으면 도약 중에 포함된 다른 구간도 재생될 수 있다.

Profile을 사용하는 동안 실행 중 모델 복사본의 기존 Controller는 잠시 분리된다. `UnityChanARPose.controller`의 POSE01, Face 레이어, Next/Back 파라미터나 `UnityChanLocomotions.controller`의 Speed/Jump 설정을 SDK가 자동 갱신하는 방식이 아니다. SDK가 지정한 클립을 직접 재생한다. Profile을 비우면 저장해 둔 Controller를 다시 연결한다. 원본 모델 자산의 Controller 연결은 그대로다.

## 6. Root Motion과 게시

플레이어의 이동·중력·점프는 바깥 **CharacterController**가 담당한다. Profile 재생 중 Animator의 **Apply Root Motion은 SDK가 끈다.** 게시용 모델 복사본도 이 설정을 끈다. 클립 평가가 Animator 루트의 위치·회전을 직접 바꾸는 경우 SDK는 그 모델의 장착 위치·회전을 보존한다. 자식 본의 애니메이션은 유지되므로, 가능한 한 in-place 동작을 사용하고 본 이동 때문에 시각 모델이 캡슐에서 벗어나거나 클립의 Y 움직임이 코드 점프와 겹치지 않는지 확인한다.

| 변경 내용 | 반영 방법 |
|---|---|
| 설치된 앱이 지원하는 Profile·클립·모델·씬 참조 변경 | 씬/자산 저장 → Android Build & Publish → 새 revision QR/링크 |
| 이 애니메이션 Profile/driver를 지원하지 않는 이전 APK | 새 SDK가 포함된 Runtime APK 재빌드·설치 후 콘텐츠 사용 |
| 같은 빌드의 서버 업로드만 재시도 | Publish Last Build. 최신 편집 내용을 다시 빌드하는 버튼은 아님 |
| 지원하지 않는 C# 스크립트·StateMachineBehaviour 필요 | 정식 SDK 통합·앱 갱신과 검증 필요. 번들에 소스만 넣어서 해결하지 않음 |

씬의 Player → Profile → AnimationClip 참조가 연결돼 있어야 콘텐츠 의존성으로 묶인다. 파일을 Assets에 넣기만 해서는 재생되지 않는다. revision은 덮어쓰지 않으므로 이전 QR은 이전 동작을 계속 가리킨다. 서버/게시 조작은 [모바일 월드 제작 가이드](mobile-world-guide.md)를 따른다.

## 7. 증상별 확인

| 증상 | 확인할 것 |
|---|---|
| FBX를 펼쳐도 클립이 없음 | 모델 전용 파일인지, 별도 Animations FBX가 있는지, Import Animation과 Clips 목록 |
| 이동해도 고정 포즈 | Profile 연결·호환 클립·Animator 활성 상태. Profile이 없으면 기존 Controller가 재생됨 |
| 상태는 Run인데 걷는 클립이 보임 | Run 슬롯 누락/호환 오류와 대체 정책, 혼합 진행 상태 |
| WASD에서 Walk를 보기 어려움 | 최대 입력이므로 현재 Move Speed가 Run 기준을 넘는지 확인. 부분 조이스틱 입력 사용 |
| 입력은 있는데 속도가 거의 0 | Game 포커스·Pause·CharacterController·벽/바닥 Collider·시작 위치 |
| 팔다리가 꼬이거나 움직이지 않음 | Humanoid Avatar의 본 매핑·T-pose, Generic 본 경로, 클립과 리그 종류 |
| 발이 미끄러짐 | Walk/Run 기준 속도와 실제 이동 속도, 클립의 이동 거리·루프 품질 |
| 몸이 캡슐에서 벗어남 | 클립의 루트/본 이동, in-place 여부, 코드 점프와 Y 애니메이션 중복 |
| 착지가 너무 빠름 | Land 전체 길이와 착지 유지 시간. 착지 구간만 사용 |
| 머리카락/옷 물리나 표정 UI가 사라짐 | 게시 복사 시 제거된 외부 C# 기능. Profile은 해당 기능을 복원하지 않음 |
| 휴대전화에서 이전 동작 | 새 Profile 지원 APK인지, 최신 씬 저장·새 빌드·새 revision 링크인지 |

