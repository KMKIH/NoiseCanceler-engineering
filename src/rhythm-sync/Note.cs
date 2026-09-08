using DG.Tweening;
using System;
using TMPro;
using UnityEngine;

// Note의 오브젝트를 관리하는 스크립트로 각 노트 오브젝트에 들어있다
// 판정함수 파라미터 추가
// 최초 작성자: 김기홍
// 수정자: ○○○, ○○○, 김기홍
// 최종 수정일: 2026-08-13
public abstract class Note : MonoBehaviour, IComparable<Note>
{
    // 게임 관련 스크립트
    protected DataManager dataManager;
    protected JudgeManager judgeManager;
    protected PlayManager playManager;
    protected ObjectRepository objectRepository;

    // 오브젝트가 소유한 컴포넌트
    protected SpriteRenderer noteSpriteRenderer;

    // State
    protected bool isMove = false;
    public bool IsBeingDestroyed { get; private set; }

    // Note Information
    [SerializeField] private NoteInfo m_noteInfo;
    public NoteInfo NoteInformation
    {
        get { return m_noteInfo; }
        protected set { m_noteInfo = value; }
    }

    // Object Information
    protected Transform startPoint;
    protected Transform judgePoint;
    protected float takeTime; // Note가 생성되고나서 판정지점에 도달하는데 걸리는 시간, 단위 : ms
    [Header("Color")]
    [SerializeField] private Color m_defaultColor = Color.white;
    [SerializeField] private Color m_chordColor = new Color(255, 178, 102);

    // Fade
    protected float baseAlpha;
    private Tween m_baseAlphaTween;

    // Disconect
    protected float disconnectedAlpha;
    public float DisconnectedAlpha => disconnectedAlpha;
    const float disconnectedFadeOutStart = 1f / 20f;   // 0.05
    const float disconnectedFadeOutEnd = 15f / 20f;  // 0.75
    const float disconnectedFadeInStart = 19f / 20f;   // 0.05
    const float disconnectedFadeInEnd = 1;  // 0.75

    [Header("Debug")]
    [SerializeField] protected float startTime; // 해당 노트의 생성시간, 단위 : ms
    [SerializeField] protected double judgeTime;

    // 판정음 관련
    bool m_isJudgeSoundPlayed = false;
    float Panning
    {
        get
        {
            if (GameManager.Instance.IsMono) return 0;
            return NoteInformation.lineIndex == DataManager.Left_Line_Index ? -0.8f : 0.8f;
        }
    }

    [Header("Debug UI")]
    [SerializeField] TMP_Text m_judgeTimeText;

    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Unity Functions
    ///////////////////////////////////////////////////////////////
    protected virtual void Awake()
    {
        judgeManager = FindFirstObjectByType<JudgeManager>();
        dataManager = FindFirstObjectByType<DataManager>();
        playManager = FindFirstObjectByType<PlayManager>();

        noteSpriteRenderer = GetComponent<SpriteRenderer>();

#if DEBUG_MODE
        DebugManager.OnDebugMode.AddListener(OnDebugMode);
#endif
    }
    protected virtual void Update()
    {
        if (isMove)
        {
            double curTime = dataManager.SheetData.songPosition; // ms
            double ratio = (curTime - startTime) / takeTime;

            // 노트 이동
            transform.position = Vector3.LerpUnclamped(startPoint.position, judgePoint.position, (float)ratio);

            // Disconnect 인 경우 Alpha 계산
            if (GameManager.Instance.IsDisconnected[m_noteInfo.lineIndex])
            {
                if (ratio < 19f / 20f) {
                    float alpha = Mathf.Clamp01((disconnectedFadeOutEnd - (float)ratio) / (disconnectedFadeOutEnd - disconnectedFadeOutStart));
                    SetDisconnectedAlpha(alpha);
                }
                else
                {
                    // float alpha = Mathf.Clamp01((end - (float)ratio) / (end - start)); // == 1 - InverseLerp
                    float alpha = Mathf.InverseLerp(disconnectedFadeInStart, disconnectedFadeInEnd, (float)ratio);
                    SetDisconnectedAlpha(alpha);
                }
            }
            else
            {
                SetDisconnectedAlpha(1);
            }

            // 판정음 (1프레임 전에 예약한다)
            double frameWindow = Time.deltaTime * 1000 * 2; // 60fps 기준 최대 33ms 허용

            if (judgeTime <= curTime + frameWindow)
            {
                PlayJudgementSound();
            }
        }
    }
#if DEBUG_MODE
    void LateUpdate()
    {
        m_judgeTimeText.transform.rotation = Quaternion.identity;
        if (NoteInformation.IsActive == true)
        {
            var offset = dataManager.InputOffset + dataManager.SheetData.Offset;
            m_judgeTimeText.text = $"judgeTime = {NoteInformation.judgeTime + offset:F1}ms";
        }
    }
#endif
    protected virtual void OnEnable()
    {
#if DEBUG_MODE
        m_judgeTimeText.gameObject.SetActive(DebugManager.IsDebugMode);
#else   
        m_judgeTimeText.gameObject.SetActive(false);
#endif
    }
    protected virtual void OnDisable()
    {
        isMove = false;

        playManager.RemoveNoteFromActiveList(this);
        m_noteInfo = null;

        // 초기화
        if (NoteInformation != null) NoteInformation.OnActiveStateChanged -= OnInActive;
    }
    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Note Function
    ///////////////////////////////////////////////////////////////
    public virtual GameObject CreateNote(NoteInfo _noteInfo, Transform genPoint, Transform judgePoint, float noteStartTime, float takeTime, bool isChord)
    {
        IsBeingDestroyed = false;

        // Insert Data
        NoteInformation = _noteInfo;
        Initialize(genPoint, judgePoint, noteStartTime, takeTime);

        // Add Active Note List
        if (playManager == null) playManager = FindFirstObjectByType<PlayManager>();
        playManager.AddNoteToActiveList(this);

        // Connect Event
        NoteInformation.OnActiveStateChanged -= OnInActive;
        NoteInformation.OnActiveStateChanged += OnInActive;

        // Set Active Object
        gameObject.SetActive(true);
        isMove = true;

        // Set Color
        if (isChord) noteSpriteRenderer.color = m_chordColor;
        else noteSpriteRenderer.color = m_defaultColor;

        // Set Fade
        double fadeStartTime = 0;
        double fadeDuration = 0;
        bool isTransparent = false;
        // (Set Data)
        switch (NoteInformation.lineIndex)
        {
            case DataManager.Left_Line_Index:
                fadeStartTime = TimelineActionFacade.Instance.LeftTransparentStartTime;
                fadeDuration = TimelineActionFacade.Instance.LeftTransparentDuration;
                isTransparent = TimelineActionFacade.Instance.IsLeftNoteTransparent;
                break;
            case DataManager.Right_Line_Index:
                fadeStartTime = TimelineActionFacade.Instance.RightTransparentStartTime;
                fadeDuration = TimelineActionFacade.Instance.RightTransparentDuration;
                isTransparent = TimelineActionFacade.Instance.IsRightNoteTransparent;
                break;
        }
        // (Fade)
        if (fadeStartTime + fadeDuration > dataManager.SheetData.songPosition / 1000f)
        {
            var remainTime = fadeStartTime + fadeDuration - dataManager.SheetData.songPosition / 1000f;
            var targetAlpha = isTransparent ? 0 : 1;
            double curAlpha = 0;
            if (targetAlpha >= 1)
            {
                curAlpha = 1 - remainTime / fadeDuration;
            }
            else
            {
                curAlpha = remainTime / fadeDuration;
            }
            FadeNote(curAlpha, 0).Complete();
            FadeNote(targetAlpha, remainTime);
        }
        else
        {
            FadeNote(isTransparent ? 0 : 1, 0).Complete();
        }

        return this.gameObject;
    }
    public virtual void DestroyNote()
    {
        IsBeingDestroyed = true;
        isMove = false;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(FadeNote(0, 0.2f))
            .AppendCallback(() =>
            {
                gameObject.SetActive(false); // 노트 오브젝트를 비활성화 하여 ObjectPooling에 다시 사용가능하게 한다
            });
    }
    public Tween FadeNote(double alpha, double t)
    {
        m_baseAlphaTween?.Kill();

        float target = Mathf.Clamp01((float)alpha);
        float duration = Mathf.Max(0f, (float)t);

        m_baseAlphaTween = DOTween.To(
            () => baseAlpha,
            x => { baseAlpha = x; ApplyFinalAlpha(); },
            target,
            duration
        );

        return m_baseAlphaTween;
    }
    // 판정범위를 벗어나거나 판정을 하지 못하는 경우 처리되는 함수
    protected virtual void OnInActive(bool isActive)
    {
        if (isActive) return;
        if (noteSpriteRenderer != null)
        {
            noteSpriteRenderer.color = Color.black;
        }
    }
    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Init Function
    ///////////////////////////////////////////////////////////////
    private void Initialize(Transform start, Transform judge, float noteStartTime, float takeTime)
    {
        // 필요한 데이터 저장
        startPoint = start;
        judgePoint = judge;
        this.takeTime = takeTime;
        startTime = noteStartTime;
        judgeTime = NoteInformation.judgeTime + dataManager.SheetData.Offset;

        // 초기값 설정
        transform.position = startPoint.position;
        m_isJudgeSoundPlayed = false;
    }
    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Judge Function
    ///////////////////////////////////////////////////////////////
    public void OnJudge(JudgmentType judgeType)
    {
        NoteInformation.IsJudged = true;
        Debug.Log($"{judgeType}, {NoteInformation.judgeTime}");
        // Get Note Info
        var lineIndex = NoteInformation.lineIndex;

        ///////////////////////////////////////////////////////////////
        // Judge Feedback
        ///////////////////////////////////////////////////////////////
        judgeManager.OnNoteJudged(judgeType, lineIndex, NoteInformation.noteType);
        // Score
        // Battery
        // Judgement UI
        // Judge Effect

        PlayJudgementSound();
        Shake(judgeType, lineIndex);

        ///////////////////////////////////////////////////////////////
        // 판정 이후 처리
        ///////////////////////////////////////////////////////////////
        OnJudgComplete(judgeType);
    }
    protected abstract void OnJudgComplete(JudgmentType judgeType);

    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Move Function
    ///////////////////////////////////////////////////////////////
    public void ResumeMove()
    {
        isMove = true;
    }
    public void PauseMove()
    {
        isMove = false;
    }
    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // internal Function
    ///////////////////////////////////////////////////////////////
    // 기준점을 넘어가기 전에 판정을 한 경우, 판정음을 예약한다
    void PlayJudgementSound()
    {
        // 예외처리 - 이미 플레이 되었는가?
        if (m_isJudgeSoundPlayed) return;
        m_isJudgeSoundPlayed = true;

        double curTime = dataManager.SheetData.songPosition;
        var offset = dataManager.SheetData.Offset;
        double diff = NoteInformation.judgeTime + offset - curTime;

        // 판정음
        var audioName = NoteInformation.noteType == 4 ? AudioName.Tick : AudioName.Tap;
        // Debug.Log($"호출 시점 : {curTime}\n판정 시간 : {NoteInformation.judgeTime + offset}\nOffset : {offset}\n음원재생시점 : {diff}");
        SoundManager.Instance.PlayOneShotScheduled(audioName, diff / 1000, Panning);
    }
    void Shake(JudgmentType judgeType, int lineIndex)
    {
        if (TimelineActionFacade.Instance.IsShake)
        {
            if (lineIndex == DataManager.Right_Line_Index)
                TimelineActionFacade.Instance.ApplyShake(objectRepository.GetObject(ObjectName.RightLine));
            if (lineIndex == DataManager.Left_Line_Index)
                TimelineActionFacade.Instance.ApplyShake(objectRepository.GetObject(ObjectName.LeftLine));
        }
    }
    protected virtual void SetDisconnectedAlpha(float alpha)
    {
        disconnectedAlpha = alpha;
        ApplyFinalAlpha();
    }
    protected virtual void ApplyFinalAlpha()
    {
        float finalAlpha = baseAlpha * disconnectedAlpha;

        Color c = noteSpriteRenderer.color;
        c.a = finalAlpha;
        noteSpriteRenderer.color = c;
    }


#if DEBUG_MODE
    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Debuging Function
    ///////////////////////////////////////////////////////////////
    void OnDebugMode(bool isActive)
    {
        if (isActive)
        {
            m_judgeTimeText.gameObject.SetActive(true);
        }
        else
        {
            m_judgeTimeText.gameObject.SetActive(false);
        }
    }
#endif

    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // ICompareable
    ///////////////////////////////////////////////////////////////
    public int CompareTo(Note other)
    {
        return judgeTime.CompareTo(other.judgeTime);
    }
}
