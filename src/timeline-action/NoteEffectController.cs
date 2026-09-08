using UnityEngine;
using DG.Tweening;

// Timeline Note Effect Controller
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2026-08-13
public class NoteEffectController : MonoBehaviour
{
    // Components
    private DataManager m_dataManager;
    private PlayManager m_playManager;

    // Fade Info
    private bool m_isLeftTransparent = false;
    private bool m_isRightTransparent = false;
    private double m_leftTransparentStartTime = -1;
    private double m_rightTransparentStartTime = -1;
    private double m_leftTransparentDuration = -1;
    private double m_rightTransparentDuration = -1;
    private readonly Tween[] m_fadeCompletionTweens = new Tween[2];
    private readonly int[] m_fadeRevisions = new int[2];

    // Property
    public bool IsLeftNoteTransparent { get { return m_isLeftTransparent; } }
    public bool IsRightNoteTransparent { get { return m_isRightTransparent; } }
    public double LeftTransparentStartTime { get { return m_leftTransparentStartTime; } }
    public double RightTransparentStartTime { get { return m_rightTransparentStartTime; } }
    public double LeftTransparentDuration { get { return m_leftTransparentDuration; } }
    public double RightTransparentDuration { get { return m_rightTransparentDuration; } }

    #region Unity Method
    protected void Awake()
    {
        // Component
        m_dataManager = FindFirstObjectByType<DataManager>();
        m_playManager = FindFirstObjectByType<PlayManager>();
    }
    protected void OnDestroy()
    {
        foreach (var tween in m_fadeCompletionTweens)
        {
            tween?.Kill();
        }
    }
    #endregion

    #region Public Methods
    public void FadeOutLeftNotes(float t)
    {
        m_isLeftTransparent = true;
        m_leftTransparentStartTime = m_dataManager.SheetData.songPosition / 1000f;
        m_leftTransparentDuration = t;

        FadeNotes(DataManager.Left_Line_Index, 0, t);
    }
    public void FadeInLeftNotes(float t)
    {
        m_isLeftTransparent = false;
        m_leftTransparentStartTime = m_dataManager.SheetData.songPosition / 1000f;
        m_leftTransparentDuration = t;

        FadeNotes(DataManager.Left_Line_Index, 1, t);
    }
    public void FadeOutRightNotes(float t)
    {
        m_isRightTransparent = true;
        m_rightTransparentStartTime = m_dataManager.SheetData.songPosition / 1000f;
        m_rightTransparentDuration = t;

        FadeNotes(DataManager.Right_Line_Index, 0, t);
    }
    public void FadeInRightNotes(float t)
    {
        m_isRightTransparent = false;
        m_rightTransparentStartTime = m_dataManager.SheetData.songPosition / 1000f;
        m_rightTransparentDuration = t;

        FadeNotes(DataManager.Right_Line_Index, 1, t);
    }
    #endregion

    private void FadeNotes(int lineIndex, float targetAlpha, float duration)
    {
        duration = Mathf.Max(0, duration);
        ApplyFadeToActiveNotes(lineIndex, targetAlpha, duration, false);
        ScheduleFadeReconciliation(lineIndex, targetAlpha, duration);
    }

    private void ScheduleFadeReconciliation(int lineIndex, float targetAlpha, float duration)
    {
        // 이전 Fade의 종료 보정이 더 최신 Fade를 덮어쓰지 않도록 교체한다.
        m_fadeCompletionTweens[lineIndex]?.Kill();
        int revision = ++m_fadeRevisions[lineIndex];

        if (duration <= 0)
        {
            ReconcileFadeState(lineIndex, targetAlpha, revision);
            return;
        }

        m_fadeCompletionTweens[lineIndex] = DOVirtual.DelayedCall(
            duration,
            () => ReconcileFadeState(lineIndex, targetAlpha, revision),
            false
        );
    }

    private void ReconcileFadeState(int lineIndex, float targetAlpha, int revision)
    {
        if (m_fadeRevisions[lineIndex] != revision) return;

        m_fadeCompletionTweens[lineIndex] = null;
        // Fade 도중 생성되어 최초 순회에서 빠진 노트까지 최종 상태로 맞춘다.
        ApplyFadeToActiveNotes(lineIndex, targetAlpha, 0, true);
    }

    private void ApplyFadeToActiveNotes(int lineIndex, float targetAlpha, float duration, bool skipDestroying)
    {
        foreach (var note in m_playManager.ActiveNoteList[lineIndex])
        {
            if (skipDestroying && note.IsBeingDestroyed) continue;

            var tween = note.FadeNote(targetAlpha, duration);
            if (duration <= 0)
            {
                tween.Complete();
            }
        }
    }
}
