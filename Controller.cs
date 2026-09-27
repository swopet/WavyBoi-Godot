
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

	private const int MaxResolution = 16384;

	// When true the render resolution follows the display (or the selected monitor while the
	// display is closed); when false the width/height fields are an override.
	private bool _autoResolution = true;

	private void OpenDisplayWindow()
	{
		// Close any existing window first
		CloseDisplayWindow();

		int selectedMonitor = GetNode<OptionButton>(MonitorSelectPath).Selected;

		// Configure before adding to the tree so the window doesn't first appear on the primary screen
		DisplayWindow = new Window
		{
			Title = "Display Window",
			InitialPosition = Window.WindowInitialPosition.CenterOtherScreen,
			CurrentScreen = selectedMonitor,
			Size = DisplayServer.ScreenGetSize(selectedMonitor),
		};
		// Godot doesn't close windows on its own; without this the OS close button does nothing
		DisplayWindow.CloseRequested += CloseDisplayWindow;
		DisplayWindow.SizeChanged += UpdateOutputResolution;
		GetTree().Root.AddChild(DisplayWindow);
		DisplayWindow.Mode = Window.ModeEnum.Fullscreen;
		var _displayRect = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.KeepSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f
		};
		DisplayWindow.AddChild(_displayRect);
		_displayRect.Texture = GetNode<VisualsGraphEdit>(GraphEditPath).GetOutputTexture() as Texture2D;
		GetNode<Button>(ToggleDisplayPath).Text = "Close Display Window";
		UpdateOutputResolution();
	}

	private void CloseDisplayWindow()
	{
		if (DisplayWindow != null)
		{
			DisplayWindow.Visible = false;
			DisplayWindow.QueueFree();
			DisplayWindow = null;
		}
		GetNode<Button>(ToggleDisplayPath).Text = "Open Display Window";
		UpdateOutputResolution();
	}

	/// <summary>
	/// Push the current render resolution to the graph: the display's real size in auto mode,
	/// otherwise the width/height fields.
	/// </summary>
	private void UpdateOutputResolution()
	{
		var widthField = GetNode<LineEdit>(WidthFieldPath);
		var heightField = GetNode<LineEdit>(HeightFieldPath);
		Vector2I resolution;
		if (_autoResolution)
		{
			resolution = DisplayWindow != null
				? DisplayWindow.Size
				: DisplayServer.ScreenGetSize(GetNode<OptionButton>(MonitorSelectPath).Selected);
			widthField.Text = resolution.X.ToString();
			heightField.Text = resolution.Y.ToString();
			_lastValidWidth = widthField.Text;
			_lastValidHeight = heightField.Text;
		}
		else
		{
			resolution = new Vector2I(int.Parse(_lastValidWidth), int.Parse(_lastValidHeight));
		}
		if (resolution.X <= 0 || resolution.Y <= 0) return; // window not laid out yet
		var graphEdit = GetNode<VisualsGraphEdit>(GraphEditPath);
		if (graphEdit.OutputResolution != resolution)
			graphEdit.OutputResolution = resolution;
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
		monitorSelect.ItemSelected += (index) => UpdateOutputResolution();
		// Auto is a toggle: on = follow the display, off = use the width/height fields
		var autoButton = GetNode<Button>(AutoButtonPath);
		autoButton.ToggleMode = true;
		autoButton.ButtonPressed = _autoResolution;
		autoButton.Toggled += OnAutoButtonToggled;

		// Connect validation for Width and Height fields
		var widthField = GetNode<LineEdit>(WidthFieldPath);
		var heightField = GetNode<LineEdit>(HeightFieldPath);
		widthField.TextSubmitted += OnWidthFieldTextSubmitted;
		widthField.FocusExited += OnWidthFieldFocusExited;
		heightField.TextSubmitted += OnHeightFieldTextSubmitted;
		heightField.FocusExited += OnHeightFieldFocusExited;

		widthField.Editable = !_autoResolution;
		heightField.Editable = !_autoResolution;


		// Populate AudioSelect OptionButton with available audio inputs
		var audioSelect = GetNode<OptionButton>(AudioSelectPath);
		var inputDevices = AudioServer.GetInputDeviceList();
		foreach (var device in inputDevices)
		{
			audioSelect.AddItem(device);
		}
		audioSelect.ItemSelected += (index) =>
		{
			// Switch devices live while audio is running
			if (_micPlayer != null) ApplySelectedInputDevice();
		};

		var toggleAudioButton = GetNode<Button>(ToggleAudioPath);
		toggleAudioButton.Pressed += OnToggleAudio;
	

		var normalizeAudioButton = GetNode<Button>(NormalizeAudioPath);
		normalizeAudioButton.Pressed += OnNormalizeAudio;
		
	
		// Connect ToggleDisplay button
		var toggleDisplayButton = GetNode<Button>(ToggleDisplayPath);
		toggleDisplayButton.Pressed += OnToggleDisplay;



		GetNode<VisualsGraphEdit>(GraphEditPath).CreateVisualBus();
		GetNode<VisualsGraphEdit>(GraphEditPath).spectrum = GetNode<Spectrum>(SpectrumPath);
		// Initialize fields with the selected monitor's resolution and size the graph to match
		UpdateOutputResolution();
	}

	private void ApplySelectedInputDevice()
	{
		// Look the device up by name: indices go stale if devices are plugged in or removed
		var audioSelect = GetNode<OptionButton>(AudioSelectPath);
		if (audioSelect.Selected < 0) return;
		string device = audioSelect.GetItemText(audioSelect.Selected);
		if (System.Array.IndexOf(AudioServer.GetInputDeviceList(), device) >= 0)
		{
			AudioServer.SetInputDevice(device);
		}
		else
		{
			GD.PushWarning($"Audio input device '{device}' is no longer available");
		}
	}

	private void OnNormalizeAudio()
	{
		var spectrum = GetNode<Spectrum>(SpectrumPath);
		spectrum.NormalizeAudio();
	}

	private void OnToggleAudio()
	{
		
		var toggleAudioButton = GetNode<Button>(ToggleAudioPath);

		if (_micPlayer == null)
		{
			
			_micPlayer = new AudioStreamPlayer();
			_micPlayer.Stream = new AudioStreamMicrophone();
			_micPlayer.Bus = "Spectrum";
			AddChild(_micPlayer);
			ApplySelectedInputDevice();
			_micPlayer.Play();
			toggleAudioButton.Text = "Audio Off";
			var spectrum = GetNode<Spectrum>(SpectrumPath);
			var normalizeAudio = GetNode<Button>(NormalizeAudioPath);
			normalizeAudio.Visible = true;
			spectrum.Visible = true;
			spectrum.NormalizeAudio();
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
			toggleAudioButton.Text = "Audio On";
		}
	}

	private void OnToggleDisplay()
	{
		// Button text is kept in sync by Open/CloseDisplayWindow, which the window's
		// own close button also goes through
		if (DisplayWindow == null)
		{
			OpenDisplayWindow();
		}
		else
		{
			CloseDisplayWindow();
		}
	}
	private string _lastValidWidth = "";
	private string _lastValidHeight = "";

	private void OnAutoButtonToggled(bool pressed)
	{
		_autoResolution = pressed;
		// Fields are read-only while auto; turning auto off starts the override from the current size
		GetNode<LineEdit>(WidthFieldPath).Editable = !pressed;
		GetNode<LineEdit>(HeightFieldPath).Editable = !pressed;
		UpdateOutputResolution();
	}

	private static bool IsValidDimension(string text)
	{
		return int.TryParse(text, out int value) && value > 0 && value <= MaxResolution;
	}

	private void ValidateWidthField()
	{
		var widthField = GetNode<LineEdit>(WidthFieldPath);
		string text = widthField.Text;
		if (IsValidDimension(text))
		{
			_lastValidWidth = text.Trim();
			UpdateOutputResolution();
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
		if (IsValidDimension(text))
		{
			_lastValidHeight = text.Trim();
			UpdateOutputResolution();
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

}
