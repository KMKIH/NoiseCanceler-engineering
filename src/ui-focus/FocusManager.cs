using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Focus를 Stack으로 관리
// TopFocus가 설정된 경우 Stack보다 우선해서 입력 포커스를 판별한다
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2026-05-13
public class FocusManager : MonoBehaviour
{
    Stack<IFocusable> m_focusableStack;
    IFocusable m_topFocus = null;

    [Header("Debug Manager")]
    [ResizableTextArea]
    [ReadOnly][SerializeField] string m_stack = "";
    [ResizableTextArea]
    [ReadOnly][SerializeField] string m_topFocusName = "";

    #region Public Methods
    public void SetTopFocus(IFocusable focusable)
    {
        m_topFocus = focusable;
        m_topFocusName = focusable.ToString();
        focusable.OnEnter();
        focusable.OnFocus();
    }

    public void Push(IFocusable focusable)
    {
        if (m_focusableStack == null) m_focusableStack = new Stack<IFocusable>();

        m_focusableStack.Push(focusable);
        m_stack += focusable.ToString() + "\n";
        focusable.OnEnter();
        focusable.OnFocus();
    }

    public void Pop()
    {
        if (m_focusableStack == null && m_topFocus == null) return;

        IFocusable focusable;
        if (m_topFocus != null)
        {
            focusable = m_topFocus;
            m_topFocus = null;
            m_topFocusName = "";
        }
        else
        {
            focusable = m_focusableStack.Pop();
            m_stack = RemoveLastObject(m_stack);
        }

        focusable.OnUnfocus();

        if (m_focusableStack != null && m_focusableStack.Count > 0)
        {
            m_focusableStack.Peek().OnFocus();
        }
    }

    public bool IsFocused(IFocusable focusable)
    {
        if (m_topFocus != null) return focusable == m_topFocus;
        return m_focusableStack != null &&
               m_focusableStack.Count > 0 &&
               m_focusableStack.Peek() == focusable;
    }
    #endregion

    #region Helper Method
    string RemoveLastObject(string original)
    {
        if (string.IsNullOrEmpty(original)) return "";

        var parts = original.Split('\n');
        int count = parts[^1] == "" ? parts.Length - 1 : parts.Length;

        if (count <= 1) return "";

        return string.Join("\n", parts.Take(count - 1)) + "\n";
    }
    #endregion
}
