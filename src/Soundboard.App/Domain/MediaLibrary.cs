namespace Soundboard.Core;

/// <summary>
/// Copies user-chosen audio into the app library under the config directory
/// (<c>%AppData%\Soundboard\library\</c> in production) and stores a relative path.
/// </summary>
public static class MediaLibrary
{
    public const string FolderName = "library";

    public static string DirectoryFor(string configDirectory) =>
        Path.Combine(configDirectory, FolderName);

    public static string ToStoredPath(string fileName) =>
        FolderName + "/" + fileName;

    public static bool IsLibraryRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = PathResolver.NormalizeSeparators(path.Trim())
            .TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = FolderName + Path.DirectorySeparatorChar;
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string SanitizeFileName(string? name)
    {
        name ??= "";
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        name = name.Trim().Trim('.');
        if (name.Length > 80)
        {
            name = name[..80];
        }

        return string.IsNullOrWhiteSpace(name) ? "audio" : name;
    }

    /// <summary>
    /// Copies <paramref name="sourcePath"/> into the library. Name is
    /// <c>{slotId}_{original}{ext}</c> so re-import of the same original name replaces.
    /// Returns a relative path like <c>library/abc_cheer.mp3</c>.
    /// </summary>
    public static string Import(string sourcePath, string configDirectory, string slotId)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("找不到要导入的音频文件。", sourcePath);
        }

        var library = DirectoryFor(configDirectory);
        Directory.CreateDirectory(library);

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(ext))
        {
            ext = ".bin";
        }

        var original = SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        var idPart = SanitizeFileName(string.IsNullOrWhiteSpace(slotId) ? Guid.NewGuid().ToString("N") : slotId);
        var destName = $"{idPart}_{original}{ext.ToLowerInvariant()}";
        var dest = Path.Combine(library, destName);
        File.Copy(sourcePath, dest, overwrite: true);
        return ToStoredPath(destName);
    }

    public static bool TryDeleteStoredFile(string configDirectory, string? storedPath)
    {
        if (!IsLibraryRelative(storedPath))
        {
            return false;
        }

        var full = PathResolver.Resolve(storedPath, configDirectory);
        try
        {
            var library = Path.GetFullPath(DirectoryFor(configDirectory));
            var candidate = Path.GetFullPath(full);
            if (!candidate.StartsWith(library, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(candidate))
            {
                return false;
            }

            File.Delete(candidate);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort: copy any still-present referenced files into the library and
    /// rewrite slot paths to relative <c>library/...</c> entries.
    /// </summary>
    public static (int Copied, int Missing) ImportReferencedFiles(
        AppConfig config,
        string configDirectory,
        params string[] searchDirectories)
    {
        var copied = 0;
        var missing = 0;
        foreach (var slot in config.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.FilePath))
            {
                continue;
            }

            string? found = null;
            foreach (var root in searchDirectories.Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                var resolved = PathResolver.Resolve(slot.FilePath, root, root);
                if (File.Exists(resolved))
                {
                    found = resolved;
                    break;
                }
            }

            if (found is null)
            {
                var fromConfig = PathResolver.Resolve(slot.FilePath, configDirectory);
                if (File.Exists(fromConfig))
                {
                    found = fromConfig;
                }
            }

            if (found is null)
            {
                missing++;
                continue;
            }

            if (IsLibraryRelative(slot.FilePath)
                && File.Exists(PathResolver.Resolve(slot.FilePath, configDirectory)))
            {
                continue;
            }

            try
            {
                slot.FilePath = Import(found, configDirectory, slot.Id);
                copied++;
            }
            catch
            {
                missing++;
            }
        }

        return (copied, missing);
    }
}
