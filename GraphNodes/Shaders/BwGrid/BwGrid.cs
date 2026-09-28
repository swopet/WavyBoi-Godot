using Godot;
using System;
using System.Linq;

public partial class BwGrid : ShaderNode
{
    private Parameter cellSizeParameter;
    private Parameter cellRotationDegreesParameter;
    private Parameter texture0Parameter;
    private Parameter texture1Parameter;

    private float defaultRotationSpeed = 15.0f;
    private float rotationSpeed;

    public override void _Ready()
    {
        cellSizeParameter = new Parameter
        {
            Name = "Cell Size",
            Type = SlotType.Float,
            ShaderParameterName = "cell_size",
            Value = 70.0f + (GD.Randi() % 20) * 5.0f
        };
        cellRotationDegreesParameter = new Parameter
        {
            Name = "Cell Rotation Degrees",
            Type = SlotType.Float,
            ShaderParameterName = "cell_rotation_degrees",
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
        Parameters = [cellSizeParameter, cellRotationDegreesParameter, texture0Parameter, texture1Parameter];
        
        // Randomize rotation speed between -1.0x and 1.0x the default
        rotationSpeed = defaultRotationSpeed * (GD.Randf() * 2.0f - 1.0f);
        
        base._Ready();
    }

    public override Godot.Collections.Dictionary Save()
    {
        var data = base.Save();
        data["rotation_speed"] = rotationSpeed;
        return data;
    }

    public override void Load(Godot.Collections.Dictionary data)
    {
        base.Load(data);
        if (data.ContainsKey("rotation_speed")) rotationSpeed = (float)data["rotation_speed"];
    }

    public override void _Process(double delta)
    {
        if (!cellRotationDegreesParameter.Updated){
            cellRotationDegreesParameter.Value = (float)(((float)cellRotationDegreesParameter.Value + (float)delta * rotationSpeed) % 360.0f);
            cellRotationDegreesParameter.Updated = true;
        }
        base._Process(delta);
    }
}
