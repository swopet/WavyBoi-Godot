using Godot;
using System.Collections.Generic;

/// <summary>
/// Inside a Module: whatever is connected to input port N leaves the Module node on output N.
/// </summary>
public partial class OutputsNode : GraphNode, IGraphNode
{
	public ModuleNode Module;

	public override void _Ready()
	{
		Title = "Outputs";
	}

	public void Rebuild(List<ModulePort> ports)
	{
		ModulePortRows.Rebuild(this, ports, rightSide: false, new ModulePortRows.Editing
		{
			OnRename = (index, label) => Module != null && Module.RenamePort(false, index, label),
			OnDelete = index => Module?.RemoveOutputPort(index),
			OnAdd = type => Module?.AddOutputPort(type),
			AddText = "+ Add Output",
		});
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
		Module?.SetOutputValue(inputSlot, data);
	}
}
