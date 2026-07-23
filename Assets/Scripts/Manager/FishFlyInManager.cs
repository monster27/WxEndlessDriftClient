using System.Collections.Generic;
using UnityEngine;

public class FishFlyInManager : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;
    [SerializeField] private int _poolSize = 10;
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _midPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private Material _fishMaterial;

    private Queue<FishFlyInCtrl> _pool = new Queue<FishFlyInCtrl>();

    void Awake()
    {
        for (int i = 0; i < _poolSize; i++)
        {
            var obj = Instantiate(_prefab, transform);
            obj.SetActive(false);
            var ctrl = obj.GetComponent<FishFlyInCtrl>();
            if (ctrl == null)
            {
                ctrl = obj.AddComponent<FishFlyInCtrl>();
            }
            ctrl.Manager = this;
            ctrl.SetMaterial(_fishMaterial);
            _pool.Enqueue(ctrl);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Fly();
        }
    }

    public void Fly()
    {
        var ctrl = GetFish();
        ctrl.Fly(_startPoint, _midPoint, _endPoint);
    }

    private FishFlyInCtrl GetFish()
    {
        if (_pool.Count > 0) return _pool.Dequeue();

        var obj = Instantiate(_prefab, transform);
        obj.SetActive(false);
        var ctrl = obj.GetComponent<FishFlyInCtrl>();
        if (ctrl == null)
        {
            ctrl = obj.AddComponent<FishFlyInCtrl>();
        }
        ctrl.Manager = this;
        ctrl.SetMaterial(_fishMaterial);
        return ctrl;
    }

    public void ReturnToPool(FishFlyInCtrl ctrl)
    {
        ctrl.transform.SetParent(transform);
        if (ctrl.gameObject.activeSelf)
        {
            ctrl.gameObject.SetActive(false);
        }
        _pool.Enqueue(ctrl);
    }
}
