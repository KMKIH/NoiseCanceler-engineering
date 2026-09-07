# Additive 씬 전환 중 FocusManager 참조 불일치 문제

> 이 문서는 [UI Focus Stack 시스템](../architecture/ui-focus-stack-system.md)에서 발생한 씬 전환 문제를 다룹니다.

## 개요

에피소드 선택 씬에서 게임 씬으로 전환한 뒤 백로그 포커스가 간헐적으로 정상 동작하지 않는 문제가 발생했습니다. 인게임 메뉴의 포커스에는 문제가 없었으며, 백로그를 조작할 때만 포커스 상태가 맞지 않는 현상이 나타났습니다.

디버깅 과정에서 `GameManager`와 백로그가 서로 다른 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 참조하고 있다는 사실을 확인했습니다. 오브젝트가 로드되는 순서에 따라 전역 탐색이 이전 에피소드 선택 씬의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 반환하는 경우가 있었던 것으로 추정했습니다.

## 문제 상황

각 씬에는 별도의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)가 있었고, `GameManager`와 백로그를 비롯한 UI 컴포넌트는 `Awake()`에서 각자 `FindFirstObjectByType<FocusManager>()`를 호출해 참조를 얻고 있었습니다.

```csharp
// GameManager
m_focusManager = FindFirstObjectByType<FocusManager>();
m_focusManager.Push(this);

// InGameMenuUIController, BacklogUIController
m_focusManager = FindFirstObjectByType<FocusManager>();
```

코드에서는 이 참조들이 같은 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 가리킨다고 전제했지만, 오류가 발생했을 때 `GameManager`와 백로그는 서로 다른 인스턴스를 가리키고 있었습니다. 그 결과 `GameManager`가 백로그를 `Push()`한 포커스 상태를 백로그의 `IsFocused()`에서는 확인할 수 없었습니다.

## 원인 분석

### Additive 씬의 공존 구간

에피소드 선택 씬에서 게임 씬으로 이동할 때 씬을 Additive 방식으로 프리로드하고 있었습니다.

```csharp
m_loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
m_loadOp.allowSceneActivation = false;
```

이후 게임 씬을 활성화하고 활성 씬으로 지정한 다음 이전 씬을 언로드합니다.

```csharp
m_loadOp.allowSceneActivation = true;
await m_loadOp.ToUniTask(cancellationToken: ct);

var next = SceneManager.GetSceneByName(m_targetScene);
SceneManager.SetActiveScene(next);

await SceneManager.UnloadSceneAsync(m_prevScene)
    .ToUniTask(cancellationToken: ct);
```

이 순서에서는 게임 씬의 오브젝트가 활성화되어 `Awake()`를 실행할 때 이전 에피소드 선택 씬이 아직 언로드되지 않았습니다.

```text
에피소드 선택 씬 활성 상태
        ↓
게임 씬 Additive 프리로드
        ↓
게임 씬 활성화 및 Awake 실행
        ↓
두 씬의 FocusManager가 공존한 상태에서 각 객체가 전역 탐색
        ↓
새 씬을 활성 씬으로 지정
        ↓
이전 씬 언로드
```

Additive 로드로 두 씬이 공존한 상태에서 `FindFirstObjectByType<FocusManager>()`가 호출되었고, 오브젝트 로드 순서에 따라 이전 씬의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 가져오는 경우가 발생했습니다. `FindFirstObjectByType`는 호출한 오브젝트와 같은 씬의 인스턴스를 반환한다고 보장하지 않으므로, `GameManager`와 백로그가 각자 탐색한 결과가 달라질 수 있었습니다. 이것이 오류가 항상 재현되지 않고 간헐적으로 발생한 원인이었습니다.

## 해결

당시에는 [`FocusManager`](../../src/ui-focus/FocusManager.cs)의 구조 자체를 수정하는 대신, `GameManager`가 한 번 얻은 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 인게임 UI가 함께 사용하도록 변경했습니다.

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

백로그와 인게임 메뉴에서는 개별적인 [`FocusManager`](../../src/ui-focus/FocusManager.cs) 탐색을 제거하고 `GameManager`가 보관한 참조를 사용했습니다.

```csharp
if (GameManager.Instance.focusManager.IsFocused(this) == false)
    return;

GameManager.Instance.focusManager.Pop();
GameManager.Instance.focusManager.Push(m_optionUI);
```

이 방식은 씬에 존재하는 [`FocusManager`](../../src/ui-focus/FocusManager.cs) 자체를 하나로 합친 것이 아니라, 인게임 포커스 처리에서 사용하는 참조를 하나로 통일한 임시 방편입니다. 당시 문제를 해결하는 데에는 어떤 씬의 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 선택했는지보다 관련 객체들이 동일한 포커스 스택을 사용하는지가 중요했습니다.

[`FocusManager`](../../src/ui-focus/FocusManager.cs)의 생명주기와 탐색 구조 전체를 손보는 것보다 변경 범위가 작고 간단했기 때문에 이 방법을 선택했습니다.

## 회고

처음에는 UI가 씬마다 독립되어 있으므로 [`FocusManager`](../../src/ui-focus/FocusManager.cs)도 씬별로 하나씩 두면 된다고 생각했습니다. 하지만 Additive 씬 전환에서는 이전 씬과 새 씬이 잠시 함께 존재하므로, 씬마다 하나라는 조건만으로는 전역 탐색 결과가 하나로 정해지지 않습니다.

씬별로 [`FocusManager`](../../src/ui-focus/FocusManager.cs)를 둔 구조가 씬 전환 시 참조 불일치를 만들 수 있다는 점을 미리 고려하지 못했습니다. 특히 동일한 상태를 공유해야 하는 객체들이 각자 전역 탐색을 수행하면, 전환 시점에 서로 다른 인스턴스를 선택할 수 있다는 사실을 이 문제를 통해 확인했습니다.


## 관련 코드 및 기록

- [`FocusManager.cs`](../../src/ui-focus/FocusManager.cs)
- [UI Focus Stack 시스템](../architecture/ui-focus-stack-system.md)
