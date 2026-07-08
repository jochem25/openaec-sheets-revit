using System.Text.Json;
using System.Text.Json.Serialization;

using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.Core.Services;

/// <summary>
/// Bewaart exportprofielen als JSON in %APPDATA%\OpenAEC\SheetExporter\Profiles.
/// </summary>
public sealed class ProfileStore
{
    private readonly string _directory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public ProfileStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenAEC", "SheetExporter", "Profiles");
        Directory.CreateDirectory(_directory);
    }

    public IReadOnlyList<string> ListNames() =>
        Directory.EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public ExportProfile? Load(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ExportProfile>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(ExportProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(PathFor(profile.Name), json);
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    private string PathFor(string name) =>
        Path.Combine(_directory, Naming.NamingEngine.Sanitize(name) + ".json");
}
