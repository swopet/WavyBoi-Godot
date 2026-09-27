using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Breadcrumb bar for stepping into and out of SubGraphs. Only the current graph is visible;
/// the others stay in the tree (hidden) so they keep processing and rendering.
/// </summary>
public partial class GraphNavigator : HBoxContainer
{
	public VisualsGraphEdit Root { get; private set; }
	private readonly List<SubGraphNode> path = new();

	public VisualsGraphEdit Current => path.Count == 0 ? Root : path[^1].InnerGraph;

	public void Init(VisualsGraphEdit root)
	{
		Root = root;
		root.Navigator = this;
		root.Host = root.GetParent();
		ShowCurrent();
	}

	public void Enter(SubGraphNode subGraph)
	{
		if (subGraph.GetParent() != Current) return;
		path.Add(subGraph);
		ShowCurrent();
	}

	/// <summary>Go back to a level of the path: 0 is the main graph.</summary>
	public void GoTo(int depth)
	{
		if (depth < path.Count) path.RemoveRange(depth, path.Count - depth);
		ShowCurrent();
	}

	private void ShowCurrent()
	{
		// Drop levels whose SubGraph node no longer exists
		int valid = path.FindIndex(node => !IsInstanceValid(node));
		if (valid >= 0) path.RemoveRange(valid, path.Count - valid);

		var current = Current;
		foreach (var graph in Root.Host.GetChildren().OfType<VisualsGraphEdit>())
		{
			graph.Visible = graph == current;
		}
		Refresh();
	}

	/// <summary>Rebuild the breadcrumb buttons (also called when a SubGraph is renamed).</summary>
	public void Refresh()
	{
		foreach (var child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
		// Nothing to navigate while at the top level
		Visible = path.Count > 0;
		AddCrumb("Main", 0);
		for (int i = 0; i < path.Count; i++)
		{
			AddChild(new Label { Text = "›" });
			AddCrumb(path[i].Title, i + 1);
		}
	}

	private void AddCrumb(string text, int depth)
	{
		var button = new Button { Text = text, Disabled = depth == path.Count };
		button.Pressed += () => GoTo(depth);
		AddChild(button);
	}
}
