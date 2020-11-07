using UnityEngine;

public class ApplicationSettings : MonoBehaviour
{

    [SerializeField]
    private int _targetFrameRate = 60;
    
    [SerializeField]
    private int _sleepTimeout = SleepTimeout.SystemSetting;
    
    private void Awake()
    {
        Screen.sleepTimeout = _sleepTimeout;
        Application.targetFrameRate = _targetFrameRate;
    }
}
