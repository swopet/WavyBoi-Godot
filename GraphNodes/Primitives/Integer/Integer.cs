using Godot;
using System;

public partial class IntegerNode : GraphNode , IGraphNode
{
    int value = 0;
    [Export] public LineEdit lineEdit;
    public override void _Ready()
    {
        GetTitlebarHBox().Visible = false;
        SetSlotEnabledRight(0, true);
        SetSlotEnabledLeft(0, true);
        SetSlotTypeRight(0, (int)SlotType.Integer);
        SetSlotTypeLeft(0, (int)SlotType.Integer);
        lineEdit.Text = value.ToString();
        lineEdit.TextSubmitted += (string newText) => 
        {
            if (int.TryParse(newText, out int newValue))
            {
                value = newValue;
            }
            else
            {
                lineEdit.Text = value.ToString();
            }
        };
    }

    Variant IGraphNode.GetOutputData(int outputSlot)
    {
        if (outputSlot == 0)
        {
            return value;
        }
        return default;
    }

    void IGraphNode.SetInputData(int inputSlot, Variant data)
    {
        // Nil means the input was disconnected: keep the last value instead of resetting to 0
        if (inputSlot != 0 || data.VariantType == Variant.Type.Nil) return;
        int newValue = (int)data;
        if (newValue == value) return;
        value = newValue;
        lineEdit.Text = value.ToString(); // only on change, so the field isn't rewritten every frame
    }
}
