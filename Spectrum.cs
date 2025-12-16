using Godot;
using System;

public partial class Spectrum : PanelContainer
{
	private int spectrum_index = 0;
	private AudioEffectSpectrumAnalyzerInstance spectrum_analyzer;
	// Called when the node enters the scene tree for the first time.
	private const float MinFreq = 20.0f;
    private const float MaxFreq = 20000.0f; 
	private int hboxWidthOnStartup = 0;
	public override void _Ready()
	{
		spectrum_index = AudioServer.GetBusIndex("Spectrum");
		spectrum_analyzer = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(spectrum_index, 0);
		var hbox = GetNodeOrNull<HBoxContainer>("VBoxContainer/HBoxContainer");
        // Connect HSlider to update bars when changed
        var slider = GetNodeOrNull<HSlider>("VBoxContainer/HSlider");
        if (slider != null)
            slider.ValueChanged += (value) => PopulateBars();
	}

	public void UpdateHboxWidth()
	{
		var hbox = GetNodeOrNull<HBoxContainer>("VBoxContainer/HBoxContainer");
		if (hbox != null && hboxWidthOnStartup == 0)
		{
			hboxWidthOnStartup = (int)hbox.Size.X;
            GD.Print("Hbox width on startup: " + hboxWidthOnStartup);
		}
		PopulateBars();
	}

	public void DeleteBars()
	{
		var hbox = GetNodeOrNull<HBoxContainer>("VBoxContainer/HBoxContainer");
		if (hbox != null)
		{
			for (int i = hbox.GetChildCount() - 1; i >= 0; i--)
			{
				var child = hbox.GetChild(i);
				hbox.RemoveChild(child);
				child.QueueFree();
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (spectrum_analyzer == null)
        {
            return;
        }
		UpdateSpectrumBars();
	}

    private void UpdateSpectrumBars()
    {
        // Get barCount from the HSlider node
        int barCount = 16;
        var slider = GetNodeOrNull<HSlider>("VBoxContainer/HSlider");
        if (slider != null)
            barCount = (int)slider.Value;
		float barWidth = hboxWidthOnStartup / barCount;
        if (barWidth < 1) barWidth = 1;
        // Set the height of each bar (scale magnitude to 0-100 px)
        var hbox = GetNodeOrNull<HBoxContainer>("VBoxContainer/HBoxContainer");
        if (hbox != null)
        {
            // Logarithmic frequency spacing
            double logMin = Math.Log10(MinFreq);
            double logMax = Math.Log10(MaxFreq);
            for (int i = 0; i < barCount; i++)
            {
                var rect = hbox.GetChildOrNull<ColorRect>(i);
                if (rect != null)
                {
                    double t0 = (double)i / barCount;
                    double t1 = (double)(i + 1) / barCount;
                    float startFreq = (float)Math.Pow(10, logMin + (logMax - logMin) * t0);
                    float endFreq = (float)Math.Pow(10, logMin + (logMax - logMin) * t1);
                    Vector2 mag = spectrum_analyzer.GetMagnitudeForFrequencyRange(
                        startFreq,
                        endFreq,
                        AudioEffectSpectrumAnalyzerInstance.MagnitudeMode.Average
                    );
                    float barHeight = Mathf.Clamp(mag.Length() * 100f, 0, 100); // scale to 0-100 px
                    rect.Size = new Vector2(barWidth, barHeight);
                }
			}
        }
    }

    private void PopulateBars()
    {
		// Get barCount from the HSlider node
        int barCount = 16;
        var slider = GetNodeOrNull<HSlider>("VBoxContainer/HSlider");
        if (slider != null)
            barCount = (int)slider.Value;
        var hbox = GetNodeOrNull<HBoxContainer>("VBoxContainer/HBoxContainer");
        if (hbox == null || barCount <= 0)
            return;

        // Remove extra bars if any
        while (hbox.GetChildCount() > barCount)
        {
            var child = hbox.GetChild(hbox.GetChildCount() - 1);
            hbox.RemoveChild(child);
            child.QueueFree();
        }

        // Add missing bars
        while (hbox.GetChildCount() < barCount)
        {
            var rect = new ColorRect();
            rect.Color = new Color(0.2f, 0.6f, 1.0f);
            rect.Name = $"Bar{hbox.GetChildCount()}";
            hbox.AddChild(rect);
        }

        float barWidth = hboxWidthOnStartup / barCount;
        if (barWidth < 1) barWidth = 1;

        // Set all bar heights to 0 initially and width dynamically
        for (int i = 0; i < barCount; i++)
        {
            var rect = hbox.GetChild(i) as ColorRect;
            if (rect != null)
            {
                rect.CustomMinimumSize = new Vector2(barWidth, 0);
                rect.Size = new Vector2(barWidth, 0);
                rect.AnchorTop = 0.0f;
				rect.AnchorBottom = 1.0f;
            }
        }
    }
}
