using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// NavigationButton을 사용하는 메뉴얼
// 최초 작성자: 김기홍
// 수정자: 
// 최종 수정일: 2026-01-06
public abstract class NavigationButtonController : MonoBehaviour
{
    protected List<INavigationButton> activeMenuBtnArray;
    private List<INavigationButton> m_entireMenuBtnArray;
    private int m_initMenuBtnIndex;

    [Header("Index")]
    [ReadOnly][SerializeField] private int m_curMenuBtnIndex = 0;
    protected UnityEvent<int> OnMenuBtnIndexChanged;
    protected int CurMenuBtnIndex
    {
        private set
        {
            m_curMenuBtnIndex = value;
            OnMenuBtnIndexChanged?.Invoke(m_curMenuBtnIndex);
        }
        get { return m_curMenuBtnIndex; }
    }

    [Header("Option")]
    [SerializeField] bool m_isIndexLoop = false;
    [SerializeField] bool m_isKeyDownMode = true;

    // State
    bool m_isButtonClicked;

    /////////////////////////////////////////////////////
    /////////////////////////////////////////////////////
    // Unity Method
    /////////////////////////////////////////////////////
    protected virtual void Awake()
    {
        m_isButtonClicked = false;

        OnMenuBtnIndexChanged = new UnityEvent<int>();
    }

    /////////////////////////////////////////////////////
    /////////////////////////////////////////////////////
    // Initializer
    /////////////////////////////////////////////////////
    // 버튼의 기능을 호출하는 함수, 버튼의 내장함수인 Initialize 호출
    abstract protected void InitializeButton();

    // 컨트롤러를 초기화하는 함수, 초기 상태
    protected void InitializeController(int initButtonIndex, params INavigationButton[] navigationButton)
    {
        // 예외처리
        if (navigationButton == null || navigationButton.Length == 0) return;

        // 초기 값 저장
        m_initMenuBtnIndex = initButtonIndex;

        // 버튼 List 생성
        // 버튼 저장
        m_entireMenuBtnArray = new List<INavigationButton>();
        foreach (var button in navigationButton)
        {
            if (button == null) continue;
            m_entireMenuBtnArray.Add(button);
        }

        // 버튼 초기상태 설정
        ResetState();
    }
    protected void AddButtonToController(INavigationButton button)
    {
        m_entireMenuBtnArray.Add(button);
    }

    // 초기 상태로 되돌리는 함수 (InitializeController가 호출된 이후에 호출되어야함)
    // Add (25.05.03) : 초기 버튼이 비활성화되어있다면, 다음 버튼을 강조한다
    protected virtual void ResetState()
    {
        m_isPressed = false;

        // 비활성화된 버튼은 리스트에서 제거한다
        activeMenuBtnArray = new List<INavigationButton>();
        foreach (var button in m_entireMenuBtnArray)
        {
            if (button.gameObject.activeSelf == false) continue;
            activeMenuBtnArray.Add(button);
        }

        // 버튼 선택
        CurMenuBtnIndex = m_initMenuBtnIndex;
        activeMenuBtnArray[CurMenuBtnIndex].OnHighlighted();
        for (int i = 0; i < activeMenuBtnArray.Count; i++)
        {
            if (i == CurMenuBtnIndex) continue;
            activeMenuBtnArray[i].OnNormal();
        }
    }

    /////////////////////////////////////////////////////
    /////////////////////////////////////////////////////
    // 버튼 조작
    /////////////////////////////////////////////////////
    bool m_isPressed = false;
    protected async void HandleNavigation()
    {
        if (m_isPressed == false)
        {
            int input = 0;
            if (InputGetButton("Left")) input = -1;
            else if (InputGetButton("Right")) input = 1;
            if (input != 0)
            {
                if (GetNewIndex(out int newIndex, CurMenuBtnIndex, input))
                {
                    // 이전 버튼에 대해서 UnSelected 처리를 한다
                    activeMenuBtnArray[CurMenuBtnIndex].OnNormal();

                    // 새로운 버튼을 갱신
                    CurMenuBtnIndex = newIndex;

                    // 새로운 버튼에 대한 선택 처리
                    SoundManager.Instance.PlayOneShot(AudioName.Move_Button);
                    activeMenuBtnArray[CurMenuBtnIndex].OnHighlighted();

                    // 콜백함수 (Hook)
                    OnIndexChanged(newIndex);
                }
            }
            else if (Input.GetButtonDown("Submit"))
            {
                m_isPressed = true;
                activeMenuBtnArray[CurMenuBtnIndex].OnPressed();

                await UniTask.NextFrame();
            }
        }
        else if (Input.GetButtonUp("Submit") && m_isPressed)
        {
            m_isPressed = false;
            activeMenuBtnArray[CurMenuBtnIndex].OnReleased();

            await UniTask.NextFrame();
        }
    }
    protected virtual void OnIndexChanged(int index) { }
    protected bool IsButtonLocked()
    {
        if (m_isButtonClicked) return true;
        m_isButtonClicked = true;
        return false;
    }

    /////////////////////////////////////////////////////
    /////////////////////////////////////////////////////
    // Helper Method
    /////////////////////////////////////////////////////
    bool GetNewIndex(out int newIndex, int curIndex, int deltaIndex)
    {
        newIndex = curIndex + deltaIndex;
        if (m_isIndexLoop)
        {
            if (newIndex < 0) newIndex = activeMenuBtnArray.Count - 1;
            else if (newIndex >= activeMenuBtnArray.Count) newIndex = 0;
        }
        else if (newIndex < 0 || newIndex >= activeMenuBtnArray.Count) return false;
        return true;
    }
    private bool InputGetButton(string buttonName)
    {
        if (m_isKeyDownMode)
        {
            return Input.GetButtonDown(buttonName);
        }
        else
        {
            return Input.GetButtonUp(buttonName);
        }
    }
}
