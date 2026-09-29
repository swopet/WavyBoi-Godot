using Godot;
using System;

/// <summary>
/// Morphing rhombus tiling; contour bands blend Texture 0 (black) and Texture 1 (white).
/// Time runs on its own (x Speed) unless a Time node is wired into it.
/// </summary>
public partial class Rhombi : ShaderNode
{
    public override void _Ready()
    {
        AutoTime = new Parameter { Name = "Time", Type = SlotType.Float, ShaderParameterName = "time", Value = 0.0f };
        AutoTimeSpeed = new Parameter { Name = "Speed", Type = SlotType.Float, ShaderParameterName = "", Value = 1.0f };
        Parameters =
        [
            new Parameter { Name = "Texture 0", Type = SlotType.Texture, ShaderParameterName = "texture0" },
            new Parameter { Name = "Texture 1", Type = SlotType.Texture, ShaderParameterName = "texture1" },
            AutoTime,
            AutoTimeSpeed,
            new Parameter { Name = "Bass", Type = SlotType.Float, ShaderParameterName = "bass", Value = 0.0f },
            new Parameter { Name = "Scale", Type = SlotType.Float, ShaderParameterName = "scale", Value = 12.0f },
        ];
        base._Ready();
    }
}
