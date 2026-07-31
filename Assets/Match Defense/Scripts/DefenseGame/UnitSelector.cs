using UnityEngine;

public class UnitSelector : MonoBehaviour
{
    public Camera worldCamera;

    [Header("Selectable Layers")]
    public LayerMask unitLayer; // 클릭 가능한 유닛 레이어
    public LayerMask groundLayer; // 클릭 시 포커스 해제할 땅 레이어

    private Unit currentSelectedUnit;

    private void Update()
    {
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
            return;

        if (worldCamera == null)
            return;

        Ray ray = worldCamera.ScreenPointToRay(pointer.position.ReadValue());

        // 1. 3D 공간에서 유닛을 클릭했는지 검사
        if (Physics.Raycast(ray, out RaycastHit unitHit, 1000f, unitLayer))
        {
            Unit clickedUnit = unitHit.collider.GetComponent<Unit>();
            if (clickedUnit != null)
            {
                // 기존에 선택된 유닛이 있으면 사거리 표시 끄기
                if (currentSelectedUnit != null)
                    currentSelectedUnit.SetRangeVisible(false);

                // 새로운 유닛 선택 및 사거리 표시 켜기
                currentSelectedUnit = clickedUnit;
                currentSelectedUnit.SetRangeVisible(true);
            }
        }
        // 2. 유닛이 아닌 땅(Ground)을 클릭했다면 포커스 해제
        else if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, groundLayer))
        {
            if (currentSelectedUnit != null)
            {
                currentSelectedUnit.SetRangeVisible(false);
                currentSelectedUnit = null;
            }
        }
    }
}
