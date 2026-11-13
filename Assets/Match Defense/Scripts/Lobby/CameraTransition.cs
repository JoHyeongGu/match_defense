using UnityEngine;
using DG.Tweening;

public class CameraTransition : MonoBehaviour
{
    [System.Serializable]
    private class CamPosInLevel
    {
        public Transform[] positions;
    }

    [SerializeField] private Camera cam;
    [SerializeField] private CamPosInLevel[] camPositions;
    [SerializeField] private Ease[] curves;

    private Transform camTransform;
    private float duration = 1f;
    private int currentLevel = 0;
    private int curveIndex = 0;

    private void Awake()
    {
        if (cam != null) camTransform = cam.transform;
    }

    public void SetCamPosition(int index)
    {
        if (camPositions == null || currentLevel < 0 || currentLevel >= camPositions.Length) return;

        Transform[] positions = camPositions[currentLevel].positions;
        if (positions == null || index < 0 || index >= positions.Length || positions[index] == null) return;

        if (duration <= 0f)
        {
            camTransform.position = positions[index].position;
            camTransform.rotation = Quaternion.Euler(positions[index].eulerAngles);
        }
        else
        {
            camTransform.DOMove(positions[index].position, duration).SetEase(curves[curveIndex]);
            camTransform.DORotate(positions[index].eulerAngles, duration).SetEase(curves[curveIndex]);
        }
    }

    public void SetDuration(float _duration = 1f)
    {
        duration = _duration;
    }

    public void SetFieldOfView(float fieldOfView)
    {
        if (cam == null) return;

        if (duration <= 0f)
        {
            cam.fieldOfView = fieldOfView;
        }
        else
        {
            cam.DOFieldOfView(fieldOfView, duration).SetEase(curves[curveIndex]);
        }
    }

    public void SetEase(int index)
    {
        if (curves == null || index < 0 || index >= curves.Length) return;
        curveIndex = index;
    }
}
