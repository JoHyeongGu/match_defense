using UnityEngine;

public class ProgramManager : MonoBehaviour
{
    void Start()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
    }
}
