using Godot;
using System;

/// <summary>
/// One pass of a feedback loop: zoom/rotate/shift and fade the Feedback texture (last frame,
/// from a Frame Buffer), then blend the Fresh texture on top.
/// </summary>
public partial class FeedbackTransform : ShaderNode
{
    public override void _Ready()
    {
        Parameters =
        [
            new Parameter { Name = "Feedback", Type = SlotType.Texture, ShaderParameterName = "textureFeedback" },
            new Parameter { Name = "Fresh", Type = SlotType.Texture, ShaderParameterName = "textureFresh" },
            new Parameter { Name = "Zoom", Type = SlotType.Float, ShaderParameterName = "zoom", Value = 1.02f },
            new Parameter { Name = "Rotation Degrees", Type = SlotType.Float, ShaderParameterName = "rotation_degrees", Value = 1.0f },
            new Parameter { Name = "Offset X", Type = SlotType.Float, ShaderParameterName = "offset_x", Value = 0.0f },
            new Parameter { Name = "Offset Y", Type = SlotType.Float, ShaderParameterName = "offset_y", Value = 0.0f },
            new Parameter { Name = "Decay", Type = SlotType.Float, ShaderParameterName = "decay", Value = 0.02f },
            new Parameter { Name = "Fresh Amount", Type = SlotType.Float, ShaderParameterName = "fresh_amount", Value = 1.0f },
            new Parameter { Name = "Blend Mode (0 mix, 1 add, 2 lighten)", Type = SlotType.Integer, ShaderParameterName = "blend_mode", Value = 2 },
        ];
        base._Ready();
    }
}
