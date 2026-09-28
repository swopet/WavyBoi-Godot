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
		ModulePortRows.Rebuild(this, ports, rightSide: true, new ModulePortRows.Editing
		{
			OnRange = (index, min, max, step) => Module != null && Module.SetPortRange(index, min, max, step),
			OnRename = (index, label) => Module != null && Module.RenamePort(true, index, label),
			OnDelete = index => Module?.RemoveInputPort(index),
			OnAdd = type => Module?.AddInputPort(type),
			AddText = "+ Add Input",
		});
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => Module?.GetInputValue(outputSlot) ?? default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
	}
}

internal static class ModulePortRows
{
	/// <summary>What the rows can change on the module (all optional).</summary>
	public class Editing
	{
		public Func<int, float, float, float, bool> OnRange;
		public Func<int, string, bool> OnRename;
		public Action<int> OnDelete;
		public Action<SlotType> OnAdd;
		public string AddText = "+ Add";
	}

	/// <summary>
	/// One row per port (child N carries slot N), then an "add" menu that has no slot.
	/// Inputs show their slot on the right, outputs on the left.
	/// </summary>
	public static void Rebuild(GraphNode node, List<ModulePort> ports, bool rightSide, Editing editing)
	{
		while (node.GetChildCount() > 0)
		{
			var child = node.GetChild(0);
			node.RemoveChild(child);
			child.QueueFree();
		}
		node.ClearAllSlots();
		for (int i = 0; i < ports.Count; i++)
		{
			node.AddChild(PortRow(ports[i], i, rightSide, editing));
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
		node.AddChild(AddMenu(editing));
	}

	private static MenuButton AddMenu(Editing editing)
	{
		var menu = new MenuButton { Text = editing.AddText, Flat = false, TooltipText = "Add a port" };
		var popup = menu.GetPopup();
		foreach (var type in ModuleNode.AddablePortTypes) popup.AddItem(type.ToString(), (int)type);
		popup.IdPressed += id => editing.OnAdd?.Invoke((SlotType)(int)id);
		return menu;
	}

	/// <summary>"[✕] [name] [min] – [max] step [step]" (range fields only for numeric inputs).</summary>
	private static HBoxContainer PortRow(ModulePort port, int index, bool isInputsNode, Editing editing)
	{
		var row = new HBoxContainer();
		var delete = new Button { Text = "✕", Flat = true, TooltipText = $"Delete this {(isInputsNode ? "input" : "output")} (and its wires)" };
		delete.Pressed += () => editing.OnDelete?.Invoke(index);
		var nameEdit = new LineEdit
		{
			Text = port.Label,
			TooltipText = $"{port.Type} {(isInputsNode ? "input" : "output")} name",
			CustomMinimumSize = new Vector2(130, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Alignment = isInputsNode ? HorizontalAlignment.Right : HorizontalAlignment.Left,
		};
		void CommitName()
		{
			if (nameEdit.Text.Trim() == port.Label) return;
			if (editing.OnRename == null || !editing.OnRename(index, nameEdit.Text)) nameEdit.Text = port.Label;
		}
		nameEdit.TextSubmitted += _ => CommitName();
		nameEdit.FocusExited += CommitName;

		row.AddChild(delete);
		row.AddChild(nameEdit);
		if (isInputsNode && port.IsNumeric && editing.OnRange != null) AddRangeFields(row, port, index, editing.OnRange);
		return row;
	}

	private static void AddRangeFields(HBoxContainer row, ModulePort port, int index, Func<int, float, float, float, bool> onRangeEdited)
	{
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
		row.AddChild(minField);
		row.AddChild(new Label { Text = "–" });
		row.AddChild(maxField);
		row.AddChild(new Label { Text = "step" });
		row.AddChild(stepField);
	}
}
