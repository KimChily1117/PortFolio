# Chili Island · Original 3D art

민트·크림·분홍색의 작은 공중정원에서 친구들과 문양 발판을 나눠 밟아 포털을 깨우는 데모입니다. 환경과 소품은 이 프로젝트를 위해 직접 작성한 절차적 메시입니다. 외부 3D 모델이나 텍스처를 다운로드하지 않았습니다. 플레이어는 기존 Unity-Chan 모델과 애니메이션을 재사용하며, 해당 에셋의 기존 라이선스는 별도로 유지됩니다.

## 편집 가능한 원본

- 씬: `KimchilyCreator/Assets/Demos/ChiliIsland/Scenes/ChiliIsland.unity`
- 생성기: `KimchilyCreator/Assets/Editor/ChiliIslandArt.cs`
- 게임 연출: `KimchilyCreator/Assets/Demos/ChiliIsland/Scripts/PortalGarden.ts`
- 에셋: 같은 폴더의 `Meshes`, `Materials`, `Prefabs`, `Shaders`
- 다른 모델링 도구용: `Models/*.obj`와 `Models/ChiliIsland.mtl`

| 종류 | 프리팹 |
|---|---|
| 지형·포털 | FloatingIsland, GardenPortal |
| 협동 발판 | StarPad, MoonPad, SunPad, LeafPad |
| 식물 | MintTree, BlossomTree, GardenShrub, DaisyCluster |
| 소품 | PebbleCluster, GardenLantern, WelcomeSign |
| 마스코트 | ChiliSprout |

14개 프리팹에 사용하는 고유 메시의 합계는 5,540 삼각형입니다. 씬 전체는 이 메시들을 반복 배치하며, 기존 플레이어 메시도 별도로 포함합니다. OBJ는 기본 정지 모습과 재질 색을 내보냅니다. 포털 셰이더 애니메이션과 TS 동작은 Unity에서 제공됩니다. 새싹 마스코트는 소품 모델이며 리깅된 플레이어 모델은 아닙니다.

## 빌드와 게시

Task의 `KimchilyCreator`에서 다음 명령을 실행합니다.

```powershell
.\tools\build_chili_island.ps1 -Publish
```

원본 에디터를 닫지 않아도 별도 `Artifacts/ChiliIslandBuild`에서 빌드합니다. 게시 서버가 실행 중이어야 합니다. 기본 실행은 저장된 데모 씬을 사용합니다. `-RegenerateArt`를 추가하면 생성기 기준으로 데모 에셋과 씬을 다시 만드므로, 이 폴더에 수동으로 수정한 내용은 먼저 보관해야 합니다. 기존 MyWorld는 대상이 아닙니다.

재생성 시 `Artifacts/ChiliIslandBuild/Artifacts`에 실제 Unity 렌더 PNG 4장과 `ChiliIsland-OriginalAssets.unitypackage`도 생성합니다. Unity 패키지는 환경/소품/셰이더와 OBJ를 담습니다. SDK와 데모 씬은 패키지에 포함하지 않습니다.

## 게임 규칙과 연출

입장 → 닉네임·채팅 연결 → 모두 준비되면 시작 → 빛나는 서로 다른 발판 점유 → 3초 유지 → 포털 개방 순서입니다. 혼자서는 별 발판 하나로 시연할 수 있고, 2~4인에서는 참가 인원에 맞춰 발판을 활성화합니다.

서버의 `PortalRules.ts`가 발판 위치·점유·유지 시간·완료 상태를 판정합니다. 클라이언트 `PortalGarden.ts`가 UI·버튼·진행률·발판 빛·포털 연출을 작성합니다. C#은 통신·방 관리·범용 JS 호스트를 제공합니다. 로컬 컴파일/등록 도구로 승인된 규칙 번들을 배포하며, 임의 접속자의 코드 업로드 기능은 없습니다. 포털은 완료 연출이며 다른 월드로 이동하지는 않습니다. [상세 설명](portfolio-typescript-multiplayer.md)

실제 휴대폰들은 같은 Wi-Fi에서 게시된 QR로 입장합니다. 숨긴 브라우저 탭은 위치 갱신이 멈출 수 있으므로 협동 플레이에서는 각 화면을 활성 상태로 유지합니다.
