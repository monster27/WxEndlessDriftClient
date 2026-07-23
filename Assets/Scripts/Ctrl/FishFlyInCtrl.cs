using UnityEngine;

public class FishFlyInCtrl : MonoBehaviour
{
    public FishFlyInManager Manager { get; set; }

    private float _timer;
    private float _duration = 0.8f;
    private bool _isFlying;
    private System.Action _onComplete;
    private MeshRenderer _renderer;

    private Vector3 _startPos;
    private Vector3 _midPos;
    private Vector3 _endPos;
    private Vector3 _ctrl1;
    private Vector3 _ctrl2;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null)
        {
            _renderer = gameObject.AddComponent<MeshRenderer>();
        }
        _renderer.enabled = false;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        transform.position = Vector3.zero;
    }

    void Update()
    {
        if (!_isFlying) return;

        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / _duration);

        // 三次贝塞尔曲线：经过中间点
        float u = 1 - t;
        transform.position = u * u * u * _startPos
                           + 3 * u * u * t * _ctrl1
                           + 3 * u * t * t * _ctrl2
                           + t * t * t * _endPos;

        // 角度变化：-30° → -90° → -150°
        float angle;
        if (t <= 0.5f)
        {
            float p = t / 0.5f;
            float easedP = p * p;
            angle = Mathf.Lerp(-30f, -90f, easedP);
        }
        else
        {
            float p = (t - 0.5f) / 0.5f;
            float easedP = 1 - (1 - p) * (1 - p);
            angle = Mathf.Lerp(-90f, -150f, easedP);
        }
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (t >= 1)
        {
            _isFlying = false;
            _renderer.enabled = false;
            gameObject.SetActive(false);
            _onComplete?.Invoke();
            Manager?.ReturnToPool(this);
        }
    }

    public void Fly(Transform start, Transform mid, Transform end, System.Action onComplete = null)
    {
        _startPos = start.position;
        _midPos = mid.position;
        _endPos = end.position;

        // 计算控制点：使曲线经过中间点
        // 控制点1：起点到中间点的1/3处，略微抬高
        _ctrl1 = _startPos + (_midPos - _startPos) * 0.333f + Vector3.up * 1.5f;
        // 控制点2：终点到中间点的1/3处，略微抬高
        _ctrl2 = _endPos + (_midPos - _endPos) * 0.333f + Vector3.up * 1.5f;

        _timer = 0;
        _isFlying = true;
        _onComplete = onComplete;

        float randomScale = Random.Range(0.5f, 1.5f);
        transform.localScale = Vector3.one * randomScale;

        transform.rotation = Quaternion.Euler(0, 0, -30f);
        transform.position = _startPos;

        if (_renderer != null)
        {
            _renderer.enabled = true;
        }
        gameObject.SetActive(true);
    }

    public void Stop()
    {
        _isFlying = false;
        if (_renderer != null) _renderer.enabled = false;
        gameObject.SetActive(false);
        Manager?.ReturnToPool(this);
    }

    public void SetMaterial(Material mat)
    {
        if (_renderer != null && mat != null)
        {
            _renderer.material = mat;
            _renderer.enabled = false;
        }
    }
}
