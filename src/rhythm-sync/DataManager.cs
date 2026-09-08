using FMODUnity;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// Data 전처리 및 Global Data 관리
// 최초 작성자: 김기홍
// 수정자: 
// 최종 수정일: 2026-06-05
public class DataManager : Updateable, IPauseable
{
    ////////////////////////////////////////////
    // Components
    PlayManager m_playManager;

    ////////////////////////////////////////////
    // 악보 Data
    [Header("Datas")]
    [Tooltip("Sheet Data")]
    [SerializeField] SheetContainer m_sheetContainer;
    public static int CurChapter = -1;
    public static int CurEpisode = -1;
    public static GameMode CurGameMode = GameMode.Default;
    private Sheet m_sheetData;
    public Sheet SheetData { get { return m_sheetData; } }
    public GameMode GameMode { get { return CurGameMode; } }

    private bool m_isPaused = false;

    ////////////////////////////////////////////
    // LineIndex
    public const int Left_Line_Index = 0;
    public const int Right_Line_Index = 1;

    ////////////////////////////////////////////
    // 플레이 관련 정보
    [Header("Input Offset (입력값)")]
    [SerializeField] float m_inputOffset = 0; // 단위 : ms
    public float InputOffset { get { return m_inputOffset; } }

#if UNITY_EDITOR
    ////////////////////////////////////////////
    // Debug
    [HorizontalLine(color: EColor.Gray)]
    [Header("Debuging용 정보 입력")]
    [SerializeField] float m_inputOffset_debug;
#endif

    ////////////////////////////////////////////
    // FMOD
    FMOD.ChannelGroup m_channelGroup;
    int m_sampleRate;

    ////////////////////////////////////////////
    ////////////////////////////////////////////
    // Unity Methods
    ////////////////////////////////////////////
    private void Awake()
    {
        ////////////////////////////////////////////
        // Pauseable
        this.Register();

        ////////////////////////////////////////////
        // Components
        m_playManager = GetComponent<PlayManager>();
        m_inputOffset = SaveLoadManager.GetInputOffset();
        if (CurChapter == -1 || CurEpisode == -1) Debug.LogError("에피소드 정보가 초기화되지 않았습니다.");
        m_sheetData = m_sheetContainer.GetSheet(CurGameMode, CurChapter, CurEpisode);

        // Get FMOD Core System
        RuntimeManager.CoreSystem.getMasterChannelGroup(out m_channelGroup);
        RuntimeManager.CoreSystem.getSoftwareFormat(out m_sampleRate, out _, out _);
    }
    public override void CustomUpdate()
    {
        if (m_sheetData != null && m_isPaused == false)
        {
            m_playManager.SongEventInstance.getTimelinePosition(out m_sheetData.songPosition);
        }
    }
    private void OnDestroy()
    {
        this.Unregister();
    }

    ////////////////////////////////////////////
    ////////////////////////////////////////////
    // Other Methods
    ////////////////////////////////////////////
    // 라인 인덱스 가져오기 위한 통합 함수
    public virtual List<int> GetLineIndexesByKey(InputEnum input)
    {
        List<int> lineIndexes = new List<int>();

        if (GameManager.Instance.IsMono)
        {
            lineIndexes.Add(Left_Line_Index); // LeftLine
            lineIndexes.Add(Right_Line_Index); // RightLine
        }
        else
        {
            switch (input)
            {
                case InputEnum.Left:
                    lineIndexes.Add(Left_Line_Index);
                    break;
                case InputEnum.Right:
                    lineIndexes.Add(Right_Line_Index);
                    break;
            }
        }
        return lineIndexes;
    }

    public virtual bool TryGetLineSideIndex(int lineIndex, out int sideIndex)
    {
        if (lineIndex == Left_Line_Index || lineIndex == Right_Line_Index)
        {
            sideIndex = lineIndex;
            return true;
        }

        sideIndex = -1;
        return false;
    }

    ////////////////////////////////////////////
    ////////////////////////////////////////////
    // Pause / Resume
    ////////////////////////////////////////////
    public void Pause()
    {
        m_isPaused = true;
    }

    public void Resume()
    {
        /*
        m_sheetData.songStartDspTime += GetAudioDspTime() - m_pausedPointDspTime;
        m_sheetData.songPosition = (GetAudioDspTime() - m_sheetData.songStartDspTime) * 1000;
        */
        m_playManager.SetTime((float)m_sheetData.songPosition / 1000); // InGame Timeline을 보정한다
        m_isPaused = false;
    }

#if UNITY_EDITOR
    private (int chapter, int episode) ParseChapterEpisode(string sceneName)
    {
        var match = Regex.Match(sceneName, @"Ch(\d+)Ep(\d+)");
        if (match.Success)
        {
            int chapter = int.Parse(match.Groups[1].Value);
            int episode = int.Parse(match.Groups[2].Value);
            return (chapter, episode);
        }
        throw new System.FormatException("입력 형식이 올바르지 않습니다: " + sceneName);
    }
#endif
}
