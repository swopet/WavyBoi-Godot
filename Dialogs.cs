using Godot;
using System;

/// <summary>
/// Small popup dialogs. Each one frees itself when closed.
/// </summary>
public static class Dialogs
{
	private static void Show(Node context, AcceptDialog dialog)
	{
		dialog.VisibilityChanged += () =>
		{
			if (!dialog.Visible) dialog.QueueFree();
		};
		context.GetTree().Root.AddChild(dialog);
		dialog.PopupCentered();
	}

	public static void ShowMessage(Node context, string title, string text)
	{
		Show(context, new AcceptDialog { Title = title, DialogText = text });
	}

	public static void Confirm(Node context, string title, string text, Action onConfirmed)
	{
		var dialog = new ConfirmationDialog { Title = title, DialogText = text };
		dialog.Confirmed += onConfirmed;
		Show(context, dialog);
	}

	/// <summary>
	/// Ask for a name, then save getData() under it in dir, confirming before overwriting.
	/// </summary>
	public static void SaveNamed(Node context, string title, string dir, string defaultName, Func<Godot.Collections.Dictionary> getData, Action<string> onSaved = null)
	{
		var dialog = new ConfirmationDialog { Title = title, OkButtonText = "Save" };
		var nameEdit = new LineEdit
		{
			Text = defaultName,
			PlaceholderText = "Name",
			SelectAllOnFocus = true,
			CustomMinimumSize = new Vector2(260, 0),
		};
		dialog.AddChild(nameEdit);
		dialog.RegisterTextEnter(nameEdit);
		dialog.Confirmed += () =>
		{
			string name = nameEdit.Text.Trim();
			if (GraphIO.SanitizeName(name) == "")
			{
				ShowMessage(context, title, "Please enter a name.");
				return;
			}
			void DoSave()
			{
				var error = GraphIO.Save(dir, name, getData());
				if (error != Error.Ok) ShowMessage(context, title, $"Couldn't save '{name}': {error}");
				else onSaved?.Invoke(name);
			}
			if (GraphIO.Exists(dir, name))
				Confirm(context, title, $"'{name}' already exists. Overwrite it?", DoSave);
			else
				DoSave();
		};
		Show(context, dialog);
		nameEdit.GrabFocus();
	}
}
