using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum InGamePhase
{
    Start, InGame, End
}

// 게임의 진행을 관리하는 스크립트이다
// - 입력 오프셋 init, 음악 볼륨 init
// - Start / Game / End 이 3개의 페이즈로 게임을 관리한다
// - [Game]
//      - (물리적)Note를 생성하고 이동시키는 스크립트

// 최초 작성자: 김기홍
// 수정자: ○○○, 김기홍
// 최종 수정일: 2026-11-01
public class PlayManager : Updateable, IPauseable
{
    ////////////////////////////////////////////
    // Components
    protected DataManager dataManager;
    ApodsSnapshotController m_snapshotController;

    ////////////////////////////////////////////
    // SYNC
    private const double SYNC_THRESHOLD_MS = 20.0;
    private const float SYNC_INTERVAL_SECONDS = 0.25f;
    private double m_songTimelineStartTime;
    private float m_nextSyncTime;
#if UNITY_EDITOR
    private float m_nextBgmTimingLogTime;
#endif

    ////////////////////////////////////////////
    // Data
    [Header("Data")]
    [SerializeField] AudioDatabase m_audioDatabase;

    ////////////////////////////////////////////
    // AudioSource
    EventDescription m_songEventDescription;
    EventInstance m_songEventInstance;
    public EventInstance SongEventInstance => m_songEventInstance;
    private UniTaskCompletionSource m_songPreloadCompletionSource;
    private UniTaskCompletionSource m_songInstanceCompletionSource;
    private CustomFMODEventClip m_bgmClip;
    private bool m_sampleDataRequested;
    private double m_songStartPositionSeconds;
    public bool IsStartingManualTimeline { get; private set; }
    public bool IsSongPreloaded { get; private set; }
    public bool UsesTimelineBgm => m_bgmClip != null;

    ////////////////////////////////////////////
    // Timeline 
    [Header("Timeline")]
    [SerializeField] PlayableDirector m_startTimeline;

    [SerializeField]
#if DEBUG_MODE
    public
#endif
    PlayableDirector m_inGameTimeline;
    [SerializeField] PlayableDirector m_endTimeline;
    [ReadOnly][SerializeField] InGamePhase m_inGamePhase;
    [HideInInspector] public UnityEvent<InGamePhase> OnInGamePhaseChanged;
#if DEBUG_MODE
    const int MinTimelinePlaybackSpeedStep = -1;
    const int MaxTimelinePlaybackSpeedStep = 2;
    [ReadOnly][SerializeField] int m_timelinePlaybackSpeedStep = 0;
    [ReadOnly][SerializeField] float m_timelinePlaybackSpeed = 1f;
#endif
    public InGamePhase InGamePhase
    {
        get { return m_inGamePhase; }
        set
        {
            m_inGamePhase = value;
#if DEBUG_MODE
            ResetTimelinePlaybackSpeed_Debug();
#endif
            OnInGamePhaseChanged?.Invoke(m_inGamePhase);
        }
    }

    ////////////////////////////////////////////
    // Cancellation
    private CancellationTokenSource cts;

    ////////////////////////////////////////////
    // Note 관련 Info
    [Header("Note life Cycle 관련 Object")]
    [SerializeField] protected Transform[] genPoints;
    public Transform[] GenPoints { get { return genPoints; } }
    [SerializeField] protected Transform[] judgePoints;
    public Transform[] JudgePoints { get { return judgePoints; } }

    ////////////////////////////////////////////
    // Note Prefab (ObjectPooling)
    [Header("Object Pooling")]
    [SerializeField] private Note m_tapNotePrefab;
    [SerializeField] private Note m_longNotePrefab;
    [SerializeField] private Note m_longNoteEndPrefab;
    [SerializeField] private Note m_tickNotePrefab;
    [SerializeField] private Note m_tabNoteAutoPrefab;
    private NotePool m_tapNotePool;
    private NotePool m_longNotePool;
    private NotePool m_longNoteEndPool;
    private NotePool m_tickNotePool;
    private NotePool m_tabNoteAutoPool;

    // 활성화된 Note들
    [Header("Active Notes")]
    [SerializeField] protected SortedSet<Note>[] activeNoteList; // 현재 Active 중인 노트의 목록
    public IReadOnlyCollection<Note>[] ActiveNoteList => activeNoteList;

    ////////////////////////////////////////////
    // States
    [HideInInspector] public bool isWaitStart = false;
    [HideInInspector] public bool isGameStart = false;
    private bool m_isSongPlayed = false;

    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Unity Methods
    ///////////////////////////////////////////////////////////////
    private void Awake()
    {
        // Pauseable
        this.Register();

        // Components
        dataManager = GetComponent<DataManager>();
        m_snapshotController = GetComponent<ApodsSnapshotController>();
        m_songPreloadCompletionSource = new UniTaskCompletionSource();
        m_songInstanceCompletionSource = new UniTaskCompletionSource();

        // 초기화
        InitActiveNoteList();
        InitializeObjectPooling();
    }
    private void Start()
    {
        // Start Timline
        if (m_startTimeline.time <= 0)
            m_startTimeline.Play();
        InGamePhase = InGamePhase.Start;

        // 딜레이를 없애기 위해 미리 로드한다
        PreloadSongAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }
#if DEBUG_MODE
    private void Update()
    {
        HandleTimelinePlaybackSpeedInput_Debug();
    }
#endif
    private void LateUpdate()
    {
        if (!UsesTimelineBgm && m_isSongPlayed && Time.unscaledTime >= m_nextSyncTime)
        {
            m_nextSyncTime = Time.unscaledTime + SYNC_INTERVAL_SECONDS;
            Synchronize();
        }
    }

    public override void CustomUpdate()
    {
        if (!UsesTimelineBgm || !m_isSongPlayed || m_inGamePhase != InGamePhase.InGame ||
            dataManager.SheetData == null || !m_songEventInstance.isValid() ||
            (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;

        m_inGameTimeline.time = GetTimelineTime(dataManager.SheetData.songPosition);
        // 실험용: Evaluate는 Timeline Signal을 발화하지 않는다.
        m_inGameTimeline.Evaluate();
#if UNITY_EDITOR
        if (Time.unscaledTime >= m_nextBgmTimingLogTime && m_songEventInstance.isValid() &&
            m_songEventInstance.getTimelinePosition(out int currentPositionMs) == FMOD.RESULT.OK)
        {
            m_nextBgmTimingLogTime = Time.unscaledTime + 0.1f;
            double timelineMs = m_inGameTimeline.time * 1000.0;
            Debug.Log($"[BGM Sync CustomUpdate] frame={Time.frameCount} FMOD={currentPositionMs}ms Timeline={timelineMs:F1}ms Diff(FMOD-Timeline)={currentPositionMs - timelineMs:F1}ms", this);
        }
#endif
    }
    private void OnDisable()
    {
        // Pauseable
        this.Unregister();
    }
    private void OnDestroy()
    {
        // 새 트랙이 만든 인스턴스는 그래프가 해제한다.
        if (!UsesTimelineBgm && m_songEventInstance.isValid())
        {
            m_songEventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            m_songEventInstance.release();
        }
        if (m_sampleDataRequested && m_songEventDescription.isValid())
            m_songEventDescription.unloadSampleData();
        // Cancel UniTask
        cts?.Cancel();

        // ObjectPool
        m_tapNotePool.Clear();
        m_longNotePool.Clear();
        m_longNoteEndPool.Clear();
        m_tickNotePool.Clear();
        m_tabNoteAutoPool?.Clear();
    }

    ///////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////
    // Game Play
    ///////////////////////////////////////////////////////////////
    private async UniTaskVoid PreloadSongAsync(CancellationToken cancellationToken)
    {
        try
        {
            m_bgmClip = FindTimelineBgmClip();
            var eventRef = UsesTimelineBgm ? m_bgmClip.EventReference :
                m_audioDatabase.GetMusic(DataManager.CurChapter, DataManager.CurEpisode);

            // 1) EventDescription 얻기
            FMOD.RESULT result = RuntimeManager.StudioSystem.getEventByID(eventRef.Guid, out m_songEventDescription);
            if (result != FMOD.RESULT.OK)
                throw new InvalidOperationException($"FMOD 음원 이벤트를 찾을 수 없습니다: {eventRef} ({result})");

            // 2) 샘플 데이터 로드 요청
            result = m_songEventDescription.loadSampleData();
            if (result != FMOD.RESULT.OK)
                throw new InvalidOperationException($"FMOD 샘플 로드를 요청할 수 없습니다: {eventRef} ({result})");

            m_sampleDataRequested = true;
            // 비스트리밍 샘플만 로드한다. Stream 음원 전체의 로드 완료가 아니다.
            // 3) LOADED 상태가 될 때까지 대기
            Debug.Log($"샘플 로드 중");
            LOADING_STATE loadingState = LOADING_STATE.LOADING;
            FMOD.RESULT loadingResult = FMOD.RESULT.OK;
            await UniTask.WaitUntil(() =>
            {
                loadingResult = m_songEventDescription.getSampleLoadingState(out loadingState);
                return loadingResult != FMOD.RESULT.OK ||
                       loadingState == LOADING_STATE.LOADED ||
                       loadingState == LOADING_STATE.ERROR;
            }, cancellationToken: cancellationToken);

            if (loadingResult != FMOD.RESULT.OK || loadingState != LOADING_STATE.LOADED)
                throw new InvalidOperationException($"FMOD 샘플 로드에 실패했습니다: {eventRef} ({loadingResult}, {loadingState})");

            // 전환하지 않은 에피소드만 기존 Signal 재생 경로를 유지한다.
            if (!UsesTimelineBgm)
            {
                result = m_songEventDescription.createInstance(out m_songEventInstance);
                if (result != FMOD.RESULT.OK || !m_songEventInstance.isValid())
                    throw new InvalidOperationException($"FMOD 음원 인스턴스를 생성할 수 없습니다: {eventRef} ({result})");
            }
            IsSongPreloaded = true;
            m_songPreloadCompletionSource.TrySetResult();

#if UNITY_EDITOR
            Debug.Log($"[{eventRef}] 샘플 로드 완료");
#endif
        }
        catch (OperationCanceledException)
        {
            m_songPreloadCompletionSource.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            m_songPreloadCompletionSource.TrySetException(exception);
            Debug.LogException(exception, this);
        }
    }

    private CustomFMODEventClip FindTimelineBgmClip()
    {
        if (m_inGameTimeline == null || m_inGameTimeline.playableAsset is not TimelineAsset timeline)
            return null;
        CustomFMODEventClip found = null;
        foreach (var output in timeline.GetOutputTracks())
        {
            if (output is not CustomFMODEventTrack track || track.mutedInHierarchy) continue;
            if (m_inGameTimeline.GetGenericBinding(track) != this)
                throw new InvalidOperationException("Custom FMOD Event Track을 이 PlayManager에 바인딩해야 합니다.");
            foreach (var clip in track.GetClips())
            {
                if (found != null)
                    throw new InvalidOperationException("인게임 BGM은 한 개의 클립만 지원합니다.");
                found = clip.asset as CustomFMODEventClip;
                if (found == null || found.EventReference.IsNull)
                    throw new InvalidOperationException("BGM 이벤트가 지정되지 않았습니다.");
                if (Math.Abs(found.StartPositionSeconds * 1000.0 - dataManager.SheetData.PlayStartOffset) > 1)
                    throw new InvalidOperationException("BGM 시작 위치와 채보 PlayStartOffset이 다릅니다.");
            }
        }
        return found;
    }

    public void ConnectTimelineSong(EventInstance instance, double clipStart, double songStartSeconds)
    {
        if (!UsesTimelineBgm || !instance.isValid()) return;
        m_songEventInstance = instance;
        m_songTimelineStartTime = clipStart;
        m_songStartPositionSeconds = songStartSeconds;
        m_isSongPlayed = true;
        m_nextSyncTime = Time.unscaledTime + SYNC_INTERVAL_SECONDS;
        m_songInstanceCompletionSource.TrySetResult();
    }

    public void DisconnectTimelineSong(EventInstance instance)
    {
        if (m_songEventInstance.handle != instance.handle) return;
        m_songEventInstance.clearHandle();
        m_isSongPlayed = false;
    }

    public void FailTimelineSong(string message)
    {
        m_songInstanceCompletionSource.TrySetException(new InvalidOperationException(message));
    }

    // Timeline용 함수
    public void PlaySong()
    {
        if (UsesTimelineBgm || m_isSongPlayed) return;

        if (m_songEventInstance.isValid() == false)
        {
            Debug.LogError("FMOD 음원 인스턴스가 유효하지 않습니다.", this);
            return;
        }

        // 0초 이후에 시작 Signal이 오는 에피소드도 실제 Timeline 시작점을 기준으로 맞춘다
        m_songTimelineStartTime = m_inGameTimeline.time;

        int songStartPositionMs = Mathf.Max(0, Mathf.RoundToInt(dataManager.SheetData.PlayStartOffset));
        FMOD.RESULT result = m_songEventInstance.setTimelinePosition(songStartPositionMs);
        if (result != FMOD.RESULT.OK)
        {
            Debug.LogError($"FMOD 음원 시작 위치를 설정할 수 없습니다: {result}", this);
            return;
        }
#if DEBUG_MODE
        m_songEventInstance.setPitch(m_timelinePlaybackSpeed);
#endif
        result = m_songEventInstance.start();
        if (result != FMOD.RESULT.OK)
        {
            Debug.LogError($"FMOD 음원을 시작할 수 없습니다: {result}", this);
            return;
        }

        m_isSongPlayed = true;
        m_nextSyncTime = Time.unscaledTime + SYNC_INTERVAL_SECONDS;
    }
    public async void Play()
    {
        if (isGameStart || isWaitStart) return;
        isWaitStart = true;

        // Cancellation Token
        cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        try
        {
            // 노래 재생전, 스냅샷 초기화
            m_snapshotController.ClearAll();

            // 노래, 타임라인 재생
            await UniTask.Delay((int)dataManager.SheetData.MusicStartTime, DelayType.Realtime, cancellationToken: cts.Token);

            // Timeline을 먼저 시작한 뒤 음원 로딩을 기다리면 연출만 앞서 진행한다
            // 로딩을 완료한 후 Timeline과 음원 시작 Signal을 함께 진행한다
            await m_songPreloadCompletionSource.Task.AttachExternalCancellation(cts.Token);
            await UniTask.WaitUntil(() => GameManager.Instance.IsPaused == false, cancellationToken: cts.Token);
            isWaitStart = false;
            isGameStart = true;

            // Custom FMOD Event Track 또는 기존 시작 Signal이 음악을 재생한다
            if (UsesTimelineBgm)
                m_inGameTimeline.timeUpdateMode = DirectorUpdateMode.Manual;
            m_inGameTimeline.Play();

            if (UsesTimelineBgm)
            {
                // Manual 모드에서는 최초 평가를 직접 수행해 트랙의 인스턴스를 생성한다.
                IsStartingManualTimeline = true;
                try { m_inGameTimeline.Evaluate(); }
                finally { IsStartingManualTimeline = false; }
            }

            // Set Phase
            InGamePhase = InGamePhase.InGame;

            // Track이 만든 재생 인스턴스를 받은 뒤 노트 처리를 시작한다.
            if (UsesTimelineBgm)
                await m_songInstanceCompletionSource.Task.AttachExternalCancellation(cts.Token);
            ///////////////////////////////////////////////////////////////
            // Note 처리
            for (int barIndex = 0; barIndex < dataManager.SheetData.BarInfoList.Count; barIndex++) // 모든 Bar에 대한 처리
            {
                // 각 마디별 변수 설정
                BarInfo curBar = dataManager.SheetData.BarInfoList[barIndex];
                await MoveNote(curBar, token: cts.Token);
                await ExecuteAction(curBar, token: cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            isWaitStart = false;
        }
        catch (Exception exception)
        {
            isWaitStart = false;
            isGameStart = false;
            m_inGameTimeline.Stop();
            Debug.LogException(exception, this);
        }
    }

    private void Synchronize()
    {
        if (m_inGamePhase != InGamePhase.InGame) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (m_songEventInstance.isValid() == false) return;

        if (m_songEventInstance.getPlaybackState(out PLAYBACK_STATE playbackState) != FMOD.RESULT.OK ||
            (playbackState != PLAYBACK_STATE.PLAYING && playbackState != PLAYBACK_STATE.STARTING))
        {
            return;
        }

        if (m_songEventInstance.getTimelinePosition(out int songPositionMs) != FMOD.RESULT.OK) return;
        dataManager.SheetData.songPosition = songPositionMs;

        double expectedTimelineTime = GetTimelineTime(songPositionMs);
        double driftMs = (m_inGameTimeline.time - expectedTimelineTime) * 1000.0;
        if (Math.Abs(driftMs) > SYNC_THRESHOLD_MS)
        {
            // 다음 자동 평가에서 애니메이션과 Signal을 함께 처리한다
            m_inGameTimeline.time = expectedTimelineTime;
        }
    }

    private double GetTimelineTime(int songPositionMs)
    {
        double startSeconds = UsesTimelineBgm ? m_songStartPositionSeconds : dataManager.SheetData.PlayStartOffset / 1000.0;
        double songElapsedSeconds = songPositionMs / 1000.0 - startSeconds;
        return Math.Max(m_songTimelineStartTime, m_songTimelineStartTime + songElapsedSeconds);
    }

    /// <summary>
    /// 한 마디(curBar)에 포함된 노트들을 시간에 맞춰 떨어뜨리는 함수
    /// 상속받아 라인 수 / 노트 분배 로직을 커스터마이징할 수 있도록 virtual 처리
    /// </summary>
    protected virtual async UniTask MoveNote(BarInfo curBar, CancellationToken token)
    {
        int beatCount = curBar.BeatCount;
        float beatInterval = curBar.BeatInterval;
        float noteTakeTime = dataManager.SheetData.noteTakeTime;

        float barStartTime =
            curBar.RelativeStartTime +
            dataManager.InputOffset +
            dataManager.SheetData.Offset;

        for (int beat = 0; beat < beatCount; beat++)
        {
            // Wait 
            float noteStartTime = barStartTime - noteTakeTime + beatInterval * beat;
            while (true)
            {
                if (dataManager.SheetData.songPosition >= noteStartTime) break;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
            }

            // 동시에 치는 노트인가?
            bool isChord =
                curBar.NoteInfos[DataManager.Left_Line_Index][beat] != null &&
                curBar.NoteInfos[DataManager.Right_Line_Index][beat] != null;

            // Drop Note
            if (curBar.NoteInfos[DataManager.Left_Line_Index][beat] != null)
            {
                // 노트 플레이 정보 초기화
                var noteInfo = curBar.NoteInfos[DataManager.Left_Line_Index][beat];
                noteInfo.InitializeNoteInfo();
                var noteObject = DropNote(noteInfo, noteStartTime, noteTakeTime, isChord);
                if (noteObject != null)
                {
                    noteObject.transform.parent = genPoints[DataManager.Left_Line_Index];
                    noteObject.transform.localRotation = Quaternion.identity;
                    noteObject.transform.localScale = Vector3.one;
                    noteObject.name = $"LEFT : {noteInfo.noteType} : {noteInfo.noteIndex}";
                }
            }
            if (curBar.NoteInfos[DataManager.Right_Line_Index][beat] != null)
            {
                // 노트 플레이 정보 초기화
                var noteInfo = curBar.NoteInfos[DataManager.Right_Line_Index][beat];
                noteInfo.InitializeNoteInfo();
                var noteObject = DropNote(noteInfo, noteStartTime, noteTakeTime, isChord);
                if (noteObject != null)
                {
                    noteObject.transform.parent = genPoints[DataManager.Right_Line_Index];
                    noteObject.transform.localRotation = Quaternion.identity;
                    noteObject.transform.localScale = Vector3.one;
                    noteObject.name = $"Right : {noteInfo.noteType} : {noteInfo.noteIndex}";
                }
            }
        }
    }

    async UniTask ExecuteAction(BarInfo curBar, CancellationToken token)
    {
        float barLength = curBar.Length;
        float barStartTime =
            curBar.RelativeStartTime +
            dataManager.InputOffset +
            dataManager.SheetData.Offset;

        // 해당 함수를 실행했는지 여부 확인
        Dictionary<Action, bool> isActionExecute = new Dictionary<Action, bool>();
        for (int i = 0; i < curBar.Actions.Count; i++)
        {
            isActionExecute.Add(curBar.Actions[i], false);
        }

        for (int i = 0; i < curBar.Actions.Count; i++)
        {
            float relativeActionStartTime =
                curBar.Actions[i].Beat.Numerator * barLength /
                curBar.Actions[i].Beat.Denominator;

            while (true)
            {
                if (dataManager.SheetData.songPosition >= barStartTime + relativeActionStartTime)
                    break;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
            }
            if (isActionExecute[curBar.Actions[i]] == false)
            {
                curBar.Actions[i].ExecuteAction();
                isActionExecute[curBar.Actions[i]] = true;
            }
        }
    }

    /// <summary>
    /// NoteInfo를 기반으로 실제 노트 오브젝트를 생성하는 함수
    /// 상속 클래스에서 라인별 gen/judgePoint를 재정의하기 위해 virtual 처리
    /// </summary>
    protected virtual GameObject DropNote(NoteInfo noteInfo, float noteStartTime, float takeTime, bool isChord)
    {
        /*
        Debug.Log($"Line: {noteInfo.lineName}\n" +
            $"NoteType: {noteInfo.noteType}\n" +
            $"CreateTime: {m_progressTime}\n" +
            $"TakeTime: {takeTime}\n" +
            $"JudgeTime: {m_progressTime + takeTime}");
        */
        // NoteInfo를 기반으로 노트 오브젝트 생성
        Note noteObject = FindUsableObject(noteInfo.noteType); // 사용 가능한 오브젝트 찾기
        if (noteObject == null)
        {
            Debug.LogError("Note가 부족합니다");
            return null;
        }
        var lineIndex = noteInfo.lineIndex;
        return noteObject.CreateNote(
            noteInfo,
            genPoints[lineIndex],
            judgePoints[lineIndex],
            noteStartTime,
            takeTime,
            isChord
        );
    }

    ///////////////////////////////////////////////////////////////
    // Object Pooling
    ///////////////////////////////////////////////////////////////
    private void InitializeObjectPooling()
    {
        m_tapNotePool = new NotePool(m_tapNotePrefab);
        m_longNotePool = new NotePool(m_longNotePrefab);
        m_longNoteEndPool = new NotePool(m_longNoteEndPrefab);
        m_tickNotePool = new NotePool(m_tickNotePrefab);
        m_tabNoteAutoPool = new NotePool(m_tabNoteAutoPrefab);
    }

    protected Note FindUsableObject(int noteType)
    {
        switch (noteType)
        {
            case 1: return m_tapNotePool.Rent();
            case 2: return m_longNotePool.Rent();
            case 3: return m_longNoteEndPool.Rent();
            case 4: return m_tickNotePool.Rent();
            case 5: return m_tabNoteAutoPool.Rent();
        }
        return null;
    }

    ///////////////////////////////////////////////////////////////
    // Active Note List Function
    ///////////////////////////////////////////////////////////////
    protected virtual void InitActiveNoteList()
    {
        activeNoteList = new SortedSet<Note>[2];
        activeNoteList[0] = new SortedSet<Note>();
        activeNoteList[1] = new SortedSet<Note>();
    }
    public virtual int GetNoteSide(int lineIndex) => lineIndex;

    public void AddNoteToActiveList(Note note)
    {
        var lineIndex = note.NoteInformation.lineIndex;
        activeNoteList[lineIndex].Add(note);
    }

    public void RemoveNoteFromActiveList(Note note)
    {
        var lineIndex = note.NoteInformation.lineIndex;
        if (activeNoteList[lineIndex] != null && activeNoteList[lineIndex].Contains(note))
            activeNoteList[lineIndex].Remove(note);
    }
    public virtual Note GetActiveNote(NoteInfo noteInfo)
    {
        var lineIndex = noteInfo.lineIndex;
        foreach (var note in activeNoteList[lineIndex])
        {
            if (note.NoteInformation.noteIndex == noteInfo.noteIndex &&
               note.NoteInformation.parentBar.BarIndex == noteInfo.parentBar.BarIndex)
            {
                return note;
            }
        }
        return null;
    }
    /// <summary>
    /// 이름 수정하지말것 (Timeline Binding 해제됨)
    /// </summary>
    #region Timeline Action
    public void StartEndTimeline()
    {
        m_endTimeline?.Play();
    }
    public void PauseTImeline()
    {
        var curTimeline = GetTimelineByPhase(InGamePhase);
        curTimeline.playableGraph.GetRootPlayable(0).SetSpeed(0);

    }
    #endregion

    ///////////////////////////////////////////////////////////////
    // Helper Method
    ///////////////////////////////////////////////////////////////
    private float CalculateMusicStartTime(float offset, float takeTime)
    {
        if (offset > takeTime) return 0;
        else return takeTime - offset;
    }


    ///////////////////////////////////////////////////////////////
    // Pause,Resume Method
    ///////////////////////////////////////////////////////////////
    bool m_curTImelinePaused = false;
    #region Pause, Resume
    public void Pause()
    {
        if (m_inGamePhase == InGamePhase.InGame)
        {
            m_songEventInstance.setPitch(0);
        }


        // Get CurTimeline
        PlayableDirector curTimeline = GetTimelineByPhase(InGamePhase);
        if (curTimeline == null) return;

        // Check if Already Paused
        m_curTImelinePaused = curTimeline.playableGraph.GetRootPlayable(0).GetSpeed() <= 0;
        curTimeline.playableGraph.GetRootPlayable(0).SetSpeed(0);
    }

    public void Resume()
    {
        if (m_inGamePhase == InGamePhase.InGame)
        {
#if DEBUG_MODE
            m_songEventInstance.setPitch(m_timelinePlaybackSpeed);
#else
            m_songEventInstance.setPitch(1);
#endif
        }

        // Get CurTimeline
        PlayableDirector curTimeline = GetTimelineByPhase(InGamePhase);
        if (curTimeline == null) return;

        // Check if Already Paused
        if (m_curTImelinePaused == false && curTimeline.state == PlayState.Playing)
        {
#if DEBUG_MODE
            curTimeline.playableGraph.GetRootPlayable(0).SetSpeed(m_timelinePlaybackSpeed);
#else
            curTimeline.playableGraph.GetRootPlayable(0).SetSpeed(1);
#endif
        }
    }
    #endregion

    public PlayableDirector GetTimelineByPhase(InGamePhase phase)
    {
        switch (phase)
        {
            case InGamePhase.Start:
                return m_startTimeline;
            case InGamePhase.End:
                return m_endTimeline;
            default: // case InGamePhase.InGame:
                return m_inGameTimeline;
        }
    }
    public void SetTime(float songTimeSeconds)
    {
        if (InGamePhase != InGamePhase.InGame) return;
        if (songTimeSeconds < 0) return;

        int songPositionMs = Mathf.RoundToInt(songTimeSeconds * 1000f);
        if (IsPlaying(m_songEventInstance))
            m_songEventInstance.setTimelinePosition(songPositionMs);

        dataManager.SheetData.songPosition = songPositionMs;
        m_inGameTimeline.time = GetTimelineTime(songPositionMs);
        m_inGameTimeline.Evaluate();
    }
    private bool IsPlaying(EventInstance instance)
    {
        // 1) 현재 재생 상태를 얻어온다
        PLAYBACK_STATE state;
        var result = instance.getPlaybackState(out state);

        // 2) 에러가 없고, 상태가 PLAYING(또는 STARTING)인지 체크
        if (result == FMOD.RESULT.OK)
        {
            return state == PLAYBACK_STATE.PLAYING
                || state == PLAYBACK_STATE.STARTING;
        }
        // 유효하지 않은 인스턴스거나 에러일 때는 재생 중이 아님으로 간주
        return false;
    }
    public float GetPlayRatio()
    {
        if (IsPlaying(m_songEventInstance))
        {
            m_songEventDescription.getLength(out int lengthMs);
            m_songEventInstance.getTimelinePosition(out int positionMs);
            return (float)positionMs / lengthMs;
        }
        else
        {
            return 0;
        }
    }
#if DEBUG_MODE
    public bool CanEnterEndTimeline_Debug(out string errorMessage)
    {
        errorMessage = "";
        if (m_endTimeline == null)
        {
            errorMessage = $"End Timeline is not assigned in the current scene.";
            return false;
        }
        return true;
    }

    public void EnterEndTimeline_Debug(DialogueManager dialogueManager)
    {
        dialogueManager?.TerminateDialogue();

        cts?.Cancel();
        isWaitStart = false;
        isGameStart = false;

        StopTimeline_Debug(m_startTimeline);
        StopTimeline_Debug(m_inGameTimeline);
        StopSong_Debug();
        ClearActiveNotes_Debug();

        InGamePhase = InGamePhase.End;

        m_endTimeline.Stop();
        m_endTimeline.time = 0;
        m_endTimeline.Evaluate();
        m_endTimeline.Play();
    }

    private void StopTimeline_Debug(PlayableDirector timeline)
    {
        if (timeline == null) return;
        timeline.Stop();
    }

    private void StopSong_Debug()
    {
        if (m_songEventInstance.isValid() == false) return;
        m_songEventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        m_isSongPlayed = false;
    }

    private void ClearActiveNotes_Debug()
    {
        if (activeNoteList == null) return;

        for (int i = 0; i < activeNoteList.Length; i++)
        {
            if (activeNoteList[i] == null) continue;

            var notes = new List<Note>(activeNoteList[i]);
            foreach (var note in notes)
            {
                if (note == null) continue;
                note.gameObject.SetActive(false);
            }
            activeNoteList[i].Clear();
        }
    }

    private void HandleTimelinePlaybackSpeedInput_Debug()
    {
        if (Input.GetKeyDown(KeyCode.Less) || Input.GetKeyDown(KeyCode.Comma))
        {
            DecreaseTimelinePlaybackSpeed_Debug();
        }
        else if (Input.GetKeyDown(KeyCode.Greater) || Input.GetKeyDown(KeyCode.Period))
        {
            IncreaseTimelinePlaybackSpeed_Debug();
        }
    }

    public void IncreaseTimelinePlaybackSpeed_Debug()
    {
        SetTimelinePlaybackSpeedStep_Debug(m_timelinePlaybackSpeedStep + 1);
    }

    public void DecreaseTimelinePlaybackSpeed_Debug()
    {
        SetTimelinePlaybackSpeedStep_Debug(m_timelinePlaybackSpeedStep - 1);
    }

    private void ResetTimelinePlaybackSpeed_Debug()
    {
        m_timelinePlaybackSpeedStep = 0;
        m_timelinePlaybackSpeed = 1f;
        ApplyTimelinePlaybackSpeed_Debug();
    }

    private void SetTimelinePlaybackSpeedStep_Debug(int speedStep)
    {
        int clampedSpeedStep = Mathf.Clamp(
            speedStep,
            MinTimelinePlaybackSpeedStep,
            MaxTimelinePlaybackSpeedStep
        );
        if (m_timelinePlaybackSpeedStep == clampedSpeedStep) return;

        m_timelinePlaybackSpeedStep = clampedSpeedStep;
        m_timelinePlaybackSpeed = Mathf.Pow(2f, m_timelinePlaybackSpeedStep);
        ApplyTimelinePlaybackSpeed_Debug();
        Debug.Log($"[PlayManager] Timeline Playback Speed: x{m_timelinePlaybackSpeed}");
    }

    private void ApplyTimelinePlaybackSpeed_Debug()
    {
        PlayableDirector curTimeline = GetTimelineByPhase(InGamePhase);
        if (curTimeline != null &&
            curTimeline.playableGraph.IsValid() &&
            curTimeline.playableGraph.GetRootPlayableCount() > 0 &&
            curTimeline.state == PlayState.Playing)
        {
            curTimeline.playableGraph.GetRootPlayable(0).SetSpeed(m_timelinePlaybackSpeed);
        }

        if (InGamePhase == InGamePhase.InGame && m_songEventInstance.isValid())
        {
            m_songEventInstance.setPitch(m_timelinePlaybackSpeed);
        }
    }

    public void CancelPlay()
    {
        m_startTimeline.Stop();
        m_inGameTimeline.Stop();
        cts?.Cancel();
    }
#endif
}
