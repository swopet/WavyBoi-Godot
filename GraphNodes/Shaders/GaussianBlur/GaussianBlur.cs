using Godot;
using System;

public partial class GaussianBlur : ShaderNode
{
    Parameter originalTextureParemeter;
    Parameter blurSizeParameter;
    Parameter xStepParameter;
    Parameter yStepParameter;

    public override void _Ready()
    {
        originalTextureParemeter = new Parameter
        {
            Name = "Original Texture",
            Type = SlotType.Texture,
            ShaderParameterName = "textureOriginal"
        };
        blurSizeParameter = new Parameter
        {
            Name = "Blur Radius",
            Type = SlotType.Float,
            ShaderParameterName = "radius",
            Value = 10.0f
        };
        xStepParameter = new Parameter
        {
            Name = "X Step",
            Type = SlotType.Float,
            ShaderParameterName = "x_step",
            Value = 0.0f
        };
        yStepParameter = new Parameter
        {
            Name = "Y Step",
            Type = SlotType.Float,
            ShaderParameterName = "y_step",
            Value = 0.0f
        };
        Parameters = [originalTextureParemeter, blurSizeParameter, xStepParameter, yStepParameter];
        
        base._Ready();
        _shaderMaterial.SetShaderParameter("step", new Vector2(1, 0));
    }
}
