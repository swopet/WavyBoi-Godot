
using Godot;
using System;


public partial class Controller : Node2D
{
	private Window? DisplayWindow = null;
		private void OpenDisplayWindow()
		{
			// Close any existing window first
			CloseDisplayWindow();

			var monitorSelect = GetNode<OptionButton>("UI/VBoxContainer/MonitorSelect");
			int selectedMonitor = monitorSelect.Selected;
			var widthField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/WidthField");
			var heightField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/HeightField");
			int width = int.TryParse(widthField.Text, out var w) ? w : 1280;
			int height = int.TryParse(heightField.Text, out var h) ? h : 720;

			DisplayWindow = new Window();
			
			GetTree().Root.AddChild(DisplayWindow);
			DisplayWindow.Title = "Display Window";
			DisplayWindow.Size = new Vector2I(width, height);
			DisplayWindow.CurrentScreen = selectedMonitor;
			DisplayWindow.Mode = Window.ModeEnum.Fullscreen;
			DisplayWindow.Visible = true;
		}

		private void CloseDisplayWindow()
		{
			if (DisplayWindow != null)
			{
				DisplayWindow.Visible = false;
				DisplayWindow.QueueFree();
				DisplayWindow = null;
			}
		}
	public override void _Ready()
	{
		// Populate MonitorSelect OptionButton with monitor names
		var monitorSelect = GetNode<OptionButton>("UI/VBoxContainer/MonitorSelect");
		int screenCount = DisplayServer.GetScreenCount();
		for (int i = 0; i < screenCount; i++)
		{
			string name = i.ToString() + DisplayServer.ScreenGetSize(i).ToString();
			monitorSelect.AddItem(name);
		}
		// Connect Auto button
		var autoButton = GetNode<Button>("UI/VBoxContainer/ResolutionHBox/AutoButton");
		autoButton.Pressed += OnAutoButtonPressed;

		// Connect validation for Width and Height fields
		var widthField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/WidthField");
		var heightField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/HeightField");
		widthField.TextSubmitted += OnWidthFieldTextSubmitted;
		widthField.FocusExited += OnWidthFieldFocusExited;
		heightField.TextSubmitted += OnHeightFieldTextSubmitted;
		heightField.FocusExited += OnHeightFieldFocusExited;

		// Store initial valid values
		_lastValidWidth = widthField.Text;
		_lastValidHeight = heightField.Text;

		// Connect ToggleDisplay button
		var toggleDisplayButton = GetNode<Button>("UI/VBoxContainer/ToggleDisplay");
		toggleDisplayButton.Pressed += OnToggleDisplay;

		// Initialize fields with first monitor's resolution
		OnAutoButtonPressed();
		
	}

	private void OnToggleDisplay()
		{
			var toggleDisplayButton = GetNode<Button>("UI/VBoxContainer/ToggleDisplay");
			if (DisplayWindow == null)
			{
				OpenDisplayWindow();
				toggleDisplayButton.Text = "Close Display Window";
			}
			else
			{
				CloseDisplayWindow();
				toggleDisplayButton.Text = "Open Display Window";
			}
		}
	private string _lastValidWidth = "";
	private string _lastValidHeight = "";

	private void OnAutoButtonPressed()
	{
		var monitorSelect = GetNode<OptionButton>("UI/VBoxContainer/MonitorSelect");
		int selectedMonitor = monitorSelect.Selected;
		Vector2I res = DisplayServer.ScreenGetSize(selectedMonitor);
		var widthField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/WidthField");
		var heightField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/HeightField");
		widthField.Text = res.X.ToString();
		heightField.Text = res.Y.ToString();
		_lastValidWidth = widthField.Text;
		_lastValidHeight = heightField.Text;
	}

	private void ValidateWidthField()
	{
		var widthField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/WidthField");
		string text = widthField.Text;
		if (int.TryParse(text, out _))
		{
			_lastValidWidth = text;
		}
		else
		{
			widthField.Text = _lastValidWidth;
		}
	}

	private void ValidateHeightField()
	{
		var heightField = GetNode<LineEdit>("UI/VBoxContainer/ResolutionHBox/HeightField");
		string text = heightField.Text;
		if (int.TryParse(text, out _))
		{
			_lastValidHeight = text;
		}
		else
		{
			heightField.Text = _lastValidHeight;
		}
	}

	private void OnWidthFieldTextSubmitted(string newText)
	{
		ValidateWidthField();
	}

	private void OnWidthFieldFocusExited()
	{
		ValidateWidthField();
	}

	private void OnHeightFieldTextSubmitted(string newText)
	{
		ValidateHeightField();
	}

	private void OnHeightFieldFocusExited()
	{
		ValidateHeightField();
	}

	// ...existing code...
}
