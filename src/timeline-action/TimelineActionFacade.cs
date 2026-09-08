using UnityEngine;

// (Facade) Timeline 시그널이 호출하는 연출 실행용
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2026-04-14
public class TimelineActionFacade : Singleton<TimelineActionFacade>
{
    // Controllers
    [SerializeField] NoteEffectController m_noteEffectController;
    [SerializeField] GlowEffectController m_glowEffectController;
    [SerializeField] ShakeEffectController m_shakeEffectController;

    #region TimeLine - Action (Note Fade)
    public bool IsLeftNoteTransparent => m_noteEffectController.IsLeftNoteTransparent;
    public bool IsRightNoteTransparent => m_noteEffectController.IsRightNoteTransparent;
    public double LeftTransparentStartTime => m_noteEffectController.LeftTransparentStartTime;
    public double RightTransparentStartTime => m_noteEffectController.RightTransparentStartTime;
    public double LeftTransparentDuration => m_noteEffectController.LeftTransparentDuration;
    public double RightTransparentDuration => m_noteEffectController.RightTransparentDuration;
    public void FadeOutNotes(float t)
    {
        m_noteEffectController.FadeOutLeftNotes(t);
        m_noteEffectController.FadeOutRightNotes(t);
    }
    public void FadeInNotes(float t)
    {
        m_noteEffectController.FadeInLeftNotes(t);
        m_noteEffectController.FadeInRightNotes(t);
    }
    public void FadeOutLeftNotes(float t) => m_noteEffectController.FadeOutLeftNotes(t);
    public void FadeInLeftNotes(float t) => m_noteEffectController.FadeInLeftNotes(t);
    public void FadeOutRightNotes(float t) => m_noteEffectController.FadeOutRightNotes(t);
    public void FadeInRightNotes(float t) => m_noteEffectController.FadeInRightNotes(t);
    #endregion

    #region TimeLine - Action (Disconnect)
    /*
    // ※ 라인을 페이드 아웃하는 기능은 타임라인 이용해야함 ※
    public void Disconnect(bool isRight)
    {
        // 1. 해당 유닛을 즉시 어둡게 표시
        // 타임리인에서 구현

        // 2. 해당 유닛 위에 빗금 표시
        // (아직 미구현)

        // 3. 해당 유닛 방향에 끊김 효과음 재생
        // SoundManager.Instance.Play(AudioName.,false, isRight?0.8f:-0.8f);
        // (아직 미구현)


        // 4. 해당 라인 페이드 아웃 애니메이션
        if (isRight) FadeOutRightNotes(1f);
        else FadeOutLeftNotes(1f);
        // ※ 라인을 페이드 아웃하는 기능은 타임라인 이용해야함 ※
    }
    public void Reconnect(bool isRight)
    {
        // 1. 해당 유닛을 즉시 밝게 표시
        // 타임리인에서 구현

        // 2. 해당 유닛 위에 빗금 표시 해제
        // (아직 미구현)

        // 3. 해당 유닛 방향에 연결 효과음 재생
        // SoundManager.Instance.Play(AudioName.,false, isRight?0.8f:-0.8f);
        // (아직 미구현)

        // 4. 해당 라인 페이드 아웃 애니메이션
        if (isRight) FadeInRightNotes(1f);
        else FadeInLeftNotes(1f);
        // ※ 라인을 페이드 아웃하는 기능은 타임라인 이용해야함 ※
    }
    */
    #endregion

    #region TimeLine - Action (Glow)
    public void TurnOnGlow() => m_glowEffectController.TurnOnGlow();
    public void TurnOffGlow() => m_glowEffectController.TurnOffGlow();

    /// <summary>
    /// Timeline용 함수, Glow Color 변경
    /// </summary>
    /// <param name="param">
    /// 인자 1 = color(HexCode)
    /// 인자 2 = duration
    /// </param>
    public void ChangeGlowColor(string param) => m_glowEffectController.ChangeGlowColor(param);

    /// <summary>
    /// Timeline용 함수, Glow Intensity 설정
    /// </summary>
    /// <param name="param">
    /// 인자 1 = intensity
    /// 인자 2 = duration
    /// </param>
    public void SetGlowIntensity(string param) => m_glowEffectController.SetGlowIntensity(param);
    #endregion

    #region TimeLine - Action (Shake)
    public bool IsShake => m_shakeEffectController.IsShake;
    // 판정 시 Shake 시작
    public void StartJudgementShake(float duration, float strength, int vibrato, float randomness, bool snapping, bool fadeOut, string randomnessMode)
     => m_shakeEffectController.StartJudgementShake(duration,strength, vibrato, randomness, snapping, fadeOut, randomnessMode);
    // 판정 시 Shake 종료
    public void EndJudgementShake() => m_shakeEffectController.EndJudgementShake();
    public void ApplyShake(GameObject targetLine) => m_shakeEffectController.ApplyShake(targetLine);
    #endregion
}
