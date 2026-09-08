using Cysharp.Threading.Tasks;
using DG.Tweening;
using NaughtyAttributes;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Timeline Glow Effect Controller
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2026-04-13
public class GlowEffectController : MonoBehaviour
{

    [Header("Glow Effect")]
    [SerializeField] bool m_applyOnAwake = false;

    [ReadOnly][SerializeField] Volume m_volume;
    Bloom m_bloom;

    [ReadOnly][SerializeField] SpriteGlow.SpriteGlowEffect[] m_glowObjectList;

    private Tween m_glowIntensityTween;

    #region Unity Methods
    private void Awake()
    {
        InitializeGlowEffect();
    }
    #endregion

    #region Public Methods
    public void TurnOnGlow()
    {
        for (int i = 0; i < m_glowObjectList.Count(); i++)
        {
            m_glowObjectList[i].AlphaThreshold = 0;
        }
    }
    public void TurnOffGlow()
    {
        for (int i = 0; i < m_glowObjectList.Count(); i++)
        {
            m_glowObjectList[i].AlphaThreshold = 1;
        }
    }

    /// <summary>
    /// Timeline용 함수, Glow Color 변경
    /// </summary>
    /// <param name="param">
    /// 인자 1 = color(HexCode)
    /// 인자 2 = duration
    /// </param>
    public async void ChangeGlowColor(string param)
    {
        // Parse
        string[] strArr = param.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        // Parse - Color
        Color targetColor;
        ColorUtility.TryParseHtmlString(strArr[0], out targetColor);
        // Parse - Time
        float duration = float.Parse(strArr[1]);

        // Set Init Color
        var initColors = m_glowObjectList.Select(e => e.GlowColor).ToArray();

        // Change By Time
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            for (int i = 0; i < m_glowObjectList.Length; i++)
            {
                var obj = m_glowObjectList[i];
                var initColor = initColors[i];
                obj.GlowColor = Color.Lerp(initColor, targetColor, time / duration);
            }
            await UniTask.Yield(); // 한 프레임 대기
        }
    }
    /// <summary>
    /// Timeline용 함수, Glow Intensity 설정
    /// </summary>
    /// <param name="param">
    /// 인자 1 = intensity
    /// 인자 2 = duration
    /// </param>
    public void SetGlowIntensity(string param)
    {
        // Parsing
        string[] strArr = param.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        float[] floatArr = strArr.Select(e => float.Parse(e)).ToArray();

        float intensity = floatArr[0];
        float duration;
        if (floatArr.Length > 1) duration = floatArr[1];
        else duration = 0;

        m_glowIntensityTween?.Kill();
        m_glowIntensityTween = DOTween.To(
            () => m_bloom.intensity.value,      // 현재 값: Getter (람다식)
            x => m_bloom.intensity.value = x,   // 변경할 값: Setter (람다식)
            intensity,                          // 목표 값
            duration                                // 지속 시간(초)
            );
    }
    #endregion

    #region Private Method
    void InitializeGlowEffect()
    {
        m_volume = FindFirstObjectByType<Volume>();
        if (!m_volume.profile.TryGet(out m_bloom)) Debug.LogError("Volume Profile에 Bloom이 설정되어 있지 않습니다.");

        // 초기값 저장
        m_glowObjectList = FindObjectsByType<SpriteGlow.SpriteGlowEffect>(sortMode: FindObjectsSortMode.None);

        // 초기 상태 설정
        foreach (var obj in m_glowObjectList)
        {
            obj.AlphaThreshold = m_applyOnAwake ? 0 : 1;
        }
        if (m_applyOnAwake)
        {
            m_bloom.intensity.value = 0;
        }
    }
    #endregion

    #region Editor Function
#if UNITY_EDITOR
    bool m_isActive = false;
    [Button("Toggle Glow In Editor", EButtonEnableMode.Editor)]
    void ToggleGlowInEditor()
    {
        var glowObjectList = FindObjectsByType<SpriteGlow.SpriteGlowEffect>(sortMode: FindObjectsSortMode.None);
        foreach (var obj in glowObjectList)
        {
            obj.AlphaThreshold = m_isActive ? 0 : 1;
        }
        m_isActive = !m_isActive;
    }
#endif
    #endregion
}
