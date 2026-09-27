using Godot;
using System;
using System.Linq;

public partial class BwSpiral : ShaderNode
{
    private Parameter gapSizeParameter;
    private Parameter spiralCountParameter;
    private Parameter rotationDegreesParameter;
    private Parameter texture0Parameter;
    private Parameter texture1Parameter;

    private float defaultRotationSpeed = 30.0f;
    private float rotationSpeed;

    public override void _Ready()
    {
        gapSizeParameter = new Parameter
        {
            Name = "Gap Size",
            Type = SlotType.Float,
            ShaderParameterName = "gap_size",
            Value = 70.0f + (GD.Randi() % 20) * 5.0f
        };
        spiralCountParameter = new Parameter
        {
            Name = "Spiral Count",
            Type = SlotType.Float,
            ShaderParameterName = "spiral_count",
            Value = 3.0f + (GD.Randi() % 2)
        };
        rotationDegreesParameter = new Parameter
        {
            Name = "Rotation Degrees",
            Type = SlotType.Float,
            ShaderParameterName = "rotation_degrees",
            Value = 0.0f
        };
        texture0Parameter = new Parameter
        {
            Name = "Texture 0",
            Type = SlotType.Texture,
            ShaderParameterName = "texture0"
        };
        texture1Parameter = new Parameter
        {
            Name = "Texture 1",
            Type = SlotType.Texture,
            ShaderParameterName = "texture1"
        };
        Parameters = [gapSizeParameter, spiralCountParameter, rotationDegreesParameter, texture0Parameter, texture1Parameter];
        
        // Randomize rotation speed between -1.0x and 1.0x the default
        rotationSpeed = defaultRotationSpeed * (GD.Randf() * 2.0f - 1.0f);
        
        base._Ready();
    }

    public override void _Process(double delta)
    {
        if (!rotationDegreesParameter.Updated){
            rotationDegreesParameter.Value = (float)(((float)rotationDegreesParameter.Value + (float)delta * rotationSpeed) % 360.0f);
            rotationDegreesParameter.Updated = true;
        }
        base._Process(delta);
    }
}
