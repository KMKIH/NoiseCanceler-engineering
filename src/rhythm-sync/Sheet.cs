using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// Sheet Data를 저장하는 스크립트
// 최초 작성자: 김기홍
// 수정자: 
// 최종 수정일: 2026-04-16
public class Sheet
{
    int m_lineNum = 2;

    // Initalize Sheet 함수를 통해 초기화 된다
    [SerializeField] int m_beatPerBar; // 한마디에 들어가는 박자의 수
    [SerializeField] float m_bpm;
    [SerializeField] float m_offset; // (단위 : ms)
    [SerializeField] float m_playStartOffset; // n초 지점부터 음악을 시작 (단위 : ms)
    [SerializeField] float m_crotchet; // bpm에서 계산되는 한박자의 시간 (단위 : ms)
    [SerializeField] float m_musicStartTime; // 노트가 악보보다 먼저 생성되어야할때, 노래를 몇초 뒤에 재생할 지 계산 (단위 : ms)
    [SerializeField] int m_noteCount;
    [SerializeField] int m_validLeftNoteCount;
    [SerializeField] int m_validRightNoteCount;

    public int songPosition = -1; // (단위 : ms)
    public bool isInitialiszed = false;
    public float noteTakeTime = 4000f;

    public int BeatPerBar { get { return m_beatPerBar; } }
    public float Bpm { get { return m_bpm; } }
    public float Offset { get { return m_offset; } }
    public float PlayStartOffset { get { return m_playStartOffset; } }
    public float Crotchet { get { return m_crotchet; } }
    public float MusicStartTime { get { return m_musicStartTime; } }
    public int NoteCount { get { return (int)m_noteCount; } }
    public int ValidLeftNoteCount { get { return (int)m_validLeftNoteCount; } }
    public int ValidRightNoteCount { get { return (int)m_validRightNoteCount; } }

    ///////////////////////////////////////////////
    // 악보
    private List<BarInfo> m_sheet; // CSV를 통해 읽어온 데이터 저장
    public IReadOnlyList<BarInfo> BarInfoList => m_sheet; // 악보

    ///////////////////////////////////////////////
    ///////////////////////////////////////////////
    // 악보에 대한 데이터 처리
    ///////////////////////////////////////////////
    public void InitializeSheet(string csvData)
    {
        ///////////////////////////////////////////////
        // Parse Sheet
        ///////////////////////////////////////////////
        m_sheet = new List<BarInfo>();
        var rows = ParseCsv(csvData); // CSV 파싱
        var headers = rows[0]; // 헤더와 데이터 분리
        for(int i = 0;i < headers.Count();i++)
        {
            if (headers[i].EndsWith("\r"))
                headers[i] = headers[i].Substring(0, headers[i].Length - 1);
        }
        PreprocessInitValue(headers, rows[1]); // Bpm, Offset등의 정보 처리

        // 노트, 액션 데이터 처리
        var dataRows = rows.Skip(2).ToList();
        m_crotchet = 60f / m_bpm * m_beatPerBar * 1000; // 단위 : ms
        for (int i = 0; i < dataRows.Count; i++)
        {
            m_sheet.Add(CreateBarInfo(dataRows[i], i));
        }

        // 기타 필요한 정보 처리
        m_musicStartTime = CalculateMusicStartTime(m_offset, noteTakeTime);


        ///////////////////////////////////////////////
        // Insert Song Info
        ///////////////////////////////////////////////
    }

    ///////////////////////////////////////////////
    ///////////////////////////////////////////////
    // Preprocess
    ///////////////////////////////////////////////
    private void PreprocessInitValue(string[] headers, string[] initDataRow)
    {
        int monomodeNoteCount_Left = 0;
        int monomodeNoteCount_Right = 0;
        for (int i = 0; i < headers.Length; i++)
        {
            switch (headers[i])
            {
                case "beat":
                    m_beatPerBar = int.Parse(initDataRow[i]);
                    break;
                case "bpm":
                    m_bpm = int.Parse(initDataRow[i]);
                    break;
                case "offset":
                    m_offset = int.Parse(initDataRow[i]);
                    break;
                case "playStartOffset":
                    m_playStartOffset = int.Parse(initDataRow[i]);
                    break;
                case "noteCount":
                    m_noteCount = int.Parse(initDataRow[i]);
                    break;
                case "Mono Mode Note Count (Left)":
                    monomodeNoteCount_Left = int.Parse(initDataRow[i]);
                    break;
                case "Mono Mode Note Count (Right)":
                    monomodeNoteCount_Right = int.Parse(initDataRow[i]);
                    break;
                case "Valid  Left Note Count":
                    m_validLeftNoteCount = int.Parse(initDataRow[i]);
                    break;
                case "Valid  Right Note Count":
                    m_validRightNoteCount = int.Parse(initDataRow[i]);
                    break;
            }
        }
        m_validLeftNoteCount += monomodeNoteCount_Left;
        m_validRightNoteCount += monomodeNoteCount_Right;
    }
    /// <summary>
    /// TODO: beatPerBar, bpm, offset의 정보를 이용하여 note의 데이터를 처리한다
    /// </summary>
    /// <param name="datas"> 각 lane별 Note의 정보와 Action의 정보가 문자열로 저장된 형태 </param>
    /// <returns></returns>
    float relativeStartTime = 0; // 단위 : ms
    NoteInfo[] lastNoteInfo = new NoteInfo[2] { null, null };
    private BarInfo CreateBarInfo(string[] datas, int index)
    {
        BarInfo ret = null;
        relativeStartTime = index * m_crotchet;

        // Data Parsing
        string[] notes_Raw = datas;
        int[][] notes = notes_Raw.Select(array => array.Select(x => x - '0').ToArray()).ToArray();

        NoteInfo[][] ret_line = new NoteInfo[m_lineNum][];
        for (int line = 0; line < m_lineNum; line++)
        {
            // 선언 및 초기화
            var beatCount = notes[line].Length;
            ret_line[line] = new NoteInfo[beatCount];
            List<NoteInfo> befs = new List<NoteInfo>();

            // for문으로 순회를 하면서 각 노트에 정보 넣기
            for (int i = 0; i < beatCount; i++)
            {
                var curBeat = notes[line][i];
                if (curBeat == 0) continue;

                ret_line[line][i] = new NoteInfo(curBeat, i, line, befs.Count > 0 ? befs[befs.Count - 1] : lastNoteInfo[line]);
                befs.Add(ret_line[line][i]);
                lastNoteInfo[line] = ret_line[line][i];
            }
        }
        // 현재 Action시스템 비활성화
        ret = new BarInfo(ret_line[0], ret_line[1], new Action[0], index, m_beatPerBar, m_bpm, relativeStartTime);

        /*
        // 예외처리 (Action이 없는 경우)
        if (datas.Length <= m_lineNum || datas[m_lineNum] == "\r" || datas[m_lineNum] == "")
        {
            ret = new BarInfo(ret_line[0], ret_line[1], new Action[0], index, m_beatPerBar, m_bpm, relativeStartTime);
        }
        */
        ///////////////////////////////////////////////
        ///////////////////////////////////////////////
        // action
        ///////////////////////////////////////////////
        /*
        else
        {
            try
            {
                // Action 처리
                string actions_Raw = datas[m_lineNum].TrimEnd('\r');
                List<Action> actions = new List<Action>();
                string[] actionStringArray = actions_Raw.Split('\n');
                foreach (var data in actionStringArray)
                {
                    if (data == String.Empty || data.Length == 0 || data == "") continue;
                    var dic = ConvertJsonToDictionary_oneObject(data);
                    if (dic == null || dic.ContainsKey("comment")) continue;

                    /////////////////////////////////////////////
                    // Parsing
                    /////////////////////////////////////////////

                    // Beat
                    string jsonString = JsonConvert.SerializeObject(dic["Beat"]);
                    Beat[] beat = JsonConvert.DeserializeObject<Beat[]>(jsonString);

                    // Action Name
                    jsonString = JsonConvert.SerializeObject(dic["Action"]);
                    string[] actionName = JsonConvert.DeserializeObject<string[]>(jsonString);

                    // Param
                    jsonString = JsonConvert.SerializeObject(dic["Param"]);
                    Parameter[][] parameters = JsonConvert.DeserializeObject<Parameter[][]>(jsonString);

                    // Set Action
                    var action = new Action(beat[0], actionName[0], parameters[0]);
                    actions.Add(action);
                }

                ret = new BarInfo(ret_line[0], ret_line[1], actions.ToArray(), index, m_beatPerBar, m_bpm, relativeStartTime);
            }
            catch
            {
                ret = new BarInfo(ret_line[0], ret_line[1], new Action[0], index, m_beatPerBar, m_bpm, relativeStartTime);
            }
        }
        */
        return ret;
    }

    ///////////////////////////////////////////////
    ///////////////////////////////////////////////
    // Help Methods
    ///////////////////////////////////////////////
    private static List<string[]> ParseCsv(string csvData)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csvData.Length; i++)
        {
            char c = csvData[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < csvData.Length && csvData[i + 1] == '"')
                {
                    // 큰따옴표 이스케이핑 처리
                    sb.Append('"');
                    i++; // 다음 따옴표 건너뛰기
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',' || c == '\n')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    if (c == '\n')
                    {
                        rows.Add(fields.ToArray());
                        fields.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        // 마지막 필드와 행 처리
        if (sb.Length > 0)
        {
            fields.Add(sb.ToString());
        }
        if (fields.Count > 0)
        {
            rows.Add(fields.ToArray());
        }

        return rows;
    }
    private Dictionary<string, object[]> ConvertJsonToDictionary(string jsonData)
    {
        try
        {
            // JSON 문자열 파싱
            JObject jsonObject = JObject.Parse(jsonData);

            // 결과 Dictionary 생성
            Dictionary<string, object[]> result = new Dictionary<string, object[]>();

            // JObject의 각 프로퍼티를 순회
            foreach (var property in jsonObject.Properties())
            {
                string key = property.Name;
                JToken value = property.Value;

                if (value is JArray array)
                {
                    // 배열인 경우 처리
                    object[] objectArray = new object[array.Count];
                    for (int i = 0; i < array.Count; i++)
                    {
                        if (array[i] is JObject obj)
                        {
                            // JObject를 Dictionary<string, object>로 변환
                            objectArray[i] = obj.ToObject<Dictionary<string, object>>();
                        }
                        else
                        {
                            // 기본 타입의 경우 그대로 사용
                            objectArray[i] = array[i].ToObject<object>();
                        }
                    }
                    result[key] = objectArray;
                }
                else
                {
                    // 배열이 아닌 경우, 단일 요소 배열로 처리
                    result[key] = new object[] { value.ToObject<object>() };
                }
            }
            return result;
        }
        catch (JsonReaderException e)
        {
            Debug.LogError($"JSON 파싱 오류: {e.Message} \n 문제가 되는 json: {jsonData}");
            Debug.LogException(e);
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"오류 발생: {e.Message} \n 문제가 되는 json: {jsonData}");
            Debug.LogException(e);
            return null;
        }
    }
    private Dictionary<string, object> ConvertJsonToDictionary_oneObject(string jsonData)
    {
        try
        {
            // JSON 문자열 파싱
            JObject jsonObject = JObject.Parse(jsonData);

            // 결과 Dictionary 생성
            Dictionary<string, object> result = new Dictionary<string, object>();

            // JObject의 각 프로퍼티를 순회
            foreach (var property in jsonObject.Properties())
            {
                string key = property.Name;
                JToken value = property.Value;

                result[key] = new object[] { value.ToObject<object>() };
            }
            return result;
        }
        catch (JsonReaderException e)
        {
            Debug.LogError($"JSON 파싱 오류: {e.Message} \n 문제가 되는 json: {jsonData}");
            Debug.LogException(e);
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"오류 발생: {e.Message} \n 문제가 되는 json: {jsonData}");
            Debug.LogException(e);
            return null;
        }
    }
    object ConvertValue(string type, object value)
    {
        // Type 문자열에 따라 적절한 변환을 수행
        switch (type.ToLower())
        {
            case "int":
                return Convert.ToInt32(value);
            case "float":
                return Convert.ToSingle(value);
            case "double":
                return Convert.ToDouble(value);
            case "string":
                return Convert.ToString(value);
            case "bool":
                return Convert.ToBoolean(value);
            default:
                throw new NotSupportedException($"Type {type} is not supported.");
        }
    }
    private float CalculateMusicStartTime(float offset, float takeTime)
    {
        if (offset > takeTime) return 0;
        else return takeTime - offset;
    }
}
