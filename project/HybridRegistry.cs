using Godot;
using System.Collections.Generic;

public partial class HybridRegistry : Node
{
    public static HybridRegistry Instance { get; private set; }

    private readonly Dictionary<string, Texture2D> _hybridTextures = new();

    public override void _EnterTree()
    {
        if (Instance == null)
            Instance = this;
        else
            QueueFree();
    }

    public override void _Ready()
    {
        LoadHybrids();
    }

    private void LoadHybrids()
    {
        _hybridTextures.Clear();

        const string hybridsDir = "res://assets/hybrids";

        if (!DirAccess.DirExistsAbsolute(hybridsDir))
        {
            GD.Print($"[Hybrids] Directory not found: {hybridsDir}");
            return;
        }

        using var dir = DirAccess.Open(hybridsDir);
        if (dir == null)
        {
            GD.PrintErr($"[Hybrids] Failed to open: {hybridsDir}");
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            var fileName = dir.GetNext();
            if (string.IsNullOrEmpty(fileName))
                break;

            if (dir.CurrentIsDir())
                continue;

            if (!fileName.EndsWith(".png") && !fileName.EndsWith(".jpg"))
                continue;

            // Имя файла = speciesA_speciesB.png
            var nameWithoutExt = fileName.GetBaseName();
            var parts = nameWithoutExt.Split('_');

            if (parts.Length < 2)
            {
                GD.Print($"[Hybrids] Skipping {fileName} (expected format: speciesA_speciesB.png)");
                continue;
            }

            var speciesA = parts[0].Trim().ToLowerInvariant();
            var speciesB = parts[1].Trim().ToLowerInvariant();

            var fullPath = $"{hybridsDir}/{fileName}";
            var texture = GD.Load<Texture2D>(fullPath);

            if (texture == null)
            {
                GD.PrintErr($"[Hybrids] Failed to load: {fullPath}");
                continue;
            }

            var key = MakeKey(speciesA, speciesB);
            _hybridTextures[key] = texture;
            GD.Print($"[Hybrids] Loaded: {key} from {fileName}");
        }
        dir.ListDirEnd();

        GD.Print($"[Hybrids] Total loaded: {_hybridTextures.Count}");
    }

    public Texture2D GetHybridTexture(string speciesA, string speciesB)
    {
        var key = MakeKey(speciesA, speciesB);

        if (_hybridTextures.TryGetValue(key, out var texture))
            return texture;

        return null;
    }

    private string MakeKey(string a, string b)
    {
        a = a?.Trim().ToLowerInvariant() ?? "";
        b = b?.Trim().ToLowerInvariant() ?? "";

        if (string.Compare(a, b) > 0)
            (a, b) = (b, a);

        return $"{a}+{b}";
    }
}