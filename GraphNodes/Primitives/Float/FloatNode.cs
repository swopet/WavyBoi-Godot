using Godot;
using System;

public partial class FloatNode : GraphNode , IGraphNode, ISerializableNode
{
    float value = 0.0f;
    [Export] public LineEdit lineEdit;
    public override void _Ready()
    {
        GetTitlebarHBox().Visible = false;
        SetSlotEnabledRight(0, true);
        SetSlotEnabledLeft(0, true);
        SetSlotTypeRight(0, (int)SlotType.Float);
        SetSlotTypeLeft(0, (int)SlotType.Float);
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

    public Godot.Collections.Dictionary Save()
    {
        return new Godot.Collections.Dictionary { ["value"] = value };
    }

    public void Load(Godot.Collections.Dictionary data)
    {
        value = (float)data["value"];
        lineEdit.Text = value.ToString();
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
        float newValue = (float)data;
        if (newValue == value) return;
        value = newValue;
        lineEdit.Text = value.ToString(); // only on change, so the field isn't rewritten every frame
    }
}
