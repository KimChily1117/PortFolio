
# 🎮 Game Developer Portfolio - 김선엽

> 실시간 멀티플레이 게임 서버와 클라이언트를 직접 설계/개발한 프로젝트를 정리한 포트폴리오입니다.  
> MOBA 장르의 Unity 기반 게임과, DirectX11 기반 자체 엔진 MOBA 프로젝트를 통해  
> 네트워크, 엔진 구조, 클라이언트 개발 전반의 실전 경험을 보유하고 있습니다.

---

## 📌 프로젝트 목록

### 1. Project_Dawn (Unity 기반 MOBA 프로젝트)

- 🔧 기술 스택: Unity, C# TCP Socket, Google Protobuf, Entity Framework, MSSQL, AWS EC2/RDS  
- 🧠 핵심 구현:
  - Room 기반 마을/던전 분리 서버 구조
  - FSM 기반 몬스터 AI 및 서버 권한 판정
  - 모바일/PC 크로스플랫폼 구현
  - 서버-DB 연동 AWS 배포 경험

- 🔗 시연:
  - [기능 영상](https://youtu.be/SLZUMB9TtIY)
  - [크로스플랫폼 시연](https://youtu.be/AU5nKUrq1Uo)

📁 폴더 구조:
```
C# / Project_Dawn         👉 Unity 클라이언트
C# / Server               👉 C# 서버
```

---

### 2. DirectX11 MOBA (D3D 기반 자체 엔진 프로젝트)

- 🔧 기술 스택: C++, DirectX11, Assimp, FMOD, Google Protobuf, TinyXML, C# 서버  
- 🧠 핵심 구현:
  - ECS 구조 기반 자체 엔진 설계
  - FBX 모델 + 애니메이션 로딩
  - Protobuf 기반 C++ ↔ C# 통신
  - 넷코드 구조 및 실시간 이펙트/스킬 처리 구현

- 🔗 시연:
  - [D3D 기반 MOBA 시연 영상](https://youtu.be/qJw8m3I30y8)

📁 폴더 구조:
```
C++ / D3D_Popol            👉 DirectX11 클라이언트
C++ / D3D_Server           👉 C# TCP 서버
```

---

### 3. Kimchily (Unity WebGL·모바일 월드)

- Unity 6 제작기·SDK·WebGL 실행기, Expo 앱, 웹 앱, Python 게시 서버로 구성됩니다.
- 프로젝트 간 로컬 패키지와 상대 경로 참조를 위해 [KimchilyWebGL](KimchilyWebGL) 묶음을 함께 유지합니다.
- [프로젝트 안내](KimchilyWebGL/README.md) · [이전 후 실행 준비](KimchilyWebGL/MIGRATION.md)

DirectX 클라이언트와 서버의 새 경로 빌드 순서는 [D3D 이전 안내](C++/D3D_Popol/MIGRATION.md)를 참고하세요.

---

### 4. Cocos2d-x 턴제 전투·스킬 연출 (ASTER에서 확장)

- Cocos2d-x 4 / C++17 기반 로컬 4인 턴제 전투 프로젝트입니다.
- 전투 규칙과 화면 표현 분리, S3 연출 시간표, Spine·WebP 호환, 저장 마이그레이션을 다룹니다.
- [포트폴리오 소개·실기기 영상](C++/Cocos2dxPortfolio/docs/portfolio/README.md) · [소스·빌드 안내](C++/Cocos2dxPortfolio/README.md) · [검증 기록](C++/Cocos2dxPortfolio/docs/FINAL_VALIDATION.md)
- 원작 리소스와 실행 패키지는 포함하지 않으며, 사용 기반과 Codex 구현 지원은 [출처](C++/Cocos2dxPortfolio/docs/portfolio/CREDITS.md)에 구분했습니다.

---

### 5. DNF Raid Demo (Unity 2인 협동 액션)

- Unity 2022.3.16f1 / C# / JSON UDP 기반 호스트 권한 협동 전투 데모입니다.
- 기본 3타·스킬·회피·점프, 보스 패턴 6종, 페이즈·무력화·승패와 모션 상태 분리를 구현했습니다.
- [프로젝트·실행 안내](C%23/DnfRaidDemo/README.md) · [전투 코드](C%23/DnfRaidDemo/UnityProject/Assets/Scripts/RaidSimulation.cs) · [네트워크 코드](C%23/DnfRaidDemo/UnityProject/Assets/Scripts/RaidNetwork.cs)
- Unity 프로젝트·실행 리소스·빌드·검사 스크립트를 포함하며, 리소스 출처와 AI 구현 지원은 [출처](C%23/DnfRaidDemo/THIRD-PARTY.md)에 구분했습니다.

---

## 📞 Contact

**김선엽 / Game Developer**  
📧 work.yeop@gmail.com  
