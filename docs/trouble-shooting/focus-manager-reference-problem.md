# FocusManager 참조 불일치 문제

> 이 문서는 [UI Focus Stack 시스템](../architecture/ui-focus-stack-system.md)에서 발생한 문제를 다룬다

## 개요

게임 씬에 진입한 뒤 특정 UI가 정상적으로 포커스를 얻지 못해 조작할 수 없는 문제가 발생했다.

모든 UI에서 동일하게 발생하는 문제가 아니라 일부 UI에서만 간헐적으로 나타났으며, 다른 UI의 포커스와 입력 처리는 정상적으로 동작했다.

## 문제 분석

각 씬에는 별도의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)가 있고, `GameManager`와 백로그를 비롯한 UI 컴포넌트는 `Awake()`에서 각자 `FindFirstObjectByType<FocusManager>()`를 호출해 참조를 얻는다.

```csharp
// GameManager
m_focusManager = FindFirstObjectByType<FocusManager>();
m_focusManager.Push(this);

// InGameMenuUIController, BacklogUIController
m_focusManager = FindFirstObjectByType<FocusManager>();
```

코드에서는 이 참조들이 같은 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 가리킨다고 전제했지만, 디버깅 결과 `GameManager`와 백로그는 서로 다른 인스턴스를 가리키고 있었다. 그 결과 `GameManager`가 백로그를 `Push()`한 포커스 상태를 백로그의 `IsFocused()`에서는 확인할 수 없었다.

## 원인 분석

### Additive 씬의 공존 구간

에피소드 선택 씬에서 게임 씬으로 전환할 때 게임 씬을 Additive로 활성화한 뒤 이전 씬을 언로드한다. 따라서 게임 씬의 오브젝트가 `Awake()`를 실행하는 시점에는 두 씬의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)가 함께 존재한다.

이때 `FindFirstObjectByType<FocusManager>()`는 호출한 오브젝트와 같은 씬의 인스턴스를 반환한다고 보장하지 않는다. 그 결과 탐색 시점과 오브젝트 로드 순서에 따라 이전 씬의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 잘못 참조할 수 있었고, 각 객체가 서로 다른 포커스 스택을 사용하면서 문제가 간헐적으로 발생했다.

## 해결

[`FocusManager`](../../src/ui-focus/FocusManager.cs)의 구조를 변경하지 않고, `GameManager`가 초기화할 때 얻은 참조를 다른 UI들이 사용하도록 변경했다.

```csharp
public class GameManager : Singleton<GameManager>, IFocusable
{
    public FocusManager focusManager;

    protected override void Awake()
    {
        base.Awake();

        focusManager = FindFirstObjectByType<FocusManager>();
        focusManager.Push(this);
    }
}
```

백로그와 인게임 메뉴에서는 개별적인 [`FocusManager`](../../src/ui-focus/FocusManager.cs) 탐색을 제거하고 `GameManager`가 보관한 참조를 사용했다.

```csharp
// InGameMenuUIController, BacklogUIController
if (GameManager.Instance.focusManager.IsFocused(this) == false)
    return;

GameManager.Instance.focusManager.Pop();
GameManager.Instance.focusManager.Push(this);
```

[`FocusManager`](../../src/ui-focus/FocusManager.cs)의 생명주기와 탐색 구조 전체를 손보는 것보다 변경 범위가 작고 간단했기 때문에 이 방법을 선택했다.

## 한계

이 방법은 씬에 존재하는 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 하나로 통합한 것이 아니라, `GameManager`와 관련 UI가 사용하는 참조만 통일한 임시 방편이다.

`GameManager` 역시 전역 탐색으로 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 찾기 때문에 같은 씬의 인스턴스를 가져온다고 보장할 수 없다. 또한 다른 객체가 개별적으로 전역 탐색을 수행하면 비슷한 참조 불일치가 다시 발생할 수 있다.

## 회고

처음에는 UI가 씬마다 독립되어 있으므로 [`FocusManager`](../../src/ui-focus/FocusManager.cs)도 씬별로 하나씩 두면 된다고 생각했다. 하지만 Additive 씬 전환에서는 이전 씬과 새 씬이 잠시 함께 존재하므로, 씬마다 하나라는 조건만으로는 전역 탐색 결과가 하나로 정해지지 않는다.

씬별로 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 둔 구조가 씬 전환 시 참조 불일치를 만들 수 있다는 점을 미리 고려하지 못했다. 특히 동일한 상태를 공유해야 하는 객체들이 각자 전역 탐색을 수행하면, 전환 시점에 서로 다른 인스턴스를 선택할 수 있다는 사실을 이 문제를 통해 확인했다.


## 관련 코드 및 기록

- [`FocusManager.cs`](../../src/ui-focus/FocusManager.cs)
- [UI Focus Stack 시스템](../architecture/ui-focus-stack-system.md)
