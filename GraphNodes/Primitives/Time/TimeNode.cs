using Godot;
using System;
using System.Globalization;

/// <summary>
/// A clock: outputs seconds, advancing by Speed each second. Wire it into animated shaders'
/// Time inputs to control or sync their animation; Pause freezes it, Reset returns to 0.
/// </summary>
public partial class TimeNode : GraphNode, IGraphNode, ISerializableNode
{
	private float time = 0.0f;
	private float speed = 1.0f;
	private Variant wiredSpeed; // Nil unless the Speed input is wired
	private LineEdit speedEdit;
	private CheckButton pauseButton;
	private Label timeLabel;

	public float Time => time;

	public float Speed
	{
		get => speed;
		set
		{
			speed = value;
			if (speedEdit != null) speedEdit.Text = speed.ToString("0.###", CultureInfo.InvariantCulture);
		}
	}

	public bool Paused
	{
		get => pauseButton?.ButtonPressed ?? false;
		set { if (pauseButton != null) pauseButton.ButtonPressed = value; }
	}

	public override void _Ready()
	{
		Title = "Time";
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = "Speed" });
		speedEdit = new LineEdit { CustomMinimumSize = new Vector2(56, 0), Alignment = HorizontalAlignment.Center, TooltipText = "Seconds per second (wire a Float in to drive it)" };
		speedEdit.TextSubmitted += _ => CommitSpeed();
		speedEdit.FocusExited += CommitSpeed;
		row.AddChild(speedEdit);
		AddChild(row);

		var controls = new HBoxContainer();
		pauseButton = new CheckButton { Text = "Pause" };
		var resetButton = new Button { Text = "Reset" };
		resetButton.Pressed += () => time = 0.0f;
		controls.AddChild(pauseButton);
		controls.AddChild(resetButton);
		AddChild(controls);

		timeLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		AddChild(timeLabel);

		SetSlotEnabledLeft(0, true);
		SetSlotTypeLeft(0, (int)SlotType.Float);
		SetSlotEnabledRight(0, true);
		SetSlotTypeRight(0, (int)SlotType.Float);
		Speed = speed;
	}

	private void CommitSpeed()
	{
		if (float.TryParse(speedEdit.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && float.IsFinite(value))
			Speed = value;
		else
			Speed = speed; // revert
	}

	public void Reset() => time = 0.0f;

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;
		float rate = wiredSpeed.VariantType == Variant.Type.Float ? (float)wiredSpeed : speed;
		if (!Paused) time += (float)delta * rate;
		timeLabel.Text = $"{time:0.00} s";
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => outputSlot == 0 ? time : default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
		if (inputSlot == 0) wiredSpeed = data; // Nil when unwired: the Speed field is used
	}

	public Godot.Collections.Dictionary Save() => new() { ["speed"] = speed, ["paused"] = Paused };

	public void Load(Godot.Collections.Dictionary data)
	{
		if (data.ContainsKey("speed")) Speed = (float)data["speed"];
		if (data.ContainsKey("paused")) Paused = (bool)data["paused"];
	}
}
