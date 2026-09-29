using Godot;
using System;

/// <summary>
/// Hex dither from the web app: the Input's brightness picks how far a nested hexagon line
/// pattern is subdivided, drawing the image as hex lines. Color 0 (lines) and Color 1 (background)
/// each pick 0 black, 1 white, 2 the Input colour, 3 the Color Texture (falls back to the Input).
/// </summary>
public partial class HexDither : ShaderNode
{
    public override void _Ready()
    {
        Parameters =
        [
            new Parameter { Name = "Input", Type = SlotType.Texture, ShaderParameterName = "texture0" },
            new Parameter { Name = "Color Texture", Type = SlotType.Texture, ShaderParameterName = "texture1" },
            new Parameter { Name = "Min Size", Type = SlotType.Float, ShaderParameterName = "min_size", Value = 6.0f },
            new Parameter { Name = "Steps", Type = SlotType.Integer, ShaderParameterName = "steps", Value = 6 },
            new Parameter { Name = "Line Width", Type = SlotType.Float, ShaderParameterName = "line_width", Value = 2.0f },
            new Parameter { Name = "Line Fade", Type = SlotType.Float, ShaderParameterName = "line_fade", Value = 12.0f },
            new Parameter { Name = "Gamma", Type = SlotType.Float, ShaderParameterName = "gamma", Value = 1.0f },
            new Parameter { Name = "Color 0", Type = SlotType.Integer, ShaderParameterName = "color0", Value = 3 },
            new Parameter { Name = "Color 1", Type = SlotType.Integer, ShaderParameterName = "color1", Value = 0 },
            new Parameter { Name = "Control", Type = SlotType.Integer, ShaderParameterName = "control", Value = 1 },
            new Parameter { Name = "Invert", Type = SlotType.Integer, ShaderParameterName = "invert", Value = 1 },
        ];
        base._Ready();
    }
}
