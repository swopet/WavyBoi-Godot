using Godot;
using System;

public partial class VisualBus : HBoxContainer
{
    [Export] public NodePath DeleteButtonPath;
    [Export] public NodePath FadeInButtonPath;
    [Export] public NodePath FadeOutButtonPath;
    [Export] public NodePath SetButtonPath;

    public void ToggleDeleteButton(bool visible)
    {
        GetNode<Button>(DeleteButtonPath).Visible = visible;
    }

    public void UpdateWeight(float weight){
        (GetChild(0).GetChild(0) as Label).Text = $"{Math.Round(weight * 100)}%";
    }
}
