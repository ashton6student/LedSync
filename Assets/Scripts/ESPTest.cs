using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
//using UnityEngine.UI;
using TMPro;
public class ESPTest : MonoBehaviour
{
    public string espIp = "192.168.4.1";
    public int espPort = 4210;

    [Header("display")]
    //public Text resultText; 
    public TextMeshProUGUI resultText;
    UdpClient udp;
    IPEndPoint ep;
    float nextPingTime;
    int count;

    void Start()
    {
        ep = new IPEndPoint(IPAddress.Parse(espIp), espPort);
        udp = new UdpClient();
        udp.Client.ReceiveTimeout = 500; // 500ms超时
    }

    void Update()
    {
        if (Time.time < nextPingTime) return;
        nextPingTime = Time.time + 1f; // 每秒测一次

        count++;
        byte[] data = Encoding.ASCII.GetBytes("PING");

        try
        {
            float t0 = Time.realtimeSinceStartup;
            udp.Send(data, data.Length, ep);

            IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
            byte[] recv = udp.Receive(ref any);

            float rtt = (Time.realtimeSinceStartup - t0) * 1000f;
            string msg = $"#{count}  RTT: {rtt:F1}ms  ≈{rtt / 2f:F1}ms";
            Debug.Log(msg);
            if (resultText) resultText.text = msg;
        }
        catch
        {
            string msg = $"#{count}  超时!";
            Debug.LogWarning(msg);
            if (resultText) resultText.text = msg;
        }
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}