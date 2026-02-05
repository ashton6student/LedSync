using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class Esp32UdpController : MonoBehaviour
{
    public string espIp = "192.168.4.1";
    public int espPort = 4210;

    // 33333us=33.333ms
    public int halfPeriodUs = 33333;

    // 1 = ON; 0 = OFF;
    public bool startOn = true;

    public bool autoStartOnPlay = true;

    UdpClient udp;
    IPEndPoint ep;

    void Awake()
    {
        Application.runInBackground = true;
        ep = new IPEndPoint(IPAddress.Parse(espIp), espPort);
        udp = new UdpClient();
    }

    void Start()
    {
        if (autoStartOnPlay)
            StartBlink();
    }

    void OnDestroy()
    {
        try { StopBlink(); } catch { }
        udp?.Close();
    }

    void Send(string msg)
    {
        byte[] data = Encoding.ASCII.GetBytes(msg);
        udp.Send(data, data.Length, ep);
    }

    // 抗丢包：连发三次
    public void StartBlink()
    {
        string cmd = $"START {halfPeriodUs} {(startOn ? 1 : 0)}";
        Send(cmd); Send(cmd); Send(cmd);
    }

    public void StopBlink()
    {
        Send("STOP"); Send("STOP");
    }

    public void Ping()
    {
        Send("PING"); Send("PING");
    }

    //STOP->START
    public void Restart()
    {
        StopBlink();
        StartBlink();
    }
}
