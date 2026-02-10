using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quits the application when the bound input action is performed.
/// Works with XR controller buttons via Input System.
/// </summary>
public class Quit : MonoBehaviour
{
    [Tooltip("Button action that triggers quitting the app.")]
    public InputActionReference quitAction;

    void OnEnable()
    {
        if (quitAction != null)
        {
            quitAction.action.Enable();
            quitAction.action.performed += OnQuit;
        }
    }

    void OnDisable()
    {
        if (quitAction != null)
        {
            quitAction.action.performed -= OnQuit;
            quitAction.action.Disable();
        }
    }

    private void OnQuit(InputAction.CallbackContext ctx)
    {
#if UNITY_EDITOR
        // Stops play mode in the editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quits the built application (Quest / Android / PC)
        Application.Quit();
#endif
    }
}
