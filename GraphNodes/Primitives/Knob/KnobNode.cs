using Godot;
using System;

/// <summary>
/// Outputs 0-1 from a knob. 0 points at 260°, turning clockwise to 1 at -80°
/// (math angles: 0° = right, counter-clockwise positive), leaving the gap at the bottom.
/// Drag up/down or scroll to turn; hold Shift for fine control.
/// </summary>
public partial class KnobNode : GraphNode, IGraphNode, ISerializableNode
{
	public const float StartAngleDegrees = 260.0f;
	public const float EndAngleDegrees = -80.0f;
	private const float DragPixelsForFullTurn = 200.0f;
	private const float WheelStep = 0.05f;
	private const float FineFactor = 0.1f;

	private static readonly Color FaceColor = new Color(0.25f, 0.25f, 0.25f); // dark grey
	private static readonly Color OutlineColor = Colors.Black;
	private static readonly Color IndicatorColor = new Color(0.95f, 0.95f, 0.95f);

	private float value = 0.0f;
	private Control dial;
	private Label valueLabel;
	private bool dragging = false;

	public float Value
	{
		get => value;
		set
		{
			this.value = Mathf.Clamp(value, 0.0f, 1.0f);
			if (valueLabel != null) valueLabel.Text = this.value.ToString("0.00");
			dial?.QueueRedraw();
		}
	}

	/// <summary>Knob angle in degrees for a value, stepping clockwise from 260° to -80°.</summary>
	public static float AngleForValue(float v) => Mathf.Lerp(StartAngleDegrees, EndAngleDegrees, Mathf.Clamp(v, 0.0f, 1.0f));

	public override void _Ready()
	{
		Title = "Knob";
		dial = new Control
		{
			CustomMinimumSize = new Vector2(64, 64),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			TooltipText = "Drag up/down or scroll to turn (Shift: fine)",
		};
		dial.Draw += DrawDial;
		dial.GuiInput += OnDialInput;
		AddChild(dial);
		valueLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		AddChild(valueLabel);
		SetSlotEnabledRight(0, true);
		SetSlotTypeRight(0, (int)SlotType.Float);
		Value = value;
	}

	private void DrawDial()
	{
		Vector2 center = dial.Size / 2;
		float radius = Mathf.Min(dial.Size.X, dial.Size.Y) / 2 - 2;
		dial.DrawCircle(center, radius, FaceColor);
		dial.DrawArc(center, radius, 0, Mathf.Tau, 48, OutlineColor, 2.0f, true);
		// Screen y points down, so flip the sine to keep math angles counter-clockwise
		float angle = Mathf.DegToRad(AngleForValue(value));
		var direction = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle));
		dial.DrawLine(center + direction * radius, center + direction * radius / 2, IndicatorColor, 3.0f, true);
	}

	private void OnDialInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				dragging = button.Pressed;
				dial.AcceptEvent(); // don't let the graph start dragging the node
				break;
			case InputEventMouseMotion motion when dragging:
				float scale = motion.ShiftPressed ? FineFactor : 1.0f;
				Value -= motion.Relative.Y / DragPixelsForFullTurn * scale; // up turns clockwise
				dial.AcceptEvent();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
				float step = WheelStep * (wheel.ShiftPressed ? FineFactor : 1.0f);
				Value += wheel.ButtonIndex == MouseButton.WheelUp ? step : -step;
				dial.AcceptEvent(); // don't zoom the graph
				break;
		}
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => outputSlot == 0 ? value : default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
	}

	public Godot.Collections.Dictionary Save() => new() { ["value"] = value };

	public void Load(Godot.Collections.Dictionary data) => Value = (float)data["value"];
}
