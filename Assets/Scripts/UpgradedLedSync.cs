using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;
using TMPro;

public class UpgradedLedSync : MonoBehaviour
{
    [Header("ESP")]
    public string espIp = "192.168.4.1";
    public int espPort = 4210;

    [Header("Passthrough")]
    public PassthroughCameraAccess cameraAccess;

    [Header("Display (Result Texture)")]
    public RawImage display;

    [Header("UI Text (Stats)")]
    public TextMeshProUGUI statsText;

    [Header("Subtract Material")]
    public Material subtractMaterialAsset;

    [Header("Delay Tuning (ms)")]
    [Tooltip("Derived as baseOneWayMs + safetyMarginMs.")]
    [Range(10, 500)]
    public int delayMs = 60;

    [Range(0, 200)]
    public int safetyMarginMs = 40;

    [Tooltip("Computed from average RTT/2 when you re-measure.")]
    [SerializeField] private float baseOneWayMs = 20f;

    public bool autoPingOnStart = true;

    [Header("Mask Threshold")]
    [Range(0f, 0.5f)]
    public float threshold = 0.05f;

    [Header("RTT Display / Re-measure")]
    [Range(0f, 10f)]
    public float pingHz = 0f; // 0 = disabled
    public bool showApproxOneWay = true;

    enum Phase { SendOn, WaitOn, CaptureOn, SendOff, WaitOff, CaptureOff }
    Phase phase;
    float timer;
    float delaySeconds;

    float nextPingTime;
    int pingCount;
    float lastRttMs = -1f;
    bool lastPingTimedOut = false;

    UdpClient udp;
    IPEndPoint ep;

    RenderTexture onRT;
    RenderTexture offRT;
    RenderTexture outputRT;
    Material subtractMat;
    bool initialized;

    static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");

    void Awake()
    {
        Application.runInBackground = true;
        ep = new IPEndPoint(IPAddress.Parse(espIp), espPort);
        udp = new UdpClient();
        udp.Client.ReceiveTimeout = 200;
    }

    void Start()
    {
        if (!cameraAccess || !display || !subtractMaterialAsset)
        {
            Debug.LogError("[UpgradedLedSync] Inspector references missing (cameraAccess, display, subtractMaterialAsset).");
            enabled = false;
            return;
        }

        subtractMat = new Material(subtractMaterialAsset);
        display.material = null;

        // Ensure delayMs matches current baseline + margin at start
        RecomputeDelayFromBaseline();

        if (autoPingOnStart)
            MeasureDelay(); // updates baseOneWayMs + delayMs

        delaySeconds = delayMs / 1000f;
        phase = Phase.SendOn;

        UpdateStatsText();
    }

    void Update()
    {
        // Optional periodic ping for DISPLAY ONLY (does not change delayMs)
        if (pingHz > 0f && Time.time >= nextPingTime)
        {
            nextPingTime = Time.time + (1f / pingHz);
            PingOnceForDisplay();
        }

        if (!cameraAccess.IsPlaying) return;

        Texture src = cameraAccess.GetTexture();
        if (src == null) return;

        if (!initialized)
        {
            if (src.width < 32 || src.height < 32) return;
            InitRTs(src.width, src.height);
            initialized = true;
        }

        delaySeconds = delayMs / 1000f;

        switch (phase)
        {
            case Phase.SendOn:
                SendLed(true);
                timer = 0f;
                phase = Phase.WaitOn;
                break;

            case Phase.WaitOn:
                timer += Time.deltaTime;
                if (timer >= delaySeconds)
                    phase = Phase.CaptureOn;
                break;

            case Phase.CaptureOn:
                Graphics.Blit(src, onRT);
                phase = Phase.SendOff;
                break;

            case Phase.SendOff:
                SendLed(false);
                timer = 0f;
                phase = Phase.WaitOff;
                break;

            case Phase.WaitOff:
                timer += Time.deltaTime;
                if (timer >= delaySeconds)
                    phase = Phase.CaptureOff;
                break;

            case Phase.CaptureOff:
                Graphics.Blit(src, offRT);
                subtractMat.SetTexture(PrevTexId, offRT);
                subtractMat.SetFloat(ThresholdId, threshold);
                Graphics.Blit(onRT, outputRT, subtractMat);
                display.texture = outputRT;
                phase = Phase.SendOn;
                break;
        }
    }

    // Recompute delayMs whenever safetyMarginMs or baseOneWayMs changes.
    void RecomputeDelayFromBaseline()
    {
        int computed = Mathf.RoundToInt(baseOneWayMs + safetyMarginMs);
        delayMs = Mathf.Clamp(computed, 10, 500);
    }

    // Measures multiple pings, updates baseOneWayMs from avg RTT/2, then recomputes delayMs.
    void MeasureDelay()
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
                Debug.Log($"[UpgradedLedSync] Ping {i + 1}: {rtt:F1}ms");
            }
            catch
            {
                lastPingTimedOut = true;
                Debug.LogWarning($"[UpgradedLedSync] Ping {i + 1}: timeout");
            }
        }

        if (success > 0)
        {
            float avgRtt = totalRtt / success;
            lastRttMs = avgRtt;
            lastPingTimedOut = false;

            baseOneWayMs = avgRtt / 2f;
            RecomputeDelayFromBaseline();

            Debug.Log($"[UpgradedLedSync] Avg RTT={avgRtt:F1}ms -> baseOneWayMs={baseOneWayMs:F1}ms -> delayMs={delayMs}");
        }
        else
        {
            Debug.LogWarning("[UpgradedLedSync] Ping failed; keeping existing delayMs=" + delayMs);
        }

        UpdateStatsText();
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

            UpdateStatsText();
        }
        catch
        {
            lastPingTimedOut = true;
            UpdateStatsText();
        }
    }

    void UpdateStatsText()
    {
        if (!statsText) return;

        string rttLine;
        if (lastPingTimedOut)
            rttLine = $"#{pingCount}  RTT: timeout";
        else if (lastRttMs >= 0f)
            rttLine = showApproxOneWay
                ? $"#{pingCount}  RTT: {lastRttMs:F1} ms  ≈ {lastRttMs / 2f:F1} ms"
                : $"#{pingCount}  RTT: {lastRttMs:F1} ms";
        else
            rttLine = $"#{pingCount}  RTT: (not measured)";

        string baseLine = $"baseOneWayMs: {baseOneWayMs:F1} ms";
        string timingLine = $"delayMs: {delayMs} ms   safetyMarginMs: {safetyMarginMs} ms";
        // string threshLine = $"threshold: {threshold:F3}";

        // statsText.text = $"{rttLine}\n{baseLine}\n{timingLine}\n{threshLine}";
        statsText.text = $"{rttLine}\n{baseLine}\n{timingLine}";
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
        UpdateStatsText();
    }

    void OnDestroy()
    {
        try { SendLed(false); } catch { }
        udp?.Close();
        ReleaseRTs();
        if (subtractMat != null) Destroy(subtractMat);
    }

    void SendLed(bool on)
    {
        byte[] data = Encoding.ASCII.GetBytes(on ? "ON" : "OFF");
        udp.Send(data, data.Length, ep);
        udp.Send(data, data.Length, ep);
    }

    void InitRTs(int w, int h)
    {
        ReleaseRTs();
        onRT = MakeRT(w, h);
        offRT = MakeRT(w, h);
        outputRT = MakeRT(w, h);
    }

    RenderTexture MakeRT(int w, int h)
    {
        var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        rt.Create();
        return rt;
    }

    void ReleaseRTs()
    {
        void Release(ref RenderTexture rt)
        {
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }
        Release(ref onRT);
        Release(ref offRT);
        Release(ref outputRT);
        initialized = false;
    }
}
