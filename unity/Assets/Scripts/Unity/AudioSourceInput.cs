using System.Collections;
using UnityEngine;

/// <summary>
/// Input adapter: feeds whatever an AudioSource plays into <see cref="ChoirHost"/> (channel i -> voice i).
/// Either play a clip, or tick "Use Microphone" to loop a capture device (e.g. "CABLE Output (VB-Audio Virtual Cable)")
/// through the AudioSource. Mute the speakers by routing the AudioSource to a mixer group at -80 dB.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioSourceInput : MonoBehaviour
{
    [SerializeField] private ChoirHost host;
    [SerializeField] private bool useMicrophone;
    [Tooltip("Capture device name; empty = system default. See the console list printed at start.")]
    [SerializeField] private string microphoneDevice = "";

    private IEnumerator Start()
    {
        if (!useMicrophone) yield break;

        foreach (var d in Microphone.devices) Debug.Log("Microphone device: " + d);

        string dev = string.IsNullOrEmpty(microphoneDevice) ? null : microphoneDevice;
        var src = GetComponent<AudioSource>();
        src.clip = Microphone.Start(dev, true, 1, AudioSettings.outputSampleRate);
        src.loop = true;
        while (Microphone.GetPosition(dev) <= 0) yield return null; // wait for capture to start
        src.Play();
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (host != null) host.Push(data, data.Length / channels, channels);
    }
}
