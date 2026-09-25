# Windows LAN 웹앱 / HTTPS 보조 경로

현재 개인 iPhone 테스트의 우선 경로는 [Expo Go 앱](../KimchilyExpo/README.md)이다. 이 문서는 별도로 준비한 **브라우저/PWA QR 스캐너**를 테스트할 때 사용한다. Expo 네이티브 카메라에는 이 인증서 설치가 필요하지 않다.

## 주소와 서버

| 용도 | 현재 주소 |
| --- | --- |
| 제작·게시 / 기존 HTTP 링크 | `http://192.168.0.4:8788` |
| PWA 홈·카메라 / HTTPS 월드 | `https://192.168.0.4:8789` |
| 휴대폰 공개 인증서 설치 안내 | `http://192.168.0.4:8788/dev/setup` |

Windows PC와 휴대폰이 같은 LAN에 있어야 한다. 두 서버는 동일한 게시 월드를 읽는다. HTTPS 앱은 설정에 등록한 HTTP 8788의 기존 월드 QR을 받아 서버에서 월드·revision·hash를 검증한 뒤 HTTPS 링크로 연결한다. 임의 외부 사이트 주소로 이동하거나 외부 manifest를 가져오지 않는다.

```powershell
Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL'
# 새 PC에서 한 번
powershell -File .\KimchilyPublish\tools\install.ps1
powershell -File .\KimchilyPublish\tools\install-tls.ps1

# 시작 — 기본 게시 서버를 먼저 켠다
powershell -File .\KimchilyPublish\tools\start.ps1
powershell -File .\KimchilyPublish\tools\https.ps1 -Action start

# HTTPS 상태 / 종료
powershell -File .\KimchilyPublish\tools\https.ps1 -Action status
powershell -File .\KimchilyPublish\tools\https.ps1 -Action stop

# 게시 서버까지 종료
powershell -File .\KimchilyPublish\tools\stop.ps1
```

LAN 후보가 여러 개면 두 start 명령에 `-LanAddress 실제PC주소`를 전달한다. 주소를 바꿀 때는 해당 서버를 먼저 stop한다. 새 IP가 들어간 서버 인증서는 갱신되지만 기존 개발 CA는 유지한다. 서버 재시작 도구는 확인된 자체 PID만 관리하며, 원본 Android의 8787 서버를 조작하지 않는다.

## iPhone에서 웹 QR 카메라 사용

1. Safari에서 위 **인증서 설치 안내** 주소를 연다.
2. 공개 `ca.cer`를 다운로드한다. 설치 안내에 표시한 지문과 PC의 `KimchilyPublish/.local/tls/public.json`의 `caSha256` 값이 같은지 확인할 수 있다.
3. **설정 → 다운로드된 프로파일** 또는 **일반 → VPN 및 기기 관리**에서 Kimchily LAN Development CA를 설치한다.
4. **설정 → 일반 → 정보 → 인증서 신뢰 설정**에서 해당 개발 CA의 완전한 신뢰를 켠다.
5. Safari에서 **HTTPS 홈 주소**를 열고 **월드 QR 스캔 → 카메라 켜기**를 누른다. 카메라 권한을 허용한다.
6. 필요하면 Safari 공유 메뉴의 **홈 화면에 추가**를 사용한다.

기기별 메뉴 이름은 iOS 버전에 따라 다를 수 있다. 수동 설치 인증서의 추가 신뢰 절차는 [Apple 안내](https://support.apple.com/en-us/102390)를 따른다. 테스트를 종료하고 CA가 필요 없어지면 설치한 Kimchily 프로파일을 제거할 수 있다. 휴대폰으로 전달하는 파일은 **공개 ca.cer**뿐이며, `ca-key.pem`, `server-key.pem`, 게시 토큰은 전달하지 않는다.

Android 브라우저도 개발 CA를 사용자 CA로 설치하고 HTTPS 홈에서 카메라를 허용한다. 설정의 보안/인증서 설치 메뉴는 제조사별로 다르다. 네이티브 앱의 사용자 CA 신뢰 정책과 브라우저의 정책은 같다고 가정하지 않는다.

## 동작과 범위

홈·월드 카드·QR 카메라·QR 이미지 선택·링크 입력·최근 입장·홈 화면 설치 안내를 제공한다. 카메라 인식은 브라우저별 BarcodeDetector 지원에 의존하지 않고 포함된 jsQR을 사용한다. 영상 프레임과 선택한 이미지는 QR 해독을 위해 기기에서 처리하며 업로드하지 않는다.

서비스 워커는 홈 화면 파일만 캐시한다. Unity 실행기·월드 번들·manifest·API·인증서·토큰은 캐시 대상이 아니다. 오프라인에서 홈이 열려도 월드 실행에는 서버 연결이 필요하다.

실행 도구는 Windows나 휴대폰의 인증서 신뢰 저장소를 자동 변경하지 않는다. 개발 CA/키 폴더의 NTFS 접근 권한을 현재 사용자·SYSTEM·Administrators로 제한한다. 방화벽이나 공유기 설정도 자동 변경하지 않는다. 같은 Wi-Fi에서 접속되지 않으면 사설 네트워크의 8788·8789 연결과 공유기의 기기 격리 설정을 확인한다.

이 구성은 개인 LAN 개발용이다. 외부에 공개할 때는 안정된 도메인·공인 HTTPS와 운영 서버 구성이 별도로 필요하다. 웹 카메라의 HTTPS 조건은 [MDN getUserMedia](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)에 설명돼 있다.
