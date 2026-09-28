using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class VisualsGraphEdit : Godot.GraphEdit
{
	private Spectrum _spectrum;
	public Spectrum spectrum {
		get => _spectrum;
		set {
			_spectrum = value;
		}
	}
	private VisualBusNode visualBusNode;

	// Resolution every shader node and the output bus render at. Set by the Controller
	// (auto-detected from the display, or a user override).
	private Vector2I outputResolution = new Vector2I(1920, 1080);
	public Vector2I OutputResolution
	{
		get => outputResolution;
		set
		{
			outputResolution = value;
			foreach (var node in GetChildren().OfType<IResolutionDependent>())
			{
				node.SetResolution(outputResolution);
			}
		}
	}
	
	public Texture GetOutputTexture(){
		return visualBusNode?.GetOutputTexture();
	}

	private NodeList nodeList;

	// Node type keys, stored as metadata so graphs can be saved and rebuilt
	public const string TypeMeta = "graph_type";
	public const string ModuleType = "Module";
	public const string InputsType = "ModuleInputs";
	public const string OutputsType = "ModuleOutputs";
	public const string OutputBusType = "OutputBus";
	private static readonly System.Collections.Generic.Dictionary<string, string> LegacyTypeKeys = new()
	{
		["SubGraph"] = ModuleType, ["SubGraphInputs"] = InputsType, ["SubGraphOutputs"] = OutputsType,
	};
	private const string PrimitivePrefix = "Primitive/";
	private const string ShaderPrefix = "Shader/";

	// Only the main graph starts with the demo nodes
	public bool SpawnDefaultNodes = true;
	public GraphNavigator Navigator;

	/// <summary>The main graph of the project this graph belongs to.</summary>
	public VisualsGraphEdit ProjectRoot => Navigator?.Root ?? this;

	/// <summary>Every module in the project, including ones nested inside other modules.</summary>
	public IEnumerable<ModuleNode> AllModules()
	{
		static IEnumerable<ModuleNode> Walk(VisualsGraphEdit graph)
		{
			foreach (var module in graph.GetChildren().OfType<ModuleNode>())
			{
				yield return module;
				if (module.InnerGraph != null)
					foreach (var nested in Walk(module.InnerGraph)) yield return nested;
			}
		}
		return Walk(ProjectRoot);
	}
	// Where Module editors are added (beside the main graph, under the UI)
	public Node Host;

	public void CreateVisualBus(){
		visualBusNode = nodeList.VisualBus.Instantiate<VisualBusNode>();
		visualBusNode.SetMeta(TypeMeta, OutputBusType); // saved with projects, never created from the menu
		SpawnAtClosestAvailable(visualBusNode, new Vector2(0, 0));
		
		// Connect bus deletion signal
		visualBusNode.BusDeleted += (index) => OnInputSlotDelete(visualBusNode.Name, index);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (Engine.IsEditorHint()){
			PopulateTypeNames();
			return; // Don't spawn runtime nodes into the scene being edited
		}
		EnableTypeConnections();
		
		nodeList = ResourceLoader.Load<NodeList>("res://GraphNodes/MasterList.tres");
		ConnectionRequest += OnConnectionRequest;
		DisconnectionRequest += OnDisconnectionRequest;
		PopupRequest += OnPopupRequest;
		DeleteNodesRequest += OnDeleteNodesRequest;
		if (!SpawnDefaultNodes) return;

		for (int i = 0; i < 2; i++)
		{
			AddPrimitive("Float");
		}
		AddPrimitive("Gradient");

		for (int i = 0; i < 5; i++)
		{
			AddShader("BwGrid");
		}
		AddShader("BwGradient");
		for (int i = 0; i < 2; i++)
		{
			AddShader("GaussianBlur");
		}
		for(int i = 0; i < 2; i++)
		{
			AddShader("BwSpiral");
		}
	}

	public GraphNode AddPrimitive(string primitiveName, Vector2 position = default)
	{
		return AddNode(PrimitivePrefix + primitiveName, position);
	}

	public GraphNode AddShader(string shaderName, Vector2 position = default)
	{
		return AddNode(ShaderPrefix + shaderName, position);
	}

	public GraphNode AddNode(string typeKey, Vector2 position)
	{
		var node = CreateNode(typeKey);
		if (node != null) SpawnAtClosestAvailable(node, position);
		return node;
	}

	/// <summary>
	/// Instantiate a node from its type key ("Primitive/Float", "Shader/BwGrid", "Module", ...)
	/// without adding it to the graph.
	/// </summary>
	public GraphNode CreateNode(string typeKey)
	{
		// Files saved before modules were renamed from "SubGraph"
		if (LegacyTypeKeys.TryGetValue(typeKey, out var renamed)) typeKey = renamed;
		GraphNode node = null;
		if (typeKey.StartsWith(PrimitivePrefix)
			&& nodeList.Primitives.TryGetValue(typeKey.Substring(PrimitivePrefix.Length), out var primitiveScene))
		{
			node = primitiveScene.Instantiate<GraphNode>();
		}
		else if (typeKey.StartsWith(ShaderPrefix)
			&& nodeList.Shaders.TryGetValue(typeKey.Substring(ShaderPrefix.Length), out var shaderScene))
		{
			node = shaderScene.Instantiate<ShaderNode>();
			node.Title = typeKey.Substring(ShaderPrefix.Length); // Set the node name to the shader name for easier identification
		}
		else if (typeKey == ModuleType) node = new ModuleNode();
		else if (typeKey == InputsType) node = new InputsNode();
		else if (typeKey == OutputsType) node = new OutputsNode();

		if (node == null)
		{
			GD.PushWarning($"Unknown graph node type '{typeKey}'");
			return null;
		}
		node.SetMeta(TypeMeta, typeKey);
		return node;
	}

	/// <summary>
	/// Create the (hidden) graph editor for a Module, set up like this one.
	/// </summary>
	public VisualsGraphEdit CreateChildGraph()
	{
		var graph = new VisualsGraphEdit
		{
			SpawnDefaultNodes = false,
			Navigator = Navigator,
			Host = Host,
			Visible = false,
			outputResolution = outputResolution,
			RightDisconnects = RightDisconnects,
			GridPattern = GridPattern,
			ShowMenu = ShowMenu,
			ShowZoomButtons = ShowZoomButtons,
			ShowGridButtons = ShowGridButtons,
			TypeNames = TypeNames.Duplicate(),
		};
		graph.SetAnchorsPreset(LayoutPreset.FullRect);
		var host = Host ?? GetParent();
		host.AddChild(graph);
		// Right after this graph: below the UI, and processed after its parent graph each frame
		host.MoveChild(graph, GetIndex() + 1);
		return graph;
	}

	public void SpawnAtClosestAvailable(GraphNode newNode, Vector2 targetPos)
    {
        // 1. Add to tree so Size is calculated
        AddChild(newNode, true); // readable names ("BWGrid2", not "@BWGrid@12") survive save/load unchanged
        newNode.PositionOffset = targetPos;

        // 2. Setup search parameters
        float step = 20.0f; // How far to jump each check
        float maxRadius = 1000.0f;
        Vector2 foundPos = targetPos;
        bool found = false;

        // 3. Spiral search (Square rings)
        for (float r = 0; r < maxRadius; r += step)
        {
            if (r == 0)
            {
                if (!IsOverlapping(newNode, targetPos)) { found = true; break; }
                continue;
            }

            // Check points along the square ring at distance r
            foreach (Vector2 offset in GetPointsOnRing(r, step))
            {
                Vector2 testPos = targetPos + offset;
                if (!IsOverlapping(newNode, testPos))
                {
                    foundPos = testPos;
                    found = true;
                    break;
                }
            }
            if (found) break;
        }

        newNode.PositionOffset = foundPos;
        (newNode as IResolutionDependent)?.SetResolution(outputResolution);
    }

	private Vector2 ToGraphPosition(Vector2 localPosition) => (localPosition + ScrollOffset) / Zoom;

	private GraphNode GetNodeAt(Vector2 localPosition)
	{
		// Topmost first, using the full transform so zoom is respected
		Vector2 point = GetGlobalTransformWithCanvas() * localPosition;
		return GetChildren().OfType<GraphNode>().LastOrDefault(node => node.Visible
			&& (node.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, node.Size)).HasPoint(point));
	}

	private void OnPopupRequest(Vector2 atPosition)
	{
		// Only on empty canvas: right-clicks on nodes bubble up to the graph too
		if (GetNodeAt(atPosition) != null) return;
		Vector2 spawnPosition = ToGraphPosition(atPosition);
		var menu = new PopupMenu();
		var actions = new List<System.Action>();
		void AddAction(PopupMenu target, string label, System.Action action, bool disabled = false)
		{
			target.AddItem(label, actions.Count);
			target.SetItemDisabled(target.ItemCount - 1, disabled);
			actions.Add(action);
		}
		PopupMenu AddSubmenu(PopupMenu parent, string label)
		{
			var submenu = new PopupMenu();
			submenu.IdPressed += id => actions[(int)id]();
			parent.AddSubmenuNodeItem(label, submenu);
			return submenu;
		}
		void AddList(PopupMenu target, IEnumerable<string> items, System.Action<string> onPick)
		{
			int before = target.ItemCount;
			foreach (var item in items) AddAction(target, item, () => onPick(item));
			if (target.ItemCount == before) AddAction(target, "(none saved)", () => { }, disabled: true);
		}

		var primitives = AddSubmenu(menu, "Primitives");
		foreach (var name in nodeList.Primitives.Keys) AddAction(primitives, name, () => AddPrimitive(name, spawnPosition));
		primitives.AddSeparator();
		AddList(AddSubmenu(primitives, "Gradient Presets"), GraphIO.List(GraphIO.GradientDir), name => LoadGradientPreset(name, spawnPosition));

		var shaders = AddSubmenu(menu, "Shaders");
		foreach (var name in nodeList.Shaders.Keys) AddAction(shaders, name, () => AddShader(name, spawnPosition));

		// Modules are Modules
		var modules = AddSubmenu(menu, "Modules");
		AddAction(modules, "New Empty Module", () => AddNode(ModuleType, spawnPosition));
		AddAction(modules, "Collapse Selection to Module   (Ctrl+G)", () => CollapseSelectionToModule(),
			disabled: !GetCollapsibleSelection().Any());
		modules.AddSeparator("Saved");
		AddList(modules, GraphIO.List(GraphIO.ModuleDir), name => LoadModule(name, spawnPosition));

		menu.IdPressed += id => actions[(int)id]();
		menu.PopupHide += menu.QueueFree;
		AddChild(menu);
		menu.Position = (Vector2I)(GetScreenPosition() + atPosition);
		menu.Popup();
	}

	// ---- Deleting nodes (Delete key)

	private void OnDeleteNodesRequest(Godot.Collections.Array<StringName> nodeNames)
	{
		DeleteNodes(nodeNames.Select(name => GetNodeOrNull<GraphNode>((string)name)).Where(node => node != null));
	}

	/// <summary>
	/// Remove nodes and their connections. The output bus and a Module's own Inputs/Outputs
	/// can't be deleted. Deleting a Module also frees everything inside it.
	/// </summary>
	public void DeleteNodes(IEnumerable<GraphNode> nodes)
	{
		var toDelete = nodes.Where(node => node.HasMeta(TypeMeta) && node is not InputsNode && node is not OutputsNode && node is not VisualBusNode).ToList();
		if (toDelete.Count == 0) return;
		var names = new HashSet<StringName>(toDelete.Select(node => node.Name));
		foreach (var c in GetConnections().Where(c => names.Contains(c.From) || names.Contains(c.To)))
		{
			DisconnectNode(c.From, c.FromPort, c.To, c.ToPort);
			// Inputs left behind lose their data, same as a manual disconnect
			if (!names.Contains(c.To)) (GetNode((string)c.To) as IGraphNode)?.SetInputData(c.ToPort, default);
		}
		foreach (var node in toDelete)
		{
			RemoveChild(node);
			node.QueueFree();
		}
		ReorderNodesByConnections();
	}

	// GraphEdit toggles selection with Ctrl-click, but a Shift-click clears the rest of the
	// selection. Remember the selection before the click and restore it afterwards so
	// Shift-click adds the node (or removes it if it was already selected).
	// Godot's GraphEdit pans while Space is held, but only clears that when an internal layer
	// that never takes focus loses focus. If the Space release goes elsewhere (a text field,
	// a dialog, the control panel, or stepping into a module hides this graph) panning sticks:
	// clicking an output port then starts a wire AND drags the canvas. Clear it ourselves.
	private void ReleaseStuckPanKey()
	{
		if (Input.IsKeyPressed(Key.Space) || Input.IsPhysicalKeyPressed(Key.Space)) return;
		foreach (var child in GetChildren(true).OfType<Control>())
		{
			// The internal layer wired to the panner's release_pan_key
			if (child.GetSignalConnectionList(Control.SignalName.FocusExited).Count > 0 && child.MouseFilter == MouseFilterEnum.Ignore)
				child.EmitSignal(Control.SignalName.FocusExited);
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationFocusExit || what == NotificationVisibilityChanged) ReleaseStuckPanKey();
	}

	public override void _Input(InputEvent @event)
	{
		if (Engine.IsEditorHint() || !IsVisibleInTree()) return;
		if (@event is InputEventMouseButton { Pressed: true }) ReleaseStuckPanKey();
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true, ShiftPressed: true, CtrlPressed: false } click) return;
		var clicked = GetChildren().OfType<GraphNode>()
			// Full transform (not GetGlobalRect) so the hit test respects the graph's zoom
			.LastOrDefault(node => node.Visible
				&& (node.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, node.Size)).HasPoint(click.Position));
		if (clicked == null) return; // empty space: Shift+drag box-select already adds to the selection
		var previouslySelected = GetChildren().OfType<GraphNode>().Where(node => node.Selected).ToList();
		bool wasSelected = clicked.Selected;
		Callable.From(() => ApplyShiftClick(clicked, previouslySelected, wasSelected)).CallDeferred();
	}

	private void ApplyShiftClick(GraphNode clicked, List<GraphNode> previouslySelected, bool wasSelected)
	{
		foreach (var node in previouslySelected.Where(IsInstanceValid))
			node.Selected = true;
		if (IsInstanceValid(clicked)) clicked.Selected = !wasSelected;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (Engine.IsEditorHint() || !IsVisibleInTree()) return;
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.G, CtrlPressed: true })
		{
			CollapseSelectionToModule();
			GetViewport().SetInputAsHandled();
		}
	}

	public ModuleNode LoadModule(string name, Vector2 position)
	{
		var data = GraphIO.Load(GraphIO.ModuleDir, name);
		if (data == null)
		{
			Dialogs.ShowMessage(this, "Load Module", $"Couldn't read Module '{name}'.");
			return null;
		}
		// Every load builds fresh nodes, so copies are independent
		var module = (ModuleNode)AddNode(ModuleType, position);
		module.Load(data);
		return module;
	}

	public GradientNode LoadGradientPreset(string name, Vector2 position)
	{
		var data = GraphIO.Load(GraphIO.GradientDir, name);
		if (data == null)
		{
			Dialogs.ShowMessage(this, "Load Gradient", $"Couldn't read gradient preset '{name}'.");
			return null;
		}
		var gradient = (GradientNode)AddPrimitive("Gradient", position);
		gradient.Load(data);
		return gradient;
	}

	// ---- Collapsing a selection into a Module

	private IEnumerable<GraphNode> GetCollapsibleSelection()
	{
		// The output bus has no type key, and a Module's own Inputs/Outputs must stay put
		return GetChildren().OfType<GraphNode>()
			.Where(node => node.Selected && node.HasMeta(TypeMeta) && node is not InputsNode && node is not OutputsNode && node is not VisualBusNode);
	}

	private readonly record struct Connection(StringName From, int FromPort, StringName To, int ToPort);

	private List<Connection> GetConnections()
	{
		return GetConnectionList().Select(c => new Connection(
			(StringName)c["from_node"], (int)c["from_port"], (StringName)c["to_node"], (int)c["to_port"])).ToList();
	}

	private static string PortLabel(GraphNode node, int port, bool input)
	{
		// A module's own port label (its row text also shows the range)
		if (node is ModuleNode module)
		{
			var ports = input ? module.InputPorts : module.OutputPorts;
			if (port >= 0 && port < ports.Count) return $"{module.Title}: {ports[port].Label}";
		}
		string nodeLabel = string.IsNullOrEmpty(node.Title)
			? ((string)node.GetMeta(TypeMeta, node.Name.ToString())).Split('/').Last()
			: node.Title;
		// Use the row's label (e.g. a shader parameter name) when there is one
		int slot = input ? node.GetInputPortSlot(port) : node.GetOutputPortSlot(port);
		var row = node.GetChildren().OfType<Control>().ElementAtOrDefault(slot);
		var label = row == null ? null : (row as Label ?? row.FindChildren("*", "Label", true, false).OfType<Label>().FirstOrDefault());
		return label == null || string.IsNullOrEmpty(label.Text) ? nodeLabel : $"{nodeLabel}: {label.Text}";
	}

	/// <summary>
	/// Unselected nodes that sit on a path leaving the selection and coming back into it.
	/// Collapsing would make the Module feed itself through them.
	/// </summary>
	private List<GraphNode> FindNodesBreakingInOut(HashSet<StringName> inSelection, List<Connection> connections)
	{
		HashSet<StringName> Reach(IEnumerable<StringName> start, bool downstream)
		{
			var found = new HashSet<StringName>();
			var stack = new Stack<StringName>(start);
			while (stack.Count > 0)
			{
				var name = stack.Pop();
				if (inSelection.Contains(name) || !found.Add(name)) continue;
				foreach (var c in connections)
				{
					if (downstream && c.From == name) stack.Push(c.To);
					if (!downstream && c.To == name) stack.Push(c.From);
				}
			}
			return found;
		}
		var fedBySelection = Reach(connections.Where(c => inSelection.Contains(c.From)).Select(c => c.To), downstream: true);
		var feedsSelection = Reach(connections.Where(c => inSelection.Contains(c.To)).Select(c => c.From), downstream: false);
		fedBySelection.IntersectWith(feedsSelection);
		return GetChildren().OfType<GraphNode>().Where(node => fedBySelection.Contains(node.Name)).ToList();
	}

	private string DisplayName(GraphNode node)
	{
		string title = string.IsNullOrEmpty(node.Title)
			? ((string)node.GetMeta(TypeMeta, node.Name.ToString())).Split('/').Last()
			: node.Title;
		// Several nodes can share a title (e.g. five BwGrids), so add a number to tell them apart
		var sameTitle = GetChildren().OfType<GraphNode>().Where(other => other.Title == node.Title).ToList();
		return sameTitle.Count > 1 ? $"{title} #{sameTitle.IndexOf(node) + 1}" : title;
	}

	private static void FlashWarning(GraphNode node)
	{
		node.Modulate = new Color(1.0f, 0.4f, 0.4f);
		var tween = node.CreateTween();
		tween.TweenInterval(2.0);
		tween.TweenProperty(node, "modulate", Colors.White, 1.0);
	}

	/// <summary>
	/// Move the selected nodes into a new Module node. Connections crossing the edge of the
	/// selection become the Module's ports. Returns null if nothing was collapsed.
	/// </summary>
	public ModuleNode CollapseSelectionToModule()
	{
		var selected = GetCollapsibleSelection().ToList();
		if (selected.Count == 0) return null;
		var inSelection = new HashSet<StringName>(selected.Select(node => node.Name));
		var connections = GetConnections();

		var problems = FindNodesBreakingInOut(inSelection, connections);
		if (problems.Count > 0)
		{
			foreach (var node in problems) FlashWarning(node);
			string list = string.Join("\n", problems.Select(node => "  • " + DisplayName(node)));
			Dialogs.ShowMessage(this, "Collapse to Module",
				"Can't collapse: these nodes (highlighted in red) are fed by the selection and also feed back into it, " +
				"so the Module wouldn't have a clear inputs → outputs direction:\n\n" + list +
				"\n\nAdd them to the selection, or leave out the selected nodes on one side of them.");
			return null;
		}

		var incoming = connections.Where(c => !inSelection.Contains(c.From) && inSelection.Contains(c.To)).ToList();
		var outgoing = connections.Where(c => inSelection.Contains(c.From) && !inSelection.Contains(c.To)).ToList();
		var inside = connections.Where(c => inSelection.Contains(c.From) && inSelection.Contains(c.To)).ToList();

		// One port per outside source (fanning out inside) and per inside source (fanning out outside)
		var inputSources = incoming.Select(c => (c.From, c.FromPort)).Distinct().ToList();
		var outputSources = outgoing.Select(c => (c.From, c.FromPort)).Distinct().ToList();
		var inputPorts = inputSources.Select(source =>
		{
			var first = incoming.First(c => (c.From, c.FromPort) == source);
			var target = GetNode<GraphNode>((string)first.To);
			var port = ModulePort.Create((SlotType)target.GetInputPortType(first.ToPort), PortLabel(target, first.ToPort, true));
			// Feeding a module input: keep its range rather than clamping to the 0-1 default
			if (target is ModuleNode targetModule && first.ToPort < targetModule.InputPorts.Count)
			{
				var inner = targetModule.InputPorts[first.ToPort];
				port.TrySetRange(inner.Min, inner.Max, inner.Step);
			}
			// Otherwise widen the range to fit the value flowing in now, so collapsing changes nothing.
			// Knobs output 0-1 already, which the default range maps one-to-one.
			else if (port.IsNumeric && GetNode(source.From.ToString()) is not KnobNode)
			{
				Variant current = (GetNode(source.From.ToString()) as IGraphNode)?.GetOutputData(source.FromPort) ?? default;
				if (current.VariantType is not (Variant.Type.Float or Variant.Type.Int) && target is ShaderNode shader)
					current = shader.GetParameterValue(first.ToPort); // nothing flowing yet: use the parameter's value
				if (current.VariantType is Variant.Type.Float or Variant.Type.Int) port.FitTo((float)current);
			}
			return port;
		}).ToList();
		var outputPorts = outputSources.Select(source =>
		{
			var node = GetNode<GraphNode>((string)source.From);
			return ModulePort.Create((SlotType)node.GetOutputPortType(source.FromPort), PortLabel(node, source.FromPort, false));
		}).ToList();

		foreach (var c in incoming.Concat(outgoing).Concat(inside))
			DisconnectNode(c.From, c.FromPort, c.To, c.ToPort);

		Vector2 min = new Vector2(selected.Min(n => n.PositionOffset.X), selected.Min(n => n.PositionOffset.Y));
		float width = selected.Max(n => n.PositionOffset.X + n.Size.X) - min.X;
		var module = (ModuleNode)CreateNode(ModuleType);
		AddChild(module, true);
		module.PositionOffset = min;
		module.SetResolution(outputResolution);
		module.SetPorts(inputPorts, outputPorts);

		// Move the live nodes (keeping all their state) into the inner graph
		var inner = module.InnerGraph;
		var names = new System.Collections.Generic.Dictionary<StringName, StringName>();
		Vector2 innerOrigin = new Vector2(500, 40); // right of the Inputs node (adjusted once laid out)
		foreach (var node in selected)
		{
			node.Selected = false;
			Vector2 position = node.PositionOffset - min + innerOrigin;
			var oldName = node.Name;
			RemoveChild(node);
			inner.AddChild(node, true);
			node.PositionOffset = position;
			names[oldName] = node.Name;
		}
		module.Outputs.PositionOffset = new Vector2(innerOrigin.X + width + 80, innerOrigin.Y);

		foreach (var c in inside)
			inner.ConnectNode(names[c.From], c.FromPort, names[c.To], c.ToPort);
		for (int i = 0; i < inputSources.Count; i++)
		{
			ConnectNode(inputSources[i].From, inputSources[i].FromPort, module.Name, i);
			foreach (var c in incoming.Where(c => (c.From, c.FromPort) == inputSources[i]))
				inner.ConnectNode(module.Inputs.Name, i, names[c.To], c.ToPort);
		}
		for (int i = 0; i < outputSources.Count; i++)
		{
			inner.ConnectNode(names[outputSources[i].From], outputSources[i].FromPort, module.Outputs.Name, i);
			foreach (var c in outgoing.Where(c => (c.From, c.FromPort) == outputSources[i]))
				ConnectNode(module.Name, i, c.To, c.ToPort);
		}
		inner.ReorderNodesByConnections();
		ReorderNodesByConnections();
		// The Inputs node's width (range fields, long labels) is only known once its graph is
		// shown and laid out, so the navigator spaces the moved nodes on first entry
		module.PendingClearOfInputs = selected.ToList();
		module.Selected = true;
		return module;
	}

	public static void KeepClearOfInputs(ModuleNode module, List<GraphNode> moved)
	{
		if (!IsInstanceValid(module) || !IsInstanceValid(module.Inputs)) return;
		var nodes = moved.Where(node => IsInstanceValid(node) && node.GetParent() == module.InnerGraph).ToList();
		if (nodes.Count == 0) return;
		float inputsRight = module.Inputs.PositionOffset.X + module.Inputs.Size.X;
		float shift = inputsRight + 60 - nodes.Min(node => node.PositionOffset.X);
		if (shift <= 0) return;
		foreach (var node in nodes) node.PositionOffset += new Vector2(shift, 0);
		module.Outputs.PositionOffset += new Vector2(shift, 0);
	}

	// ---- Saving and loading graph contents

	public Godot.Collections.Dictionary SerializeGraph()
	{
		var nodes = new Godot.Collections.Array();
		var saved = new HashSet<StringName>();
		foreach (var node in GetChildren().OfType<GraphNode>().Where(n => n.HasMeta(TypeMeta)))
		{
			var entry = new Godot.Collections.Dictionary
			{
				["type"] = (string)node.GetMeta(TypeMeta),
				["name"] = node.Name.ToString(),
				["position"] = GraphIO.ToArray(node.PositionOffset),
			};
			if (node is ISerializableNode serializable) entry["data"] = serializable.Save();
			nodes.Add(entry);
			saved.Add(node.Name);
		}
		var connections = new Godot.Collections.Array();
		foreach (var c in GetConnections().Where(c => saved.Contains(c.From) && saved.Contains(c.To)))
		{
			connections.Add(new Godot.Collections.Dictionary
			{
				["from"] = c.From.ToString(), ["from_port"] = c.FromPort,
				["to"] = c.To.ToString(), ["to_port"] = c.ToPort,
			});
		}
		return new Godot.Collections.Dictionary { ["nodes"] = nodes, ["connections"] = connections };
	}

	public void LoadGraph(Godot.Collections.Dictionary graph)
	{
		var names = new System.Collections.Generic.Dictionary<string, StringName>();
		foreach (Godot.Collections.Dictionary entry in (Godot.Collections.Array)graph["nodes"])
		{
			string type = (string)entry["type"];
			if (LegacyTypeKeys.TryGetValue(type, out var renamed)) type = renamed;
			GraphNode node;
			// A Module's Inputs/Outputs nodes already exist; just restore their position
			if (type == InputsType) node = GetChildren().OfType<InputsNode>().FirstOrDefault();
			else if (type == OutputBusType) node = visualBusNode;
			else if (type == OutputsType) node = GetChildren().OfType<OutputsNode>().FirstOrDefault();
			else
			{
				node = CreateNode(type);
				if (node == null) continue;
				node.Name = (string)entry["name"];
				AddChild(node, true);
				(node as IResolutionDependent)?.SetResolution(outputResolution);
			}
			if (node == null) continue;
			node.PositionOffset = GraphIO.ToVector2(entry["position"]);
			if (entry.ContainsKey("data") && node is ISerializableNode serializable)
				serializable.Load((Godot.Collections.Dictionary)entry["data"]);
			names[(string)entry["name"]] = node.Name;
		}
		foreach (Godot.Collections.Dictionary c in (Godot.Collections.Array)graph["connections"])
		{
			if (names.TryGetValue((string)c["from"], out var from) && names.TryGetValue((string)c["to"], out var to))
				ConnectNode(from, (int)c["from_port"], to, (int)c["to_port"]);
		}
		ReorderNodesByConnections();
	}

    private bool IsOverlapping(GraphNode node, Vector2 testPos)
    {
        Rect2 newRect = new Rect2(testPos, node.Size);
        foreach (var child in GetChildren().OfType<GraphNode>())
        {
            if (child == node) continue;
            if (newRect.Intersects(new Rect2(child.PositionOffset, child.Size)))
                return true;
        }
        return false;
    }

    private IEnumerable<Vector2> GetPointsOnRing(float r, float step)
    {
        // Top and Bottom edges
        for (float x = -r; x <= r; x += step)
        {
            yield return new Vector2(x, -r);
            yield return new Vector2(x, r);
        }
        // Left and Right edges (avoiding corners already covered)
        for (float y = -r + step; y < r; y += step)
        {
            yield return new Vector2(-r, y);
            yield return new Vector2(r, y);
        }
    }

	private void EnableTypeConnections()
	{
		foreach (SlotType slotType in System.Enum.GetValues(typeof(SlotType)))
		{
			AddValidConnectionType((int)SlotType.Generic, (int)slotType);
			AddValidConnectionType((int)slotType, (int)SlotType.Generic);
		}
	}

	private void PopulateTypeNames()
	{
		TypeNames.Clear();
		foreach (SlotType slotType in System.Enum.GetValues(typeof(SlotType)))
		{
			TypeNames[(int)slotType] = slotType.ToString();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;
		PropagateDataThroughGraph();
	}

	/// <summary>
	/// A port was removed from a node: drop its connections (clearing the inputs they fed)
	/// and move connections on later ports down by one so they stay on the same port.
	/// </summary>
	public void RemovePortConnections(StringName node, int port, bool isInput)
	{
		var affected = GetConnections()
			.Where(c => isInput ? c.To == node && c.ToPort >= port : c.From == node && c.FromPort >= port)
			.ToList();
		// Disconnect everything first so shifted connections can't collide with ones not yet moved
		foreach (var c in affected) DisconnectNode(c.From, c.FromPort, c.To, c.ToPort);
		foreach (var c in affected)
		{
			int p = isInput ? c.ToPort : c.FromPort;
			if (p == port)
			{
				if (!isInput) (GetNodeOrNull((string)c.To) as IGraphNode)?.SetInputData(c.ToPort, default);
				continue;
			}
			if (isInput) ConnectNode(c.From, c.FromPort, c.To, c.ToPort - 1);
			else ConnectNode(c.From, c.FromPort - 1, c.To, c.ToPort);
		}
		ReorderNodesByConnections();
	}

	private void OnInputSlotDelete(StringName toNode, int index)
	{
		var connections = GetConnectionList();
		var connectionsToRemove = new System.Collections.Generic.List<Godot.Collections.Dictionary>();
		var connectionsToModify = new System.Collections.Generic.List<Godot.Collections.Dictionary>();

		// 1. Find connections to delete (toNode matches and toSlot equals the deleted index)
		// 2. Find connections to modify (toNode matches and toSlot is higher than deleted index)
		foreach (Godot.Collections.Dictionary connection in connections)
		{
			if ((StringName)connection["to_node"] == toNode)
			{
				int toSlot = (int)connection["to_port"];
				if (toSlot == index)
				{
					connectionsToRemove.Add(connection);
				}
				else if (toSlot > index)
				{
					connectionsToModify.Add(connection);
				}
			}
		}

		// Delete connections at the deleted index
		foreach (Godot.Collections.Dictionary connection in connectionsToRemove)
		{
			DisconnectNode(
				(StringName)connection["from_node"],
				(int)connection["from_port"],
				(StringName)connection["to_node"],
				(int)connection["to_port"]
			);
		}

		// Decrement connections that are higher than the deleted index
		foreach (Godot.Collections.Dictionary connection in connectionsToModify)
		{
			StringName fromNode = (StringName)connection["from_node"];
			int fromSlot = (int)connection["from_port"];
			int toSlot = (int)connection["to_port"];

			// Disconnect the old connection
			DisconnectNode(fromNode, fromSlot, toNode, toSlot);
			// Reconnect at the decremented slot
			ConnectNode(fromNode, fromSlot, toNode, toSlot - 1);
		}
	}

	private void OnConnectionRequest(StringName fromNode, long fromSlot, StringName toNode, long toSlot)
	{
		// Texture feedback loops (including a node into itself) would make a SubViewport sample itself
		if (WouldCreateCycle(fromNode, toNode)) return;

		// Check if the destination port already has a connection
		foreach (Godot.Collections.Dictionary connection in GetConnectionList())
		{
			if ((StringName)connection["to_node"] == toNode && (int)connection["to_port"] == (int)toSlot)
			{
				// Remove the old connection first
				DisconnectNode(
					(StringName)connection["from_node"], 
					(int)connection["from_port"], 
					(StringName)connection["to_node"], 
					(int)connection["to_port"]
				);
			}
		}

    	ConnectNode(fromNode, (int)fromSlot, toNode, (int)toSlot);
		ReorderNodesByConnections();
	}

	private bool WouldCreateCycle(StringName fromNode, StringName toNode)
	{
		// A cycle exists if fromNode is already reachable downstream of toNode
		var connections = GetConnectionList();
		var visited = new HashSet<StringName>();
		var stack = new Stack<StringName>();
		stack.Push(toNode);
		while (stack.Count > 0)
		{
			var current = stack.Pop();
			if (current == fromNode) return true;
			if (!visited.Add(current)) continue;
			foreach (Godot.Collections.Dictionary connection in connections)
			{
				if ((StringName)connection["from_node"] == current)
					stack.Push((StringName)connection["to_node"]);
			}
		}
		return false;
	}

	private void OnDisconnectionRequest(StringName fromNode, long fromSlot, StringName toNode, long toSlot)
	{
		DisconnectNode(fromNode, (int)fromSlot, toNode, (int)toSlot);
		(GetNode((string)toNode) as IGraphNode)?.SetInputData((int)toSlot, default); // Clear input data on disconnect
		ReorderNodesByConnections();
	}

	public void ReorderNodesByConnections()
    {
        var connections = GetConnectionList();
        var nodes = GetChildren().OfType<GraphNode>().ToList();
        
        // 1. Build dependency maps
        var adjacency = new System.Collections.Generic.Dictionary<string, List<string>>();
        var inDegree = new System.Collections.Generic.Dictionary<string, int>();

        foreach (var node in nodes)
        {
            adjacency[node.Name] = new List<string>();
            inDegree[node.Name] = 0;
        }

        foreach (Godot.Collections.Dictionary conn in connections)
        {
            string from = (string)conn["from_node"];
            string to = (string)conn["to_node"];
            if (!adjacency.ContainsKey(from) || !inDegree.ContainsKey(to)) continue;

            adjacency[from].Add(to);
            inDegree[to]++;
        }

        // 2. Kahn's Algorithm (Topological Sort)
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var sortedNames = new List<string>();

        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            sortedNames.Add(u);

            foreach (var v in adjacency[u])
            {
                inDegree[v]--;
                if (inDegree[v] == 0) queue.Enqueue(v);
            }
        }

        // 3. Apply the new order to the Scene Tree
        for (int i = 0; i < sortedNames.Count; i++)
        {
            var node = GetNode<GraphNode>(sortedNames[i]);
            MoveChild(node, i); // Reorders node in the hierarchy
        }
    }

    public void PropagateDataThroughGraph()
    {
        // Children are kept in topological order by ReorderNodesByConnections, so walking
        // connections in source-node order lets values flow through chains in one frame
        var connections = GetConnectionList()
            .OrderBy(connection => GetNodeOrNull((string)connection["from_node"])?.GetIndex() ?? int.MaxValue);
        foreach (Godot.Collections.Dictionary connection in connections)
        {
            string fromNodeName = (string)connection["from_node"];
            string toNodeName = (string)connection["to_node"];
            int fromSlot = (int)connection["from_port"];
            int toSlot = (int)connection["to_port"];

            var fromNode = GetNodeOrNull(fromNodeName) as IGraphNode;
            var toNode = GetNodeOrNull(toNodeName) as IGraphNode;

            if (fromNode == null || toNode == null) continue;

            // Get output from source and pass to destination
            Variant outputData = fromNode.GetOutputData(fromSlot);
            // Module inputs map a knob's 0-1 onto their range, and clamp everything else
            if (toNode is ModuleNode module) module.SetInput(toSlot, outputData, fromKnob: fromNode is KnobNode);
            else toNode.SetInputData(toSlot, outputData);
        }
    }
}
