using System;
using System.Collections.Generic;
using System.Linq;

// 입력 오프셋 측정값의 이상치 제거 및 보정값 계산
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2025-10-21
public static class OffsetEstimator
{
    #region Public Methods
    /// <summary>
    /// offsetsSec: 초 단위 입력-센터 델타 목록.
    /// 반환: ms 단위 오프셋(이상치 제거 + 로버스트 평균).
    /// </summary>
    public static float ComputeOffsetMsRobust(
        List<float> offsetsSec,
        float hardLimitAbsMs = 300.0f,   // 절대 컷 (±300ms 밖 제거)
        float madK = 3.5f,              // MAD 배수 (3~3.5 일반적)
        float relaxMadK = 4.5f,         // 1차 MAD가 너무 타이트할 때 완화 재시도
        float minKeepRatio = 0.25f,     // 최소 유지 비율(원본의 25%)
        int minKeepCount = 3,        // 최소 표본 개수
        float? roundToMs = 1.0f         // 결과 반올림(ms). null이면 반올림 안함
    )
    {
        if (offsetsSec == null || offsetsSec.Count == 0)
            return 0.0f;

        // 0) 전처리: NaN/Inf 제거
        var xs = offsetsSec.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
        if (xs.Count == 0) return 0.0f;

        // 1) 하드 리밋 컷 (초 단위 입력 → ms 비교)
        double hardLimitSec = hardLimitAbsMs / 1000.0;
        xs = xs.Where(v => Math.Abs(v) <= hardLimitSec).ToList();
        if (xs.Count == 0) return 0.0f;

        int minKeep = Math.Max(minKeepCount, (int)Math.Ceiling(xs.Count * minKeepRatio));

        // 2) 1차 MAD 필터
        var f1 = MadFilter(xs, madK);
        List<float> use;
        if (f1.Count >= minKeep)
        {
            use = f1;
        }
        else
        {
            // 3) 완화된 MAD로 한 번 더 시도
            var f2 = MadFilter(xs, relaxMadK);
            if (f2.Count >= minKeep)
                use = f2;
            else
            {
                // 4) 그래도 부족하면 중앙값에 수렴 (가장 보수적)
                float med = Median(xs);
                use = new List<float> { med };
            }
        }

        float meanSec = use.Average();
        float resultMs = meanSec * 1000.0f;

        if (roundToMs.HasValue && roundToMs.Value > 0)
        {
            resultMs = (float)(Math.Round(resultMs / roundToMs.Value) * roundToMs.Value);
        }
        return resultMs;
    }

    public static string BuildSummary(List<float> raw, float resultMs)
    {
        int n = raw?.Count ?? 0;
        float meanMs = (n > 0) ? raw.Average() * 1000.0f : 0;
        float medMs = (n > 0) ? Median(raw) * 1000.0f : 0;

        return $"샘플 수: {n}\n" +
               $"Raw 평균: {meanMs:+0.0;-0.0;0} ms, Raw 중앙값: {medMs:+0.0;-0.0;0} ms\n" +
               $"결과(이상치 제거 후): {resultMs:+0.0;-0.0;0} ms";
    }
    #endregion

    #region Helper Methods
    static List<float> MadFilter(List<float> xs, float k)
    {
        if (xs.Count == 0) return new List<float>();
        float med = Median(xs);
        var absDev = xs.Select(v => Math.Abs(v - med)).ToList();
        float mad = Median(absDev);
        if (mad <= 1e-12) return new List<float>(xs); // 모두 거의 동일 → 전부 인라이어

        // 정규분포 표준편차 근사 스케일: 1.4826 * MAD
        float sigma = 1.4826f * mad;
        float thr = k * sigma;
        return xs.Where(v => Math.Abs(v - med) <= thr).ToList();
    }

    static float Median(List<float> xs)
    {
        if (xs == null || xs.Count == 0) return 0.0f;
        var s = xs.OrderBy(v => v).ToList();
        int n = s.Count;
        return (n % 2 == 1) ? s[n / 2]
                            : 0.5f * (s[n / 2 - 1] + s[n / 2]);
    }
    #endregion
}
