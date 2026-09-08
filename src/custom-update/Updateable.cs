using UnityEngine;

// Custom Update 시스템에서 실행 순서를 제어할 객체의 추상 클래스
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2025-01-02
public abstract class Updateable : MonoBehaviour
{
    public abstract void CustomUpdate();
}
