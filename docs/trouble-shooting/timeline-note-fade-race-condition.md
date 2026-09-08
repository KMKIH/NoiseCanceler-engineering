# Timeline FadeIn과 노트 생성이 겹칠 때 노트가 투명해지는 문제

## 문제 상황

일부 챕터에서 특정 노트가 생성되었지만 화면에 보이지 않는 문제가 발생했다. 해당 구간을 살펴보니 투명했던 노트를 다시 보여 주는 Timeline `FadeIn` Signal과 신규 노트 생성이 같은 프레임에 실행되고 있었다.

노트의 이동과 판정은 정상적으로 처리되었고 `FadeIn` Signal도 호출되었지만, 같은 프레임에 생성된 노트만 투명한 상태로 남았다.

## 동작 과정 확인

노트의 Fade 연출은 [`NoteEffectController`](../../src/timeline-action/NoteEffectController.cs)가 담당한다. Timeline Signal을 받으면 해당 라인의 현재 `ActiveNoteList`를 순회하며 각 노트에 Fade를 적용한다.

```csharp
foreach (var note in m_playManager.ActiveNoteList[lineIndex])
{
    note.FadeNote(targetAlpha, duration);
}
```

이 방식은 Signal이 호출될 때 이미 생성되어 있는 노트에만 Fade를 일회성으로 적용한다. 따라서 Fade가 시작된 이후 생성되는 노트는 이 순회에 포함될 수 없다.

이를 보완하기 위해 노트 생성 로직에서는 현재 Fade의 시작 시각과 지속 시간을 조회한다. Fade가 진행 중이라면 경과 시간에 맞는 현재 알파값을 구하고, 목표 알파값까지 남은 시간 동안 Fade하도록 처리한다.

```csharp
FadeNote(curAlpha, 0);
FadeNote(targetAlpha, remainTime);
```

즉, 기존 노트는 `NoteEffectController`가 Fade하고, 이후 생성되는 노트는 생성 시점에 현재 Fade 상태를 계산하여 연출에 합류하는 구조였다.

## 원인 분석

`FadeIn` Signal과 노트 생성이 같은 프레임에 실행될 경우, 함수 호출 순서에 따라 문제가 발생할 수 있었다.

```text
FadeOut Signal 완료
    ↓
모든 노트가 투명화된 상태
    ↓
FadeIn Signal 처리
    ↓
현재 ActiveNoteList의 노트에 FadeIn 적용
    ↓
(같은 프레임) 신규 노트 생성 및 ActiveNoteList 등록
    ↓
신규 노트는 앞선 FadeIn 적용 대상에서 누락
    ↓
기존 노트는 정상적으로 출력 / 신규 노트만 투명한 상태
```

즉, `FadeIn`이 호출 시점의 활성 노트에만 적용되면서 함수 호출 순서에 따라 일부 신규 노트가 Fade 대상에서 누락될 수 있었다.

## 문제 해결

### Fade 종료 시 활성 노트 재동기화

Signal을 받은 즉시 현재 활성 노트에 Fade를 적용하는 기존 동작은 유지했다. 대신 Fade가 끝나는 시점에 해당 라인의 `ActiveNoteList`를 다시 순회하고, 모든 노트를 최종 알파값으로 맞추는 보정 단계를 추가했다.

```csharp
private void FadeNotes(int lineIndex, float targetAlpha, float duration)
{
    duration = Mathf.Max(0, duration);
    ApplyFadeToActiveNotes(lineIndex, targetAlpha, duration, false);
    ScheduleFadeReconciliation(lineIndex, targetAlpha, duration);
}

private void ReconcileFadeState(int lineIndex, float targetAlpha, int revision)
{
    if (m_fadeRevisions[lineIndex] != revision) return;

    m_fadeCompletionTweens[lineIndex] = null;
    ApplyFadeToActiveNotes(lineIndex, targetAlpha, 0, true);
}
```

최초 순회 이후에 생성된 노트도 Fade 종료 시점에는 `ActiveNoteList`에 포함된다. 따라서 같은 프레임에 실행된 순서와 관계없이 최종적으로 모든 활성 노트가 현재 Fade 상태와 일치한다.

### 오래된 Fade 보정 무효화

Fade 연출이 끝나기 전에 반대 방향의 Fade가 시작될 수도 있다. 이때 이전 Fade의 종료 보정이 뒤늦게 실행되면 더 최신 상태를 덮어쓸 수 있다.

이를 방지하기 위해 라인별로 종료 보정 Tween과 리비전 번호를 관리했다. 새로운 Fade가 시작되면 기존 예약을 종료하고 리비전을 증가시켰으며, 보정 시점에는 예약 당시의 리비전이 현재 값과 같은 경우에만 알파값을 적용했다.

```csharp
m_fadeCompletionTweens[lineIndex]?.Kill();
int revision = ++m_fadeRevisions[lineIndex];

m_fadeCompletionTweens[lineIndex] = DOVirtual.DelayedCall(
    duration,
    () => ReconcileFadeState(lineIndex, targetAlpha, revision),
    false);
```

### 파괴 중인 노트는 재동기화에서 제외

판정 후 사라지는 중인 노트는 자체 FadeOut을 유지해야 한다. 전역 Fade의 종료 보정이 이 연출을 덮어쓰지 않도록 `IsBeingDestroyed` 상태를 추가하고 재동기화 대상에서 제외했다.

## 결과

- `FadeIn` Signal과 노트 생성이 같은 프레임에 실행되어도 신규 노트가 투명한 상태로 남지 않는다
- Fade 도중 생성된 노트도 연출 종료 시 현재 전역 상태와 일치한다
- 연속된 Fade에서는 가장 최근 연출의 종료 보정만 적용된다
- 판정 후 파괴 중인 노트의 개별 FadeOut은 전역 보정과 충돌하지 않는다

## 회고

문제는 `NoteEffectController`와 `Note`가 같은 알파 상태를 다루면서도, 두 모듈 사이에서 상태를 어떻게 동기화할지에 대한 규칙이 없었다는 점이다. 여러 모듈이 하나의 상태를 함께 다룰 때는 각 모듈의 역할뿐만 아니라 상태를 동기화하는 시점과 방법도 명확하게 정의해야 한다.

## 관련 코드

- [`NoteEffectController.cs`](../../src/timeline-action/NoteEffectController.cs)
- [`Note.cs`](../../src/rhythm-sync/Note.cs)
