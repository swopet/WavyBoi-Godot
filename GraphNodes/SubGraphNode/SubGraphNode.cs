using Godot;
using System;

public partial class SubGraphNode : GraphNode , IGraphNode
{
    public override void _Ready()
    {
        GetTitlebarHBox().Visible = false;
    }

    Variant IGraphNode.GetOutputData(int outputSlot)
    {
        return default;
    }

    void IGraphNode.SetInputData(int inputSlot, Variant data)
    {
        
    }
}
