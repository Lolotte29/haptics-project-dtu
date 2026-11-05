using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Sends the live amplitude envelope of <see cref="ChoirHost"/> to the ESP bracelets over UDP (Wi-Fi).
/// The same 5-byte packet (<see cref="HapticPacket"/>) goes to every target at a fixed rate from its own thread,
/// so the rate does not depend on the Unity frame rate. Nothing here runs on the audio thread.
/// Format and ESP-side requirements: docs/esp-wifi.md.
/// </summary>
public class UdpHapticSender : MonoBehaviour
{
    [Serializable]
    public struct Target
    {
        [Tooltip("IPv4 address of one ESP, e.g. 192.168.1.51. Use 127.0.0.1 to test on this computer.")]
        public string ip;
        [Tooltip("UDP port the ESP listens on.")]
        public int port;
    }

    [SerializeField] private ChoirHost host;
    [Tooltip("One entry per bracelet. Every ESP gets the same packets.")]
    [SerializeField] private Target[] targets = { new Target { ip = "127.0.0.1", port = 4210 } };
    [Tooltip("Packets per second. 200 is plenty for vibration; the envelope itself only moves once per audio buffer (about 100-200 times/s).")]
    [Range(10, 1000)] [SerializeField] private int sendRateHz = 200;
    [Tooltip("Multiplies the envelope before sending (result clamped to 1). A sung voice is quiet, so raise this if the bracelets feel weak.")]
    [Min(0f)] [SerializeField] private float outputGain = 1f;
    [SerializeField] private bool showStatus = true;

    private Thread _thread;
    private volatile bool _running;
    private volatile string _error;
    private long _sent;
    private int _lastValue;      // last quantised envelope, for the status line
    private int _targetCount;
    private long _lastSentSnapshot;
    private float _lastSnapshotTime;
    private float _packetsPerSecond;
    private string _loggedError;

    private void OnEnable()
    {
        if (host == null) host = GetComponent<ChoirHost>();
        if (host == null) { UnityEngine.Debug.LogError("UdpHapticSender: assign a ChoirHost."); enabled = false; return; }

        var endpoints = ParseTargets();
        _targetCount = endpoints.Length;
        if (endpoints.Length == 0) { UnityEngine.Debug.LogWarning("UdpHapticSender: no valid targets, not sending."); }

        _error = null;
        _loggedError = null;
        _sent = 0;
        _lastSentSnapshot = 0;
        _lastSnapshotTime = Time.unscaledTime;
        _running = true;
        // Everything the thread needs is captured here so it never touches a Unity object.
        Func<float> readEnvelope = () => host.Envelope;
        float gain = outputGain;
        int rate = sendRateHz;
        _thread = new Thread(() => SendLoop(endpoints, readEnvelope, gain, rate))
        {
            IsBackground = true,
            Name = "UdpHapticSender",
            Priority = System.Threading.ThreadPriority.AboveNormal,
        };
        _thread.Start();
    }

    private void OnDisable()
    {
        _running = false;
        if (_thread != null && !_thread.Join(500)) _thread.Interrupt();
        _thread = null;
    }

    private IPEndPoint[] ParseTargets()
    {
        var list = new System.Collections.Generic.List<IPEndPoint>();
        foreach (var t in targets)
        {
            if (IPAddress.TryParse((t.ip ?? "").Trim(), out var ip) && t.port > 0 && t.port <= 65535)
                list.Add(new IPEndPoint(ip, t.port));
            else
                UnityEngine.Debug.LogWarning($"UdpHapticSender: ignoring invalid target '{t.ip}:{t.port}'.");
        }
        return list.ToArray();
    }

    private void SendLoop(IPEndPoint[] endpoints, Func<float> readEnvelope, float gain, int rateHz)
    {
        var buffer = new byte[HapticPacket.Size];
        ushort seq = 0;
        long interval = Stopwatch.Frequency / Math.Max(1, rateHz);
        var clock = Stopwatch.StartNew();
        long next = clock.ElapsedTicks;
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                while (_running)
                {
                    float env = readEnvelope() * gain;
                    HapticPacket.Write(buffer, seq++, env);
                    Volatile.Write(ref _lastValue, HapticPacket.Quantize(env));
                    for (int i = 0; i < endpoints.Length; i++)
                    {
                        try { socket.SendTo(buffer, 0, HapticPacket.Size, SocketFlags.None, endpoints[i]); _error = null; }
                        catch (SocketException e) { _error = e.SocketErrorCode.ToString(); } // e.g. network down: keep trying
                    }
                    Interlocked.Increment(ref _sent);

                    // Sleep most of the interval, spin the last bit. No catch-up bursts if we oversleep.
                    next += interval;
                    long now = clock.ElapsedTicks;
                    if (next < now) next = now;
                    while (_running)
                    {
                        long left = next - clock.ElapsedTicks;
                        if (left <= 0) break;
                        if (left > 2 * Stopwatch.Frequency / 1000) Thread.Sleep(1);
                        else Thread.SpinWait(50);
                    }
                }
            }
        }
        catch (ThreadInterruptedException) { }
        catch (Exception e) { _error = e.GetType().Name + ": " + e.Message; }
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now - _lastSnapshotTime >= 1f)
        {
            long sent = Interlocked.Read(ref _sent);
            _packetsPerSecond = (sent - _lastSentSnapshot) / (now - _lastSnapshotTime);
            _lastSentSnapshot = sent;
            _lastSnapshotTime = now;
        }
        string err = _error;
        if (err != null && err != _loggedError)
            UnityEngine.Debug.LogWarning("UdpHapticSender: send problem: " + err);
        _loggedError = err;
    }

    private void OnGUI()
    {
        if (!showStatus) return;
        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float y = Screen.height / scale - 34f;
        string err = _error;
        GUI.Label(new Rect(10, y, 700, 24),
            $"UDP -> {_targetCount} target(s)  {_packetsPerSecond:0} pkt/s  envelope {Volatile.Read(ref _lastValue) / 65535f:0.00}" +
            (err != null ? $"   ERROR: {err}" : ""));
    }
}
