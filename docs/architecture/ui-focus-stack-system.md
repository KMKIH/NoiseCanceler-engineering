# UI Focus Stack 시스템

## 배경
- 게임 특성상 마우스로 대상을 직접 선택하는 방식이 아니라 한정된 키로 UI를 조작하므로, 한 번에 하나의 UI만 조작 대상으로 활성화해야 한다
- UI가 중첩되면 같은 입력에 여러 UI가 반응할 수 있으므로 최상단 UI만 입력을 처리해야 한다
- 따라서 열린 UI의 순서를 관리하고, 한 번에 하나의 UI에만 조작 권한을 부여하는 시스템이 필요하다

## 설계 목표
- 중첩된 UI 중 최상단 UI만 입력을 처리해야 한다
- UI가 열리고 닫히는 순서를 Stack의 `Push`와 `Pop`으로 표현한다
- 각 UI가 자신의 포커스 여부를 직접 확인해야 한다
- [`FocusManager`](../../src/ui-focus/FocusManager.cs)는 씬 단위로 배치해 해당 씬의 UI 생명주기와 함께 관리한다

## 구조
- UI 객체는 [`IFocusable`](../../src/ui-focus/IFocusable.cs)을 구현해 포커스 상태에 따른 동작을 정의한다
- [`FocusManager`](../../src/ui-focus/FocusManager.cs)는 UI 객체를 Stack으로 관리하고 현재 입력을 처리할 UI를 결정한다
- UI가 열리고 닫힐 때 `Push`와 `Pop`으로 Stack을 갱신하며, 각 UI는 `IsFocused`를 통해 현재 입력 권한을 확인한다

## 확장: TopFocus
- 일반적인 UI 중첩 순서를 유지하면서도, 디버그 콘솔처럼 모든 UI보다 입력 우선순위가 높아야 하는 경우를 위해 `TopFocus`를 별도로 두었다
- `TopFocus`가 존재하면 Stack 최상단보다 먼저 포커스를 판별하고, 해제된 뒤에는 기존 Stack의 최상단으로 포커스가 돌아간다

## 설계 효과

- 각 UI에 입력 차단 조건을 개별 구현하지 않고 `IsFocused` 검사로 통일했습니다.
- 팝업이 여러 단계로 중첩되어도 열린 순서의 역순으로 입력 권한이 복귀합니다.
- UI 표시 상태와 입력 권한을 분리해, 화면에 보이는 하위 UI가 입력까지 처리하는 문제를 방지했습니다.

## 관련 코드 및 기록

- [`FocusManager.cs`](../../src/ui-focus/FocusManager.cs)
- [`IFocusable.cs`](../../src/ui-focus/IFocusable.cs)
- [Additive 씬 전환 중 FocusManager 참조 불일치 문제](../trouble-shooting/focus-manager-scene-transition-reference.md)
