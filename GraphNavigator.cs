using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Breadcrumb bar for stepping into and out of Modules. Only the current graph is visible;
/// the others stay in the tree (hidden) so they keep processing and rendering.
/// </summary>
public partial class GraphNavigator : HBoxContainer
{
	public VisualsGraphEdit Root { get; private set; }
	private readonly List<ModuleNode> path = new();

	public VisualsGraphEdit Current => path.Count == 0 ? Root : path[^1].InnerGraph;

	public void Init(VisualsGraphEdit root)
	{
		Root = root;
		root.Navigator = this;
		root.Host = root.GetParent();
		ShowCurrent();
	}

	public void Enter(ModuleNode module)
	{
		if (module.GetParent() != Current) return;
		path.Add(module);
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
		// Drop levels whose Module node no longer exists
		int valid = path.FindIndex(node => !IsInstanceValid(node));
		if (valid >= 0) path.RemoveRange(valid, path.Count - valid);

		var current = Current;
		foreach (var graph in Root.Host.GetChildren().OfType<VisualsGraphEdit>())
		{
			graph.Visible = graph == current;
		}
		Refresh();

		// A freshly collapsed module: space its nodes once the now-visible graph is laid out
		if (path.Count > 0 && path[^1].PendingClearOfInputs is { } moved)
		{
			var module = path[^1];
			module.PendingClearOfInputs = null;
			GetTree().Connect(SceneTree.SignalName.ProcessFrame,
				Callable.From(() => VisualsGraphEdit.KeepClearOfInputs(module, moved)), (uint)ConnectFlags.OneShot);
		}
	}

	/// <summary>Rebuild the breadcrumb buttons (also called when a Module is renamed).</summary>
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
