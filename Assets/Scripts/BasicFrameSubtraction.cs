using UnityEngine;
using UnityEngine.UI;
using Meta.XR;
public class BasicFrameSubtraction : MonoBehaviour
{
    [Header("Passthrough")]
    public PassthroughCameraAccess cameraAccess;

    [Header("Output to UI")]
    public RawImage display;

    [Header("Material (assign)")]
    [Tooltip("Material whose shader subtracts _PrevTex from _MainTex (current).")]
    public Material subtractMaterialAsset;

    // RTs
    private RenderTexture currentRT;
    private RenderTexture prevRT;
    private RenderTexture outputRT;

    private Material subtractMat;
    private bool initialized = false;

    // shader property id
    private static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");

    void Start()
    {
        if (!cameraAccess)
        {
            Debug.LogError("[BasicFrameSubtraction] cameraAccess not assigned.");
            enabled = false;
            return;
        }
        if (!display)
        {
            Debug.LogError("[BasicFrameSubtraction] display RawImage not assigned.");
            enabled = false;
            return;
        }
        if (!subtractMaterialAsset)
        {
            Debug.LogError("[BasicFrameSubtraction] subtractMaterialAsset not assigned.");
            enabled = false;
            return;
        }

        subtractMat = new Material(subtractMaterialAsset);
        display.material = null; // RawImage displays the texture directly
    }

    void Update()
    {
        if (!cameraAccess.IsPlaying)
            return;

        Texture src = cameraAccess.GetTexture();
        if (src == null)
            return;

        if (!initialized)
        {
            if (src.width < 32 || src.height < 32)
                return;

            InitRTs(src.width, src.height);

            // Seed prevRT with the first frame so output starts near black.
            Graphics.Blit(src, prevRT);

            initialized = true;
        }

        // 1) Freeze the current camera frame into currentRT
        Graphics.Blit(src, currentRT);

        // 2) output = current - prev (shader does this)
        subtractMat.SetTexture(PrevTexId, prevRT);
        Graphics.Blit(currentRT, outputRT, subtractMat);

        // 3) Display result
        display.texture = outputRT;

        // 4) Update prev for next frame
        Graphics.Blit(currentRT, prevRT);
    }

    void InitRTs(int w, int h)
    {
        ReleaseRTs();

        currentRT = MakeRT(w, h);
        prevRT    = MakeRT(w, h);
        outputRT  = MakeRT(w, h);
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
        if (currentRT != null) { currentRT.Release(); Destroy(currentRT); currentRT = null; }
        if (prevRT != null)    { prevRT.Release(); Destroy(prevRT); prevRT = null; }
        if (outputRT != null)  { outputRT.Release(); Destroy(outputRT); outputRT = null; }
        initialized = false;
    }

    void OnDestroy()
    {
        ReleaseRTs();
        if (subtractMat != null) Destroy(subtractMat);
    }
}
