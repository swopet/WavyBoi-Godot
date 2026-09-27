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
	
	public Texture GetOutputTexture(){
		return visualBusNode?.GetOutputTexture();
	}

	private NodeList nodeList;

	public void CreateVisualBus(){
		visualBusNode = nodeList.VisualBus.Instantiate<VisualBusNode>();
		SpawnAtClosestAvailable(visualBusNode, new Vector2(0, 0));
		
		// Connect bus deletion signal
		visualBusNode.BusDeleted += (index) => OnInputSlotDelete(visualBusNode.Name, index);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (Engine.IsEditorHint()){
			PopulateTypeNames();
		}
		EnableTypeConnections();
		
		nodeList = ResourceLoader.Load<NodeList>("res://GraphNodes/MasterList.tres");

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
		ConnectionRequest += OnConnectionRequest;
		DisconnectionRequest += OnDisconnectionRequest;
	}

	public void AddPrimitive(string primitiveName)
	{
		PackedScene primitiveScene = null;
		nodeList.Primitives.TryGetValue(primitiveName, out primitiveScene);
		if (primitiveScene != null)
		{
			var newNode = primitiveScene.Instantiate<GraphNode>();
			SpawnAtClosestAvailable(newNode, new Vector2(0, 0));
		}
	}

	public void AddShader(string shaderName)
	{
		PackedScene shaderScene = null;
		nodeList.Shaders.TryGetValue(shaderName, out shaderScene);
		if (shaderScene != null)
		{
			var newNode = shaderScene.Instantiate<ShaderNode>();
			newNode.Title = shaderName; // Set the node name to the shader name for easier identification
			SpawnAtClosestAvailable(newNode, new Vector2(0, 0));
		}
	}

	public void SpawnAtClosestAvailable(GraphNode newNode, Vector2 targetPos)
    {
        // 1. Add to tree so Size is calculated
        AddChild(newNode);
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
		PropagateDataThroughGraph();
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
        var connections = GetConnectionList();
        foreach (Godot.Collections.Dictionary connection in connections)
        {
            string fromNodeName = (string)connection["from_node"];
            string toNodeName = (string)connection["to_node"];
            int fromSlot = (int)connection["from_port"];
            int toSlot = (int)connection["to_port"];

            var fromNode = GetNode(fromNodeName) as IGraphNode;
            var toNode = GetNode(toNodeName) as IGraphNode;

            if (fromNode == null || toNode == null) continue;

            // Get output from source and pass to destination
            Variant outputData = fromNode.GetOutputData(fromSlot);
            toNode.SetInputData(toSlot, outputData);
        }
    }
}
