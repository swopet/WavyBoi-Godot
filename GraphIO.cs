using Godot;
using Godot.Collections;
using System.Linq;

/// <summary>
/// Reading and writing saved Modules and gradient presets as JSON under user://.
/// </summary>
public static class GraphIO
{
	public const string ModuleDir = "user://modules";
	// Modules that ship with the project (read-only)
	public const string BuiltinModuleDir = "res://BuiltinModules";
	public const string GradientDir = "user://gradients";
	public const string ProjectDir = "user://projects";
	private const int FormatVersion = 1;

	// Same characters Godot's String.validate_filename replaces (not exposed to C#)
	private static readonly char[] InvalidFileChars = [':', '/', '\\', '?', '*', '"', '|', '%', '<', '>'];

	public static string SanitizeName(string name)
	{
		var chars = name.Trim().Select(c => InvalidFileChars.Contains(c) || char.IsControl(c) ? '_' : c).ToArray();
		return new string(chars);
	}

	public static string PathFor(string dir, string name) => $"{dir}/{SanitizeName(name)}.json";

	public static bool Exists(string dir, string name) => FileAccess.FileExists(PathFor(dir, name));

	public static Error Save(string dir, string name, Dictionary data)
	{
		DirAccess.MakeDirRecursiveAbsolute(dir);
		var document = new Dictionary
		{
			["version"] = FormatVersion,
			["name"] = name,
			["data"] = data,
		};
		using var file = FileAccess.Open(PathFor(dir, name), FileAccess.ModeFlags.Write);
		if (file == null) return FileAccess.GetOpenError();
		file.StoreString(Json.Stringify(document, "\t", true, true));
		return Error.Ok;
	}

	public static Dictionary Load(string dir, string name)
	{
		string path = PathFor(dir, name);
		if (!FileAccess.FileExists(path)) return null;
		Variant parsed = Json.ParseString(FileAccess.GetFileAsString(path));
		if (parsed.VariantType != Variant.Type.Dictionary) return null;
		var document = (Dictionary)parsed;
		if (!document.ContainsKey("data") || document["data"].VariantType != Variant.Type.Dictionary) return null;
		return (Dictionary)document["data"];
	}

	/// <summary>Move modules saved before the SubGraph → Module rename into the modules folder.</summary>
	public static void MigrateLegacyFolders()
	{
		const string legacyModuleDir = "user://subgraphs";
		if (!DirAccess.DirExistsAbsolute(legacyModuleDir)) return;
		DirAccess.MakeDirRecursiveAbsolute(ModuleDir);
		foreach (var file in DirAccess.GetFilesAt(legacyModuleDir))
		{
			string target = $"{ModuleDir}/{file}";
			if (!FileAccess.FileExists(target)) DirAccess.RenameAbsolute($"{legacyModuleDir}/{file}", target);
		}
		if (DirAccess.GetFilesAt(legacyModuleDir).Length == 0) DirAccess.RemoveAbsolute(legacyModuleDir);
	}

	public static string[] List(string dir)
	{
		if (!DirAccess.DirExistsAbsolute(dir)) return [];
		return DirAccess.GetFilesAt(dir)
			.Where(file => file.EndsWith(".json"))
			.Select(file => file.GetBaseName())
			.OrderBy(name => name)
			.ToArray();
	}

	// JSON has no vector/color types, so store them as number arrays
	public static Array ToArray(Vector2 v) => [v.X, v.Y];
	public static Array ToArray(Color c) => [c.R, c.G, c.B, c.A];

	public static Vector2 ToVector2(Variant v)
	{
		var a = (Array)v;
		return new Vector2((float)a[0], (float)a[1]);
	}

	public static Color ToColor(Variant v)
	{
		var a = (Array)v;
		return new Color((float)a[0], (float)a[1], (float)a[2], a.Count > 3 ? (float)a[3] : 1.0f);
	}
}
