using System;
using System.Collections.Generic;
using System.Linq;

// 마디안의 데이터 저장. noteInfo * 2 (레인별로 저장), action (마디에 해당하는 Action)
// 최초 작성자: 김기홍
// 수정자: 
// 최종 수정일: 2026-02-20
public class BarInfo
{
    //////////////////////////////////////////////////////
    // Note & Action Info
    Dictionary<int, NoteInfo[]> m_noteinfos; // 라인별 note Info가 저장되어있다
    Action[] m_actions;

    public IReadOnlyDictionary<int, NoteInfo[]> NoteInfos => m_noteinfos;
    public IReadOnlyList<Action> Actions => m_actions;

    //////////////////////////////////////////////////////
    // 마디에 관련된 정보

    // (기본 데이터)
    private int m_barIndex;
    private int m_beatPerBar; // 분자
    private float m_bpm;
    private float m_relativeStartTime; // 노트 시작 시간이 기준이다, 단위 = ms
    private float m_beatInterval; // 단위 : ms

    // (가공 데이터)
    private float m_length; // 마디의 길이, 단위 = ms
    private int m_lineDenominator;
    private int m_actionDenominator;
    private int m_lcm; // 마디안의 박자의 개수

    // Getter
    public int BarIndex { get { return m_barIndex; } }
    public int BeatPerBar { get { return m_beatPerBar; } }
    public float Bpm { get { return m_bpm; } }
    public float RelativeStartTime { get { return m_relativeStartTime; } }
    public float BeatInterval { get { return m_beatInterval; } }
    public float Length { get { return m_length; } }
    public int BeatCount { get { return m_lcm; } }

    //////////////////////////////////////////////////////
    // 생성자
    public BarInfo(NoteInfo[] upperLine, NoteInfo[] lowerLine, Action[] actions,
        int barIndex, int beatPerBar, float bpm, float relativeStartTime)
    {
        // 배열을 복사하여 사용하기 위해 ToArray() 사용
        m_noteinfos = new Dictionary<int, NoteInfo[]>();
        m_noteinfos.Add(0, upperLine.ToArray());
        m_noteinfos.Add(1, lowerLine.ToArray());
        this.m_actions = actions.ToArray();
        if (actions != null && actions.Length > 0)
        {
            Array.Sort(actions, (x, y) => (x.Beat.Numerator * y.Beat.Denominator).CompareTo(y.Beat.Numerator * x.Beat.Denominator));
        }

        // Set Data
        m_barIndex = barIndex;
        m_beatPerBar = beatPerBar;
        m_bpm = bpm;
        m_relativeStartTime = relativeStartTime;

        // 데이터 가공 (Calculate Data)
        m_length = 60f * m_beatPerBar * 1000 / m_bpm;
        m_lineDenominator = NoteInfos.Values.Select(array => array.Length).Where(x => x != 0).Aggregate(1, (lcm, next) => lcm * next / GCD(lcm, next)); // Line들의 Denominator의 LCM
        m_actionDenominator = 1;
        if (Actions != null && Actions.Count > 0) m_actionDenominator = Actions.Max(action => action.Beat.Denominator); // Action의 Denominator
        m_lcm = LCM(m_lineDenominator, m_actionDenominator);
        m_beatInterval = (float)m_length / m_lcm; // 단위 : ms

        // 정규화
        m_noteinfos = NoteInfos.ToDictionary(pair => pair.Key, pair => ExpandRhythm(pair.Value, m_lcm));// 정규화

        // Set JudgeTime

        for (int beat = 0; beat < m_lcm; beat++)
        {
            if (m_noteinfos[0][beat] != null)
                m_noteinfos[0][beat].judgeTime = relativeStartTime + beat * m_beatInterval;
        }
        for (int beat = 0; beat < m_lcm; beat++)
        {
            if (m_noteinfos[1][beat] != null)
                m_noteinfos[1][beat].judgeTime = relativeStartTime + beat * m_beatInterval;
        }


        // Set Parent
        foreach (NoteInfo noteInfo in m_noteinfos.Values.SelectMany(array => array))
        {
            if (noteInfo != null)
            {
                noteInfo.SetParentBar(this);
            }
        }

    }
    ///////////////////////////////////////////////
    ///////////////////////////////////////////////
    // Helper Method
    ///////////////////////////////////////////////
    private NoteInfo[] ExpandRhythm(NoteInfo[] rhythm, int beat)
    {
        if (rhythm.Length == beat)
        {
            return rhythm;
        }
        else if (rhythm.Length == 0)
        {
            return new NoteInfo[beat];
        }
        else if (rhythm.Length < beat)
        {
            NoteInfo[] expandedRhythm = new NoteInfo[beat];
            int expansionFactor = beat / rhythm.Length;

            for (int i = 0; i < rhythm.Length; i++)
            {
                expandedRhythm[i * expansionFactor] = rhythm[i];
                if(expandedRhythm[i * expansionFactor] != null)
                    expandedRhythm[i * expansionFactor].noteIndex = i * expansionFactor;
                for (int j = 1; j < expansionFactor; j++)
                {
                    expandedRhythm[i * expansionFactor + j] = null;
                }
            }

            return expandedRhythm;
        }
        else
        {
            // N박자를 초과하는 경우, 처음 N개만 사용
            NoteInfo[] truncatedRhythm = new NoteInfo[beat];
            Array.Copy(rhythm, truncatedRhythm, beat);
            return truncatedRhythm;
        }
    }
    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
    private static int LCM(int a, int b)
    {
        return (a / GCD(a, b)) * b;
    }
}
