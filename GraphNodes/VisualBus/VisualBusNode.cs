using Godot;
using Godot.Collections;
using System;
using System.Threading.Tasks;

struct BusData
{
	public VisualBus Bus;
	public float LastSetWeight;
	public float CurrentWeight;
	public float TargetWeight;
	public float CalculatedWeight;
}

public partial class VisualBusNode : GraphNode, IGraphNode
{
	[Signal]
	public delegate void BusDeletedEventHandler(int index);
	
	[Export] public NodePath FadeSpeedLineEditPath;
	[Export] public NodePath PreviewSubViewportPath;
	private float fadeTime = 0.5f;
	private bool exclusive = false;
	private BusData[] buses = [];
	private Dictionary<VisualBus, int> busIndices = new Dictionary<VisualBus, int>();
	private float[] fadeTimes = [];  // Track elapsed time for each bus fade
	private VisualBus priority_bus;

	private PackedScene VisualBusScene = ResourceLoader.Load<PackedScene>("res://GraphNodes/VisualBus/VisualBus.tscn");
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SetSlotEnabledLeft(1, true);
		SetSlotTypeLeft(1, (int)SlotType.Texture);
		buses = [new BusData { Bus = GetChild<VisualBus>(1), LastSetWeight = 1.0f, CurrentWeight = 1.0f, TargetWeight = 1.0f, CalculatedWeight = 1.0f }];
		fadeTimes = [0.0f];
		GD.Print($"Initialized VisualBusNode with bus: {buses[0].Bus.Name}");
		priority_bus = buses[0].Bus;
		ConnectBusSignals(buses[0].Bus);
		busIndices[buses[0].Bus] = 0;
		buses[0].Bus.UpdateWeight(1.0f);
	}

	private void ConnectBusSignals(VisualBus bus)
	{
		bus.GetNode<Button>(bus.DeleteButtonPath).Pressed += () => OnBusDeleteButtonPressed(bus);
		bus.GetNode<Button>(bus.FadeInButtonPath).Pressed += () => OnBusFadeInButtonPressed(bus);
		bus.GetNode<Button>(bus.FadeOutButtonPath).Pressed += () => OnBusFadeOutButtonPressed(bus);
		bus.GetNode<Button>(bus.SetButtonPath).Pressed += () => OnBusSetButtonPressed(bus);
	}

	public Texture GetOutputTexture()
	{
		return GetNode<SubViewport>(PreviewSubViewportPath).GetTexture();
	}

	private void AddBus()
	{
		if (buses.Length >= 4) return; // Limit to 4 buses for now
		if (buses.Length == 1) buses[0].Bus.ToggleDeleteButton(true);
		var new_bus = VisualBusScene.Instantiate<VisualBus>();
		new_bus.Name = $"Bus{buses.Length + 1}";
		AddChild(new_bus);
		MoveChild(new_bus, buses.Length + 1); // Ensure the new bus is at the end of the children list (after the existing buses)
		SetSlotEnabledLeft(buses.Length + 1, true);
		SetSlotTypeLeft(buses.Length + 1, (int)SlotType.Texture);
		System.Array.Resize(ref buses, buses.Length + 1);
		System.Array.Resize(ref fadeTimes, fadeTimes.Length + 1);
		buses[^1] = new BusData { Bus = new_bus, LastSetWeight = 0.0f, CurrentWeight = 0.0f, TargetWeight = 0.0f, CalculatedWeight = 0.0f };
		fadeTimes[^1] = 0.0f;
		busIndices[new_bus] = buses.Length - 1;
		new_bus.UpdateWeight(0.0f);
		ConnectBusSignals(new_bus);
		var new_sprite = new Sprite2D { Texture = (new_bus.GetChild(0) as TextureRect).Texture };
		new_sprite.Modulate = new Color(1, 1, 1, 0); // Start fully transparent
		new_sprite.Material = new CanvasItemMaterial
		{
			BlendMode = CanvasItemMaterial.BlendModeEnum.Add
		};
		new_sprite.Offset = PreviewSubViewportPath == null ? Vector2.Zero : GetNode<SubViewport>(PreviewSubViewportPath).Size / 2;
		GetNode(PreviewSubViewportPath).AddChild(new_sprite); // Add a Sprite2D to the preview viewport to show this bus's output
	}

	private void OnBusSetButtonPressed(VisualBus bus)
	{
		
		priority_bus = bus;
		buses[busIndices[bus]].TargetWeight = 1.0f;
		buses[busIndices[bus]].LastSetWeight = buses[busIndices[bus]].CurrentWeight;
		if (exclusive)
		{
			for (int i = 0; i < buses.Length; i++)
			{
				if (priority_bus != buses[i].Bus)
				{
					buses[i].TargetWeight = 0.0f;
					buses[i].LastSetWeight = buses[i].CurrentWeight;
				}
			}
		}
	}

	private void OnBusFadeInButtonPressed(VisualBus bus)
	{
		priority_bus = bus;
		buses[busIndices[bus]].TargetWeight = 1.0f;
		buses[busIndices[bus]].LastSetWeight = buses[busIndices[bus]].CurrentWeight;
		for (int i = 0; i < buses.Length; i++)
		{
			GD.Print($"Bus {i}: TargetWeight={buses[i].TargetWeight}");
		}
	}

	private void OnBusFadeOutButtonPressed(VisualBus bus)
	{
		GD.Print(buses.Length, " buses");
		if (priority_bus == bus)
		{
			priority_bus = null;
		}
		buses[busIndices[bus]].TargetWeight = 0.0f;
		buses[busIndices[bus]].LastSetWeight = buses[busIndices[bus]].CurrentWeight;
		for (int i = 0; i < buses.Length; i++)
		{
			GD.Print($"Bus {i}: TargetWeight={buses[i].TargetWeight}");
			if (buses[i].TargetWeight > 0.01f && priority_bus == null)
			{
				priority_bus = buses[i].Bus;
			}
		}

	}

	private void OnBusDeleteButtonPressed(VisualBus bus)
	{
		DeleteBus(busIndices[bus]);
	}

	private void DeleteBus(int index)
	{
		if (buses.Length == 1) return; // Don't allow deleting the last bus
		GD.Print($"Deleting bus at index {index}");
		if (index >= buses.Length) return;
		
		// Emit signal to notify graph of deletion
		EmitSignal(SignalName.BusDeleted, index);
		
		buses[index].Bus.Name = $"Bus{index + 1}_Deleting";
		buses[index].Bus.QueueFree();
		
		for (int i = index; i < buses.Length - 1; i++)
		{
			buses[i] = buses[i + 1];
			busIndices[buses[i].Bus] = i;
			buses[i].Bus.Name = $"Bus{i + 1}";
		}
		System.Array.Resize(ref buses, buses.Length - 1);
		System.Array.Resize(ref fadeTimes, fadeTimes.Length - 1);
		GetNode(PreviewSubViewportPath).GetChild<Sprite2D>(index).QueueFree();
		SetSlotEnabledLeft(buses.Length + 1, false);
		if (buses.Length == 1) buses[0].Bus.ToggleDeleteButton(false);
	}

	Variant IGraphNode.GetOutputData(int outputSlot)
	{
		return default;
	}

	void IGraphNode.SetInputData(int inputSlot, Variant data)
	{
		int busIndex = inputSlot;
		buses[busIndex].Bus.GetChild<TextureRect>(0).Texture = (Texture2D)data;
	
	}

	public void OnFadeSpeedLineEditTextSubmitted(string text)
	{
		if (float.TryParse(text, out float newFadeTime))
		{
			fadeTime = newFadeTime;
		}
		else
		{
			GetNode<LineEdit>(FadeSpeedLineEditPath).Text = fadeTime.ToString();
		}
	}

	public void OnExclusiveButtonToggled(bool pressed)
	{
		exclusive = pressed;
		if (exclusive)
		{
			for (int i = 0; i < buses.Length; i++)
			{
				if (priority_bus == buses[i].Bus)
				{
					buses[i].TargetWeight = 1.0f;
				}
				else
				{
					buses[i].TargetWeight = 0.0f;
				}
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		float totalWeight = 0.0f;
		for (int i = 0; i < buses.Length; i++)
		{
			if (buses[i].TargetWeight != buses[i].CurrentWeight)
			{
				fadeTimes[i] += (float)delta;
				float t = Mathf.Clamp(fadeTimes[i] / fadeTime,0,1);
				buses[i].CurrentWeight = Mathf.Lerp(buses[i].LastSetWeight, buses[i].TargetWeight, t);
				
				if (t >= 1.0f)
				{
					buses[i].CurrentWeight = buses[i].TargetWeight;
					buses[i].LastSetWeight = buses[i].CurrentWeight;
					fadeTimes[i] = 0.0f;
				}
			}
			totalWeight += buses[i].CurrentWeight;
			var sprite = GetNode(PreviewSubViewportPath).GetChild<Sprite2D>(i);
			var busTexture = (buses[i].Bus.GetChild(0) as TextureRect).Texture;
			sprite.Texture = busTexture;
		}
		for (int i = 0; i < buses.Length; i++)
		{
			var sprite = GetNode(PreviewSubViewportPath).GetChild<Sprite2D>(i);
			float displayWeight = totalWeight >= 1 ? buses[i].CurrentWeight / totalWeight : buses[i].CurrentWeight;
			sprite.Modulate = new Color(1, 1, 1, displayWeight);
			buses[i].Bus.UpdateWeight(displayWeight);
		}
	}
}
