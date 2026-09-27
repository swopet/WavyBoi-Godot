using Godot;
using System;

public partial class BwGradient : ShaderNode
{
    private Parameter originalTextureParameter;

    private Parameter gradientTextureParameter;

    public override void _Ready()
    {
        originalTextureParameter = new Parameter
        {
            Name = "Original Texture",
            Type = SlotType.Texture,
            ShaderParameterName = "textureOriginal"
        };
        gradientTextureParameter = new Parameter
        {
            Name = "Gradient Texture",
            Type = SlotType.Texture,
            ShaderParameterName = "textureGradient"
        };
        Parameters = [originalTextureParameter, gradientTextureParameter];
        
        base._Ready();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }
}
