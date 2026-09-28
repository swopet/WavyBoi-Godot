using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

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
		ModulePortRows.Rebuild(this, ports, rightSide: true, onRangeEdited: (index, min, max, step) =>
			Module != null && Module.SetPortRange(index, min, max, step));
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => Module?.GetInputValue(outputSlot) ?? default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
	}
}

internal static class ModulePortRows
{
	public static void Rebuild(GraphNode node, List<ModulePort> ports, bool rightSide,
		Func<int, float, float, float, bool> onRangeEdited = null)
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
			var label = new Label
			{
				Text = ports[i].Label,
				HorizontalAlignment = rightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left,
			};
			if (onRangeEdited != null && ports[i].IsNumeric)
				node.AddChild(RangeRow(label, ports[i], i, onRangeEdited));
			else
				node.AddChild(label);
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

	/// <summary>"Label  [min] – [max]  step [step]" for annotating a numeric input.</summary>
	private static HBoxContainer RangeRow(Label label, ModulePort port, int index, Func<int, float, float, float, bool> onRangeEdited)
	{
		var row = new HBoxContainer();
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		LineEdit Field(string tooltip) => new LineEdit
		{
			TooltipText = tooltip,
			CustomMinimumSize = new Vector2(56, 0),
			Alignment = HorizontalAlignment.Center,
			SelectAllOnFocus = true,
		};
		var minField = Field("Minimum value");
		var maxField = Field("Maximum value");
		var stepField = Field("Step (knob values snap to this)");
		void Show()
		{
			minField.Text = ModulePort.Format(port.Min);
			maxField.Text = ModulePort.Format(port.Max);
			stepField.Text = ModulePort.Format(port.Step);
		}
		static bool Parse(LineEdit field, out float value) =>
			float.TryParse(field.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		void Commit()
		{
			// Applies all three together; anything invalid reverts to the current range
			if (Parse(minField, out float min) && Parse(maxField, out float max) && Parse(stepField, out float step))
				onRangeEdited(index, min, max, step);
			Show();
		}
		foreach (var field in new[] { minField, maxField, stepField })
		{
			field.TextSubmitted += _ => Commit();
			field.FocusExited += Commit;
		}
		Show();
		row.AddChild(label);
		row.AddChild(minField);
		row.AddChild(new Label { Text = "–" });
		row.AddChild(maxField);
		row.AddChild(new Label { Text = "step" });
		row.AddChild(stepField);
		return row;
	}
}
