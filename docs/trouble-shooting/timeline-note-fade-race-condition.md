# Timeline Signal과 노트 생성이 겹칠 때 Fade 상태가 누락되는 문제

## 개요

챕터 3-3의 1분 54초 구간에서 일부 노트가 투명한 상태로 생성되어 보이지 않는 문제가 발생했습니다. 해당 시점에는 Timeline의 `FadeIn` Signal과 노트 생성이 같은 프레임에 실행되고 있었습니다.

`FadeIn` Signal 자체는 정상적으로 호출됐지만, Signal 처리 시점에 아직 `ActiveNoteList`에 등록되지 않은 노트는 Fade 대상에서 빠졌습니다. 이후 생성된 노트가 오브젝트 풀에 남아 있던 알파값까지 이어받으면서 투명한 상태가 유지됐습니다.

이 문제는 단순한 연출 데이터 오류가 아니라, 일회성 이벤트와 동적으로 생성되는 객체 사이의 상태 동기화가 누락된 Race Condition이었습니다.

## 관련 코드

| 파일 | 역할 |
|---|---|
| [NoteEffectController.cs](../../src/note-fade/NoteEffectController.cs) | Timeline Signal의 Fade 상태 저장, 활성 노트 적용, 종료 시점 재동기화 |
| [Note.cs](../../src/rhythm-sync/Note.cs) | 생성 시 현재 Fade 상태 반영, Tween 교체, 파괴 상태 관리 |

## 문제 상황

노트 Fade는 Timeline Signal을 받은 순간 활성화된 노트를 순회하는 방식으로 구현되어 있었습니다.

```csharp
public void FadeInLeftNotes(float duration)
{
    isLeftTransparent = false;

    foreach (var note in playManager.ActiveNoteList[LeftLineIndex])
    {
        note.FadeNote(1, duration);
    }
}
```

일반적인 실행 순서에서는 문제가 없었지만, `FadeIn`과 노트 생성이 같은 프레임에 겹치면 호출 순서에 따라 결과가 달라졌습니다.

```text
같은 프레임

Timeline FadeIn Signal 처리
    ↓
현재 ActiveNoteList 순회
    ↓
신규 노트 생성 및 목록 등록
    ↓
신규 노트는 앞선 FadeIn 대상에서 누락
```

이때 신규 노트는 직전에 풀에 반환될 때의 `baseAlpha`가 `0`이었다면 그대로 투명하게 남을 수 있었습니다. 따라서 Timeline과 노트 생성 로직이 모두 정상 실행됐는데도 특정 노트만 보이지 않는 현상이 발생했습니다.

## 원인 분석

### 일회성 이벤트가 현재 객체만 갱신

기존 `FadeIn`은 호출 시점의 `ActiveNoteList`를 스냅샷처럼 사용했습니다. 이벤트 이후 같은 프레임에 등록된 노트는 이미 끝난 순회에 참여할 수 없었습니다.

반면 투명 여부는 특정 순간에만 의미가 있는 명령이 아니라, 이후 생성되는 노트도 따라야 하는 전역 상태입니다. 그러나 기존 구조는 현재 상태를 저장하면서도 Fade 종료 시 전체 노트가 그 상태에 도달했는지 다시 확인하지 않았습니다.

### 길이가 0인 Tween의 적용 시점

노트 생성 시 현재 Fade 상태를 반영하는 보정 로직은 이미 있었지만, 즉시 알파값을 설정할 때도 길이가 `0`인 Tween을 생성했습니다.

```csharp
FadeNote(currentAlpha, 0);
FadeNote(targetAlpha, remainTime);
```

첫 Tween이 실제로 값을 반영하기 전에 다음 `FadeNote()`가 기존 Tween을 종료하면, 계산한 시작 알파값이 적용되지 않을 수 있었습니다. 오브젝트 풀에서 재사용된 노트일수록 이전 알파값이 남아 문제를 드러내기 쉬운 구조였습니다.

### Fade가 연속될 때 발생하는 추가 경쟁 조건

Fade 종료 시점에 다시 동기화하는 것만으로는 충분하지 않았습니다. 이전 Fade의 종료 보정이 예약된 상태에서 반대 방향의 새 Fade가 시작되면, 먼저 예약된 보정이 나중에 실행되어 최신 상태를 덮어쓸 수 있기 때문입니다.

따라서 다음 세 가지를 함께 보장해야 했습니다.

1. Signal 시점에 존재하는 노트는 즉시 Fade를 시작합니다.
2. Fade 도중 또는 같은 프레임에 생성된 노트는 종료 시점에 최종 상태로 다시 맞춥니다.
3. 이전 Fade의 보정 작업은 이후 Fade 상태를 변경하지 못해야 합니다.

## 해결

### Fade 종료 시 활성 노트 상태 재동기화

Fade를 시작할 때 현재 활성 노트에 Tween을 적용한 뒤, Fade 시간만큼 지난 시점에 같은 라인의 `ActiveNoteList`를 다시 순회하도록 변경했습니다.

```csharp
private void FadeNotes(int lineIndex, float targetAlpha, float duration)
{
    duration = Mathf.Max(0, duration);
    ApplyFadeToActiveNotes(lineIndex, targetAlpha, duration, false);
    ScheduleFadeReconciliation(lineIndex, targetAlpha, duration);
}

private void ReconcileFadeState(int lineIndex, float targetAlpha, int revision)
{
    if (fadeRevisions[lineIndex] != revision) return;

    fadeCompletionTweens[lineIndex] = null;
    ApplyFadeToActiveNotes(lineIndex, targetAlpha, 0, true);
}
```

첫 순회 이후 생성된 노트도 Fade 종료 시점에는 목록에 포함되므로, 일회성 이벤트를 놓쳤더라도 최종 알파값을 복구할 수 있습니다.

현재 구현 전체: [NoteEffectController.cs](../../src/note-fade/NoteEffectController.cs)

### 최신 Fade만 종료 보정을 수행

라인별로 예약된 보정 Tween과 리비전 번호를 관리했습니다. 새 Fade가 시작되면 이전 예약을 취소하고 리비전을 증가시킵니다.

```csharp
private void ScheduleFadeReconciliation(
    int lineIndex,
    float targetAlpha,
    float duration)
{
    fadeCompletionTweens[lineIndex]?.Kill();
    int revision = ++fadeRevisions[lineIndex];

    fadeCompletionTweens[lineIndex] = DOVirtual.DelayedCall(
        duration,
        () => ReconcileFadeState(lineIndex, targetAlpha, revision),
        false);
}
```

보정 콜백은 예약 당시의 리비전과 현재 리비전이 같을 때만 실행됩니다. 이로써 오래된 `FadeOut` 또는 `FadeIn`의 완료 처리가 더 최근 연출을 덮어쓰는 문제를 막았습니다.

### 즉시 적용할 알파값은 명시적으로 완료

길이가 `0`인 Tween에 값 적용을 맡기지 않고 `Complete()`를 호출해 같은 호출 흐름 안에서 알파값이 확정되도록 변경했습니다.

```csharp
FadeNote(currentAlpha, 0).Complete();
FadeNote(targetAlpha, remainTime);

// 진행 중인 Fade가 없다면 현재 전역 상태를 즉시 반영
FadeNote(isTransparent ? 0 : 1, 0).Complete();
```

또한 각 노트가 자신의 현재 Fade Tween을 직접 보관하고, 새 Tween이 시작될 때 기존 Tween을 종료하도록 수정했습니다.

```csharp
public Tween FadeNote(double alpha, double duration)
{
    baseAlphaTween?.Kill();

    baseAlphaTween = DOTween.To(
        () => baseAlpha,
        value =>
        {
            baseAlpha = value;
            ApplyFinalAlpha();
        },
        Mathf.Clamp01((float)alpha),
        Mathf.Max(0f, (float)duration));

    return baseAlphaTween;
}
```

### 파괴 중인 노트는 종료 보정에서 제외

판정되거나 화면 밖으로 나간 노트는 자체적으로 FadeOut한 뒤 풀로 돌아갑니다. 전역 Fade의 종료 보정이 이 Tween을 덮으면 사라져야 할 노트가 다시 보일 수 있으므로, 노트에 파괴 진행 상태를 추가하고 재동기화 대상에서 제외했습니다.

```csharp
foreach (var note in playManager.ActiveNoteList[lineIndex])
{
    if (skipDestroying && note.IsBeingDestroyed) continue;
    note.FadeNote(targetAlpha, duration);
}
```

노트 생성과 Fade 처리의 현재 구현 전체: [Note.cs](../../src/rhythm-sync/Note.cs)

## 결과 및 검증

- 문제가 발생했던 챕터 3-3의 1분 54초 구간에서 신규 노트가 투명하게 남는 현상을 수정했습니다.
- `FadeIn`과 노트 생성의 같은 프레임 내 실행 순서와 관계없이 Fade 종료 후 최종 상태가 일치하도록 했습니다.
- 오브젝트 풀에서 재사용된 노트의 이전 알파값이 남지 않도록 즉시 상태 적용을 보장했습니다.
- Fade가 연속으로 호출돼도 이전 종료 보정이 최신 Fade를 덮어쓰지 않도록 했습니다.
- 파괴 중인 노트의 개별 FadeOut은 전역 상태 보정의 영향을 받지 않도록 분리했습니다.

자동화된 프레임 순서 테스트를 추가한 것은 아니며, 당시에는 문제가 발생한 Timeline 구간을 재생하여 수정 결과를 확인했습니다.

## 수정 이력

| 날짜 | 커밋 | 변경 내용 |
|---|---|---|
| 2026-08-13 | `dde0cad6` | Fade 종료 재동기화, 최신 Fade 리비전 검사, 즉시 알파 적용, 파괴 중 노트 제외 |

## 회고

처음에는 `FadeIn` Signal이 호출되지 않은 문제처럼 보였지만, 실제로는 이벤트가 처리된 시점과 노트가 등록된 시점이 어긋난 문제였습니다. 호출 로그만 확인했다면 Signal이 정상이라는 결론에서 조사가 끝날 수 있었지만, 이벤트가 어느 객체 집합을 대상으로 실행됐는지까지 추적하면서 누락 지점을 찾을 수 있었습니다.

이 경험을 통해 동적으로 생성되는 객체에 전역 연출 상태를 적용할 때는 다음 원칙이 필요하다는 점을 확인했습니다.

- 일회성 이벤트만 전달하지 않고 현재 상태도 별도로 유지합니다.
- 객체 생성 시 현재 전역 상태를 즉시 반영합니다.
- 비동기 연출 종료 시 대상 집합과 최종 상태를 다시 검증합니다.
- 지연된 콜백에는 세대 또는 리비전을 부여해 오래된 작업을 무효화합니다.
- 전역 상태 보정과 객체 고유의 종료 연출이 서로 덮어쓰지 않도록 생명주기를 구분합니다.

핵심은 실행 순서를 우연히 맞추는 것이 아니라, 어떤 순서로 실행되더라도 최종 상태가 같아지는 구조를 만드는 것이었습니다.
