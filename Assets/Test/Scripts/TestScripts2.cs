using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScripts2 : MonoBehaviour
{
    public static TestScripts2 instance;
    void Awake()
    {
        instance = this;
        Z_Logger.Log("TestScripts2 " + gameObject.name);
    }

    void Update()
    {
        
    }
}
