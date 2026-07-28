using UnityEngine;

public class GameSceneManager : SingletonMonoFromScene<GameSceneManager>
{
    public GameObject indoorSceneObj;
    public GameObject outdoorSceneObj;

    private float outdoorCameraX = 0f;
    private bool isIndoor = true;

    private void Awake()
    {
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(false);
        }
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(true);
        }
        isIndoor = false;
        Debug.Log("[GameSceneManager] 初始化完成，默认打开室外场景");
    }

    private void OnEnable()
    {
        CommunicateEvent.Register("UI_ToggleScene", OnToggleScene);
    }

    private void OnDisable()
    {
        CommunicateEvent.Unregister("UI_ToggleScene", OnToggleScene);
    }

    private void OnToggleScene()
    {
        Debug.Log("[GameSceneManager] 收到切换场景事件");
        ToggleScene();
    }

    public void SwitchToIndoor()
    {
        if (isIndoor) return;

        if (CameraManager.Instance != null)
        {
            outdoorCameraX = CameraManager.Instance.GetCurrentX();
            Debug.Log($"[GameSceneManager] 保存室外摄像头X位置: {outdoorCameraX}");
            CameraManager.Instance.MoveToCenter();
        }

        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(false);
        }
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(true);
        }

        isIndoor = true;
        Debug.Log("[GameSceneManager] 切换到室内场景");
    }

    public void SwitchToOutdoor()
    {
        if (!isIndoor) return;

        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(false);
        }
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(true);
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.MoveToX(outdoorCameraX);
            Debug.Log($"[GameSceneManager] 恢复室外摄像头X位置: {outdoorCameraX}");
        }

        isIndoor = false;
        Debug.Log("[GameSceneManager] 切换到室外场景");
    }

    public void ToggleScene()
    {
        if (isIndoor)
        {
            SwitchToOutdoor();
        }
        else
        {
            SwitchToIndoor();
        }
    }

    public bool IsIndoor()
    {
        return isIndoor;
    }

    public bool IsOutdoor()
    {
        return !isIndoor;
    }
}
