using UnityEngine;

// Update 순서를 부여
// 최초 작성자: 김기홍
// 수정자: 
// 최종 수정일: 2025-01-02
public class UpdateManager : MonoBehaviour
{
    [SerializeField]
    private Array<Updateable>[] updateables;

    private void Update()
    {
        // Execute updates in order based on phase
        foreach(var ups in updateables)
        {
            foreach(var up in ups)
            {
                up.CustomUpdate();
            }
        }
    }
}
