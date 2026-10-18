using System.IO;
using FileGrid.Portable.Models;

namespace FileGrid.Portable.Services;

public sealed class LayoutRepository
{
    public List<LayoutState> List()
    {
        AppPaths.EnsureDataDirs();
        var layouts = new List<LayoutState>();
        foreach (var file in Directory.GetFiles(AppPaths.LayoutsDir, "*.json"))
        {
            try
            {
                var layout = JsonStore.Read<LayoutState>(file);
                if (layout != null)
                {
                    Normalize(layout);
                    layouts.Add(layout);
                }
            }
            catch { /* skip corrupt */ }
        }
        return layouts.OrderByDescending(l => l.UpdatedAt).ToList();
    }

    public LayoutState? Load(string id)
    {
        var path = LayoutPath(id);
        var layout = JsonStore.Read<LayoutState>(path);
        if (layout != null) Normalize(layout);
        return layout;
    }

    private static void Normalize(LayoutState layout)
    {
        layout.Links ??= new();
        layout.Boxes ??= new();
        layout.View ??= new ViewTransform();
    }

    public void Save(LayoutState layout)
    {
        layout.UpdatedAt = DateTime.UtcNow.ToString("o");
        layout.Links ??= new();
        JsonStore.Write(LayoutPath(layout.Id), layout);
    }

    public void Delete(string id)
    {
        try { File.Delete(LayoutPath(id)); } catch { /* ignore */ }
    }

    private static string LayoutPath(string id) => Path.Combine(AppPaths.LayoutsDir, $"{id}.json");
}
