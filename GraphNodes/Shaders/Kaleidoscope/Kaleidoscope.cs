using Godot;
using System;

/// <summary>
/// Kaleidoscope: folds the input around a centre into mirrored wedges.
/// </summary>
public partial class Kaleidoscope : ShaderNode
{
    public override void _Ready()
    {
        Parameters =
        [
            new Parameter { Name = "Input", Type = SlotType.Texture, ShaderParameterName = "textureInput" },
            new Parameter { Name = "Zoom", Type = SlotType.Float, ShaderParameterName = "zoom", Value = 1.0f },
            new Parameter { Name = "Reflections", Type = SlotType.Float, ShaderParameterName = "reflections", Value = 4.0f },
            new Parameter { Name = "Rotation (radians)", Type = SlotType.Float, ShaderParameterName = "rotation", Value = 0.0f },
            new Parameter { Name = "Sample Center X", Type = SlotType.Float, ShaderParameterName = "sample_center_x", Value = 0.5f },
            new Parameter { Name = "Sample Center Y", Type = SlotType.Float, ShaderParameterName = "sample_center_y", Value = 0.5f },
            new Parameter { Name = "Offset Center X", Type = SlotType.Float, ShaderParameterName = "offset_center_x", Value = 0.5f },
            new Parameter { Name = "Offset Center Y", Type = SlotType.Float, ShaderParameterName = "offset_center_y", Value = 0.5f },
            new Parameter { Name = "Circle Mode (0 off, 1 on)", Type = SlotType.Integer, ShaderParameterName = "circle_mode", Value = 0 },
        ];
        base._Ready();
        // Circle mode leaves the corners transparent; keep that alpha for whatever composites this
        GetNode<SubViewport>("PreviewPanel/TextureRect/SubViewport").TransparentBg = true;
    }
}
