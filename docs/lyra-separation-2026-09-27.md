# Lyra 분리 운영 계획

2026-09-27에 LyraClone을 PortFolio의 현재 Git 추적 대상에서 제외했다. PortFolio는 기존 Unity 프로젝트, D3D와 Kimchily 작업을 유지한다. Lyra의 원본, 개발 내용과 에셋은 보존하고 소스 관리만 별도로 준비한다.

## 로컬 구성

| 용도 | 경로 |
|---|---|
| 분리한 전체 작업본 | `E:\task\LyraStandalone\Lyra_Clone` |
| 소스 전용 독립 로컬 Git 저장소 | `E:\GItHub\LyraClone-Source` |
| 이전부터 사용하던 원본 | `E:\task\Unreal Project\Lyra_Clone` |

전체 작업본의 프로젝트 파일은 `E:\task\LyraStandalone\Lyra_Clone\LyraClone\LyraClone.uproject`이다. 이전 원본 폴더는 변경하지 않았다. 새 GitHub 계정이나 원격 저장소는 생성하지 않는다.

전체 작업본은 에셋과 기존 생성물을 포함한 디렉터리 이동으로 보존했다. 소스 전용 저장소에는 현재 프로젝트의 `Source`, 설정, `.uproject`, `.vsconfig`, 플러그인 소스와 descriptor, 작은 아이콘, 현재 개발 문서만 담는다. `Content`, 회차별 스냅샷·ZIP, 엔진, 캐시와 빌드 결과는 담지 않는다. 소스 전용 저장소는 전체 에셋 백업을 대신하지 않는다.

## 권장 운영

1. 당분간 전체 작업본에서 Unreal 개발을 이어가고 소스 전용 로컬 저장소를 변경 이력 관리에 사용한다. 소스 전용 저장소의 README에 동기화 방법을 기록한다.
2. 공개가 필요해지면 같은 계정에 **독립 소스 전용 저장소**를 새로 만드는 방법을 우선 검토한다. 처음에는 비공개로 준비하고 공개 범위를 확인한 뒤 전환한다. 현재 PortFolio나 전체 Lyra 이력의 fork/import를 사용하지 않는다.
3. 전체 에셋은 별도 백업을 유지한다. 소스 전용 clone만으로 실행할 수 없으며, 프로젝트와 플러그인의 `Content`를 함께 준비해야 한다. 런타임 맵·Experience·UI 설정이 이 에셋들을 참조한다.
4. 새 원격 저장소에 연결할 때는 새 저장소의 로컬 `main`만 일반 푸시한다. 과거 PortFolio의 백업 브랜치나 LFS 캐시를 가져와 일괄 푸시하지 않는다.

새 계정은 소유권과 인증을 별도로 관리할 필요가 있을 때 선택한다. 이번에는 계정 추가 대신 소스와 에셋을 분리하는 방안을 준비한다. 전체 에셋을 Git LFS로 다시 관리하려면 실제 소유 계정의 예산·사용량 확인 또는 별도 저장소 서비스 검토가 선행돼야 한다. 비용 설정은 자동 변경하지 않는다.

## 같은 계정에 새 저장소만 만드는 것으로는 충분하지 않은 이유

GitHub LFS 사용량은 저장소 소유자의 계정에 계산된다. 따라서 같은 계정에 새 저장소를 만들어 동일한 대용량 에셋을 다시 올리는 방식은 계정의 LFS 제한을 분리하지 못한다. 소스 전용 저장소에는 LFS 오브젝트를 넣지 않아 이 의존성을 없앤다. [GitHub LFS 과금 안내](https://docs.github.com/en/billing/concepts/product-billing/git-lfs)

공식 안내상 월중 LFS 오브젝트를 삭제하더라도 이미 누적된 해당 월 사용량은 다시 계산되지 않는다. 지원팀의 정리 이후에도 현재 업로드 오류가 지속되는 정확한 계정 상태는 추가 확인이 필요하다. [사용량 및 예산 기준](https://docs.github.com/en/billing/concepts/product-billing/git-lfs)

## PortFolio에서 제거되는 범위와 남는 기록

- 현재 트리의 `Unreal Project/Lyra_Clone` 전체를 추적 해제하고 루트 `.gitignore`에 해당 경로를 추가한다.
- 미전송 Lyra 커밋 `b2c011d5fec07b2b19b7f53f5fd42ab4e080fd66`은 로컬 `backup/lyra-before-separation-2026-09-27`에 보존한다. 제거 커밋의 부모는 기존 원격 `main`인 `d20e4e562`이므로 실패했던 새 에셋 업로드를 포함하지 않는다.
- 기존 원격 커밋과 LFS 오브젝트는 유지한다. 현재 파일 삭제만으로 과거 저장 용량이 해제되지는 않는다. 지원팀이 추가 강제 푸시를 보류하라고 안내했으므로 원격 이력 재작성은 하지 않는다.
- 과거 이력과 서버 LFS까지 영구 정리하려면 현재 백업과 분리 결과를 바탕으로 지원팀과 별도 절차를 협의해야 한다. [GitHub LFS 제거 안내](https://docs.github.com/en/repositories/working-with-files/managing-large-files/removing-files-from-git-large-file-storage)

분리 전에는 Lyra Editor Development 빌드까지만 검증했다. Game 빌드 완료와 Server 빌드는 미검증이며, 이번 폴더 분리는 빌드 성공을 추가로 보장하지 않는다. 새 경로에서는 UE 5.4.4 소스 엔진 연결과 생성 파일 재생성 등 기존 준비 절차가 필요하다.
