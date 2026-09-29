using Godot;
using System;
using System.Globalization;

/// <summary>
/// Outputs 0-1 for how loud a frequency range is right now (same analyzer and scale as the
/// spectrum display), for audio-reactive wiring. Defaults to bass (20-150 Hz). Rises
/// instantly and falls back smoothly, like the spectrum bars.
/// </summary>
public partial class AudioBandNode : GraphNode, IGraphNode, ISerializableNode
{
	private const float FallSpeed = 2.5f; // full scale per second
	private float lowHz = 20.0f;
	private float highHz = 150.0f;
	private float level = 0.0f;
	private LineEdit lowEdit, highEdit;
	private ProgressBar meter;

	public float Level => level;
	public float LowHz => lowHz;
	public float HighHz => highHz;

	public override void _Ready()
	{
		Title = "Audio Band";
		var row = new HBoxContainer();
		LineEdit Field(string tooltip) => new LineEdit { CustomMinimumSize = new Vector2(64, 0), Alignment = HorizontalAlignment.Center, TooltipText = tooltip };
		lowEdit = Field("Lowest frequency (Hz)");
		highEdit = Field("Highest frequency (Hz)");
		foreach (var field in new[] { lowEdit, highEdit })
		{
			field.TextSubmitted += _ => CommitRange();
			field.FocusExited += CommitRange;
		}
		row.AddChild(lowEdit);
		row.AddChild(new Label { Text = "–" });
		row.AddChild(highEdit);
		row.AddChild(new Label { Text = "Hz" });
		AddChild(row);
		meter = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
		AddChild(meter);
		SetSlotEnabledRight(0, true);
		SetSlotTypeRight(0, (int)SlotType.Float);
		ShowRange();
	}

	private void ShowRange()
	{
		lowEdit.Text = lowHz.ToString("0.#", CultureInfo.InvariantCulture);
		highEdit.Text = highHz.ToString("0.#", CultureInfo.InvariantCulture);
	}

	/// <summary>Set the band; false (no change) unless 0 &lt; low &lt; high.</summary>
	public bool SetRange(float low, float high)
	{
		if (!float.IsFinite(low) || !float.IsFinite(high) || low <= 0.0f || high <= low) return false;
		lowHz = low;
		highHz = high;
		if (lowEdit != null) ShowRange();
		return true;
	}

	private void CommitRange()
	{
		static bool Parse(LineEdit f, out float v) => float.TryParse(f.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
		if (Parse(lowEdit, out float low) && Parse(highEdit, out float high)) SetRange(low, high);
		ShowRange();
	}

	private Spectrum FindSpectrum() => (GetParent() as VisualsGraphEdit)?.ProjectRoot.spectrum;

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;
		float target = FindSpectrum()?.GetNormalizedMagnitudeForFrequencyRange(lowHz, highHz) ?? 0.0f;
		level = Mathf.Max(target, level - FallSpeed * (float)delta);
		meter.Value = level;
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => outputSlot == 0 ? level : default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
	}

	public Godot.Collections.Dictionary Save() => new() { ["low_hz"] = lowHz, ["high_hz"] = highHz };

	public void Load(Godot.Collections.Dictionary data)
	{
		if (data.ContainsKey("low_hz") && data.ContainsKey("high_hz")) SetRange((float)data["low_hz"], (float)data["high_hz"]);
	}
}
