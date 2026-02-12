using UnityEngine;
using UnityEngine.UI;
using Meta.XR;
using TMPro;

public class TimeSubtraction : MonoBehaviour
{
    [Header("LED Controller Reference")]
    public LedController led;

    [Header("Passthrough")]
    public PassthroughCameraAccess cameraAccess;

    [Header("Display (Result Texture)")]
    public RawImage display;

    [Header("UI Text (Stats)")]
    public TextMeshProUGUI statsText;

    [Header("Subtract Material")]
    public Material subtractMaterialAsset;

    [Header("Mask Threshold")]
    [Range(0f, 0.5f)]
    public float threshold = 0.05f;

    enum Phase { SendOn, WaitOn, CaptureOn, SendOff, WaitOff, CaptureOff }
    Phase phase;
    float timer;
    float delaySeconds;

    RenderTexture onRT;
    RenderTexture offRT;
    RenderTexture outputRT;
    Material subtractMat;
    bool initialized;

    static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");

    void Start()
    {
        if (!led || !cameraAccess || !display || !subtractMaterialAsset)
        {
            Debug.LogError("[TimeSubtraction] Inspector references missing (led, cameraAccess, display, subtractMaterialAsset).");
            enabled = false;
            return;
        }

        subtractMat = new Material(subtractMaterialAsset);
        display.material = null;

        delaySeconds = led.delayMs / 1000f;
        phase = Phase.SendOn;

        UpdateStatsText();
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

        delaySeconds = led.delayMs / 1000f;

        switch (phase)
        {
            case Phase.SendOn:
                led.SendLed(true);
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
                led.SendLed(false);
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
                UpdateStatsText();
                break;
        }
    }

    void UpdateStatsText()
    {
        if (!statsText || !led) return;

        int pingCount = led.PingCount;
        float lastRttMs = led.LastRttMs;
        bool timedOut = led.LastPingTimedOut;

        string rttLine;
        if (timedOut)
            rttLine = $"#{pingCount}  RTT: timeout";
        else if (lastRttMs >= 0f)
            rttLine = led.showApproxOneWay
                ? $"#{pingCount}  RTT: {lastRttMs:F1} ms  ≈ {lastRttMs / 2f:F1} ms"
                : $"#{pingCount}  RTT: {lastRttMs:F1} ms";
        else
            rttLine = $"#{pingCount}  RTT: (not measured)";

        string baseLine = $"baseOneWayMs: {led.BaseOneWayMs:F1} ms";
        string timingLine = $"delayMs: {led.delayMs} ms   safetyMarginMs: {led.safetyMarginMs} ms";

        statsText.text = $"{rttLine}\n{baseLine}\n{timingLine}";
    }

    // Public method UI can call on button press (delegates to LedController)
    public void RemeasureDelay()
    {
        led.RemeasureDelay();
        UpdateStatsText();
    }

    // Public method UI can call when safetyMargin changes (delegates to LedController)
    public void SetSafetyMarginMs(int newMargin)
    {
        led.SetSafetyMarginMs(newMargin);
        UpdateStatsText();
    }

    void OnDestroy()
    {
        ReleaseRTs();
        if (subtractMat != null) Destroy(subtractMat);
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