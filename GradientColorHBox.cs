using Godot;
using System;

public partial class GradientColorHBox : HBoxContainer
{
    [Export] public ColorPickerButton colorPicker;
    [Export] public Button deleteButton;
    [Export] public Button upButton;
    [Export] public Button downButton;
}
