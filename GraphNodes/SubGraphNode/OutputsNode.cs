using Godot;
using System.Collections.Generic;

/// <summary>
/// Inside a SubGraph: whatever is connected to input port N leaves the SubGraph node on output N.
/// </summary>
public partial class OutputsNode : GraphNode, IGraphNode
{
	public SubGraphNode SubGraph;

	public override void _Ready()
	{
		Title = "Outputs";
	}

	public void Rebuild(List<SubGraphPort> ports)
	{
		SubGraphPortRows.Rebuild(this, ports, rightSide: false);
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
		SubGraph?.SetOutputValue(inputSlot, data);
	}
}
