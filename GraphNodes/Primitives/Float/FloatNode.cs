using Godot;
using System;

public partial class FloatNode : GraphNode , IGraphNode
{
    float value = 0.0f;
    [Export] public LineEdit lineEdit;
    public override void _Ready()
    {
        GetTitlebarHBox().Visible = false;
        SetSlotEnabledRight(0, true);
        SetSlotEnabledLeft(0, true);
        SetSlotTypeRight(0, (int)SlotType.Float);
        lineEdit.Text = value.ToString();
        lineEdit.TextSubmitted += (string newText) => 
        {
            if (float.TryParse(newText, out float newValue))
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
            value = (float)data;
            lineEdit.Text = value.ToString();
        }
    }
}
