using UnityEngine;
using System;
using System.Net.Sockets;

public class ChoirHapticAdvancedBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChoirHost choirHost;

    [Header("ESP32 Network Settings")]
    public string esp32IP = "192.168.137.44";
    public int port = 4210;
    public float sendRateHz = 50f;

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
    private float timer, sendInterval;
    private float smoothed, onsetEnv;
    private byte lastSent = 255;

    void Start()
    {
        if (choirHost == null) choirHost = GetComponent<ChoirHost>();
        udpClient = new UdpClient();
        udpClient.Connect(esp32IP, port);
        sendInterval = 1f / sendRateHz;
    }

    void Update()
    {
        if (choirHost == null) return;

        timer += Time.unscaledDeltaTime;
        if (timer < sendInterval) return;
        timer -= sendInterval;                    // keep the remainder
        if (timer > sendInterval) timer = 0f;     // avoid burst after a stall

        float dt = sendInterval;
        var mix = choirHost.Mix;

        // Entropy-aware flux: less flux contribution for tonal singing
        float flux = Mathf.Clamp01(mix.SpectralFlux * 2f) * Mathf.Lerp(0.3f, 1f, mix.SpectralEntropy);

        float raw = choirHost.Envelope * envelopeWeight + flux * fluxWeight;

        // Gate -> perceptual curve -> soft clip (no hard saturation)
        float gated = Mathf.Clamp01((raw - noiseGate) / (1f - noiseGate));
        float shaped = Mathf.Pow(gated, gamma) * masterGain;
        float target = (float)Math.Tanh(shaped);

        // Asymmetric one-pole smoothing: fast attack, slower release
        float tau = (target > smoothed ? attackMs : releaseMs) / 1000f;
        smoothed += (target - smoothed) * (1f - Mathf.Exp(-dt / tau));

        // Onset punch as a short decaying envelope, added AFTER smoothing
        onsetEnv = Mathf.Max(onsetEnv * Mathf.Exp(-dt / (onsetHoldMs / 1000f)), mix.Onset ? 1f : 0f);

        float level = Mathf.Clamp01(smoothed + onsetEnv * onsetBonus);
        byte intensity = (byte)Mathf.Clamp(Mathf.RoundToInt(level * maxIntensity), 0, maxIntensity);

        if (logValues) Debug.Log($"env {choirHost.Envelope:F2} sent {intensity}");

        // Skip redundant packets (still send zeros so it stops)
        if (intensity == lastSent && intensity != 0) return;
        lastSent = intensity;

        try { udpClient.Send(new[] { intensity }, 1); }
        catch (Exception e) { Debug.LogError("UDP error: " + e.Message); }
    }

    void OnDisable() { udpClient?.Close(); }
}