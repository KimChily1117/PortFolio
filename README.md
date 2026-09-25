
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

## 📞 Contact

**김선엽 / Game Developer**  
📧 work.yeop@gmail.com  
