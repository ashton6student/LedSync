using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Meta.XR;

public class TimeSubtraction : MonoBehaviour
{
    [Header("Passthrough")]
    public PassthroughCameraAccess cameraAccess;

    [Header("Output")]
    public RawImage display;

    [Header("Materials (assets)")]
    public Material materialA;
    public Material materialB;

    [Header("Input Actions")]
    public InputActionReference toggleButton;
    public InputActionReference joystick; // Y: phaseComp, X: threshold

    [Header("LED Controller")]
    public LedController led;

    [Header("Phase Alignment (ms)")]
    [Range(0f, 200f)]
    public float phaseCompensationMs = 40f;

    [Range(1f, 500f)]
    public float phaseAdjustRate = 80f;

    [Header("Threshold")]
    [Range(0f, 0.3f)]
    public float subtractionThreshold = 0.05f;

    [Range(0.01f, 1f)]
    public float thresholdAdjustRate = 0.08f;

    [Range(0f, 0.5f)]
    public float deadzone = 0.2f;

    enum DisplayMode { Raw = 0, MatA = 1, MatB = 2 }
    DisplayMode mode = DisplayMode.Raw;
    public int CurrentModeIndex => (int)mode;

    // Runtime instances (avoid mutating shared asset materials)
    Material matAInst;
    Material matBInst;

    RenderTexture onRT;
    RenderTexture offRT;
    RenderTexture outRT;

    bool initialized;
    bool haveOn;
    bool haveOff;

    int lastW = -1;
    int lastH = -1;

    float lastSeenToggleTime = -999f;
    bool lastSeenLedState = false;
    bool capturedForThisEdge = false;

    static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");

    void Awake()
    {
        Application.runInBackground = true;

        if (materialA) matAInst = new Material(materialA);
        if (materialB) matBInst = new Material(materialB);
    }

    void OnEnable()
    {
        toggleButton?.action.Enable();
        joystick?.action.Enable();
    }

    void OnDisable()
    {
        toggleButton?.action.Disable();
        joystick?.action.Disable();
    }

    void Update()
    {
        if (!cameraAccess || !display) return;
        if (!cameraAccess.IsPlaying) return;

        Texture src = cameraAccess.GetTexture();
        if (src == null) return;

        // Handle passthrough resolution changes safely
        if (!initialized || src.width != lastW || src.height != lastH)
        {
            if (src.width < 32 || src.height < 32) return;

            lastW = src.width;
            lastH = src.height;

            InitRTs(lastW, lastH);
            initialized = true;

            // Reset capture state on resize
            haveOn = false;
            haveOff = false;
            capturedForThisEdge = false;
        }

        HandleInputs();
        CaptureIfDue(src);
        Render(src);
    }

    void HandleInputs()
    {
        if (toggleButton != null && toggleButton.action.WasPressedThisFrame())
            mode = (DisplayMode)(((int)mode + 1) % 3);

        if (joystick == null) return;

        Vector2 stick = joystick.action.ReadValue<Vector2>();

        if (Mathf.Abs(stick.y) >= deadzone)
        {
            phaseCompensationMs += stick.y * phaseAdjustRate * Time.deltaTime;
            phaseCompensationMs = Mathf.Clamp(phaseCompensationMs, 0f, 200f);
        }

        if (Mathf.Abs(stick.x) >= deadzone)
        {
            subtractionThreshold += stick.x * thresholdAdjustRate * Time.deltaTime;
            subtractionThreshold = Mathf.Clamp(subtractionThreshold, 0f, 0.3f);
        }
    }

    void CaptureIfDue(Texture src)
    {
        if (led == null) return;
        if (!led.IsBlinking) return;

        // Detect a new edge (new ON/OFF command sent)
        if (Mathf.Abs(led.LastToggleTime - lastSeenToggleTime) > 0.0001f || led.LedState != lastSeenLedState)
        {
            lastSeenToggleTime = led.LastToggleTime;
            lastSeenLedState = led.LedState;
            capturedForThisEdge = false;
        }

        if (capturedForThisEdge) return;

        float oneWayMs = (!led.LastPingTimedOut && led.LastRttMs >= 0f) ? (led.LastRttMs * 0.5f) : 0f;
        float captureAt = lastSeenToggleTime + (oneWayMs + phaseCompensationMs) / 1000f;

        if (Time.time < captureAt) return;

        // Capture the frame corresponding to the LED state *after the edge*
        if (lastSeenLedState)
        {
            if (onRT != null) Graphics.Blit(src, onRT);
            haveOn = true;
        }
        else
        {
            if (offRT != null) Graphics.Blit(src, offRT);
            haveOff = true;
        }

        capturedForThisEdge = true;
    }

    void Render(Texture src)
    {
        // Always let Raw mode show the live passthrough immediately
        if (mode == DisplayMode.Raw)
        {
            display.texture = src;
            return;
        }

        // Only process if we actually have a valid pair and RTs exist
        bool havePair = haveOn && haveOff && onRT != null && offRT != null && outRT != null;

        if (!havePair)
        {
            // Avoid running shaders with missing textures; prevents "stuck last frame" behavior
            display.texture = src;
            return;
        }

        switch (mode)
        {
            case DisplayMode.MatA:
                if (!matAInst) { display.texture = src; return; }
                RenderProcessed(matAInst);
                break;

            case DisplayMode.MatB:
                if (!matBInst) { display.texture = src; return; }
                RenderProcessed(matBInst);
                break;
        }
    }

    void RenderProcessed(Material matInst)
    {
        // Shaders expect:
        // _MainTex = ON frame (input to Blit)
        // _PrevTex = OFF frame
        matInst.SetTexture(PrevTexId, offRT);
        matInst.SetFloat(ThresholdId, subtractionThreshold);

        Graphics.Blit(onRT, outRT, matInst);
        display.texture = outRT;
    }

    void InitRTs(int w, int h)
    {
        ReleaseRTs();
        onRT = MakeRT(w, h);
        offRT = MakeRT(w, h);
        outRT = MakeRT(w, h);
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
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }

        Release(ref onRT);
        Release(ref offRT);
        Release(ref outRT);

        initialized = false;
    }

    void OnDestroy()
    {
        ReleaseRTs();

        if (matAInst != null) Destroy(matAInst);
        if (matBInst != null) Destroy(matBInst);
    }
}
