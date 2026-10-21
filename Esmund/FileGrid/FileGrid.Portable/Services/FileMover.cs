using System.IO;

namespace FileGrid.Portable.Services;

public enum ConflictChoice { Replace, KeepBoth, Skip, Cancel }

public sealed class MoveConflictInfo
{
    public required string SourcePath { get; init; }
    public required string DestDir { get; init; }
    public required string Name { get; init; }
    public required string ExistingPath { get; init; }
}

public sealed class MoveResult
{
    public required string Path { get; init; }
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public bool Skipped { get; init; }
    public bool Renamed { get; init; }
    public bool Aborted { get; init; }
    public string? DestPath { get; init; }
}

public sealed class FileMover
{
    public Func<MoveConflictInfo, Task<ConflictChoice>>? ResolveConflict { get; set; }

    public async Task<List<MoveResult>> MovePathsAsync(string destDir, IEnumerable<string> paths)
    {
        var results = new List<MoveResult>();
        foreach (var source in paths)
        {
            try
            {
                var r = await MoveOneAsync(destDir, source, async info =>
                {
                    if (ResolveConflict == null) throw new IOException($"\"{info.Name}\" already exists.");
                    return await ResolveConflict(info);
                });
                results.Add(r);
                if (r.Aborted) break;
            }
            catch (Exception ex)
            {
                results.Add(new MoveResult { Path = source, Ok = false, Error = ex.Message });
            }
        }

        return results;

        async Task<MoveResult> MoveOneAsync(string dir, string source, Func<MoveConflictInfo, Task<ConflictChoice>> resolve)
        {
            var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name)) throw new IOException("Invalid path.");
            var dest = Path.Combine(dir, name);
            var renamed = false;

            if (File.Exists(dest) || Directory.Exists(dest))
            {
                var choice = await resolve(new MoveConflictInfo
                {
                    SourcePath = source,
                    DestDir = dir,
                    Name = name,
                    ExistingPath = dest
                });
                if (choice == ConflictChoice.Cancel)
                    return new MoveResult { Path = source, Ok = false, Aborted = true, Error = "Cancelled" };
                if (choice == ConflictChoice.Skip)
                    return new MoveResult { Path = source, Ok = true, Skipped = true };
                if (choice == ConflictChoice.Replace)
                {
                    if (Directory.Exists(dest)) Directory.Delete(dest, true);
                    else File.Delete(dest);
                }
                else
                {
                    dest = UniqueDest(dir, name, Directory.Exists(source));
                    renamed = true;
                }
            }

            MoveTo(source, dest);
            return new MoveResult { Path = source, Ok = true, DestPath = dest, Renamed = renamed };
        }
    }

    private static string UniqueDest(string dir, string name, bool isDir)
    {
        var initial = Path.Combine(dir, name);
        if (!File.Exists(initial) && !Directory.Exists(initial)) return initial;

        string stem, ext;
        if (isDir)
        {
            stem = name;
            ext = "";
        }
        else
        {
            ext = Path.GetExtension(name);
            stem = Path.GetFileNameWithoutExtension(name);
        }

        for (var n = 2; n < 10_000; n++)
        {
            var candidateName = isDir ? $"{stem} ({n})" : $"{stem} ({n}){ext}";
            var candidate = Path.Combine(dir, candidateName);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException($"Could not find unique name for {name}");
    }

    private static void MoveTo(string source, string dest)
    {
        try
        {
            if (Directory.Exists(source))
            {
                Directory.Move(source, dest);
                return;
            }
            File.Move(source, dest);
        }
        catch (IOException)
        {
            if (Directory.Exists(source))
                CopyDirectory(source, dest, true);
            else
            {
                File.Copy(source, dest, overwrite: false);
                File.Delete(source);
            }
        }
    }

    private static void CopyDirectory(string source, string dest, bool deleteSource)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
        if (deleteSource) Directory.Delete(source, true);
    }
}
