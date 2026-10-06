# UGC C# 코드 작성 스타일

갱신일: 2026-10-06. 서버, Creator SDK, Networking·TypeScript·Lua SDK와 공통 Unity 실행기의 직접 작성한 C# 코드에 적용한다.

## 읽기 기준

- 함수·생성자·프로퍼티의 본문은 중괄호 블록으로 작성한다. 함수 사이에는 빈 줄을 둔다.
- `if`, `else`, 반복문, `try`·`catch`·`finally`의 실행문을 같은 줄에 붙이지 않는다. 한 줄 조건문도 중괄호를 쓴다.
- 초기화, 입력 검사, 상태 변경, 전송·정리 같은 처리 단계는 빈 줄로 나눈다.
- 일반 클래스는 필드와 생성자를 명시한다. 생성자 매개변수를 클래스 이름 옆에 붙이는 주 생성자는 피한다.
- 서버 진입점은 `Program.Main`으로 표시하고 namespace는 중괄호로 감싼다.
- `new()` 또는 `[]`만으로 타입을 생략하기보다 `new Queue<IJob>()`처럼 생성할 타입을 적는다.
- 여러 필드를 한 줄에 묶기보다 각각 선언한다. 긴 조건식·호출 인수·객체 초기화는 의미 단위로 줄을 나눈다.
- 여러 단계의 삼항식은 일반 `if` 또는 `switch`로 풀어 쓴다. 짧은 null 처리나 간단한 값 선택은 문맥에 따라 유지할 수 있다.
- 람다와 LINQ는 콜백·조회 의도가 분명할 때 사용한다. 여러 작업이 들어가면 블록과 줄바꿈으로 순서를 드러낸다.

루트의 [`.editorconfig`](../.editorconfig)에 중괄호·줄바꿈·축약 문법에 대한 편집기 제안을 기록했다. 함수 사이와 처리 단계의 빈 줄은 코드 검토에서도 확인한다. Vendor·Unity-Chan·SpringBone 등 외부 소스는 원래 형식을 유지한다.

## 예시

```csharp
public sealed class RoomPump : BackgroundService
{
    private readonly RoomHub _rooms;

    public RoomPump(RoomHub rooms)
    {
        _rooms = rooms;
    }

    // 실제 서버 코드에서는 취소 처리와 timer 정리도 수행한다.
}
```

## 동작을 보존할 부분

이 기준은 읽기 쉬운 표현을 위한 것이며 대상 프레임워크를 내리는 작업은 아니다. 서버는 .NET SDK 10.0.401 / net10.0, Unity는 6000.3.24f1을 유지한다.

통신 DTO의 `record`, `with`, `init`은 값 비교·복사·역직렬화 계약 때문에 유지한다. nullable 표기, 비동기 처리와 .NET API도 필요에 따라 유지한다. 람다의 캡처, 잠금 범위, 취소·자원 해제 순서, Unity 직렬화 필드명과 스크립트 VM 제한은 표현을 정리하면서 바꾸지 않는다.

이번 적용 범위와 확인 결과는 [2026-10-06 가독성 정리 기록](reports/2026-10-06-csharp-readability.md)에 있다.
