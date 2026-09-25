# Kimchily 보조 웹 홈

빌드 도구 없이 게시 서버가 제공하는 정적 클라이언트입니다. Expo 네이티브 앱과 같은 월드 목록·링크 API를 사용합니다.

- `/`: 게시된 WebGL 월드, QR 카메라/이미지, 링크 입력, 최근 기록.
- `/app/*`: 로컬 스크립트·스타일·아이콘. 외부 CDN을 사용하지 않습니다.
- `/manifest.webmanifest`, `/sw.js`: HTTPS에서 설치와 홈 화면의 오프라인 읽기를 지원합니다. 월드·플레이어·API·인증서 응답은 캐시하지 않습니다.
- 서버의 `/api/resolve` 확인 후 동일 origin 플레이어로만 이동합니다. 입장 전에 `sessionStorage['kimchily:returnHome']='1'`을 기록합니다.
- HTTP에서는 카메라가 비활성화되며 QR 이미지와 링크 입력을 사용합니다. `/dev/setup`은 선택적인 개발 HTTPS 연결 안내입니다.

검증: `npm test`. PNG 아이콘 재생성: `python tools/generate_icons.py`.

QR 구현과 라이선스는 `vendor/README.md`를 참고하세요. 자동 검증은 실제 iPhone/Android 설치 및 실물 카메라 검증을 대신하지 않습니다.
