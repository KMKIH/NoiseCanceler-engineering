using UnityEngine;

// 네비게이션 버튼의 공통 상태 전환과 이벤트 연결을 정의하는 추상 클래스
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2025-07-01
public abstract class INavigationButton : MonoBehaviour
{
    // Get Object
    public virtual Transform GetTransform() => transform;

    // Initialize
    public abstract void Initialize(System.Action onPressed, System.Action onReleased);

    // Select / Click
    public abstract void OnNormal();
    public abstract void OnHighlighted();
    public abstract void OnPressed();
    public abstract void OnReleased();
}
