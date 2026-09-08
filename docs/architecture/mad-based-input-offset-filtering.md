# MAD 기반 입력 오프셋 이상치 제거

## 설계 목표

- 적은 수의 입력으로도 순간적인 오입력에 흔들리지 않는 보정값을 계산한다
- 고정된 범위가 아니라 현재 측정값의 중심과 산포를 기준으로 이상치를 판별한다
- 정상적인 입력은 최대한 유지하면서 크게 벗어난 값만 제거한다

## 이상치 제거 알고리즘 선택

이상치를 탐지하는 대표적인 방법으로 Z-Score와 IQR을 고려할 수 있다

### Z-Score

Z-Score는 평균과 표준편차를 이용해 각 데이터가 평균에서 얼마나 떨어져 있는지를 판단한다.

하지만 입력 오프셋 데이터에는 순간적인 오입력처럼 극단적인 값이 포함될 수 있다. 이러한 값은 평균을 자신의 방향으로 이동시키고, 편차의 제곱을 사용하는 표준편차 역시 증가시킨다.

```text
이상치 포함
    ↓
평균 이동
    ↓
표준편차 증가
    ↓
이상치를 판별하는 기준도 함께 변화
```

즉, 제거하려는 이상치가 이상치를 판단하기 위한 기준 자체에 영향을 줄 수 있다.

### IQR

IQR(Interquartile Range)은 제1사분위수 `Q1`과 제3사분위수 `Q3` 사이의 범위로 데이터의 산포를 나타낸다.

```text
IQR = Q3 - Q1
```

일반적으로 다음 범위를 벗어나는 값을 이상치로 판단한다.

```text
Lower Bound = Q1 - 1.5 × IQR
Upper Bound = Q3 + 1.5 × IQR
```

IQR은 데이터의 중앙 50%를 이용하므로 평균과 표준편차를 사용하는 Z-Score보다 극단값의 영향을 적게 받는다.

다만 실제로 확보되는 표본 수는 곡 길이와 사용자의 입력 성공 여부에 따라 달라진다. 표본 수가 적은 경우에는 개별 측정값과 사분위수 계산 방식에 따라 `Q1`, `Q3`, IQR이 크게 달라질 수 있다.

또한 이번 문제에서는 대표값인 중앙값을 기준으로 각 입력이 얼마나 떨어져 있는지를 직접 측정하고, 중앙값을 중심으로 대칭적인 허용 범위를 만들고자 했다. 따라서 사분위수 사이의 범위를 사용하는 IQR보다 중앙값에 대한 절대 편차를 사용하는 MAD를 선택했다.

### MAD

따라서 중앙값을 중심으로 데이터의 산포를 측정할 수 있는 MAD(Median Absolute Deviation)​를 사용했다.

평균 대신 Median, 표준편차 대신 MAD를 사용하면 극단적인 값이 중심과 산포의 추정에 미치는 영향을 줄일 수 있다.

## MAD 기반 이상치 제거 구현

### Median 및 MAD 계산

먼저 측정값 `X`의 중앙값을 구한다.

```text
m = Median(X)
```

각 측정값과 중앙값 사이의 거리를 절댓값으로 계산하고, 그 값들의 중앙값을 MAD로 사용한다.

```text
MAD = Median(|xᵢ - m|)
```

### MAD 스케일링

MAD와 표준편차는 모두 데이터의 산포를 나타내지만, 계산 방식과 값의 스케일이 다르다.

정규분포에서는 다음 관계가 성립한다.

```text
MAD ≈ 0.6745σ
```

따라서 MAD에 1.4826을 곱해, 정규분포에서 표준편차와 같은 기준으로 해석할 수 있도록 스케일을 보정한다

```csharp
float sigma = 1.4826f * mad;
```

### 임계값을 이용한 이상치 제거

스케일링한 MAD에 배수 `k`를 적용해 중앙값을 기준으로 한 허용 범위를 계산한다.

```text
Threshold = k × σMAD
|xᵢ - Median(X)| > Threshold이면 이상치
```

```csharp
float thr = k * sigma;

return xs
    .Where(v => Math.Abs(v - med) <= thr)
    .ToList();
```

전체 처리 과정은 다음과 같다.

```text
입력 데이터
    ↓
Median 계산
    ↓
중앙값과 각 입력값 사이의 절대 편차 계산
    ↓
MAD 계산
    ↓
1.4826 × MAD로 산포 스케일 변환
    ↓
허용 범위 계산
    ↓
중앙값에서 지나치게 먼 입력 제거
```

## 안정성 보완

### 비정상 수치 및 Hard Limit 제거

MAD 필터를 적용하기 전에 `NaN`과 무한대 값을 제거한다. 이후 절댓값이 `300ms`를 초과하는 입력은 사용자 반응의 편차로 보기 어려운 명백한 오입력으로 판단해 제외한다.

```csharp
var xs = offsetsSec
    .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
    .ToList();

double hardLimitSec = hardLimitAbsMs / 1000.0;
xs = xs.Where(v => Math.Abs(v) <= hardLimitSec).ToList();
```

### MAD가 0인 경우

동일한 측정값이 반복되어 중앙값과 같은 값이 표본의 다수를 차지하면 MAD가 0이 될 수 있다.

이 상태에서 일반적인 MAD 임계값을 계산하면 허용 범위도 0에 가까워진다. 현재 구현에서는 MAD가 `1e-12` 이하이면 유효한 산포를 계산하기 어려운 경우로 보고 해당 `MadFilter` 호출에서 필터링을 건너뛴다.

```csharp
if (mad <= 1e-12)
    return new List<float>(xs);
```

### 최소 표본 수와 완화 재시도

Hard Limit을 적용하고 남은 표본 수를 기준으로 최소 유지 표본 수를 계산한다.

```text
minKeep = max(3, ceil(Hard Limit 통과 표본 수 × 0.25))
```

먼저 `k = 3.5`인 MAD 필터를 적용한다. 필터 결과가 `minKeep`보다 적으면 `k`를 `4.5`로 높여 같은 Hard Limit 통과 표본에 MAD 필터를 다시 적용한다.

```csharp
var f1 = MadFilter(xs, madK);

if (f1.Count < minKeep)
{
    var f2 = MadFilter(xs, relaxMadK);
    // ...
}
```

### Median Fallback

완화된 기준을 적용한 뒤에도 최소 표본을 확보하지 못하면 추가로 범위를 넓히지 않고 중앙값 하나를 최종 계산에 사용한다.

```csharp
float med = Median(xs);
use = new List<float> { med };
```

## 최종 오프셋 계산

필터링을 통과한 입력이 충분하면 남은 값의 평균을 최종 오프셋으로 사용한다. 이후 초 단위 측정값을 밀리초 단위로 변환하고 기본 `1ms` 단위로 반올림한다.

```csharp
float meanSec = use.Average();
float resultMs = meanSec * 1000.0f;

resultMs = (float)(Math.Round(resultMs / roundToMs.Value)
    * roundToMs.Value);
```

```text
OffsetEstimator에 측정값 전달
    ↓
NaN / Infinity 제거
    ↓
±300ms Hard Limit 적용
    ↓
최소 유지 표본 수 계산
    ↓
1차 MadFilter 실행(k = 3.5)
    ├─ MAD ≤ 1e-12 → 전체 입력 반환
    └─ MAD > 1e-12 → MAD 임계값으로 필터링
    ↓
결과가 minKeep 이상인가?
    ├─ 예 → 필터 결과의 평균 사용
    └─ 아니요
          ↓
       2차 MadFilter 실행(k = 4.5)
          ├─ 결과가 minKeep 이상 → 필터 결과의 평균
          └─ 결과가 minKeep 미만 → Hard Limit 통과 표본의 중앙값
    ↓
ms 단위 변환 및 반올림
```

## 관련 코드

- [`OffsetEstimator.cs`](../../src/input-offset/OffsetEstimator.cs)
