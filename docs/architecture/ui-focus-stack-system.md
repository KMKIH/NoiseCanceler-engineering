# UI Focus Stack 시스템

## 개요

게임 특성상 마우스로 대상을 직접 선택하는 대신 한정된 키로 UI를 조작하므로, 동시에 여러 UI가 입력을 받아서는 안 된다.

특히 UI가 중첩된 상태에서 동일한 입력이 여러 UI에 전달되면 의도하지 않은 동작이 발생할 수 있다. 이를 방지하기 위해 열린 UI의 순서를 관리하고, 최상단 UI 하나에만 조작 권한을 부여하는 시스템을 구성했다.

## 설계 목표

- 중첩된 UI 중 최상단 UI에만 입력 권한 부여
- UI의 열림·닫힘 순서를 Stack의 `Push`와 `Pop`으로 관리
- 각 UI에서 자신의 포커스 여부를 직접 확인
- [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 씬 단위로 배치하여 UI 생명주기와 함께 관리

## 구조
- UI 객체는 [`IFocusable`](../../src/ui-focus/IFocusable.cs)을 구현해 포커스 상태에 따른 동작을 정의한다
- [`FocusManager`](../../src/ui-focus/FocusManager.cs)는 UI 객체를 Stack으로 관리하고 현재 입력을 처리할 UI를 결정한다
- UI가 열리고 닫힐 때 `Push`와 `Pop`으로 Stack을 갱신하며, 각 UI는 `IsFocused`를 통해 현재 입력 권한을 확인한다

## 확장: TopFocus
- 일반적인 UI 중첩 순서를 유지하면서도, 디버그 콘솔처럼 모든 UI보다 입력 우선순위가 높아야 하는 경우를 위해 `TopFocus`를 별도로 두었다
- `TopFocus`가 존재하면 Stack 최상단보다 먼저 포커스를 판별하고, 해제된 뒤에는 기존 Stack의 최상단으로 포커스가 돌아간다

## 관련 코드 및 기록

- [`IFocusable.cs`](../../src/ui-focus/IFocusable.cs)
- [`FocusManager.cs`](../../src/ui-focus/FocusManager.cs)
- [Additive 씬 전환 중 FocusManager 참조 불일치 문제](../trouble-shooting/focus-manager-scene-transition-reference.md)
