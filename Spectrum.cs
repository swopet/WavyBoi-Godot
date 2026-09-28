using Godot;
using System;

public partial class Spectrum : PanelContainer
{
	private const float MinFreq = 20.0f;
	private const float MaxFreq = 20000.0f;
	// Magnitudes are mapped on a dB scale: the calibrated peak is the top of the bar,
	// DynamicRangeDb below it is the bottom
	private const float DynamicRangeDb = 60.0f;
	// How fast bars fall back down (fraction of full height per second); they rise instantly
	private const float BarFallSpeed = 2.5f;
	private const float NormalizeDuration = 5.0f;
	private static readonly Color BarColor = new Color(0.2f, 0.6f, 1.0f);

	private AudioEffectSpectrumAnalyzerInstance spectrum_analyzer;
	private float binWidthHz = 1.0f;

	private Control bars;
	private HSlider slider;
	private float[] bandEdges = [];
	private float[] barHeights = [];

	private float MaxMagnitude = 0.0001f;
	private float normalizeTimeLeft = 0.0f;

	public override void _Ready()
	{
		int spectrum_index = AudioServer.GetBusIndex("Spectrum");
		if (spectrum_index >= 0)
		{
			spectrum_analyzer = AudioServer.GetBusEffectInstance(spectrum_index, 0) as AudioEffectSpectrumAnalyzerInstance;
			if (AudioServer.GetBusEffect(spectrum_index, 0) is AudioEffectSpectrumAnalyzer effect)
			{
				// FftSize enum: 0 = 256 samples, each step doubles
				int fftSamples = 256 << (int)effect.FftSize;
				binWidthHz = AudioServer.GetMixRate() / fftSamples;
			}
		}
		else
		{
			GD.PushWarning("Spectrum: no audio bus named \"Spectrum\"");
		}

		bars = GetNode<Control>("VBoxContainer/Bars");
		bars.Draw += DrawBars;
		bars.Resized += bars.QueueRedraw;
		slider = GetNode<HSlider>("VBoxContainer/HSlider");
		slider.ValueChanged += (value) => RebuildBands();
		RebuildBands();
	}

	public override void _Process(double delta)
	{
		if (spectrum_analyzer == null) return;

		if (normalizeTimeLeft > 0.0f)
		{
			normalizeTimeLeft -= (float)delta;
			MaxMagnitude = Mathf.Max(MaxMagnitude, GetMagnitude(MinFreq, MaxFreq));
		}

		if (!IsVisibleInTree()) return;
		float fall = BarFallSpeed * (float)delta;
		for (int i = 0; i < barHeights.Length; i++)
		{
			float target = GetNormalizedMagnitudeForFrequencyRange(bandEdges[i], bandEdges[i + 1]);
			barHeights[i] = Mathf.Max(target, barHeights[i] - fall);
		}
		bars.QueueRedraw();
	}

	/// <summary>
	/// Capture the loudest level over the next few seconds and use it as the top of the scale.
	/// </summary>
	public void NormalizeAudio()
	{
		MaxMagnitude = 0.0001f;
		normalizeTimeLeft = NormalizeDuration;
	}

	/// <summary>
	/// Level of a frequency range from 0 to 1, relative to the calibrated peak on a dB scale.
	/// </summary>
	public float GetNormalizedMagnitudeForFrequencyRange(float startFreq, float endFreq)
	{
		if (spectrum_analyzer == null) return 0.0f;
		float magnitude = GetMagnitude(startFreq, endFreq);
		if (magnitude <= 0.0f) return 0.0f;
		float dbBelowPeak = Mathf.LinearToDb(magnitude) - Mathf.LinearToDb(MaxMagnitude);
		return Mathf.Clamp(1.0f + dbBelowPeak / DynamicRangeDb, 0.0f, 1.0f);
	}

	private float GetMagnitude(float startFreq, float endFreq)
	{
		// Max mode for both bands and calibration so they're on the same scale.
		// Take the louder channel rather than the vector length, which reads ~1.41x high for mono.
		Vector2 mag = spectrum_analyzer.GetMagnitudeForFrequencyRange(
			startFreq, endFreq, AudioEffectSpectrumAnalyzerInstance.MagnitudeMode.Max);
		return Mathf.Max(mag.X, mag.Y);
	}

	private void RebuildBands()
	{
		int barCount = Math.Max(1, (int)slider.Value);
		bandEdges = new float[barCount + 1];
		barHeights = new float[barCount];

		// Logarithmic spacing, but no band narrower than one FFT bin; at the low end pure log
		// bands are narrower than a bin and several bars would just repeat the same bin.
		float freq = MinFreq;
		bandEdges[0] = freq;
		for (int i = 0; i < barCount; i++)
		{
			int remaining = barCount - i;
			float next = freq * Mathf.Pow(MaxFreq / freq, 1.0f / remaining);
			freq = Mathf.Min(Mathf.Max(next, freq + binWidthHz), MaxFreq);
			bandEdges[i + 1] = freq;
		}
		bars?.QueueRedraw();
	}

	private void DrawBars()
	{
		if (barHeights.Length == 0) return;
		Vector2 size = bars.Size;
		float barWidth = size.X / barHeights.Length;
		for (int i = 0; i < barHeights.Length; i++)
		{
			float height = barHeights[i] * size.Y;
			// Bars grow up from the bottom; the 1px gap keeps neighbouring bars distinguishable
			bars.DrawRect(new Rect2(i * barWidth, size.Y - height, Mathf.Max(barWidth - 1.0f, 1.0f), height), BarColor);
		}
	}
}
