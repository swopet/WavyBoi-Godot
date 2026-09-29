using Godot;
using System;

/// <summary>
/// Rotates each pixel's hue by its angle around a centre point (0-1 across the image, default the
/// middle) plus an Offset in degrees; one turn around the centre is one turn of the colour wheel.
/// </summary>
public partial class RadialHueShift : ShaderNode
{
    public override void _Ready()
    {
        Parameters =
        [
            new Parameter { Name = "Input", Type = SlotType.Texture, ShaderParameterName = "texture0" },
            new Parameter { Name = "Center X", Type = SlotType.Float, ShaderParameterName = "center_x", Value = 0.5f },
            new Parameter { Name = "Center Y", Type = SlotType.Float, ShaderParameterName = "center_y", Value = 0.5f },
            new Parameter { Name = "Offset", Type = SlotType.Float, ShaderParameterName = "offset", Value = 0.0f },
        ];
        base._Ready();
    }
}
