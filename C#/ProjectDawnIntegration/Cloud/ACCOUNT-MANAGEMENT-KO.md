# 게임 계정 조회·비밀번호 재설정·잠금

이 문서는 **ProjectDawn 게임 회원 계정**을 관리하는 방법이다. AWS 루트/IAM 계정, SSH 키, MySQL 접속 계정과 다르다. 친구는 게임 안에서 회원가입하고, 운영자는 아래 SSH 도구로 관리한다. 웹 관리자 화면과 이메일 비밀번호 찾기는 아직 없다.

## 무엇을 조회할 수 있나

| 항목 | 방법 |
|---|---|
| 게임 로그인 아이디 | `list`로 조회. 대소문자는 소문자로 통일 |
| 연결된 캐릭터 | `characters 아이디`로 캐릭터 번호·닉네임 조회 |
| 비밀번호 원문 | 조회 불가. DB에는 PBKDF2-SHA256 해시와 무작위 salt만 저장 |
| 비밀번호 변경 | `reset-password 아이디`로 새 비밀번호를 두 번 입력 |
| 계정 이용 중지/해제 | `disable 아이디` / `enable 아이디` |

비밀번호는 **8~128자**이며 공백으로만 된 값과 제어문자는 거절한다. 정확히는 .NET UTF-16 길이 기준이다. 기존 12자 이상 비밀번호도 계속 사용할 수 있다. 해시는 PBKDF2-SHA256 600,000회, salt와 결과 각각 32바이트다.

## 1. 내 개발 PC에서 SSH 접속

Windows PowerShell에 아래를 붙여 넣는다. 키 파일은 이 개발 PC에 이미 보관되어 있고 친구에게 전달하지 않는다.

```powershell
$keyRoot = Join-Path $env:LOCALAPPDATA 'ProjectDawn/AwsFreePlaytest-20261018'
ssh.exe -t -i "$keyRoot/projectdawn-ssh" -o BatchMode=yes -o "UserKnownHostsFile=$keyRoot/known_hosts" ubuntu@203.0.113.10
```

접속하면 이후 명령은 Ubuntu 터미널에서 실행한다. `-t`는 비밀번호를 화면에 표시하지 않고 입력할 터미널을 만든다. 비밀번호를 명령 뒤에 붙이거나 채팅으로 보내지 않는다. 접속 IP는 현재 서버 기준이며 EC2 중지/재시작으로 바뀌면 새 IP·인증서·게임 접속 설정을 함께 확인해야 한다.

## 2. 아이디와 캐릭터 조회

```bash
sudo python3 /opt/projectdawn/release/ops/freevm/admin-members.py list
# dawnfriend는 예시다. 위에서 조회한 실제 아이디로 바꾼다.
sudo python3 /opt/projectdawn/release/ops/freevm/admin-members.py list dawnfriend
sudo python3 /opt/projectdawn/release/ops/freevm/admin-members.py characters dawnfriend
```

`LoginId`가 로그인 아이디, `AccountDbId`가 DB 내부 계정 번호다. `Disabled`가 0이면 로그인 허용, 1이면 중지다. `LastLoginUtc`는 UTC 마지막 인증 시각이고 `CharacterCount`는 해당 계정의 캐릭터 수다. QA용 계정도 목록에 나타날 수 있다. 출력에는 비밀번호 해시나 salt를 포함하지 않는다.

## 3. 비밀번호 변경

```bash
sudo python3 /opt/projectdawn/release/ops/freevm/admin-members.py reset-password dawnfriend
```

`New game password`에 새 비밀번호, `Repeat new password`에 같은 값을 다시 입력한다. 입력 중 글자나 별표가 보이지 않는 것이 정상이다. 불일치·길이 오류면 변경하지 않는다. 정상 처리 뒤 다음 게임 로그인부터 새 비밀번호를 사용한다. 기존 캐릭터·장비·코디와 계정 번호는 유지되며 잠긴 계정을 자동으로 해제하지 않는다.

다른 사람의 요청은 계정 소유자임을 확인한 후 처리한다. 이 도구는 임시 비밀번호의 다음 로그인 변경 강제 기능을 제공하지 않는다.

## 4. 잠금과 해제

```bash
sudo python3 /opt/projectdawn/release/ops/freevm/admin-members.py disable dawnfriend
sudo python3 /opt/projectdawn/release/ops/freevm/admin-members.py enable dawnfriend
```

**잠금과 비밀번호 변경은 다음 로그인에 적용된다. 이미 접속한 세션을 즉시 추방하지 않는다.** 즉시 차단이 필요하면 점검 시간을 잡고 전체 게이트웨이를 재시작할 수 있지만 모든 사용자의 연결이 끊기며 방장 이탈로 레이드가 중단될 수 있다. 개별 사용자 강제 로그아웃은 후속 개발 항목이다.

```bash
# 전체 접속자를 끊어야 하는 점검 때만 실행
sudo systemctl restart dawn-member-gateway
```

## DB 구조와 도구 설치

`accountcredential.AccountDbId` → `account.AccountDbId` → `player.AccountDbId`로 연결한다. 계정 삭제·아이디 직접 변경은 이 도구에서 제공하지 않는다. 계정을 지우고 다시 만드는 방식으로 비밀번호를 변경하면 기존 캐릭터 소유권을 잃을 수 있으므로 사용하지 않는다.

도구 원본은 `Cloud/freevm/admin-members.py`, 서버 패키지에서는 `ops/freevm/admin-members.py`다. 운영 서버의 파일 소유자는 root, 권한은 0750이다. 로컬 MySQL socket의 root 인증을 사용하므로 DB 비밀번호를 파일/명령행으로 전달하거나 3306을 공개하지 않는다. 향후 서버 배포 후에도 파일 소유자/권한을 확인한다.

검증 기록은 `Logs/admin-members-tests.log`와 `Logs/admin-members-aws-readonly.log`를 참고한다. 실제 사용자 계정의 비밀번호·잠금 상태는 이 작업에서 변경하지 않았다. 쓰기 동작은 DB 호출을 대체한 단위 시험으로 대상 계정·해시·실패 시 변경 차단을 검증했으며, 실제 AWS 계정에 대한 재설정·잠금 시험은 수행하지 않았다.

서버 전체 중지·시작은 [서버 운영 안내](SERVER-STOP-START-KO.md)를 본다. AWS 계정 비밀번호를 바꾸려는 경우에는 이 도구를 사용하지 않는다.
