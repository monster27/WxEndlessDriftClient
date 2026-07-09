using UnityEngine;
using System.Net;
using System.Security.Cryptography.X509Certificates;

public class SSLBypass : MonoBehaviour
{
    void Start()
    {
        ServicePointManager.ServerCertificateValidationCallback =
            (object sender, X509Certificate certificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors) =>
            {
                Debug.Log("🔓 SSL验证回调已被触发！");
                return true;
            };
        Debug.Log("✅ SSL绕过已设置");
        DontDestroyOnLoad(gameObject);
    }
}