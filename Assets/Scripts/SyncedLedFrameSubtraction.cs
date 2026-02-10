using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;

public class SyncedLedFrameSubtraction : MonoBehaviour
{
    [Header("ESP")]
    public string espIp = "192.168.4.1";
    public int espPort = 4210;

    [Header("Passthrough")]
    public PassthroughCameraAccess cameraAccess;

    [Header("Display")]
    public RawImage display;

    [Header("Subtract Material")]
    public Material subtractMaterialAsset;

    [Header("Delay Tuning (ms)")]
    [Range(10, 500)]
    public int delayMs = 60;

    public bool autoPingOnStart = true;

    [Range(0, 100)]
    public int safetyMarginMs = 40;

    [Header("Mask Threshold")]
    [Range(0f, 0.5f)]
    public float threshold = 0.05f;

    enum Phase { SendOn, WaitOn, CaptureOn, SendOff, WaitOff, CaptureOff }
    Phase phase;
    float timer;
    float delaySeconds;

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
            Debug.LogError("[SyncedLed] Inspector references missing.");
            enabled = false;
            return;
        }

        subtractMat = new Material(subtractMaterialAsset);
        display.material = null;

        if (autoPingOnStart)
            MeasureDelay();

        delaySeconds = delayMs / 1000f;
        phase = Phase.SendOn;

        Debug.Log($"[SyncedLed] delayMs={delayMs}  delaySeconds={delaySeconds:F4}");
    }

    void Update()
    {
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

    void MeasureDelay()
    {
        float totalRtt = 0;
        int success = 0;
        byte[] ping = Encoding.ASCII.GetBytes("PING");
        byte[] recv;

        for (int i = 0; i < 5; i++)
        {
            try
            {
                float t0 = Time.realtimeSinceStartup;
                udp.Send(ping, ping.Length, ep);

                IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
                recv = udp.Receive(ref any);

                float rtt = (Time.realtimeSinceStartup - t0) * 1000f;
                totalRtt += rtt;
                success++;
                Debug.Log($"[SyncedLed] Ping {i + 1}: {rtt:F1}ms");
            }
            catch
            {
                Debug.LogWarning($"[SyncedLed] Ping {i + 1}: timeout");
            }
        }

        if (success > 0)
        {
            float avgRtt = totalRtt / success;
            delayMs = Mathf.RoundToInt(avgRtt / 2f + safetyMarginMs);
            Debug.Log($"[SyncedLed] Avg RTT={avgRtt:F1}ms -> delayMs={delayMs}");
        }
        else
        {
            Debug.LogWarning("[SyncedLed] Ping failed, using default delayMs=" + delayMs);
        }
    }

    [ContextMenu("Re-measure Ping Delay")]
    public void RemeasureDelay()
    {
        MeasureDelay();
        Debug.Log($"[SyncedLed] Updated delayMs={delayMs}");
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