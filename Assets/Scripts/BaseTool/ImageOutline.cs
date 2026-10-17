using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ImageOutline : MonoBehaviour
{
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");

    public Color outlineColor = new Color(249,241,229);
    [Range(0, 10)] public float outlineWidth = 2f;

    private Image _image;
    private Material _mat;

    private void Awake()
    {
        _image = GetComponent<Image>();

        // 每个实例独立 material，参数独立
        if (_image.material != null && _image.material.shader.name == "UI/ImageOutline")
            _mat = new Material(_image.material);
        else
            _mat = new Material(Shader.Find("UI/ImageOutline"));

        _image.material = _mat;
        Apply();
    }

    private void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        Apply();
    }

    private void Apply()
    {
        if (_mat == null) return;
        _mat.SetColor(OutlineColorID, outlineColor);
        _mat.SetFloat(OutlineWidthID, outlineWidth);
    }
}
