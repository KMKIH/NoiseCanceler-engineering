# UI Navigation 시스템

## 개요

리듬게임 플레이와 메뉴 조작 사이의 입력 경험을 통일하기 위해 `F`, `J`, `Space`만으로 UI를 조작하는 키보드 기반 Navigation 시스템을 구현했다.

`F`와 `J`는 이전·다음 항목으로 이동하거나 옵션 값을 조절하고, `Space`는 현재 항목을 선택한다. 메뉴마다 같은 입력 판정과 인덱스 이동을 반복해서 구현하지 않도록 공통 이동 로직과 버튼의 시각적 반응을 분리했으며, 버튼을 눌렀을 때 실행할 화면별 기능은 콜백으로 주입하도록 구성했다.

## 요구사항

- `F`, `J`, `Space`의 한정된 입력으로 메뉴 이동과 선택 처리
- 버튼 목록의 양 끝에서 이동을 멈추거나 반대쪽으로 순환하는 옵션 제공
- 화면에 따라 Key Down 또는 Key Up 시점에 이동 입력 처리
- 비활성화된 버튼을 제외하고 현재 선택 인덱스 관리
- 버튼 종류별 Normal, Highlight, Press, Release 연출 통일
- 메뉴 이동, 씬 전환, 팝업 열기, 슬라이더 조절 등 서로 다른 기능 연결

## 구조

```text
F / J / Space 입력
        │
        ▼
NavigationButtonController
  ├─ 활성 버튼 목록과 현재 인덱스 관리
  ├─ 인덱스 범위 및 순환 처리
  ├─ Press / Release 입력 상태 관리
  └─ 선택 변경 Hook 제공
        │
        ▼
INavigationButton (abstract MonoBehaviour)
  ├─ OnNormal()
  ├─ OnHighlighted()
  ├─ OnPressed()
  └─ OnReleased()
        │
        ├─ 버튼별 색상·크기·사운드 연출
        └─ 주입된 화면 기능 실행
```

현재 코드의 [`INavigationButton`](../../src/ui-navigation/INavigationButton.cs)은 이름과 달리 C# 인터페이스가 아니라 `MonoBehaviour`를 상속한 추상 클래스다. Unity Inspector에서 버튼 컴포넌트로 참조하고 공통 메서드를 강제하기 위해 추상 클래스로 구현했다.

### INavigationButton

모든 Navigation 버튼이 가져야 할 상태 전환을 추상 메서드로 정의한다.

```csharp
public abstract class INavigationButton : MonoBehaviour
{
    public abstract void Initialize(Action onPressed, Action onReleased);

    public abstract void OnNormal();
    public abstract void OnHighlighted();
    public abstract void OnPressed();
    public abstract void OnReleased();
}
```

구현체는 색상 변경, 크기 Tween, 포커스 표시처럼 자신에게 필요한 시각·청각 연출을 담당한다. 실제 메뉴 기능은 `Initialize`에서 `Action`으로 전달받아 Press 또는 Release 시점에 실행한다.

이 구조를 통해 Controller는 버튼의 구체적인 연출 방식을 알지 않아도 동일한 상태 전환 메서드를 호출할 수 있다.

### NavigationButtonController

[`NavigationButtonController`](../../src/ui-navigation/NavigationButtonController.cs)는 여러 화면에서 반복되는 입력 판정과 선택 인덱스 관리를 담당하는 추상 Controller다.

- 초기화 시 전체 버튼을 등록하고 활성화된 버튼만 조작 목록에 포함한다
- `F`, `J` 입력에 따라 이전 버튼은 Normal, 새 버튼은 Highlight 상태로 변경한다
- 범위를 벗어난 인덱스는 설정에 따라 이동을 막거나 반대쪽으로 순환시킨다
- `Space`를 누른 동안 이동 입력과 중복 선택이 발생하지 않도록 Press 상태를 유지한다
- `Space`를 누른 시점과 뗀 시점을 나누어 버튼의 Press·Release 연출과 기능을 호출한다
- 선택 인덱스가 바뀌면 Hook과 이벤트를 통해 화면별 추가 연출을 실행할 수 있게 한다

```text
대기 상태
  ├─ F / J 입력
  │    └─ 기존 버튼 Normal → 인덱스 변경 → 새 버튼 Highlight
  │
  └─ Space Down
       └─ 현재 버튼 Press → 입력 잠금
                              │
                         Space Up
                              └─ 현재 버튼 Release → 대기 상태
```

### 화면별 Controller

각 화면의 Controller는 `NavigationButtonController`를 상속하고, `InitializeButton`에서 버튼과 실제 기능을 연결한다.

```csharp
startButton.Initialize(
    onPressed: null,
    onReleased: () => LoadScene("SelectChapter"));

optionButton.Initialize(
    onPressed: null,
    onReleased: () => focusManager.Push(optionWindow));
```

단순 메뉴에서는 버튼마다 별도 기능 클래스를 만들지 않고도 씬 전환이나 팝업 열기 같은 짧은 동작을 선언할 수 있다. 버튼의 표현은 버튼 구현체에, 화면 전환 흐름은 해당 화면 Controller에 남으므로 기능이 사용되는 위치도 바로 확인할 수 있다.

### FocusManager와의 역할 분리

중첩된 UI 중 어느 화면이 입력을 받을지는 Navigation 시스템이 직접 판단하지 않는다. 각 화면은 [`FocusManager`](ui-focus-stack-system.md)를 통해 자신이 최상단 UI인지 확인한 뒤에만 `HandleNavigation`을 호출한다.

따라서 FocusManager는 **입력을 받을 화면**을 결정하고, NavigationButtonController는 그 화면 안에서 **입력을 받을 버튼**을 결정한다. 화면 간 입력 우선순위와 화면 내부의 선택 이동을 분리해 두 시스템의 책임이 겹치지 않도록 했다.

## 옵션 조작 모드

옵션 화면에서는 같은 `F`, `J` 입력이 두 가지 의미로 사용된다.

```text
Navigation 모드 : F / J로 항목 이동, Space로 진입
Adjust 모드     : F / J로 값 조절, Space로 확정
```

슬라이더나 언어 항목을 선택하면 일반 Navigation 입력을 잠시 멈추고 Adjust 모드로 전환한다. 이 상태에서는 좌우 입력으로 값이나 항목을 변경하고, 다시 `Space`를 누르면 구독을 해제한 뒤 Navigation 모드로 돌아온다. 입력 오프셋과 볼륨처럼 연속 조절이 필요한 값에는 최초 지연과 반복 간격을 둔 Hold & Repeat도 적용했다.

이를 통해 플레이 입력과 동일한 키만으로 메뉴 탐색뿐 아니라 세부 옵션 조작까지 처리할 수 있었다.

## 설계 선택과 트레이드오프

버튼마다 실행하는 기능이 달라질 것을 예상해 두 가지 구조를 검토했다.

| 방식 | 장점 | 비용 |
|---|---|---|
| 기능별 클래스를 생성해 버튼에 부착 | 기능 단위 책임이 명확하고 독립적인 재사용·테스트가 쉽다 | 한 번만 사용하는 짧은 동작까지 클래스로 만들어 파일과 컴포넌트 수가 빠르게 늘어난다 |
| 화면 Controller에서 콜백 주입 | 화면 상태와 참조에 바로 접근할 수 있고 단순 기능을 적은 코드로 연결할 수 있다 | 복합 기능까지 콜백에 포함되면 Controller가 입력, 상태, 저장, 연출을 함께 알아야 한다 |

처음에는 버튼에 할당될 기능이 다양해질 것으로 예상했다. 기능을 하나씩 클래스로 만들어 버튼에 부착하는 방식도 고려했지만, 기능이 늘어날수록 클래스 수도 계속 늘어날 것이라고 판단했다. 따라서 각 화면의 Controller에서 `Initialize`의 콜백으로 버튼 기능을 연결하는 방식을 선택했다.

이 방식으로 별도 기능 클래스의 수는 줄일 수 있었지만, 버튼의 기능이 늘어나면서 Controller 내부의 콜백 코드가 길어졌다. 결과적으로 클래스 수를 줄이는 대신 Controller가 비대해지는 비용을 감수한 선택이었다.

## 관련 코드

- [`INavigationButton.cs`](../../src/ui-navigation/INavigationButton.cs)
- [`NavigationButtonController.cs`](../../src/ui-navigation/NavigationButtonController.cs)
