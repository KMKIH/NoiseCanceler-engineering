# Unity Timeline과 FMOD 음원의 동기화 개선

## 문제 상황

음원에 맞춰 게임 내 연출을 구성하기 위해 Unity Timeline을 사용하고, Timeline 내 FMOD Event Track에서 음원을 재생했다.

Editor Preview에서는 음원과 연출의 싱크가 맞았지만, Play Mode에서는 어긋나는 현상이 발생했다. Timeline 시각과 FMOD 이벤트의 재생 위치를 비교한 결과, 평균 약 120ms의 차이를 확인했다.

> [Editor Preview와 Play Mode에서의 FMOD 이벤트 실행 차이](timeline-audio-track-preview.md)

## 1차 개선 - 음원 별도 재생과 주기 보정

기존 FMOD Event Track에서는 Timeline의 클립 진입 시점에 음원 재생을 요청한다. 이때 실제 재생이 시작되기까지의 준비 과정에서 지연이 발생했을 가능성이 있다고 판단했다.

이를 개선하기 위해 음원 재생과 Timeline 연출을 분리하고, 음원을 미리 준비하도록 변경했다. 또한 재생 중 발생하는 위치 차이를 줄이기 위해 FMOD 재생 위치를 기준으로 Timeline을 주기적으로 보정하는 방식을 구현했다.

- **재생 준비:** FMOD 프리로드가 완료된 후 Timeline을 시작한다.
- **음원 시작:** Timeline에서 Signal을 받으면 준비한 FMOD 이벤트를 재생한다.
- **주기 보정:** Timeline과 FMOD의 재생 위치를 주기적으로 비교하고, 차이가 20ms를 초과하면 Timeline 시각을 FMOD 재생 위치에 맞춘다.

측정 결과는 다음과 같았다.

| 음원 재생 방식 | 평균 절대 차이 |
|---|---:|
| 기존 FMOD Event Track | 120.505ms |
| 음원 별도 재생 + 주기 보정 | 7.241ms |

## 추가 검증 - 동기화에 영향을 주는 요소 비교

### 추가 검증 배경

1차 개선으로 음원과 연출의 싱크가 개선됐지만, 박자에 정확히 맞춰야 하는 연출에서는 여전히 어긋남이 느껴졌다.

이에 동기화에 영향을 줄 수 있는 다른 요소를 확인하고, 재생 위치 차이를 더욱 줄이기 위해 추가 실험을 진행했다.

### 비교 요소

동기화에 영향을 줄 수 있는 다른 요소로 FMOD Stream과 Timeline Update Method를 살펴봤다.

| 요소 | 동기화에 영향을 줄 수 있는 이유 |
|---|---|
| **FMOD Stream** | Stream을 활성화하면 음원 전체를 메모리에 미리 적재하지 않고, 재생에 필요한 데이터를 순차적으로 읽어온다. 이 과정에서 데이터 로딩과 버퍼 준비에 따른 지연이 발생할 수 있다고 판단했다. |
| **Timeline Update Method** | Timeline의 시각을 갱신하는 기준을 설정하는 옵션이다. Timeline이 자체 시계로 진행되면 음원과 연출 사이에 생긴 시간 차이가 유지될 수 있다. FMOD 재생 위치를 읽어 매 프레임 Timeline 시각을 맞추면 이 차이를 줄일 수 있다고 봤다. |

### 실험 조건

Unity 에디터의 Game View에서 같은 음원을 조건별로 115초씩 3회 재생했다. FMOD Stream 설정(켬·끔), 음원 재생 방식(Custom FMOD Event Track·별도 재생), Timeline의 Update Method를 조합해 총 8가지 조건을 비교했다.

> Update Method는 기존 게임에서 사용하던 `DSPClock`으로 잡았다. 이후 Stream을 켠 상태에서 `GameTime`으로 바꿔 비교한 결과, 커스텀 트랙과 별도 재생 모두 평균 절대 차이의 변화가 0.5ms 미만이었다. 이에 큰 결과 차이가 없다고 판단하여 `DSPClock` 결과를 기준으로 정리했다.

#### 1. 음원 재생 방식

**①Custom FMOD Event Track**

트랙에서 실제로 재생한 음원의 위치를 읽기 위해 `CustomFMODEventTrack`을 구현했다. 실험에 사용할 이벤트와 음원 시작 위치는 Custom FMOD Event Clip에 지정했다. 클립에 진입하면 믹서가 FMOD 인스턴스를 생성해 재생하고, 그 인스턴스를 `PlayManager`에 전달한다. 클립 시작보다 늦게 진입한 경우에는 경과한 시간만큼 음원 시작 위치도 옮긴다.

```csharp
// CustomFMODEventMixer.ProcessFrame()의 클립 진입 처리 발췌
instance = RuntimeManager.CreateInstance(asset.EventReference);

int startMs = (int)Math.Round(
    (asset.StartPositionSeconds + Director.time - current.start) * 1000.0);

if (!Check(instance.setTimelinePosition(Math.Max(0, startMs)))) return;
if (!Check(instance.start())) return;

receiver.ConnectTimelineSong(instance, current.start, asset.StartPositionSeconds);
```

**②음원 별도 재생과 주기 보정**

별도 재생에서는 트랙을 음소거하고 `PlayManager`가 프리로드 단계에서 준비한 인스턴스를 재생했다. 기존 게임에서는 음악 시작 Signal이 `PlaySong()`을 호출한다. 비교 실험에서는 시작 조건을 통일하기 위해 Timeline을 시작한 직후 `PlaySong()`을 직접 호출했다. 실행 도중 차이가 20ms를 넘으면 Timeline을 음원에 맞췄다.

```csharp
// PlaySong()의 시작 위치 설정과 재생 처리 발췌
m_songTimelineStartTime = m_inGameTimeline.time;

int songStartPositionMs = Mathf.Max(0, Mathf.RoundToInt(dataManager.SheetData.PlayStartOffset));
FMOD.RESULT result = m_songEventInstance.setTimelinePosition(songStartPositionMs);
// 오류 처리 및 디버그 배속 설정 생략
result = m_songEventInstance.start();
```

```csharp
// Synchronize()의 보정 처리 발췌
if (m_songEventInstance.getTimelinePosition(out int songPositionMs) != FMOD.RESULT.OK) return;

double expectedTimelineTime = GetTimelineTime(songPositionMs);
double driftMs = (m_inGameTimeline.time - expectedTimelineTime) * 1000.0;

if (Math.Abs(driftMs) > SYNC_THRESHOLD_MS)
    m_inGameTimeline.time = expectedTimelineTime;
```
#### 2. Timeline 갱신 방식
**①DSPClock**

Unity의 오디오 DSP 시계를 기준으로 Timeline을 갱신한다.

**②Manual**

Manual에서는 [`CustomUpdate`](../architecture/custom-update-system.md)를 통해 DataManager에서 FMOD 재생 위치를 갱신한 후, 해당 위치를 Timeline 시각에 반영하고 Evaluate()를 호출했다.

```csharp
m_inGameTimeline.time = GetTimelineTime(dataManager.SheetData.songPosition);
m_inGameTimeline.Evaluate();
```

### 실험 결과

**①DSPClock에서의 재생 위치 차이**

수치는 Timeline 시각과 FMOD 재생 위치의 평균 절대 차이다.

| 음원 재생 방식 | Stream 활성화 | Stream 비활성화 |
|---|---:|---:|
| Custom FMOD Event Track | 110.405ms | 39.814ms |
| 음원 별도 재생 + 주기 보정 | 6.448ms | 8.172ms |

Stream 설정에 따른 차이는 Custom FMOD Event Track에서 가장 크게 나타났다. 반면 음원 별도 재생과 주기 보정 방식에서는 Stream 설정에 따른 차이가 작았다.

> **참고** — [실험 조건과 상세 측정 결과](../data/timeline-fmod-sync-results.md)


**②Manual 갱신 검토**

Manual에서는 매 프레임 FMOD 재생 위치를 Timeline 시각에 직접 대입하고 Evaluate()를 호출했다.

이 방식은 FMOD 재생 위치를 Timeline에 직접 반영하므로, 갱신 직후 측정한 두 값의 차이를 독립적인 동기화 정확도 지표로 보기 어렵다고 판단했다.

따라서 Manual은 재생 위치 차이보다 기존 Timeline 기능을 정상적으로 유지할 수 있는지를 중심으로 검토했다.

## 최종 구현 방식 선택

Timeline의 Update Method를 Manual로 설정한 방식은 FMOD 재생 위치를 기준으로 Timeline을 매 프레임 갱신할 수 있지만, 이번에 구현한 방식에서는 Timeline 시각을 지정하고 PlayableDirector.Evaluate()를 호출해도 기존 연출에 필요한 Signal이 호출되지 않았다.

따라서 기존 Signal 동작을 유지하면서 평균 절대 차이를 6~8ms 수준으로 줄일 수 있는 음원 별도 재생과 주기 보정 방식을 최종적으로 선택했다.

### 시연 영상

<table>
  <thead>
    <tr>
      <th width="50%">Editor Preview (목표 연출)</th>
      <th width="50%">최종 Play Mode (개선 방식 적용 결과)</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>
        <video src="https://github.com/user-attachments/assets/afd42389-094f-416e-84ff-7f347b5dd33c" width="100%" controls></video>
        <br>
        <a href="../../assets/videos/ch5-ep2-editor-preview.mp4">원본 MP4</a>
      </td>
      <td>
        <video src="https://github.com/user-attachments/assets/94d0d6d8-45a8-4f6b-975d-1b87c3145af9" width="100%" controls></video>
        <br>
        <a href="../../assets/videos/ch5-ep2-play-mode-final.mp4">원본 MP4</a>
      </td>
    </tr>
  </tbody>
</table>

## 측정 범위와 한계

이번 실험에서는 Timeline 시각과 FMOD 이벤트의 재생 위치를 비교했다. 음원 별도 재생과 주기 보정을 적용한 뒤 두 재생 위치의 평균 절대 차이가 줄어든 것을 확인했다.

실제 화면과 소리의 출력 시점에는 렌더링, VSync, 디스플레이 갱신, 오디오 버퍼 등이 영향을 준다. 이번 실험의 검증 범위는 게임 내부의 재생 위치 차이까지다.

## 관련 코드

- [PlayManager.cs](../../src/rhythm-sync/PlayManager.cs) — 음원 프리로드, Signal 기반 별도 재생과 Timeline 주기 보정
- [CustomFMODEventTrack.cs](../../src/rhythm-sync/CustomFMODEventTrack.cs) — 실험용 트랙에서 FMOD 인스턴스를 재생하고 PlayManager에 연결
- [CustomFMODEventClip.cs](../../src/rhythm-sync/CustomFMODEventClip.cs) — 트랙에서 재생할 이벤트, 음원 시작 위치와 음량 설정
- [DataManager.cs](../../src/rhythm-sync/DataManager.cs) — FMOD 인스턴스의 재생 위치로 음원 시각 갱신
- [UpdateManager.cs](../../src/custom-update/UpdateManager.cs) — 등록 순서에 따라 각 컴포넌트의 CustomUpdate 호출
