
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
	[Export] NodePath ProjectMenuPath;
	private GraphNavigator _navigator;
	private string _projectName = null; // null until the project is saved or opened
	private Window DisplayWindow = null;
	[Export] NodePath AudioFileHBoxPath;
	private AudioStreamPlayer _audioPlayer = null; // plays the mic or the audio file while audio is on

	// Audio source: the first AudioSelect entry means "no input device, play a file instead"
	private const int NoInputDeviceIndex = 0;
	private const string SpectrumBus = "Spectrum";
	private AudioStream _audioFileStream = null;
	private string _audioFilePath = null;
	private float _spectrumBusVolumeDb;

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
		GraphIO.MigrateLegacyFolders();
		SetupProjectMenu();

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
		audioSelect.AddItem("None (play audio file)");
		foreach (var device in inputDevices)
		{
			audioSelect.AddItem(device);
		}
		// Keep the first real device selected by default, as before
		if (inputDevices.Length > 0) audioSelect.Select(NoInputDeviceIndex + 1);
		audioSelect.ItemSelected += (index) => OnAudioSourceChanged();
		GetNode<Button>(AudioFileHBoxPath + "/LoadAudioFile").Pressed += ShowAudioFileDialog;
		_spectrumBusVolumeDb = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(SpectrumBus));
		UpdateAudioSourceUI();

		var toggleAudioButton = GetNode<Button>(ToggleAudioPath);
		toggleAudioButton.Pressed += OnToggleAudio;
	

		var normalizeAudioButton = GetNode<Button>(NormalizeAudioPath);
		normalizeAudioButton.Pressed += OnNormalizeAudio;
		
	
		// Connect ToggleDisplay button
		var toggleDisplayButton = GetNode<Button>(ToggleDisplayPath);
		toggleDisplayButton.Pressed += OnToggleDisplay;



		// Breadcrumb bar for stepping in and out of Modules, along the bottom-left edge
		_navigator = new GraphNavigator { Name = "GraphNavigator" };
		AddChild(_navigator);
		_navigator.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize, 8);
		_navigator.GrowVertical = GrowDirection.Begin;
		_navigator.Init(GetNode<VisualsGraphEdit>(GraphEditPath));

		GetNode<VisualsGraphEdit>(GraphEditPath).CreateVisualBus();
		GetNode<VisualsGraphEdit>(GraphEditPath).spectrum = GetNode<Spectrum>(SpectrumPath);
		// Initialize fields with the selected monitor's resolution and size the graph to match
		UpdateOutputResolution();
	}

	private bool UsingAudioFile => GetNode<OptionButton>(AudioSelectPath).Selected == NoInputDeviceIndex;

	private void ApplySelectedInputDevice()
	{
		// Look the device up by name: indices go stale if devices are plugged in or removed
		var audioSelect = GetNode<OptionButton>(AudioSelectPath);
		if (audioSelect.Selected <= NoInputDeviceIndex) return;
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

	private void OnAudioSourceChanged()
	{
		UpdateAudioSourceUI();
		// Switch sources live while audio is running
		if (_audioPlayer != null)
		{
			StopAudio();
			StartAudio();
		}
	}

	/// <summary>The file row only shows when no input device is selected.</summary>
	private void UpdateAudioSourceUI()
	{
		GetNode<Control>(AudioFileHBoxPath).Visible = UsingAudioFile;
		GetNode<Label>(AudioFileHBoxPath + "/AudioFileLabel").Text =
			_audioFilePath == null ? "(no file)" : _audioFilePath.GetFile();
		var toggleAudio = GetNode<Button>(ToggleAudioPath);
		bool canPlay = !UsingAudioFile || _audioFileStream != null;
		toggleAudio.Disabled = _audioPlayer == null && !canPlay;
		toggleAudio.TooltipText = canPlay ? "" : "Load an audio file first";
	}

	private void ShowAudioFileDialog()
	{
		var dialog = new FileDialog
		{
			Title = "Load Audio File",
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Filesystem,
			Filters = ["*.mp3, *.ogg, *.wav ; Audio Files"],
			UseNativeDialog = true,
		};
		dialog.FileSelected += path => LoadAudioFile(path);
		dialog.VisibilityChanged += () =>
		{
			if (!dialog.Visible) dialog.QueueFree();
		};
		GetTree().Root.AddChild(dialog);
		dialog.PopupCentered(new Vector2I(800, 500));
	}

	public bool LoadAudioFile(string path)
	{
		AudioStream stream = path.GetExtension().ToLowerInvariant() switch
		{
			"mp3" => AudioStreamMP3.LoadFromFile(path),
			"ogg" => AudioStreamOggVorbis.LoadFromFile(path),
			"wav" => AudioStreamWav.LoadFromFile(path),
			_ => null,
		};
		if (stream == null)
		{
			Dialogs.ShowMessage(this, "Load Audio File", $"Couldn't load '{path.GetFile()}'. Supported formats: MP3, OGG Vorbis, WAV.");
			return false;
		}
		_audioFileStream = stream;
		_audioFilePath = path;
		UpdateAudioSourceUI();
		// Swap to the new file right away if it's already playing
		if (_audioPlayer != null && UsingAudioFile)
		{
			StopAudio();
			StartAudio();
		}
		return true;
	}

	private void StartAudio()
	{
		bool fromFile = UsingAudioFile;
		if (fromFile && _audioFileStream == null) return;
		_audioPlayer = new AudioStreamPlayer
		{
			Stream = fromFile ? _audioFileStream : new AudioStreamMicrophone(),
			Bus = SpectrumBus,
		};
		// Loop the file
		if (fromFile) _audioPlayer.Finished += () => _audioPlayer?.Play();
		AddChild(_audioPlayer);
		// The mic is analysed silently (muted bus, so no feedback); a file should be heard at normal level
		int bus = AudioServer.GetBusIndex(SpectrumBus);
		AudioServer.SetBusMute(bus, !fromFile);
		AudioServer.SetBusVolumeDb(bus, fromFile ? 0.0f : _spectrumBusVolumeDb);
		if (!fromFile) ApplySelectedInputDevice();
		_audioPlayer.Play();

		GetNode<Button>(ToggleAudioPath).Text = "Audio Off";
		GetNode<Button>(NormalizeAudioPath).Visible = true;
		var spectrum = GetNode<Spectrum>(SpectrumPath);
		spectrum.Visible = true;
		spectrum.NormalizeAudio();
		UpdateAudioSourceUI();
	}

	private void StopAudio()
	{
		if (_audioPlayer != null)
		{
			_audioPlayer.Stop();
			_audioPlayer.QueueFree();
			_audioPlayer = null;
		}
		AudioServer.SetBusMute(AudioServer.GetBusIndex(SpectrumBus), true);
		GetNode<Button>(ToggleAudioPath).Text = "Audio On";
		GetNode<Button>(NormalizeAudioPath).Visible = false;
		GetNode<Spectrum>(SpectrumPath).Visible = false;
		UpdateAudioSourceUI();
	}

	private void OnNormalizeAudio()
	{
		var spectrum = GetNode<Spectrum>(SpectrumPath);
		spectrum.NormalizeAudio();
	}

	private void OnToggleAudio()
	{
		if (_audioPlayer == null) StartAudio();
		else StopAudio();
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

	// ---- Projects: the whole graph (with nested modules), the output bus and settings

	private enum ProjectMenuId { New, Save, SaveAs }

	private void SetupProjectMenu()
	{
		var menuButton = GetNode<MenuButton>(ProjectMenuPath);
		menuButton.AboutToPopup += RebuildProjectMenu;
		menuButton.GetPopup().IdPressed += id =>
		{
			switch ((ProjectMenuId)(int)id)
			{
				case ProjectMenuId.New:
					Dialogs.Confirm(this, "New Project", "Start a new, empty project? Unsaved changes will be lost.", NewProject);
					break;
				case ProjectMenuId.Save: SaveProject(); break;
				case ProjectMenuId.SaveAs: SaveProjectAs(); break;
			}
		};
		UpdateProjectTitle();
	}

	private void RebuildProjectMenu()
	{
		var popup = GetNode<MenuButton>(ProjectMenuPath).GetPopup();
		popup.Clear(true); // also frees the old Open submenu
		popup.AddItem("New", (int)ProjectMenuId.New);
		var openMenu = new PopupMenu();
		var projects = GraphIO.List(GraphIO.ProjectDir);
		foreach (var name in projects) openMenu.AddItem(name);
		if (projects.Length == 0)
		{
			openMenu.AddItem("(none saved)");
			openMenu.SetItemDisabled(0, true);
		}
		openMenu.IndexPressed += index =>
		{
			string name = openMenu.GetItemText((int)index);
			Dialogs.Confirm(this, "Open Project", $"Open '{name}'? Unsaved changes will be lost.", () => OpenProject(name));
		};
		popup.AddSubmenuNodeItem("Open", openMenu);
		popup.AddItem("Save   (Ctrl+S)", (int)ProjectMenuId.Save);
		popup.AddItem("Save As…", (int)ProjectMenuId.SaveAs);
	}

	private void UpdateProjectTitle()
	{
		GetNode<MenuButton>(ProjectMenuPath).Text = $"Project: {_projectName ?? "Untitled"}";
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.S, CtrlPressed: true })
		{
			SaveProject();
			GetViewport().SetInputAsHandled();
		}
	}

	public void SaveProject()
	{
		if (_projectName == null)
		{
			SaveProjectAs();
			return;
		}
		var error = GraphIO.Save(GraphIO.ProjectDir, _projectName, GetProjectData());
		if (error != Error.Ok) Dialogs.ShowMessage(this, "Save Project", $"Couldn't save '{_projectName}': {error}");
	}

	public void SaveProjectAs()
	{
		Dialogs.SaveNamed(this, "Save Project", GraphIO.ProjectDir, _projectName ?? "Untitled", GetProjectData, name =>
		{
			_projectName = name;
			UpdateProjectTitle();
		});
	}

	public bool OpenProject(string name)
	{
		var data = GraphIO.Load(GraphIO.ProjectDir, name);
		if (data == null)
		{
			Dialogs.ShowMessage(this, "Open Project", $"Couldn't read project '{name}'.");
			return false;
		}
		ClearProject();
		GetNode<VisualsGraphEdit>(GraphEditPath).LoadGraph((Godot.Collections.Dictionary)data["graph"]);
		ApplySettings((Godot.Collections.Dictionary)data["settings"]);
		_projectName = name;
		UpdateProjectTitle();
		return true;
	}

	public void NewProject()
	{
		ClearProject();
		_projectName = null;
		UpdateProjectTitle();
	}

	private Godot.Collections.Dictionary GetProjectData()
	{
		return new Godot.Collections.Dictionary
		{
			["graph"] = GetNode<VisualsGraphEdit>(GraphEditPath).SerializeGraph(),
			["settings"] = GetSettings(),
		};
	}

	/// <summary>Back to just the output bus with a single bus.</summary>
	private void ClearProject()
	{
		_navigator.GoTo(0);
		var graph = GetNode<VisualsGraphEdit>(GraphEditPath);
		graph.DeleteNodes(graph.GetChildren().OfType<GraphNode>().ToList());
		graph.GetChildren().OfType<VisualBusNode>().FirstOrDefault()?.Load(new Godot.Collections.Dictionary
		{
			["weights"] = new Godot.Collections.Array { 1.0f },
			["priority"] = 0,
			["fade_time"] = 0.5f,
			["exclusive"] = false,
		});
		graph.ScrollOffset = Vector2.Zero;
	}

	private Godot.Collections.Dictionary GetSettings()
	{
		return new Godot.Collections.Dictionary
		{
			["auto_resolution"] = _autoResolution,
			["width"] = int.Parse(_lastValidWidth),
			["height"] = int.Parse(_lastValidHeight),
			["audio_file"] = _audioFilePath ?? "",
		};
	}

	private void ApplySettings(Godot.Collections.Dictionary settings)
	{
		if (settings.ContainsKey("width") && settings.ContainsKey("height"))
		{
			string width = ((int)settings["width"]).ToString();
			string height = ((int)settings["height"]).ToString();
			if (IsValidDimension(width) && IsValidDimension(height))
			{
				_lastValidWidth = width;
				_lastValidHeight = height;
				GetNode<LineEdit>(WidthFieldPath).Text = width;
				GetNode<LineEdit>(HeightFieldPath).Text = height;
			}
		}
		if (settings.ContainsKey("auto_resolution"))
		{
			// Emits Toggled (which updates the resolution) only when the state changes
			GetNode<Button>(AutoButtonPath).ButtonPressed = (bool)settings["auto_resolution"];
		}
		UpdateOutputResolution();
		// Reload the project's audio file if it's still there (it isn't auto-played)
		if (settings.ContainsKey("audio_file") && (string)settings["audio_file"] is string audioFile
			&& audioFile != "" && audioFile != _audioFilePath && FileAccess.FileExists(audioFile))
		{
			LoadAudioFile(audioFile);
		}
	}
}
