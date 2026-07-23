using UnityEngine;

public class FishFlyInCtrl : MonoBehaviour
{
    public GameObject go;  // 渲染子物体（固定不变）

    private float _timer;
    private float _duration;
    private bool _isFlying;
    private System.Action _onComplete;
    private MeshRenderer _renderer;
    private Material _material;

    private Vector3 _startPos;
    private Vector3 _midPos;
    private Vector3 _endPos;
    private Vector3 _ctrl1;
    private Vector3 _ctrl2;

    public void Init()
    {
        if (go == null)
        {
            Debug.LogError("FishFlyInCtrl: go 未赋值!");
            return;
        }

        _renderer = go.GetComponent<MeshRenderer>();
        if (_renderer == null)
        {
            _renderer = go.AddComponent<MeshRenderer>();
        }

        if (go.GetComponent<MeshFilter>() == null)
        {
            go.AddComponent<MeshFilter>().mesh = CreateQuadMesh();
        }

        // go 始终保持在本地归零
        //go.transform.localPosition = Vector3.zero;
        //go.transform.localRotation = Quaternion.identity;
        //go.transform.localScale = Vector3.one;

        // ctrl 根物体初始状态
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        transform.position = Vector3.zero;

        gameObject.SetActive(false);
        if (_renderer != null) _renderer.enabled = false;
    }

    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Quad";

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };

        int[] triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Update()
    {
        if (!_isFlying) return;

        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / _duration);

        // ===== ctrl 根物体控制位移 =====
        float u = 1 - t;
        transform.position = u * u * u * _startPos
                           + 3 * u * u * t * _ctrl1
                           + 3 * u * t * t * _ctrl2
                           + t * t * t * _endPos;

        // ===== ctrl 根物体控制旋转 =====
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
            if (_renderer != null) _renderer.enabled = false;
            gameObject.SetActive(false);
            _onComplete?.Invoke();
        }
    }

    public void Fly(Vector3 start, Vector3 mid, Vector3 end, float scale, float duration, System.Action onComplete = null)
    {
        _startPos = start;
        _midPos = mid;
        _endPos = end;
        _duration = duration;

        _ctrl1 = _startPos + (_midPos - _startPos) * 0.333f + Vector3.up * 1.5f;
        _ctrl2 = _endPos + (_midPos - _endPos) * 0.333f + Vector3.up * 1.5f;

        _timer = 0;
        _isFlying = true;
        _onComplete = onComplete;

        // ===== ctrl 根物体控制大小 =====
        transform.localScale = Vector3.one * scale;

        transform.rotation = Quaternion.Euler(0, 0, -30f);
        transform.position = _startPos;

        gameObject.SetActive(true);
        if (_renderer != null)
        {
            _renderer.enabled = true;
        }
    }

    public void SetMaterial(Material mat)
    {
        _material = mat;
        if (_renderer != null && _material != null)
        {
            _renderer.material = _material;
            _renderer.enabled = false;
        }
    }

    public void SetMainTexture(Texture2D tex)
    {
        if (_material != null)
        {
            _material.SetTexture("_MainTex", tex);
        }
    }

    public void SetFlip(float flip)
    {
        if (_material != null)
        {
            _material.SetFloat("_Flip", flip);
        }
    }

    public void SetRenderQueue(int queue)
    {
        if (_material != null)
        {
            _material.renderQueue = queue;
        }
    }

    public void Stop()
    {
        _isFlying = false;
        if (_renderer != null) _renderer.enabled = false;
        gameObject.SetActive(false);
    }
}
