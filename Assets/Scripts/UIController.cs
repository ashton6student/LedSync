using UnityEngine;
using UnityEngine.InputSystem;

public class UIController : MonoBehaviour
{
    [Header("Canvas To Toggle")]
    public GameObject canvasRoot;

    [Header("Input Action")]
    public InputActionReference toggleCanvasAction;

    bool canvasIsOn = true;

    void OnEnable()
    {
        toggleCanvasAction?.action.Enable();

        if (toggleCanvasAction != null)
            toggleCanvasAction.action.performed += OnTogglePerformed;
    }

    void OnDisable()
    {
        if (toggleCanvasAction != null)
            toggleCanvasAction.action.performed -= OnTogglePerformed;

        toggleCanvasAction?.action.Disable();
    }

    void Start()
    {
        if (!canvasRoot)
        {
            Debug.LogError("[UIController] canvasRoot not assigned.");
            enabled = false;
            return;
        }

        canvasIsOn = canvasRoot.activeSelf;
    }

    void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        canvasIsOn = !canvasIsOn;
        canvasRoot.SetActive(canvasIsOn);
    }
}
