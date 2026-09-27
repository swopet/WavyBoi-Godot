
using Godot;
using System;
using System.Linq;


public partial class Controller : Control
{
	[Export] NodePath WidthFieldPath;
	[Export] NodePath HeightFieldPath;
	[Export] NodePath MonitorSelectPath;
	[Export] NodePath AutoButtonPath;
	[Export] NodePath ToggleDisplayPath;
	[Export] NodePath AudioSelectPath;
	[Export] NodePath ToggleAudioPath;
	[Export] NodePath NormalizeAudioPath;
	[Export] NodePath SpectrumPath;
	[Export] NodePath GraphEditPath;
	private Window DisplayWindow = null;
	private AudioStreamPlayer _micPlayer = null;

	private void OpenDisplayWindow()
	{
		// Close any existing window first
		CloseDisplayWindow();

		var monitorSelect = GetNode<OptionButton>(MonitorSelectPath);
		int selectedMonitor = monitorSelect.Selected;
		var widthField = GetNode<LineEdit>(WidthFieldPath);
		var heightField = GetNode<LineEdit>(HeightFieldPath);
		int width = int.TryParse(widthField.Text, out var w) ? w : 1280;
		int height = int.TryParse(heightField.Text, out var h) ? h : 720;

		DisplayWindow = new Window();
		
		GetTree().Root.AddChild(DisplayWindow);
		DisplayWindow.Title = "Display Window";
		DisplayWindow.Size = new Vector2I(width, height);
		DisplayWindow.CurrentScreen = selectedMonitor;
		DisplayWindow.Mode = Window.ModeEnum.Fullscreen;
		DisplayWindow.Visible = true;
		var _displayRect = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.KeepSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f
		};
		DisplayWindow.AddChild(_displayRect);
		_displayRect.Texture = GetNode<VisualsGraphEdit>(GraphEditPath).GetOutputTexture() as Texture2D;
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
		var monitorSelect = GetNode<OptionButton>(MonitorSelectPath);
		int screenCount = DisplayServer.GetScreenCount();
		for (int i = 0; i < screenCount; i++)
		{
			string name = i.ToString() + DisplayServer.ScreenGetSize(i).ToString();
			monitorSelect.AddItem(name);
		}
		// Connect Auto button
		var autoButton = GetNode<Button>(AutoButtonPath);
		autoButton.Pressed += OnAutoButtonPressed;

		// Connect validation for Width and Height fields
		var widthField = GetNode<LineEdit>(WidthFieldPath);
		var heightField = GetNode<LineEdit>(HeightFieldPath);
		widthField.TextSubmitted += OnWidthFieldTextSubmitted;
		widthField.FocusExited += OnWidthFieldFocusExited;
		heightField.TextSubmitted += OnHeightFieldTextSubmitted;
		heightField.FocusExited += OnHeightFieldFocusExited;

		// Store initial valid values
		_lastValidWidth = widthField.Text;
		_lastValidHeight = heightField.Text;


		// Populate AudioSelect OptionButton with available audio inputs
		var audioSelect = GetNode<OptionButton>(AudioSelectPath);
		var inputDevices = AudioServer.GetInputDeviceList();
		foreach (var device in inputDevices)
		{
			audioSelect.AddItem(device);
		}

		var toggleAudioButton = GetNode<Button>(ToggleAudioPath);
		toggleAudioButton.Pressed += OnToggleAudio;
	

		var normalizeAudioButton = GetNode<Button>(NormalizeAudioPath);
		normalizeAudioButton.Pressed += OnNormalizeAudio;
		
	
		// Connect ToggleDisplay button
		var toggleDisplayButton = GetNode<Button>(ToggleDisplayPath);
		toggleDisplayButton.Pressed += OnToggleDisplay;



		// Initialize fields with first monitor's resolution
		OnAutoButtonPressed();
		GetNode<VisualsGraphEdit>(GraphEditPath).CreateVisualBus();
		GetNode<VisualsGraphEdit>(GraphEditPath).spectrum = GetNode<Spectrum>(SpectrumPath);
	}

	private void OnNormalizeAudio()
	{
		var spectrum = GetNode<Spectrum>(SpectrumPath);
		spectrum.NormalizeAudio();
	}

	private void OnToggleAudio()
	{
		
		// Set the input device if you want a specific one
		var audioSelect = GetNode<OptionButton>(AudioSelectPath);
		var toggleAudioButton = GetNode<Button>(ToggleAudioPath);
		var inputDevices = AudioServer.GetInputDeviceList();
		
		if (_micPlayer == null)
		{
			
			_micPlayer = new AudioStreamPlayer();
			_micPlayer.Stream = new AudioStreamMicrophone();
			_micPlayer.Bus = "Spectrum";
			AddChild(_micPlayer);
			if (audioSelect.Selected >= 0 && audioSelect.Selected < inputDevices.Length)
			{
				AudioServer.SetInputDevice(inputDevices[audioSelect.Selected]);
			}
			_micPlayer.Play();
			toggleAudioButton.Text = "Audio Off";
			var spectrum = GetNode<Spectrum>(SpectrumPath);
			var normalizeAudio = GetNode<Button>(NormalizeAudioPath);
			normalizeAudio.Visible = true;
			spectrum.Visible = true;
			spectrum.NormalizeAudio();
			spectrum.UpdateHboxWidth();
		}
		else
		{
			_micPlayer.Stop();
			_micPlayer.QueueFree();
			_micPlayer = null;
			var spectrum = GetNode<Spectrum>(SpectrumPath);
			var normalizeAudio = GetNode<Button>(NormalizeAudioPath);
			normalizeAudio.Visible = false;
			spectrum.Visible = false;
			spectrum.DeleteBars();
			toggleAudioButton.Text = "Audio On";
		}
	}

	private void OnToggleDisplay()
		{
			var toggleDisplayButton = GetNode<Button>(ToggleDisplayPath);
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
		var monitorSelect = GetNode<OptionButton>(MonitorSelectPath);
		int selectedMonitor = monitorSelect.Selected;
		Vector2I res = DisplayServer.ScreenGetSize(selectedMonitor);
		var widthField = GetNode<LineEdit>(WidthFieldPath);
		var heightField = GetNode<LineEdit>(HeightFieldPath);
		widthField.Text = res.X.ToString();
		heightField.Text = res.Y.ToString();
		_lastValidWidth = widthField.Text;
		_lastValidHeight = heightField.Text;
	}

	private void ValidateWidthField()
	{
		var widthField = GetNode<LineEdit>(WidthFieldPath);
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
		var heightField = GetNode<LineEdit>(HeightFieldPath);
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
