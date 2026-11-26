using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Turns the choir features of <see cref="ChoirHost"/> (envelope, spectral flux, onsets) into ONE intensity byte
/// (0..maxIntensity) and sends it over UDP (Wi-Fi) to one or several ESP32 bracelets. Every bracelet gets the same byte.
/// Wire format and ESP-side requirements: docs/esp-wifi.md.
/// </summary>
public class ChoirHapticAdvancedBridge : MonoBehaviour
{
    [Serializable]
    public struct Target
    {
        [Tooltip("IPv4 address of one ESP, e.g. 192.168.137.44. Use 127.0.0.1 to test on this computer.")]
        public string ip;
        public int port;
    }

    [Header("References")]
    [SerializeField] private ChoirHost choirHost;

    [Header("ESP32 Network Settings")]
    [Tooltip("First bracelet (kept as two fields so older scenes keep working). Leave empty to use only the list below.")]
    public string esp32IP = "192.168.137.44";
    public int port = 4210;
    [Tooltip("More bracelets: one entry each. They receive the same byte as the first one.")]
    public Target[] additionalTargets = new Target[0];
    public float sendRateHz = 50f;
    [Tooltip("Resend the current value at least this often (ms) even when it did not change. Repairs a lost UDP packet " +
             "and keeps the ESP's safety timeout from firing during a steady note. 0 = send only on change.")]
    [Min(0f)] public float keepAliveMs = 100f;

    public enum HapticSource { Envelope, Harmony }

    [Header("Source (can be switched while playing)")]
    [Tooltip("Envelope: loudness + spectral flux + onset punch (as before). Harmony: the harmonic tension of the voices " +
             "(0 = calm chord in the key, 1 = tense), only while something is sung. Needs Estimate Key on the ChoirHost.")]
    public HapticSource source = HapticSource.Envelope;
    [Tooltip("Buttons in the Game view to switch the source and a line showing the value sent.")]
    public bool showControls = true;

    [Header("Harmony source")]
    [Range(0f, 1f)] [Tooltip("Share of the full intensity felt on a calm chord (tension 0), so singing is never silent.")]
    public float harmonyBase = 0.25f;
    [Tooltip("Multiplies the tension before sending (result clamped to 1). Raise it if the tension rarely gets high.")]
    [Min(0f)] public float harmonyGain = 1.0f;
    [Tooltip("Envelope level at which something counts as sounding. Below it the harmony output fades to 0 (the tension " +
             "memory lasts about half a second after the singing stops).")]
    [Min(0.0001f)] public float presenceEnvelope = 0.03f;
    [Tooltip("The tension already lags about 0.5 s, so these are slower than the envelope smoothing.")]
    public float harmonyAttackMs = 200f;
    public float harmonyReleaseMs = 600f;

    [Header("Feature Weights")]
    public float envelopeWeight = 1.0f;
    public float fluxWeight = 0.15f;
    [Range(0f, 1f)] public float onsetBonus = 0.35f;

    [Header("Shaping")]
    [Range(0f, 0.2f)] public float noiseGate = 0.03f;
    [Tooltip("<1 expands quiet sounds, >1 compresses them")]
    public float gamma = 0.6f;
    public float masterGain = 1.5f;
    [Range(20, 127)] public int maxIntensity = 110;   // leave headroom

    [Header("Smoothing (ms)")]
    public float attackMs = 15f;
    public float releaseMs = 90f;
    public float onsetHoldMs = 60f;

    [Header("Debug")]
    public bool logValues = false;

    private UdpClient udpClient;
    private IPEndPoint[] endpoints = new IPEndPoint[0];
    private float timer, sendInterval, lastTick, sinceSend;
    private float smoothed, onsetEnv;
    private int lastSent = -1;          // -1 = nothing sent yet, so the first value always goes out
    private int currentIntensity;       // latest computed value, for the on-screen line
    private string loggedError;
    private readonly byte[] packet = new byte[1];

    void OnEnable()
    {
        if (choirHost == null) choirHost = GetComponent<ChoirHost>();
        sendInterval = 1f / Mathf.Max(1f, sendRateHz);

        endpoints = ParseTargets();
        if (endpoints.Length == 0)
        {
            Debug.LogWarning("ChoirHapticAdvancedBridge: no valid target, not sending.");
            return;
        }

        // Not connected to one address: one socket serves every bracelet, and one unreachable bracelet cannot affect the others.
        udpClient = new UdpClient();
        timer = 0f;
        lastTick = Time.unscaledTime;
        sinceSend = 0f;
        smoothed = 0f;
        onsetEnv = 0f;
        lastSent = -1;
        loggedError = null;
    }

    private IPEndPoint[] ParseTargets()
    {
        var list = new List<IPEndPoint>();
        TryAdd(list, esp32IP, port, skipSilentlyIfEmpty: true);
        if (additionalTargets != null)
            foreach (var t in additionalTargets) TryAdd(list, t.ip, t.port, skipSilentlyIfEmpty: false);
        return list.ToArray();
    }

    private static void TryAdd(List<IPEndPoint> list, string ip, int p, bool skipSilentlyIfEmpty)
    {
        ip = (ip ?? "").Trim();
        if (ip.Length == 0 && skipSilentlyIfEmpty) return;
        if (IPAddress.TryParse(ip, out var addr) && p > 0 && p <= 65535)
        {
            var ep = new IPEndPoint(addr, p);
            foreach (var existing in list) if (existing.Equals(ep)) return; // same bracelet listed twice
            list.Add(ep);
        }
        else
        {
            Debug.LogWarning($"ChoirHapticAdvancedBridge: ignoring invalid target '{ip}:{p}'.");
        }
    }

    void Update()
    {
        if (choirHost == null || udpClient == null) return;

        float now = Time.unscaledTime;
        timer += Time.unscaledDeltaTime;
        if (timer < sendInterval) return;
        timer -= sendInterval;                    // keep the remainder
        if (timer > sendInterval) timer = 0f;     // avoid burst after a stall

        // Real time since the last step (the frame rate decides when we get here, not the send rate).
        float dt = Mathf.Clamp(now - lastTick, 0.001f, 0.1f);
        lastTick = now;
        sinceSend += dt;

        var mix = choirHost.Mix;

        // Entropy-aware flux: less flux contribution for tonal singing
        float flux = Mathf.Clamp01(mix.SpectralFlux * 2f) * Mathf.Lerp(0.3f, 1f, mix.SpectralEntropy);

        bool harmony = source == HapticSource.Harmony;
        float target, attack, release;
        if (harmony)
        {
            // Tension (0 calm .. 1 tense) lifts the intensity from harmonyBase to full, and only while something sounds.
            float tension = Mathf.Clamp01(choirHost.Harmony.Tension * harmonyGain);
            float presence = Mathf.Clamp01(choirHost.Envelope / presenceEnvelope);
            target = Mathf.Lerp(harmonyBase, 1f, tension) * presence;
            attack = harmonyAttackMs;
            release = harmonyReleaseMs;
        }
        else
        {
            float raw = choirHost.Envelope * envelopeWeight + flux * fluxWeight;

            // Gate -> perceptual curve -> soft clip (no hard saturation)
            float gated = Mathf.Clamp01((raw - noiseGate) / (1f - noiseGate));
            float shaped = Mathf.Pow(gated, gamma) * masterGain;
            target = (float)Math.Tanh(shaped);
            attack = attackMs;
            release = releaseMs;
        }

        // Asymmetric one-pole smoothing: fast attack, slower release
        float tau = Mathf.Max(0.0001f, (target > smoothed ? attack : release) / 1000f);
        smoothed += (target - smoothed) * (1f - Mathf.Exp(-dt / tau));

        // Onset punch as a short decaying envelope, added AFTER smoothing (envelope source only: harmony should stay calm)
        onsetEnv = Mathf.Max(onsetEnv * Mathf.Exp(-dt / Mathf.Max(0.0001f, onsetHoldMs / 1000f)), mix.Onset ? 1f : 0f);

        float level = Mathf.Clamp01(smoothed + (harmony ? 0f : onsetEnv * onsetBonus));
        int intensity = Mathf.Clamp(Mathf.RoundToInt(level * maxIntensity), 0, maxIntensity);
        currentIntensity = intensity;

        if (logValues) Debug.Log($"{source}: env {choirHost.Envelope:F2} tension {choirHost.Harmony.Tension:F2} sent {intensity}");

        // Send on change, and otherwise at the keep-alive interval (so a lost packet is repaired and zeros keep arriving).
        bool changed = intensity != lastSent;
        bool keepAliveDue = keepAliveMs > 0f && sinceSend * 1000f >= keepAliveMs;
        if (!changed && !keepAliveDue) return;
        lastSent = intensity;
        sinceSend = 0f;
        Send((byte)intensity);
    }

    private void Send(byte intensity)
    {
        packet[0] = intensity;
        string error = null;
        for (int i = 0; i < endpoints.Length; i++)
        {
            try { udpClient.Send(packet, 1, endpoints[i]); }
            catch (Exception e) { error = $"{endpoints[i]}: {e.Message}"; } // keep going: the other bracelets still get their packet
        }
        // Log each distinct problem once, not 50 times per second.
        if (error != null && error != loggedError) Debug.LogError("ChoirHapticAdvancedBridge: UDP error: " + error);
        loggedError = error;
    }

    void OnDisable()
    {
        udpClient?.Close();
        udpClient = null;
    }

    private static readonly string[] SourceNames = { "Envelope", "Harmony" };

    void OnGUI()
    {
        if (!showControls) return;
        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float y = Screen.height / scale - 64f;

        GUI.Label(new Rect(10, y, 100, 24), "Haptic source");
        source = (HapticSource)GUI.Toolbar(new Rect(110, y, 180, 24), (int)source, SourceNames);

        string state = udpClient == null ? "NOT SENDING (no valid target)" : $"{endpoints.Length} target(s)";
        string warn = source == HapticSource.Harmony && choirHost != null && !choirHost.HarmonyAvailable
            ? "   harmony analysis is OFF (tick Estimate Key on ChoirHost)" : "";
        GUI.Label(new Rect(10, y + 24, 760, 24), $"{state}   sent {currentIntensity}/{maxIntensity}{warn}");
    }
}
