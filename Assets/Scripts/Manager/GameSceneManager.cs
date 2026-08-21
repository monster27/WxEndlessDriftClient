using UnityEngine;

public class GameSceneManager : SingletonMonoFromScene<GameSceneManager>
{
    [Header("场景对象")]
    public GameObject indoorSceneObj;
    public GameObject outdoorSceneObj;

    private bool isIndoor = true;

    protected override void Awake()
    {
        base.Awake();
        // 默认显示室外场景
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(false);
        }
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(true);
        }

        // 初始化摄像头管理器，默认使用室外模式（可移动）
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToOutdoor();
        }

        isIndoor = false;
        Z_Logger.Log("[GameSceneManager] 初始化完成，默认打开室外场景");
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
        Z_Logger.Log("[GameSceneManager] 收到切换场景事件");
        ToggleScene();
    }

    public void SwitchToIndoor()
    {
        if (isIndoor) return;

        // 切换场景对象
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(false);
        }
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(true);
        }

        // 切换到室内模式（X=0，不可移动）
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToIndoor();
        }

        isIndoor = true;
        Z_Logger.Log("[GameSceneManager] 切换到室内场景（摄像头不可移动，X=0）");
    }

    public void SwitchToOutdoor()
    {
        if (!isIndoor) return;

        // 切换场景对象
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(false);
        }
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(true);
        }

        // 切换到室外模式（恢复到保存的位置，可移动）
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToOutdoor();
        }

        isIndoor = false;
        Z_Logger.Log("[GameSceneManager] 切换到室外场景（摄像头可移动）");
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
