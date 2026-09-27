using Godot;
using System;


public class Parameter
{
	public string Name;
	public SlotType Type;
	public string ShaderParameterName;

	public Variant Value = default; // The value of the parameter, which can be of various types depending on the SlotType
	public bool Updated = false; // Flag to track if the parameter has been updated since the last shader parameter update

    public Parameter()
    {
    }

}

[Tool]
public partial class ShaderNode : GraphNode, IGraphNode, IResolutionDependent, ISerializableNode
{
	private NodePath _previewPanelPath;
	[Export] public NodePath PreviewPanelPath
	{
		get => _previewPanelPath;
		set => _previewPanelPath = value;
	}
	
	private Shader _shader;
	
	[Export] public Shader Shader
	{
		get => _shader;
        set {
            _shader = value;
            OnShaderChanged();
		}
	}
	private Parameter[] _parameters = [];
	public Parameter[] Parameters
	{
		get => _parameters;
		set => _parameters = value;
	}
	protected ShaderMaterial _shaderMaterial = null;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		UpdateShaderMaterial();
		UpdateParameterSlots();
	}

	private void OnShaderChanged()
	{
		// The setter runs during scene instantiation, before the preview panel exists; _Ready handles that case
		if (!IsNodeReady()) return;
		UpdateShaderMaterial();
	}

	private SubViewport GetPreviewSubViewport()
	{
		return GetNode<PanelContainer>(PreviewPanelPath).GetChild(0).GetChild(0) as SubViewport;
	}

	public void SetResolution(Vector2I resolution)
	{
		GetPreviewSubViewport().Size = resolution;
		_shaderMaterial?.SetShaderParameter("viewport_size", resolution);
	}

	private void OnShowHideButtonToggled(bool state)
	{
		(GetNode<PanelContainer>(PreviewPanelPath).GetChild(0) as TextureRect).Visible = state;
	}

	private void UpdateShaderMaterial()
	{
		_shaderMaterial = new ShaderMaterial
        {
            Shader = _shader
        };
		var colorRect = GetNode<PanelContainer>(PreviewPanelPath).GetChild(0).GetChild(0).GetChild(0) as ColorRect;
		colorRect.Material = _shaderMaterial;
		if (_shaderMaterial.Shader == null) return;
		_shaderMaterial.SetShaderParameter("viewport_size", GetPreviewSubViewport().Size);
	}

	private void UpdateParameterSlots()
	{
		if (Parameters == null) return;
		while (GetChildCount() > 1) // Remove existing slots, keep the first child which is the preview panel
		{
			var child = GetChild(1);
			RemoveChild(child); // QueueFree alone doesn't change the child count, so this loop would never end
			child.QueueFree();
		}
		SetSlotEnabledRight(0, true);
		SetSlotTypeRight(0, (int)SlotType.Texture); // Set the preview panel slot to be a texture slot
		for (int i = 0; i < Parameters.Length; i++)
		{
			var hbox = new HBoxContainer();
			hbox.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			AddChild(hbox);
			hbox.AddChild(new Label { Text = Parameters[i].Name }, true);
			var param = Parameters[i];
			SetSlotEnabledLeft(i+1, true);
			SetSlotTypeLeft(i+1, (int)param.Type);
		}
	}

	public Parameter[] GetParameters()
	{
		return Parameters;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return; // Don't update shader parameters in the editor, only at runtime
		if (_shaderMaterial != null && Parameters != null)
		{
			
			for (int i = 0; i < Parameters.Length; i++)
			{
				var param = Parameters[i];
				if (param.ShaderParameterName == "") continue; // Skip parameters that aren't linked to a shader parameter
				switch (param.Type)
				{
					case SlotType.Integer:
						_shaderMaterial.SetShaderParameter(param.ShaderParameterName, (int)param.Value);
						break;
					case SlotType.Float:
						_shaderMaterial.SetShaderParameter(param.ShaderParameterName, (float)param.Value);
						break;
					case SlotType.Texture:
						if (param.Value.VariantType == Variant.Type.Nil || param.Updated == false)
						{
							_shaderMaterial.SetShaderParameter("use_" + param.ShaderParameterName, false);
						}
						else
						{
							_shaderMaterial.SetShaderParameter(param.ShaderParameterName, (Texture)param.Value);
							_shaderMaterial.SetShaderParameter("use_" + param.ShaderParameterName, true);
						}
						break;
					case SlotType.Color:
						_shaderMaterial.SetShaderParameter(param.ShaderParameterName, (Color)param.Value);
						break;
				}
				Parameters[i].Updated = false; // Reset the updated flag after applying the parameter to the shader
			}
		}
	}

	public virtual Godot.Collections.Dictionary Save()
	{
		// Textures come from connections, so only the directly-set values are saved
		var values = new Godot.Collections.Dictionary();
		foreach (var param in Parameters)
		{
			if (param.Value.VariantType == Variant.Type.Nil) continue;
			switch (param.Type)
			{
				case SlotType.Integer: values[param.Name] = (int)param.Value; break;
				case SlotType.Float: values[param.Name] = (float)param.Value; break;
				case SlotType.Color: values[param.Name] = GraphIO.ToArray((Color)param.Value); break;
			}
		}
		return new Godot.Collections.Dictionary
		{
			["parameters"] = values,
			["preview_visible"] = (GetNode<PanelContainer>(PreviewPanelPath).GetChild(0) as TextureRect).Visible,
		};
	}

	public virtual void Load(Godot.Collections.Dictionary data)
	{
		var values = (Godot.Collections.Dictionary)data["parameters"];
		foreach (var param in Parameters)
		{
			if (!values.ContainsKey(param.Name)) continue;
			switch (param.Type)
			{
				case SlotType.Integer: param.Value = (int)values[param.Name]; break;
				case SlotType.Float: param.Value = (float)values[param.Name]; break;
				case SlotType.Color: param.Value = GraphIO.ToColor(values[param.Name]); break;
			}
		}
		if (data.ContainsKey("preview_visible"))
		{
			bool visible = (bool)data["preview_visible"];
			GetNode<PanelContainer>(PreviewPanelPath).GetNode<CheckButton>("ShowHideButton").ButtonPressed = visible;
			OnShowHideButtonToggled(visible);
		}
	}

    Variant IGraphNode.GetOutputData(int outputSlot)
    {
        if (outputSlot == 0)
		{
			return GetPreviewSubViewport().GetTexture();
		}
		return default;
    }

    void IGraphNode.SetInputData(int inputSlot, Variant data)
    {
		if ( inputSlot >= Parameters.Length) return; // Invalid slot index
		var param = Parameters[inputSlot];
		switch (param.Type)
		{
			case SlotType.Integer:
				if (data.VariantType == Variant.Type.Int)				{
					Parameters[inputSlot].Value = (int)data;
					Parameters[inputSlot].Updated = true;
				}
				break;
			case SlotType.Float:
				if (data.VariantType == Variant.Type.Float)				{
					Parameters[inputSlot].Value = (float)data;
					Parameters[inputSlot].Updated = true;
				}
				break;
			case SlotType.Texture:
				if (data.VariantType == Variant.Type.Object && (GodotObject)data is Texture texture)
				{
					Parameters[inputSlot].Value = texture;
					Parameters[inputSlot].Updated = true;
				}
				break;
			case SlotType.Color:
				if (data.VariantType == Variant.Type.Color)
				{
					Parameters[inputSlot].Value = (Color)data;
					Parameters[inputSlot].Updated = true;
				}
				break;
		}
    }
}
