using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;
public class AckLedFrameSubtraction : MonoBehaviour
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

    [Header("相机曝光余量(ms)")]
    [Tooltip("收到ACK后再等这么久，让相机完成曝光")]
    [Range(0, 100)]
    public int exposureMarginMs = 30;

    // 内部
    enum Phase { SendOn, WaitAckOn, WaitExposureOn, CaptureOn,
                 SendOff, WaitAckOff, WaitExposureOff, CaptureOff }
    Phase phase;
    float timer;

    UdpClient udp;
    IPEndPoint ep;
    bool ackReceived;

    RenderTexture onRT, offRT, outputRT;
    Material subtractMat;
    bool initialized;

    static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");

    void Awake()
    {
        ep = new IPEndPoint(IPAddress.Parse(espIp), espPort);
        udp = new UdpClient();
        udp.Client.Blocking = false; // 非阻塞，不卡主线程
    }

    void Start()
    {
        if (!cameraAccess || !display || !subtractMaterialAsset)
        {
            Debug.LogError("[AckLed] Inspector references missing.");
            enabled = false;
            return;
        }
        subtractMat = new Material(subtractMaterialAsset);
        display.material = null;
        phase = Phase.SendOn;
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

        // 每帧检查有没有收到ACK
        CheckAck();

        switch (phase)
        {
            case Phase.SendOn:
                SendLed(true);
                ackReceived = false;
                phase = Phase.WaitAckOn;
                break;

            case Phase.WaitAckOn:
                if (ackReceived)        // ESP确认灯已经亮了
                {
                    timer = 0f;
                    phase = Phase.WaitExposureOn;
                }
                break;

            case Phase.WaitExposureOn:   // 再等一小段让相机曝光
                timer += Time.deltaTime;
                if (timer >= exposureMarginMs / 1000f)
                    phase = Phase.CaptureOn;
                break;

            case Phase.CaptureOn:
                Graphics.Blit(src, onRT);
                phase = Phase.SendOff;
                break;

            case Phase.SendOff:
                SendLed(false);
                ackReceived = false;
                phase = Phase.WaitAckOff;
                break;

            case Phase.WaitAckOff:
                if (ackReceived)        // ESP确认灯已经灭了
                {
                    timer = 0f;
                    phase = Phase.WaitExposureOff;
                }
                break;

            case Phase.WaitExposureOff:
                timer += Time.deltaTime;
                if (timer >= exposureMarginMs / 1000f)
                    phase = Phase.CaptureOff;
                break;

            case Phase.CaptureOff:
                Graphics.Blit(src, offRT);
                subtractMat.SetTexture(PrevTexId, offRT);
                Graphics.Blit(onRT, outputRT, subtractMat);
                display.texture = outputRT;
                phase = Phase.SendOn;
                break;
        }
    }

    void CheckAck()
    {
        try
        {
            IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udp.Receive(ref any);
            string msg = Encoding.ASCII.GetString(data);
            if (msg == "ACK") ackReceived = true;
        }
        catch { } // 没数据就跳过（非阻塞模式）
    }

    void SendLed(bool on)
    {
        byte[] data = Encoding.ASCII.GetBytes(on ? "ON" : "OFF");
        udp.Send(data, data.Length, ep);
        udp.Send(data, data.Length, ep); // 冗余发送
    }

    void OnDestroy()
    {
        try { SendLed(false); } catch { }
        udp?.Close();
        ReleaseRTs();
        if (subtractMat) Destroy(subtractMat);
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
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        rt.Create();
        return rt;
    }

    void ReleaseRTs()
    {
        void Release(ref RenderTexture rt)
        { if (rt != null) { rt.Release(); Destroy(rt); rt = null; } }
        Release(ref onRT);
        Release(ref offRT);
        Release(ref outputRT);
        initialized = false;
    }
}