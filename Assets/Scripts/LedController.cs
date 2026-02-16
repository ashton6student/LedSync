using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Diagnostics;

public class LedController : MonoBehaviour
{
    [Header("ESP32")]
    public string espIp = "192.168.4.1";
    public int espPort = 4210;

    [Header("Input Actions")]
    public InputActionReference pingButton;
    public InputActionReference toggleBlinkButton;  // repurpose your old "sendFrequencyButton"
    public InputActionReference joystick;           // Y changes frequencyHz

    [Header("Blink Frequency (Hz)")]
    public int frequencyHz = 2;
    public float adjustSpeed = 5f;   // Hz per second
    public int minHz = 1;
    public int maxHz = 120;

    [Header("Ping")]
    public int receiveTimeoutMs = 500;

    public float LastRttMs => lastRttMs;
    public bool LastPingTimedOut => lastPingTimedOut;

    public bool IsBlinking => isBlinking;
    public bool LedState => ledState;                 // true = ON, false = OFF
    public float LastToggleTime => lastToggleTime;    // Time.time when we sent last ON/OFF

    float lastRttMs = -1f;
    bool lastPingTimedOut = false;

    UdpClient udp;
    IPEndPoint ep;

    float frequencyAccumulator;

    bool isBlinking = false;
    bool ledState = false;
    float lastToggleTime = -999f;
    float nextToggleTime = 0f;

    void Awake()
    {
        Application.runInBackground = true;
        ep = new IPEndPoint(IPAddress.Parse(espIp), espPort);
        udp = new UdpClient();
        udp.Client.ReceiveTimeout = receiveTimeoutMs;
    }

    void OnEnable()
    {
        pingButton?.action.Enable();
        toggleBlinkButton?.action.Enable();
        joystick?.action.Enable();
    }

    void OnDisable()
    {
        pingButton?.action.Disable();
        toggleBlinkButton?.action.Disable();
        joystick?.action.Disable();
    }

    void Update()
    {
        HandleJoystick();

        if (pingButton != null && pingButton.action.WasPressedThisFrame())
            Ping();

        if (toggleBlinkButton != null && toggleBlinkButton.action.WasPressedThisFrame())
            ToggleBlinking();

        if (isBlinking)
            BlinkStep();
    }

    void ToggleBlinking()
    {
        isBlinking = !isBlinking;

        if (isBlinking)
        {
            // start from OFF -> immediately toggle to ON at next BlinkStep
            ledState = false;
            nextToggleTime = Time.time;  // toggle immediately
        }
        else
        {
            // stop blinking and force OFF
            SendLed(false);
        }

        UnityEngine.Debug.Log(isBlinking ? "[LedController] Blinking ON" : "[LedController] Blinking OFF");
    }

    void BlinkStep()
    {
        float hz = Mathf.Clamp(frequencyHz, minHz, maxHz);
        float halfPeriod = 0.5f / hz;

        if (Time.time < nextToggleTime)
            return;

        // toggle state and send it
        ledState = !ledState;
        SendLed(ledState);

        lastToggleTime = Time.time;
        nextToggleTime = Time.time + halfPeriod;
    }

    void HandleJoystick()
    {
        if (joystick == null) return;

        Vector2 stick = joystick.action.ReadValue<Vector2>();

        const float deadzone = 0.2f;
        if (Mathf.Abs(stick.y) < deadzone) return;

        frequencyAccumulator += stick.y * adjustSpeed * Time.deltaTime;

        if (Mathf.Abs(frequencyAccumulator) >= 1f)
        {
            int delta = (int)frequencyAccumulator; // trunc toward 0
            frequencyAccumulator -= delta;

            frequencyHz = Mathf.Clamp(frequencyHz + delta, minHz, maxHz);
            UnityEngine.Debug.Log($"[LedController] Frequency: {frequencyHz} Hz");
        }
    }

    public void SendLed(bool on)
    {
        SendAscii(on ? "ON" : "OFF");
    }

    public void Ping()
    {
        FlushSocket();

        try
        {
            byte[] ping = Encoding.ASCII.GetBytes("PING");

            var sw = Stopwatch.StartNew();
            udp.Send(ping, ping.Length, ep);

            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
            byte[] resp = udp.Receive(ref from);
            sw.Stop();

            string text = Encoding.ASCII.GetString(resp).Trim();
            if (text == "PONG")
            {
                lastRttMs = (float)sw.Elapsed.TotalMilliseconds;
                lastPingTimedOut = false;
                UnityEngine.Debug.Log($"[LedController] RTT: {lastRttMs:F1} ms");
            }
        }
        catch
        {
            lastPingTimedOut = true;
            UnityEngine.Debug.LogWarning("[LedController] Ping timeout");
        }
    }

    void SendAscii(string msg)
    {
        if (udp == null) return;

        byte[] data = Encoding.ASCII.GetBytes(msg);
        try
        {
            udp.Send(data, data.Length, ep);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[LedController] UDP send error ({msg}): {ex}");
        }
    }

    void FlushSocket()
    {
        if (udp == null) return;

        try
        {
            while (udp.Available > 0)
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                udp.Receive(ref from);
            }
        }
        catch { }
    }

    void OnDestroy()
    {
        try { SendLed(false); } catch { }
        udp?.Close();
        udp = null;
    }
}
