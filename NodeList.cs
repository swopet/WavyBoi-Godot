using Godot;

[GlobalClass]
public partial class NodeList : Resource
{
    [Export] public Godot.Collections.Dictionary<string, PackedScene> Primitives;
    [Export] public Godot.Collections.Dictionary<string, PackedScene> Shaders;

    [Export] public PackedScene VisualBus;
}
