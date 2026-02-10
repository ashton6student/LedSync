using UnityEngine;
using UnityEngine.InputSystem;

public class UIController : MonoBehaviour
{
    [Header("What to Toggle")]
    public GameObject canvasRoot;

    [Header("Target Script To Control")]
    public UpgradedLedSync syncedScript;

    [Header("Input Actions")]
    public InputActionReference toggleCanvasAction;

    [Tooltip("Button action to re-measure RTT (calls syncedScript.RemeasureDelay).")]
    public InputActionReference remeasureRttAction;

    [Tooltip("2D axis action (joystick). We'll use the Y axis to increase/decrease safetyMarginMs.")]
    public InputActionReference joystickAction;

    [Header("Safety Margin Control")]
    [Tooltip("How fast safetyMarginMs changes when stick is held (ms per second).")]
    public float msPerSecond = 60f;

    [Range(0f, 0.5f)]
    public float deadzone = 0.15f;

    public int minSafetyMarginMs = 0;
    public int maxSafetyMarginMs = 200;

    public bool invertY = false;

    private bool _canvasIsOn = true;

    void OnEnable()
    {
        toggleCanvasAction?.action.Enable();
        remeasureRttAction?.action.Enable();
        joystickAction?.action.Enable();

        if (toggleCanvasAction != null)
            toggleCanvasAction.action.performed += OnTogglePerformed;

        if (remeasureRttAction != null)
            remeasureRttAction.action.performed += OnRemeasurePerformed;
    }

    void OnDisable()
    {
        if (toggleCanvasAction != null)
            toggleCanvasAction.action.performed -= OnTogglePerformed;

        if (remeasureRttAction != null)
            remeasureRttAction.action.performed -= OnRemeasurePerformed;

        toggleCanvasAction?.action.Disable();
        remeasureRttAction?.action.Disable();
        joystickAction?.action.Disable();
    }

    void Start()
    {
        if (!syncedScript)
        {
            Debug.LogError("[UIController] syncedScript not assigned.");
            enabled = false;
            return;
        }

        if (canvasRoot) _canvasIsOn = canvasRoot.activeSelf;
    }

    void Update()
    {
        if (joystickAction == null || joystickAction.action == null) return;

        Vector2 stick = joystickAction.action.ReadValue<Vector2>();
        float y = invertY ? -stick.y : stick.y;

        if (Mathf.Abs(y) < deadzone) return;

        float sign = Mathf.Sign(y);
        float mag = (Mathf.Abs(y) - deadzone) / (1f - deadzone);
        float normalized = sign * mag;

        float delta = normalized * msPerSecond * Time.deltaTime;

        int newMargin = Mathf.RoundToInt(syncedScript.safetyMarginMs + delta);
        newMargin = Mathf.Clamp(newMargin, minSafetyMarginMs, maxSafetyMarginMs);

        // Use the setter so delayMs + UI update stay consistent
        syncedScript.SetSafetyMarginMs(newMargin);
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (!canvasRoot) return;
        _canvasIsOn = !_canvasIsOn;
        canvasRoot.SetActive(_canvasIsOn);
    }

    private void OnRemeasurePerformed(InputAction.CallbackContext ctx)
    {
        if (!syncedScript) return;
        syncedScript.RemeasureDelay();
    }
}
