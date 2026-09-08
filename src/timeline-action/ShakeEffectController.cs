using DG.Tweening;
using UnityEngine;

// Timeline Shake Effect Controller
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2026-04-13
public class ShakeEffectController : MonoBehaviour
{
    // Shake 관련 변수
    public struct ShakeParams
    {
        public float duration;
        public float strength;
        public int vibrato;
        public float randomness;
        public bool snapping;
        public bool fadeOut;
        public string randomnessMode;
    }
    private ShakeParams m_shakeParams;
    private bool m_isShake;
    public bool IsShake { get { return m_isShake; } }

    #region Public Methods
    // 판정 시 Shake 시작
    public void StartJudgementShake(float duration, float strength, int vibrato, float randomness, bool snapping, bool fadeOut, string randomnessMode)
    {
        m_isShake = true;
        m_shakeParams = new ShakeParams
        {
            duration = duration,
            strength = strength,
            vibrato = vibrato,
            randomness = randomness,
            snapping = snapping,
            fadeOut = fadeOut,
            randomnessMode = randomnessMode
        };
    }
    // 판정 시 Shake 종료
    public void EndJudgementShake()
    {
        m_isShake = false;
    }
    public void ApplyShake(GameObject targetLine)
    {
        var shakeParams = m_shakeParams;

        targetLine.transform.DOKill();
        targetLine.transform.DOShakePosition(
            duration: shakeParams.duration,
            strength: shakeParams.strength,
            vibrato: shakeParams.vibrato,
            randomness: shakeParams.randomness,
            snapping: shakeParams.snapping,
            fadeOut: shakeParams.fadeOut,
            randomnessMode: shakeParams.randomnessMode == "Full" ?
                ShakeRandomnessMode.Full : ShakeRandomnessMode.Harmonic
        );
    }
    #endregion
}
