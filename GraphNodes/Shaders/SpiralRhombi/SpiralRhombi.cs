using Godot;
using System;

/// <summary>
/// The rhombus tiling wrapped into an Escher-like spiral (arms set by A and B).
/// Time runs on its own (x Speed) unless a Time node is wired into it.
/// </summary>
public partial class SpiralRhombi : ShaderNode
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
            new Parameter { Name = "A", Type = SlotType.Integer, ShaderParameterName = "a", Value = 3 },
            new Parameter { Name = "B", Type = SlotType.Integer, ShaderParameterName = "b", Value = 5 },
        ];
        base._Ready();
    }
}
