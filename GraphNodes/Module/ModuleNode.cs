using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class ModulePort
{
	public SlotType Type;
	public string Label;

	// Numeric (Float/Integer) inputs only: allowed range, and the step knob values snap to
	public float Min = 0.0f;
	public float Max = 1.0f;
	public float Step = 0.01f;

	public bool IsNumeric => Type is SlotType.Float or SlotType.Integer;

	public static ModulePort Create(SlotType type, string label) =>
		new ModulePort { Type = type, Label = label, Step = type == SlotType.Integer ? 1.0f : 0.01f };

	public static string Format(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

	public string RangeText => $"[{Format(Min)} – {Format(Max)}]";

	/// <summary>Change the range; false (and no change) if max isn't above min or step isn't positive.</summary>
	public bool TrySetRange(float min, float max, float step)
	{
		if (Type == SlotType.Integer)
		{
			min = Mathf.Round(min);
			max = Mathf.Round(max);
			step = Mathf.Max(1.0f, Mathf.Round(step));
		}
		if (!float.IsFinite(min) || !float.IsFinite(max) || !float.IsFinite(step) || max <= min || step <= 0.0f) return false;
		Min = min;
		Max = max;
		Step = step;
		return true;
	}

	/// <summary>
	/// Widen the range (to 1, 2 or 5 × a power of ten) so a value outside it fits:
	/// 77 -> 0-100, -30 -> -50-1. Values already inside are left alone.
	/// </summary>
	public void FitTo(float value)
	{
		if (!IsNumeric || !float.IsFinite(value) || (value >= Min && value <= Max)) return;
		static float RoundBound(float x)
		{
			if (x <= 1.0f) return 1.0f;
			float power = Mathf.Pow(10.0f, Mathf.Floor(Mathf.Log(x) / Mathf.Log(10.0f)));
			foreach (float m in new[] { 1.0f, 2.0f, 5.0f, 10.0f })
				if (m * power >= x) return m * power;
			return 10.0f * power;
		}
		float min = value < Min ? -RoundBound(-value) : Min;
		float max = value > Max ? RoundBound(value) : Max;
		TrySetRange(min, max, Step);
	}

	/// <summary>
	/// A knob's 0-1 is mapped onto the range and snapped to the step; any other number is clamped.
	/// Non-numeric data passes through.
	/// </summary>
	public Variant Apply(Variant data, bool fromKnob)
	{
		if (!IsNumeric || data.VariantType is not (Variant.Type.Float or Variant.Type.Int)) return data;
		float value = (float)data;
		float result = fromKnob
			? Mathf.Clamp(Min + Mathf.Round(Mathf.Clamp(value, 0.0f, 1.0f) * (Max - Min) / Step) * Step, Min, Max)
			: Mathf.Clamp(value, Min, Max);
		// Explicit Variants: "cond ? int : float" would promote the int to a float
		return Type == SlotType.Integer ? Variant.From(Mathf.RoundToInt(result)) : Variant.From(result);
	}
}

/// <summary>
/// A node that holds its own graph. Data arriving on input N comes out of the inner Inputs node's
/// port N; data arriving on the inner Outputs node's port N leaves on output N.
/// </summary>
public partial class ModuleNode : GraphNode, IGraphNode, IResolutionDependent, ISerializableNode
{
	public VisualsGraphEdit InnerGraph { get; private set; }
	public InputsNode Inputs { get; private set; }
	public OutputsNode Outputs { get; private set; }
	public List<ModulePort> InputPorts { get; private set; } = new();
	public List<ModulePort> OutputPorts { get; private set; } = new();

	private Variant[] inputValues = [];
	private Variant[] outputValues = [];
	private readonly List<Label> inputLabels = new();

	// Nodes moved in by a collapse, to space clear of the Inputs node once it's laid out
	public List<GraphNode> PendingClearOfInputs;
	private LineEdit nameEdit;

	public override void _Ready()
	{
		if (string.IsNullOrEmpty(Title)) Title = "Module";
		EnsureUniqueName();

		var header = new HBoxContainer();
		nameEdit = new LineEdit { Text = Title, CustomMinimumSize = new Vector2(140, 0), TooltipText = "Module name (unique in the project)" };
		nameEdit.TextSubmitted += (text) => CommitNameEdit();
		nameEdit.FocusExited += CommitNameEdit;
		var openButton = new Button { Text = "Open" };
		openButton.Pressed += () => InnerGraph.Navigator?.Enter(this);
		var saveButton = new Button { Text = "Save" };
		saveButton.Pressed += () => Dialogs.SaveNamed(this, "Save Module", GraphIO.ModuleDir, Title, Save);
		header.AddChild(nameEdit);
		header.AddChild(openButton);
		header.AddChild(saveButton);
		AddChild(header);

		// The inner graph lives beside the parent graph (not inside this node) so it can be shown full-size
		var parentGraph = GetParent<VisualsGraphEdit>();
		InnerGraph = parentGraph.CreateChildGraph();
		Inputs = (InputsNode)InnerGraph.CreateNode(VisualsGraphEdit.InputsType);
		Inputs.Name = "Inputs";
		Inputs.Module = this;
		InnerGraph.AddChild(Inputs);
		// Start right of the control panel, which overlays the top-left of every graph
		Inputs.PositionOffset = new Vector2(220, 40);
		Outputs = (OutputsNode)InnerGraph.CreateNode(VisualsGraphEdit.OutputsType);
		Outputs.Name = "Outputs";
		Outputs.Module = this;
		InnerGraph.AddChild(Outputs);
		Outputs.PositionOffset = new Vector2(900, 40);
		RebuildPorts();
	}

	private VisualsGraphEdit ParentGraph => GetParent() as VisualsGraphEdit;

	private bool IsNameTaken(string name)
	{
		var graph = ParentGraph;
		if (graph == null) return false;
		return graph.AllModules().Any(other => other != this
			&& string.Equals(other.Title, name, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Rename the module. Fails (returns false) if the name is empty or used by another module
	/// anywhere in the project, including inside other modules.
	/// </summary>
	public bool TryRename(string name)
	{
		name = name.Trim();
		if (name == "" || IsNameTaken(name)) return false;
		Title = name;
		if (nameEdit != null) nameEdit.Text = name;
		ParentGraph?.Navigator?.Refresh();
		return true;
	}

	/// <summary>Add " 2", " 3", ... if another module already has this name.</summary>
	private void EnsureUniqueName()
	{
		string name = Title.Trim() == "" ? "Module" : Title.Trim();
		if (IsNameTaken(name))
		{
			// Count up from an existing number ("Lead 2" -> "Lead 3", not "Lead 2 2")
			string baseName = System.Text.RegularExpressions.Regex.Replace(name, @" \d+$", "");
			int n = 2;
			while (IsNameTaken($"{baseName} {n}")) n++;
			name = $"{baseName} {n}";
		}
		Title = name;
		if (nameEdit != null) nameEdit.Text = name;
		ParentGraph?.Navigator?.Refresh();
	}

	private void CommitNameEdit()
	{
		string requested = nameEdit.Text.Trim();
		if (requested == Title) return;
		if (!TryRename(requested))
		{
			nameEdit.Text = Title;
			if (requested != "")
				Dialogs.ShowMessage(this, "Rename Module", $"A module named '{requested}' already exists in this project.");
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationPredelete && IsInstanceValid(InnerGraph))
		{
			InnerGraph.QueueFree();
		}
	}

	public void SetPorts(List<ModulePort> inputs, List<ModulePort> outputs)
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
		inputLabels.Clear();
		int rows = Math.Max(InputPorts.Count, OutputPorts.Count);
		for (int i = 0; i < rows; i++)
		{
			var row = new HBoxContainer();
			var inputLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			row.AddChild(inputLabel);
			if (i < InputPorts.Count) inputLabels.Add(inputLabel);
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
		UpdateInputLabels();
		Inputs?.Rebuild(InputPorts);
		Outputs?.Rebuild(OutputPorts);
	}

	private void UpdateInputLabels()
	{
		for (int i = 0; i < inputLabels.Count; i++)
		{
			var port = InputPorts[i];
			inputLabels[i].Text = port.IsNumeric ? $"{port.Label} {port.RangeText}" : port.Label;
		}
	}

	/// <summary>Set a numeric input's allowed range and step (from the Inputs node's fields).</summary>
	public bool SetPortRange(int index, float min, float max, float step)
	{
		if (index < 0 || index >= InputPorts.Count || !InputPorts[index].IsNumeric) return false;
		if (!InputPorts[index].TrySetRange(min, max, step)) return false;
		UpdateInputLabels();
		return true;
	}

	/// <summary>Store incoming data for an input, mapped (from a knob) or clamped to its range.</summary>
	public void SetInput(int index, Variant data, bool fromKnob)
	{
		if (index >= 0 && index < inputValues.Length) inputValues[index] = InputPorts[index].Apply(data, fromKnob);
	}

	public Variant GetInputValue(int index) => index >= 0 && index < inputValues.Length ? inputValues[index] : default;

	public void SetOutputValue(int index, Variant data)
	{
		if (index >= 0 && index < outputValues.Length) outputValues[index] = data;
	}

	Variant IGraphNode.GetOutputData(int outputSlot) =>
		outputSlot >= 0 && outputSlot < outputValues.Length ? outputValues[outputSlot] : default;

	void IGraphNode.SetInputData(int inputSlot, Variant data) => SetInput(inputSlot, data, fromKnob: false);

	public void SetResolution(Vector2I resolution)
	{
		if (InnerGraph != null) InnerGraph.OutputResolution = resolution;
	}

	private static Godot.Collections.Array SavePorts(List<ModulePort> ports)
	{
		var array = new Godot.Collections.Array();
		foreach (var port in ports)
		{
			var entry = new Dictionary { ["type"] = (int)port.Type, ["label"] = port.Label };
			if (port.IsNumeric)
			{
				entry["min"] = port.Min;
				entry["max"] = port.Max;
				entry["step"] = port.Step;
			}
			array.Add(entry);
		}
		return array;
	}

	private static List<ModulePort> LoadPorts(Variant data)
	{
		return ((Godot.Collections.Array)data)
			.Select(v => (Dictionary)v)
			.Select(d =>
			{
				var port = ModulePort.Create((SlotType)(int)d["type"], (string)d["label"]);
				// Files from before ranges existed get the defaults
				if (d.ContainsKey("min") && d.ContainsKey("max") && d.ContainsKey("step"))
					port.TrySetRange((float)d["min"], (float)d["max"], (float)d["step"]);
				return port;
			})
			.ToList();
	}

	/// <summary>The whole Module, including any Modules nested inside it.</summary>
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
		EnsureUniqueName(); // loading a saved module twice gives "Name" and "Name 2"
		SetPorts(LoadPorts(data["inputs"]), LoadPorts(data["outputs"]));
		InnerGraph.LoadGraph((Dictionary)data["graph"]);
	}
}
