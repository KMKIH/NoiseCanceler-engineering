# Custom Update 시스템

## 개요

리듬 게임은 한 프레임 안에서도 음악 시간 갱신, 입력 처리, 판정 처리가 정해진 순서대로 실행되어야 한다.

각 로직을 여러 `MonoBehaviour`의 `Update()`에 나누어 구현하면 스크립트 사이의 실행 순서가 코드에 드러나지 않으며, 순서가 달라졌을 때 이전 프레임의 음악 시간을 기준으로 입력을 판정하는 문제가 발생할 수 있다.

이를 방지하기 위해 리듬 게임의 핵심 시스템이 [`Updateable`](../../src/custom-update/Updateable.cs)을 상속하고, [`UpdateManager`](../../src/custom-update/UpdateManager.cs)가 각 시스템의 `CustomUpdate()`를 정해진 순서대로 호출하도록 구성했다.

## 처리 순서

`UpdateManager`의 인스펙터에 등록된 순서에 따라 다음과 같이 처리한다.

```text
DataManager.CustomUpdate()
    FMOD 재생 위치를 기준으로 현재 음악 시간 갱신
        ↓
InputManager_.CustomUpdate()
    현재 프레임의 키 입력 확인 및 입력 이벤트 호출
        ↓
JudgeManager.CustomUpdate()
    갱신된 음악 시간을 기준으로 자동 판정과 Miss 처리
```

입력 이벤트는 `InputManager_`에서 즉시 호출되며, 이를 구독한 `JudgeManager`가 입력 판정을 수행하고 판정 결과를 노트 상태에 반영한다. 따라서 모든 판정은 같은 프레임에서 먼저 갱신된 음악 시간을 기준으로 처리된다.

## 구현 방식

업데이트 순서를 제어할 대상은 `Updateable`을 상속하고 `CustomUpdate()`를 구현한다.

```csharp
public abstract class Updateable : MonoBehaviour
{
    public abstract void CustomUpdate();
}
```

`UpdateManager`는 Unity의 `Update()`를 실행 진입점으로 사용한다. 이후 인스펙터에 등록된 `Updateable` 그룹과 객체를 순서대로 순회하며 `CustomUpdate()`를 호출한다.

```csharp
public class UpdateManager : MonoBehaviour
{
    [SerializeField]
    private Array<Updateable>[] updateables;

    private void Update()
    {
        foreach (var ups in updateables)
        {
            foreach (var up in ups)
            {
                up.CustomUpdate();
            }
        }
    }
}
```

Unity는 **[Script Execution Order](https://docs.unity3d.com/kr/current/Manual/script-execution-order.html)** 설정으로 `MonoBehaviour` 사이의 실행 순서를 지정할 수 있다. 하지만 해당 설정은 실제 로직과 떨어진 Project Settings에서 관리되므로, 리듬 게임의 처리 흐름을 코드와 게임 오브젝트만으로 파악하기 어렵다.

별도의 Custom Update 시스템을 사용하면 실행 대상과 순서를 `UpdateManager`의 인스펙터에서 직접 확인하고 조정할 수 있다. 또한 업데이트 흐름이 하나의 진입점에 모이기 때문에 시스템을 추가하거나 순서를 변경할 때 전체 처리 과정을 명시적으로 관리할 수 있다.

## 관련 코드

- [`Updateable.cs`](../../src/custom-update/Updateable.cs)
- [`UpdateManager.cs`](../../src/custom-update/UpdateManager.cs)
- [`DataManager.cs`](../../src/rhythm-sync/DataManager.cs)
