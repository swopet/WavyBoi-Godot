using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public class SubGraphPort
{
	public SlotType Type;
	public string Label;
}

/// <summary>
/// A node that holds its own graph. Data arriving on input N comes out of the inner Inputs node's
/// port N; data arriving on the inner Outputs node's port N leaves on output N.
/// </summary>
public partial class SubGraphNode : GraphNode, IGraphNode, IResolutionDependent, ISerializableNode
{
	public VisualsGraphEdit InnerGraph { get; private set; }
	public InputsNode Inputs { get; private set; }
	public OutputsNode Outputs { get; private set; }
	public List<SubGraphPort> InputPorts { get; private set; } = new();
	public List<SubGraphPort> OutputPorts { get; private set; } = new();

	private Variant[] inputValues = [];
	private Variant[] outputValues = [];
	private LineEdit nameEdit;

	public override void _Ready()
	{
		if (string.IsNullOrEmpty(Title)) Title = "SubGraph";

		var header = new HBoxContainer();
		nameEdit = new LineEdit { Text = Title, CustomMinimumSize = new Vector2(140, 0) };
		nameEdit.TextChanged += (text) =>
		{
			Title = text;
			InnerGraph?.Navigator?.Refresh();
		};
		var openButton = new Button { Text = "Open" };
		openButton.Pressed += () => InnerGraph.Navigator?.Enter(this);
		var saveButton = new Button { Text = "Save" };
		saveButton.Pressed += () => Dialogs.SaveNamed(this, "Save SubGraph", GraphIO.SubGraphDir, Title, Save);
		header.AddChild(nameEdit);
		header.AddChild(openButton);
		header.AddChild(saveButton);
		AddChild(header);

		// The inner graph lives beside the parent graph (not inside this node) so it can be shown full-size
		var parentGraph = GetParent<VisualsGraphEdit>();
		InnerGraph = parentGraph.CreateChildGraph();
		Inputs = (InputsNode)InnerGraph.CreateNode(VisualsGraphEdit.InputsType);
		Inputs.Name = "Inputs";
		Inputs.SubGraph = this;
		InnerGraph.AddChild(Inputs);
		// Start right of the control panel, which overlays the top-left of every graph
		Inputs.PositionOffset = new Vector2(220, 40);
		Outputs = (OutputsNode)InnerGraph.CreateNode(VisualsGraphEdit.OutputsType);
		Outputs.Name = "Outputs";
		Outputs.SubGraph = this;
		InnerGraph.AddChild(Outputs);
		Outputs.PositionOffset = new Vector2(900, 40);
		RebuildPorts();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationPredelete && IsInstanceValid(InnerGraph))
		{
			InnerGraph.QueueFree();
		}
	}

	public void SetPorts(List<SubGraphPort> inputs, List<SubGraphPort> outputs)
	{
		InputPorts = inputs;
		OutputPorts = outputs;
		inputValues = new Variant[inputs.Count];
		outputValues = new Variant[outputs.Count];
		RebuildPorts();
	}

	private void RebuildPorts()
	{
		// Keep the header row (child 0), rebuild one row per port pair
		while (GetChildCount() > 1)
		{
			var child = GetChild(GetChildCount() - 1);
			RemoveChild(child);
			child.QueueFree();
		}
		ClearAllSlots();
		int rows = Math.Max(InputPorts.Count, OutputPorts.Count);
		for (int i = 0; i < rows; i++)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label
			{
				Text = i < InputPorts.Count ? InputPorts[i].Label : "",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			});
			row.AddChild(new Label
			{
				Text = i < OutputPorts.Count ? OutputPorts[i].Label : "",
				HorizontalAlignment = HorizontalAlignment.Right,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			});
			AddChild(row);
			int slot = i + 1;
			if (i < InputPorts.Count)
			{
				SetSlotEnabledLeft(slot, true);
				SetSlotTypeLeft(slot, (int)InputPorts[i].Type);
			}
			if (i < OutputPorts.Count)
			{
				SetSlotEnabledRight(slot, true);
				SetSlotTypeRight(slot, (int)OutputPorts[i].Type);
			}
		}
		Inputs?.Rebuild(InputPorts);
		Outputs?.Rebuild(OutputPorts);
	}

	public Variant GetInputValue(int index) => index >= 0 && index < inputValues.Length ? inputValues[index] : default;

	public void SetOutputValue(int index, Variant data)
	{
		if (index >= 0 && index < outputValues.Length) outputValues[index] = data;
	}

	Variant IGraphNode.GetOutputData(int outputSlot) =>
		outputSlot >= 0 && outputSlot < outputValues.Length ? outputValues[outputSlot] : default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
		if (inputSlot >= 0 && inputSlot < inputValues.Length) inputValues[inputSlot] = data;
	}

	public void SetResolution(Vector2I resolution)
	{
		if (InnerGraph != null) InnerGraph.OutputResolution = resolution;
	}

	private static Godot.Collections.Array SavePorts(List<SubGraphPort> ports)
	{
		var array = new Godot.Collections.Array();
		foreach (var port in ports)
			array.Add(new Dictionary { ["type"] = (int)port.Type, ["label"] = port.Label });
		return array;
	}

	private static List<SubGraphPort> LoadPorts(Variant data)
	{
		return ((Godot.Collections.Array)data)
			.Select(v => (Dictionary)v)
			.Select(d => new SubGraphPort { Type = (SlotType)(int)d["type"], Label = (string)d["label"] })
			.ToList();
	}

	/// <summary>The whole SubGraph, including any SubGraphs nested inside it.</summary>
	public Dictionary Save()
	{
		return new Dictionary
		{
			["name"] = Title,
			["inputs"] = SavePorts(InputPorts),
			["outputs"] = SavePorts(OutputPorts),
			["graph"] = InnerGraph.SerializeGraph(),
		};
	}

	public void Load(Dictionary data)
	{
		Title = (string)data["name"];
		nameEdit.Text = Title;
		SetPorts(LoadPorts(data["inputs"]), LoadPorts(data["outputs"]));
		InnerGraph.LoadGraph((Dictionary)data["graph"]);
	}
}
