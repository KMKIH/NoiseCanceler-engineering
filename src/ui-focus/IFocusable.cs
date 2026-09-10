// 포커스 가능한 UI의 진입·포커스·해제 동작을 정의하는 인터페이스
// 최초 작성자: 김기홍
// 수정자:
// 최종 수정일: 2025-07-28
public interface IFocusable
{
    void OnEnter();
    void OnFocus();
    void OnUnfocus();
}
