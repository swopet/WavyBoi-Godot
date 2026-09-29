using Godot;
using System;

/// <summary>
/// Rotates each Source pixel's hue by the hue angle of the same pixel in the Hue Map (x Amount),
/// plus an Offset in degrees. Grey map pixels have no hue and leave the source unshifted.
/// </summary>
public partial class MappedHueShift : ShaderNode
{
    public override void _Ready()
    {
        Parameters =
        [
            new Parameter { Name = "Source", Type = SlotType.Texture, ShaderParameterName = "texture0" },
            new Parameter { Name = "Hue Map", Type = SlotType.Texture, ShaderParameterName = "texture1" },
            new Parameter { Name = "Amount", Type = SlotType.Float, ShaderParameterName = "amount", Value = 1.0f },
            new Parameter { Name = "Offset", Type = SlotType.Float, ShaderParameterName = "offset", Value = 0.0f },
        ];
        base._Ready();
    }
}
