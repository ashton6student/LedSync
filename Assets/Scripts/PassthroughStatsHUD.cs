using UnityEngine;
using TMPro;

/// <summary>
/// Simple HUD readout for timing + display selector state.
/// Shows: frequency (Hz), RTT (ms), phase/capture delay (ms), threshold, and active mode.
/// </summary>
public class PassthroughStatsHUD : MonoBehaviour
{
    [Header("References")]
    public LedController udpTiming;        // provides frequencyHz, LastRttMs, LastPingTimedOut
    public TimeSubtraction displaySelector; // provides phaseCompensationMs, subtractionThreshold, CurrentModeIndex

    [Header("UI")]
    public TextMeshProUGUI text;

    [Header("Update Rate")]
    [Tooltip("How often to refresh the text (seconds). 0 = every frame.")]
    public float refreshInterval = 0.1f;

    float nextUpdateTime;

    void Update()
    {
        if (!text) return;

        if (refreshInterval > 0f && Time.unscaledTime < nextUpdateTime)
            return;

        nextUpdateTime = Time.unscaledTime + refreshInterval;

        // Frequency
        string freqLine = "Freq: (no source)";
        if (udpTiming != null)
            freqLine = $"Freq: {udpTiming.frequencyHz} Hz";

        // RTT
        string rttLine = "RTT: (no source)";
        if (udpTiming != null)
        {
            if (udpTiming.LastPingTimedOut)
                rttLine = "RTT: timeout";
            else if (udpTiming.LastRttMs >= 0f)
                rttLine = $"RTT: {udpTiming.LastRttMs:F1} ms";
            else
                rttLine = "RTT: (not measured)";
        }

        // Selector values
        string compLine = "Capture delay: (no selector)";
        string thrLine = "Threshold: (no selector)";
        string modeLine = "Mode: (no selector)";

        if (displaySelector != null)
        {
            compLine = $"Capture delay: {displaySelector.phaseCompensationMs:F1} ms";
            thrLine = $"Threshold: {displaySelector.subtractionThreshold:F3}";

            int modeIdx = displaySelector.CurrentModeIndex;
            string modeName = modeIdx switch
            {
                0 => "Raw",
                1 => "MatA",
                2 => "MatB",
                _ => $"Unknown({modeIdx})"
            };

            modeLine = $"Mode: {modeName} ({modeIdx})";
        }

        text.text = $"{freqLine}\n{rttLine}\n{compLine}\n{thrLine}\n{modeLine}";
    }
}
