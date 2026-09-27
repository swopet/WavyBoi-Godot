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
        if (inputSlot == 0)
        {
            value = (int)data;
            lineEdit.Text = value.ToString();
        }
    }
}
