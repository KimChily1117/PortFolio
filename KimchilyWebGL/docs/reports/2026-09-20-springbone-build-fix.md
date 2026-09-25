# AssetBundle 빌드 실패: Spring Bone 에디터 어셈블리 설정

## 원인과 수정

Unity 로그의 최초 오류는 `Packages/com.unity.springbone/Editor`의 `UnityEditor.Localization`, `EditorWindow`, `SerializedProperty`, `CustomEditor` 등을 Android 컴파일에서 찾지 못한다는 내용이었다. 이후 나타난 `Unity AssetBundle build failed`는 이 컴파일 실패의 결과다.

해당 패키지의 `com.unity.animations.springbone.editor.asmdef`는 `includePlatforms: []`로 모든 플랫폼을 대상으로 했다. 어셈블리 정의가 있는 이 구성에서는 `Editor` 폴더 이름만으로 Android 대상에서 제외되지 않았다.

[Editor asmdef](../../KimchilyCreator/Packages/com.unity.springbone/Editor/com.unity.animations.springbone.editor.asmdef)의 `includePlatforms`를 `["Editor"]`로 수정했다. 어셈블리 이름·GUID·Runtime 참조는 유지했다. Runtime 파일의 에디터 API 참조는 `UNITY_EDITOR` 조건 안에 있어 별도 변경하지 않았다.

## 현재 프로젝트 전체 검증

열려 있는 제작 프로젝트를 유지하면서 Assets·Packages·ProjectSettings를 별도 검증 프로젝트에 복사했다. 기존 모델만 다룬 검증보다 범위를 넓혀, 추가된 Spring Bone 패키지와 현재 저장된 `MyWorld` 씬까지 함께 빌드했다.

- 원본과 검증 복사본의 Assets **1,200개 파일 SHA-256 일치**.
- Unity 컴파일 대상 조회: Spring Bone Runtime은 Player에 포함, Spring Bone Editor는 Player에서 제외, Editor에서는 사용 가능.
- 게시 사전 검사 오류 0개.
- 현재 MyWorld Android AssetBundle 빌드 성공, Unity 정상 종료 코드 0.
- 로컬 게시 성공. QR 페이지 HTTP 200, QR PNG 다운로드, 게시 manifest SHA-256 일치 확인.

빌드 확인 중 멈춘 Unity ADB 서버의 실행 파일과 명령을 확인해 해당 서버만 종료했고 Unity가 재시작하도록 했다. 사용자의 Unity 에디터와 작업 씬은 종료하지 않았다.

## 결과

- 월드: `sample-world`
- revision: `20260919T165727526Z-3a771f93`
- 제작 프로젝트의 빌드 폴더: [WorldBuilds/20260919T165727526Z-3a771f93](../../KimchilyCreator/WorldBuilds/20260919T165727526Z-3a771f93)
- [새 QR 페이지](http://192.168.0.4:8787/w/sample-world/20260919T165727526Z-3a771f93)
- [전체 빌드 검증 결과](../../KimchilyCreator/Artifacts/assetbundle-fix-verification-20260920.json), [빌드 로그](../../KimchilyCreator/Artifacts/assetbundle-full-project-check-20260920.log), [게시 응답](../../KimchilyCreator/Artifacts/publish-recovered-20260920.json)

이 변경은 제작 프로젝트의 빌드 설정을 고친 것이다. APK에 Spring Bone 기능을 새로 탑재한 것은 아니며, 모델 복사본에서 제외한 물리·표정 스크립트의 동작도 추가하지 않는다. 이번에는 새 콘텐츠의 빌드·게시·다운로드까지 확인했고 휴대폰에서 새 revision을 실행하는 검증은 수행하지 않았다.

Unity로 돌아가 패키지 갱신·컴파일이 끝난 뒤 `Build & Publish`를 다시 사용할 수 있다. 기존 실패 메시지가 화면에 남아 있으면 새 빌드 결과로 갱신된다. 수정된 최신 씬을 빌드하려면 `Publish Last Build`가 아니라 `Build & Publish`를 사용한다.
