using Godot;
using System;

/// <summary>
/// Outputs the texture it received on the previous frame, so a pipeline can feed its own
/// output back into its input. Loops in the graph are only allowed through one of these.
/// Two viewports take turns: each frame one captures the input while the other (holding
/// last frame's capture) is the output, so nothing ever samples the viewport it's drawing.
/// </summary>
public partial class FrameBufferNode : GraphNode, IGraphNode, IResolutionDependent, ISerializableNode
{
	private readonly SubViewport[] viewports = new SubViewport[2];
	private readonly TextureRect[] captures = new TextureRect[2];
	private int writeIndex = 0;
	private Texture2D input;
	private TextureRect preview;
	private CheckButton holdButton;

	public bool Hold
	{
		get => holdButton?.ButtonPressed ?? false;
		set { if (holdButton != null) holdButton.ButtonPressed = value; }
	}

	private SubViewport ReadViewport => viewports[1 - writeIndex];

	public override void _Ready()
	{
		Title = "Frame Buffer";
		preview = new TextureRect
		{
			CustomMinimumSize = new Vector2(240, 135),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TooltipText = "Last frame's input",
		};
		AddChild(preview);
		for (int i = 0; i < 2; i++)
		{
			viewports[i] = new SubViewport
			{
				Size = new Vector2I(1920, 1080),
				RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
				RenderTargetClearMode = SubViewport.ClearMode.Always,
			};
			captures[i] = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
			};
			captures[i].SetAnchorsPreset(Control.LayoutPreset.FullRect);
			viewports[i].AddChild(captures[i]);
			preview.AddChild(viewports[i]); // under a Control so slot indices only count the rows
		}

		var controls = new HBoxContainer();
		var clearButton = new Button { Text = "Clear", TooltipText = "Blank the buffer" };
		clearButton.Pressed += Clear;
		holdButton = new CheckButton { Text = "Hold", TooltipText = "Freeze the buffered frame" };
		controls.AddChild(clearButton);
		controls.AddChild(holdButton);
		AddChild(controls);

		SetSlotEnabledLeft(0, true);
		SetSlotTypeLeft(0, (int)SlotType.Texture);
		SetSlotEnabledRight(0, true);
		SetSlotTypeRight(0, (int)SlotType.Texture);
		preview.Texture = ReadViewport.GetTexture();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || Hold) return;
		// Last frame's capture becomes the output; the other viewport captures this frame's input
		writeIndex = 1 - writeIndex;
		captures[writeIndex].Texture = input;
		viewports[writeIndex].RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		preview.Texture = ReadViewport.GetTexture();
	}

	/// <summary>Blank both frames (the input is captured again from the next frame).</summary>
	public void Clear()
	{
		for (int i = 0; i < 2; i++)
		{
			captures[i].Texture = null;
			viewports[i].RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}
	}

	public void SetResolution(Vector2I resolution)
	{
		foreach (var viewport in viewports) viewport.Size = resolution;
	}

	Variant IGraphNode.GetOutputData(int outputSlot) => outputSlot == 0 ? ReadViewport.GetTexture() : default;

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
		if (inputSlot == 0) input = data.VariantType == Variant.Type.Object ? data.As<Texture2D>() : null;
	}

	public Godot.Collections.Dictionary Save() => new() { ["hold"] = Hold };

	public void Load(Godot.Collections.Dictionary data)
	{
		if (data.ContainsKey("hold")) Hold = (bool)data["hold"];
	}
}
