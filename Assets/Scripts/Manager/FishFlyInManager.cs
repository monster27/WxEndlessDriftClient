using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class FishFlyInManager : SingletonMonoFromScene<FishFlyInManager>
{
    [SerializeField] private FishFlyInCtrl _prefab;
    [SerializeField] private int _poolSize = 10;
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private float _arcHeight = 10f;
    [SerializeField] private Transform _container;
    [SerializeField] private Shader _shader;
    [SerializeField] private int _renderQueue = 3000;

    [Header("=== 飞入参数 ===")]
    [SerializeField] private float _flyDuration = 0.8f;
    [SerializeField] private float _defaultScale = 1.0f;
    [SerializeField] private Texture2D _defaultTexture;

    private Queue<FishFlyInCtrl> _pool = new Queue<FishFlyInCtrl>();
    private bool _isInitialized = false;
    private AsyncOperationHandle<Sprite> _spriteHandle;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            FlyTest();
        }
    }

    void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_spriteHandle);
    }

    public void Init(int renderQueue)
    {
        if (_isInitialized) return;

        _renderQueue = renderQueue;

        if (_container == null)
        {
            _container = transform;
        }

        for (int i = 0; i < _poolSize; i++)
        {
            var ctrl = CreateNewFish();
            ctrl.gameObject.SetActive(false);
            _pool.Enqueue(ctrl);
        }

        _isInitialized = true;
    }

    public void FlyTest()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[FishFlyInManager] 未初始化，请先调用 Init()!");
            return;
        }

        Fly(_defaultTexture, 0f, _defaultScale);
    }

    public void Fly(int itemId, float weight = 0f, bool isFish = true)
    {
        AssetManager.LoadFromAddressables<Sprite>(GetIconPath(itemId), (sprite, handle) =>
        {
            _spriteHandle = handle;
            if (sprite != null)
            {
                Texture2D texture = sprite.texture;
                if (texture == null)
                {
                    Debug.LogError($"[FishFlyInManager] 无法获取物品纹理: itemId={itemId}");
                    return;
                }

                float flip = SceneMatManager.Instance != null && SceneMatManager.Instance.CurrentSceneFlip ? 1f : 0f;
                float scale = isFish ? CalculateFishScale(weight) : _defaultScale;

                Fly(texture, flip, scale);
            }
            else
            {
                Debug.LogError($"[FishFlyInManager] 无法加载物品图标: itemId={itemId}");
            }
        });
    }

    private string GetIconPath(int itemId)
    {
        if (LoadDataManager.Instance?.items == null) return "";

        foreach (var item in LoadDataManager.Instance.items)
        {
            if (item.id == itemId && !string.IsNullOrEmpty(item.iconPath))
            {
                return item.iconPath;
            }
        }
        return "";
    }

    private float CalculateFishScale(float weight)
    {
        if (weight <= 0) return _defaultScale;
        float baseScale = 0.8f;
        float maxScale = 1.5f;
        float scale = baseScale + (weight / 50f) * (maxScale - baseScale);
        return Mathf.Clamp(scale, baseScale, maxScale);
    }

    public void Fly(Texture2D texture, float flip, float scale)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[FishFlyInManager] 未初始化，请先调用 Init()!");
            return;
        }

        var ctrl = GetFish();
        if (ctrl == null) return;

        Vector3 start = _startPoint.position;
        Vector3 end = _endPoint.position;

        Vector3 mid = new Vector3(
            (start.x + end.x) / 2f,
            _arcHeight,
            (start.z + end.z) / 2f
        );

        if (texture != null)
        {
            ctrl.SetMainTexture(texture);
        }
        else if (_defaultTexture != null)
        {
            ctrl.SetMainTexture(_defaultTexture);
        }

        ctrl.SetFlip(flip);
        ctrl.SetRenderQueue(_renderQueue);

        ctrl.Fly(start, mid, end, scale, _flyDuration, OnFlyComplete);
    }

    private void OnFlyComplete()
    {
        // 飞行完成回调
    }

    private FishFlyInCtrl CreateNewFish()
    {
        if (_prefab == null)
        {
            Debug.LogError("[FishFlyInManager] _prefab 未赋值!");
            return null;
        }

        var ctrl = Instantiate(_prefab, _container);
        ctrl.gameObject.SetActive(false);

        if (ctrl.go == null)
        {
            Debug.LogError("[FishFlyInManager] 预制体中的 go 未赋值!");
            return ctrl;
        }

        ctrl.Init();

        Material mat = CreateMaterial();
        if (mat != null)
        {
            ctrl.SetMaterial(mat);
            ctrl.SetRenderQueue(_renderQueue);
        }

        return ctrl;
    }

    private Material CreateMaterial()
    {
        if (_shader == null)
        {
            _shader = Shader.Find("Custom/GameInSceneShader");
            if (_shader == null)
            {
                return new Material(Shader.Find("Sprites/Default"));
            }
        }
        return new Material(_shader);
    }

    private FishFlyInCtrl GetFish()
    {
        if (_pool.Count > 0)
        {
            var ctrl = _pool.Dequeue();
            if (ctrl != null) return ctrl;
        }

        return CreateNewFish();
    }

    public void ReturnToPool(FishFlyInCtrl ctrl)
    {
        if (ctrl == null) return;

        ctrl.transform.SetParent(_container);
        ctrl.gameObject.SetActive(false);

        if (ctrl.go != null)
        {
            var renderer = ctrl.go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        _pool.Enqueue(ctrl);
    }
}
