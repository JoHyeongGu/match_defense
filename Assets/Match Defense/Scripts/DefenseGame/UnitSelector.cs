using UnityEngine;

namespace MatchDefense.DefenseGame
{
    public class UnitSelector : MonoBehaviour
    {
        [Header("<color=yellow>Camera</color>")]
        [SerializeField] private Camera worldCamera;

        [Header("<color=yellow>Layers</color>")]
        [SerializeField] private LayerMask unitLayer;
        [SerializeField] private LayerMask groundLayer;

        private Unit currentSelectedUnit;

        private void Update()
        {
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;

            if (worldCamera == null)
                return;

            Ray ray = worldCamera.ScreenPointToRay(pointer.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit unitHit, 1000f, unitLayer))
            {
                Unit clickedUnit = unitHit.collider.GetComponent<Unit>();
                if (clickedUnit != null)
                {
                    if (currentSelectedUnit != null)
                        currentSelectedUnit.SetVisibleAtkRange(false);
                    currentSelectedUnit = clickedUnit;
                    currentSelectedUnit.SetVisibleAtkRange(true);
                }
            }
            else if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, groundLayer))
            {
                if (currentSelectedUnit != null)
                {
                    currentSelectedUnit.SetVisibleAtkRange(false);
                    currentSelectedUnit = null;
                }
            }
        }
    }
}
