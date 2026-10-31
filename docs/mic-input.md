# Microphone input: options to discuss

Status: undecided. This note collects what matters for the choice. Nothing here has been tested with hardware yet.

## What the software needs

- 4 **synchronous** mono channels (one per singer), delivered to Unity as channels 1..4 of one stream.
- Channel count and device are configuration, not hard-coded (`ChoirHost.voiceCount`).
- Each channel needs its own gain trim, which exists in the app (see [concert-manual.md](concert-manual.md)).
- Low and stable latency (mic to vibration "as good as possible"; no number set yet).

## Options

| Option | For | Against |
|--------|-----|---------|
| **A. One multichannel USB audio interface** (4+ mic inputs, XLR + preamps) | One clock, all channels sample-aligned, one driver, gains on the hardware, phantom power for condensers. Most robust choice. | Needs an interface (cost) and XLR mics. |
| **B. Digital mixer with USB multitrack out** (often available at venues) | Possibly free to use, real mixing desk features. | Driver and channel routing vary per model; adds a dependency on the venue's equipment. |
| **C. Four separate USB mics / headsets** | Cheap, plug and play. | Four devices means four clocks that drift apart, four device selections, and Unity multi-device capture is unverified. Avoid unless A and B are impossible. |

Recommendation: A, with the interface chosen so it exposes all inputs as one device with ASIO (or WASAPI exclusive) support.

## Things to check before buying anything

1. **Unity side (the real risk).**
   - `AudioSourceInput` uses Unity's `Microphone` class, which normally returns a **mono** clip from one device. It will not deliver 4 channels from an interface.
   - So a different input adapter is needed, for example [keijiro/Lasp](https://github.com/keijiro/Lasp) (Unlicense, desktop, per-device/channel input), or a small native/ASIO plugin. Whether Lasp gives clean 4-channel streams from one interface is unverified. Test with a borrowed interface early.
   - The current VB-Audio Virtual Cable setup is only for development.
2. **Microphone type and bleed.** The singers stand close together, so every mic also hears the others. Close-mic'd dynamic or headset mics reduce bleed. The software works on the sum of the voices for loudness/spectral features, so some bleed is tolerable, but YIN pitch per singer gets less reliable when a neighbour is nearly as loud as the singer.
3. **Latency.** Use the driver's smallest stable buffer (about 128-256 samples at 48 kHz, 3-5 ms) and set Unity's DSP buffer to *Best latency*. Bluetooth to the bracelets will likely add more than the audio side; measure both later.
4. **Gain staging.** Set preamp gain so the loudest singer peaks well below clipping (the app shows `CLIP` when a raw input reaches full scale), then use the per-channel trim in the app to even the channels out.
5. **Sample rate.** Use 44.1 or 48 kHz consistently on the interface, in Windows, and in Unity.

## Questions for the supervisor

- Is there an existing interface/mixer we can borrow, and which model?
- XLR mics available, or should we buy headset mics?
- Are choir members free to move (wireless mics?) or fixed on stage? Wireless adds receivers and latency.
- What latency is acceptable for the effect to feel connected to the music?
- Is the performance space and PA setup known (do we get a feed from the sound desk instead of our own mics)?
