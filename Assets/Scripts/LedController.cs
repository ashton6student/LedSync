using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class LedController : MonoBehaviour
{
    [Header("ESP")]
    public string espIp = "192.168.4.1";
    public int espPort = 4210;

    [Header("Delay Tuning (ms)")]
    [Tooltip("Derived as baseOneWayMs + safetyMarginMs.")]
    [Range(10, 500)]
    public int delayMs = 60;

    [Range(0, 200)]
    public int safetyMarginMs = 40;

    [Tooltip("Computed from average RTT/2 when you re-measure.")]
    [SerializeField] private float baseOneWayMs = 20f;

    public bool autoPingOnStart = true;

    [Header("RTT Display / Re-measure")]
    [Range(0f, 10f)]
    public float pingHz = 0f; // 0 = disabled
    public bool showApproxOneWay = true;

    // Public read-only state for UI
    public float LastRttMs => lastRttMs;
    public bool LastPingTimedOut => lastPingTimedOut;
    public int PingCount => pingCount;
    public float BaseOneWayMs => baseOneWayMs;

    float nextPingTime;
    int pingCount;
    float lastRttMs = -1f;
    bool lastPingTimedOut = false;

    UdpClient udp;
    IPEndPoint ep;

    void Awake()
    {
        Application.runInBackground = true;
        ep = new IPEndPoint(IPAddress.Parse(espIp), espPort);
        udp = new UdpClient();
        udp.Client.ReceiveTimeout = 200;
    }

    void Start()
    {
        // Ensure delayMs matches current baseline + margin at start
        RecomputeDelayFromBaseline();

        if (autoPingOnStart)
            MeasureDelay(); // updates baseOneWayMs + delayMs
    }

    void Update()
    {
        // Optional periodic ping for DISPLAY ONLY (does not change delayMs)
        if (pingHz > 0f && Time.time >= nextPingTime)
        {
            nextPingTime = Time.time + (1f / pingHz);
            PingOnceForDisplay();
        }
    }

    // Recompute delayMs whenever safetyMarginMs or baseOneWayMs changes.
    void RecomputeDelayFromBaseline()
    {
        int computed = Mathf.RoundToInt(baseOneWayMs + safetyMarginMs);
        delayMs = Mathf.Clamp(computed, 10, 500);
    }

    // Measures multiple pings, updates baseOneWayMs from avg RTT/2, then recomputes delayMs.
    public void MeasureDelay()
    {
        float totalRtt = 0;
        int success = 0;
        byte[] ping = Encoding.ASCII.GetBytes("PING");

        for (int i = 0; i < 5; i++)
        {
            try
            {
                float t0 = Time.realtimeSinceStartup;
                udp.Send(ping, ping.Length, ep);

                IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
                _ = udp.Receive(ref any);

                float rtt = (Time.realtimeSinceStartup - t0) * 1000f;
                totalRtt += rtt;
                success++;

                lastRttMs = rtt;
                lastPingTimedOut = false;
                Debug.Log($"[LedController] Ping {i + 1}: {rtt:F1}ms");
            }
            catch
            {
                lastPingTimedOut = true;
                Debug.LogWarning($"[LedController] Ping {i + 1}: timeout");
            }
        }

        if (success > 0)
        {
            float avgRtt = totalRtt / success;
            lastRttMs = avgRtt;
            lastPingTimedOut = false;

            baseOneWayMs = avgRtt / 2f;
            RecomputeDelayFromBaseline();

            Debug.Log($"[LedController] Avg RTT={avgRtt:F1}ms -> baseOneWayMs={baseOneWayMs:F1}ms -> delayMs={delayMs}");
        }
        else
        {
            Debug.LogWarning("[LedController] Ping failed; keeping existing delayMs=" + delayMs);
        }
    }

    void PingOnceForDisplay()
    {
        pingCount++;
        byte[] ping = Encoding.ASCII.GetBytes("PING");

        try
        {
            float t0 = Time.realtimeSinceStartup;
            udp.Send(ping, ping.Length, ep);

            IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
            _ = udp.Receive(ref any);

            float rtt = (Time.realtimeSinceStartup - t0) * 1000f;
            lastRttMs = rtt;
            lastPingTimedOut = false;
        }
        catch
        {
            lastPingTimedOut = true;
        }
    }

    public void SendLed(bool on)
    {
        byte[] data = Encoding.ASCII.GetBytes(on ? "ON" : "OFF");
        udp.Send(data, data.Length, ep);
        udp.Send(data, data.Length, ep);
    }

    // Public method UI can call on button press
    public void RemeasureDelay()
    {
        MeasureDelay();
    }

    // Public method UI can call when safetyMargin changes
    public void SetSafetyMarginMs(int newMargin)
    {
        safetyMarginMs = Mathf.Clamp(newMargin, 0, 200);
        RecomputeDelayFromBaseline();
    }

    void OnDestroy()
    {
        try { SendLed(false); } catch { }
        udp?.Close();
    }
}