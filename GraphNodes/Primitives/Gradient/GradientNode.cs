using Godot;
using System;
using System.Linq;

public partial class GradientNode : GraphNode, IGraphNode
{
    [Export] public Gradient gradient;
    [Export] public GradientTexture2D gradientTexture;
    private int bands;
    GradientColorHBox[] colorHBoxes = [];
    public override void _Ready()
    {
        bands = 1;
        colorHBoxes = GetChildren().OfType<GradientColorHBox>().ToArray();
        GD.Print($"Found {colorHBoxes.Length} GradientColorHBoxes");
        colorHBoxes[0].upButton.Disabled = true;
        colorHBoxes[^1].downButton.Disabled = true;
        for(int i = 0; i < colorHBoxes.Length; i++)
        {
            ConnectSignals(colorHBoxes[i]);
            SetSlotEnabledLeft(i+1, true);
            SetSlotTypeLeft(i+1, (int)SlotType.Color);
        }
        AddColor();
        AddColor();
        SetSlotEnabledRight(0, true);
        SetSlotTypeRight(0, (int)SlotType.Texture);
    }

    private void ConnectSignals(GradientColorHBox hbox)
    {
        hbox.colorPicker.ColorChanged += (Color _) => UpdateGradient();
        hbox.deleteButton.Pressed += () => DeleteColor(hbox);
        hbox.upButton.Pressed += () => MoveColorUp(hbox);
        hbox.downButton.Pressed += () => MoveColorDown(hbox);
    }

    private void UpdateGradient()
    {
        colorHBoxes = GetChildren().OfType<GradientColorHBox>().ToArray();
        gradient.Colors = colorHBoxes.Select(hbox => hbox.colorPicker.Color).ToArray();
        if (colorHBoxes.Length == 1)
        {
            gradient.Offsets = new float[] { 0.0f };
            return;
        }
        gradient.Offsets = colorHBoxes.Select((hbox, index) => (float)index / (colorHBoxes.Length - 1)).ToArray();
        gradientTexture.Width = gradient.Colors.Length + (bands-1) * (gradient.Colors.Length - 1);
    }

    public void MoveColorUp(GradientColorHBox hbox)
    {
        int index = Array.IndexOf(colorHBoxes, hbox);
        if (index <= 0) return;
        (colorHBoxes[index].colorPicker.Color, colorHBoxes[index - 1].colorPicker.Color) = (colorHBoxes[index - 1].colorPicker.Color, colorHBoxes[index].colorPicker.Color);
        UpdateGradient();
    }

    public void OnLineEditTextSubmitted(string newText)
    {
        if (int.TryParse(newText, out int newBands) && newBands > 0)
        {
            bands = newBands;
            gradientTexture.Width = gradient.Colors.Length + (bands-1) * (gradient.Colors.Length - 1);
        }
        else
        {
            GetNode<LineEdit>("GradientPreviewHBox/LineEdit").Text = bands.ToString();
        }
    }

    public void MoveColorDown(GradientColorHBox hbox)
    {
        int index = Array.IndexOf(colorHBoxes, hbox);
        if (index < 0 || index >= colorHBoxes.Length - 1) return;
        (colorHBoxes[index].colorPicker.Color, colorHBoxes[index + 1].colorPicker.Color) = (colorHBoxes[index + 1].colorPicker.Color, colorHBoxes[index].colorPicker.Color);
        UpdateGradient();
    }

    public void AddColor()
    {
        colorHBoxes[^1].downButton.Disabled = false;
        var newHBox = colorHBoxes[^1].Duplicate() as GradientColorHBox;
        AddChild(newHBox);
        colorHBoxes = GetChildren().OfType<GradientColorHBox>().ToArray();
        SetSlotEnabledLeft(colorHBoxes.Length, true);
        SetSlotTypeLeft(colorHBoxes.Length, (int)SlotType.Color);
        colorHBoxes[^1].upButton.Disabled = false;
        ConnectSignals(newHBox);
        UpdateGradient();
    }

    public void DeleteColor(GradientColorHBox hbox)
    {
        if (colorHBoxes.Length == 1) return;
        RemoveChild(hbox);
        colorHBoxes = GetChildren().OfType<GradientColorHBox>().ToArray();
        colorHBoxes[^1].downButton.Disabled = true;
        colorHBoxes[0].upButton.Disabled = true;
        UpdateGradient();
    }

    Variant IGraphNode.GetOutputData(int outputSlot)
    {
        if (outputSlot == 0)
        {
            var gradientTexture = GetChild(0).GetChild<TextureRect>(0).Texture;
            return gradientTexture as Texture2D;
        }
        return default;
    }

    void IGraphNode.SetInputData(int inputSlot, Variant data)
    {
        throw new NotImplementedException();
    }

}
