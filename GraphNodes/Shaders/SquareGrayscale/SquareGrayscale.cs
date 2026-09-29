using Godot;
using System;

/// <summary>
/// Halftone squares sized by Texture 0's brightness, blended toward Texture 1.
/// Time runs on its own (x Speed) unless a Time node is wired into it.
/// </summary>
public partial class SquareGrayscale : ShaderNode
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
            new Parameter { Name = "Bass Twist (0 off, 1 on)", Type = SlotType.Integer, ShaderParameterName = "bass_twist", Value = 0 },
        ];
        base._Ready();
    }
}
