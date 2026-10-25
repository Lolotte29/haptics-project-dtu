# Agent instructions

The full project context, architecture, current state and rules for agents are in [CLAUDE.md](CLAUDE.md). Read it first; it applies to every coding agent regardless of vendor.

Short version: real-time vocal feature extraction in Unity for a 4-singer choir (one mic each, currently via VB-Cable), with a future Bluetooth/ESP haptic bracelet. Keep DSP Unity-free in `unity/Assets/Scripts/Dsp/`, test it with `cd dsp && dotnet test Haptics.Dsp.Tests`, and never allocate on the audio thread.
