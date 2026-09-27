using Godot;
using System.Collections.Generic;

/// <summary>
/// Inside a Module: exposes the Module node's inputs as output ports.
/// </summary>
public partial class InputsNode : GraphNode, IGraphNode
{
	public ModuleNode Module;

	public override void _Ready()
	{
		Title = "Inputs";
	}

	public void Rebuild(List<ModulePort> ports)
	{
		ModulePortRows.Rebuild(this, ports, rightSide: true);
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => Module?.GetInputValue(outputSlot) ?? default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
	}
}

internal static class ModulePortRows
{
	public static void Rebuild(GraphNode node, List<ModulePort> ports, bool rightSide)
	{
		while (node.GetChildCount() > 0)
		{
			var child = node.GetChild(0);
			node.RemoveChild(child);
			child.QueueFree();
		}
		node.ClearAllSlots();
		if (ports.Count == 0)
		{
			node.AddChild(new Label { Text = "(none)" });
			return;
		}
		for (int i = 0; i < ports.Count; i++)
		{
			node.AddChild(new Label
			{
				Text = ports[i].Label,
				HorizontalAlignment = rightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left,
			});
			if (rightSide)
			{
				node.SetSlotEnabledRight(i, true);
				node.SetSlotTypeRight(i, (int)ports[i].Type);
			}
			else
			{
				node.SetSlotEnabledLeft(i, true);
				node.SetSlotTypeLeft(i, (int)ports[i].Type);
			}
		}
	}
}
