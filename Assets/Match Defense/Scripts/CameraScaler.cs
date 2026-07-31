using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraScaler : MonoBehaviour
{
    [Header("기준 해상도 비율 (가로/세로)")]
    public float targetWidth = 9f;
    public float targetHeight = 16f;

    [Header("기본 카메라 사이즈")]
    public float baseOrthographicSize = 5f;

    private void Awake()
    {
        Camera cam = GetComponent<Camera>();

        // 우리가 목표로 하는 화면 비율 (예: 9 / 16 = 0.5625)
        float targetAspect = targetWidth / targetHeight;

        // 현재 기기의 실제 화면 비율
        float currentAspect = (float)Screen.width / (float)Screen.height;

        // 실제 기기 화면이 목표 비율보다 홀쭉한 경우 (요즘 스마트폰)
        if (currentAspect < targetAspect)
        {
            // 가로 폭이 잘리지 않도록 카메라 사이즈를 키움 (줌 아웃)
            cam.orthographicSize = baseOrthographicSize * (targetAspect / currentAspect);
        }
        else
        {
            // 실제 기기 화면이 더 넓은 경우 (태블릿 등) 기본 사이즈 유지
            cam.orthographicSize = baseOrthographicSize;
        }
    }
}
