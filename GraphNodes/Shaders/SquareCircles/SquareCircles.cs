using Godot;
using System;

/// <summary>
/// Rings of orbiting squares over 4 scrolling layers, blended with Feedback by Mask.
/// Time runs on its own (x Speed) unless a Time node is wired into it.
/// </summary>
public partial class SquareCircles : ShaderNode
{
    public override void _Ready()
    {
        AutoTime = new Parameter { Name = "Time", Type = SlotType.Float, ShaderParameterName = "time", Value = 0.0f };
        AutoTimeSpeed = new Parameter { Name = "Speed", Type = SlotType.Float, ShaderParameterName = "", Value = 1.0f };
        Parameters =
        [
            new Parameter { Name = "Mask", Type = SlotType.Texture, ShaderParameterName = "texture0" },
            new Parameter { Name = "Feedback", Type = SlotType.Texture, ShaderParameterName = "textureFeedback" },
            AutoTime,
            AutoTimeSpeed,
            new Parameter { Name = "Bass", Type = SlotType.Float, ShaderParameterName = "bass", Value = 0.0f },
        ];
        base._Ready();
    }
}
