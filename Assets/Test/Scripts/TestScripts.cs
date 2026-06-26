#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestScripts : MonoBehaviour
{
    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.A))
        {
            Debug.Log("A keyUp");

            if (TestScripts2.instance != null)
            {
                Debug.Log("TestScripts2.instance Name is " + TestScripts2.instance.gameObject.name);
            }
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            SceneManager.LoadScene("TeseScene2");
        }
    }
}

#endif