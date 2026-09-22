namespace Soundboard.Core;

public static class PathResolver
{
    /// <summary>
    /// Resolves an audio path from config. Absolute paths are used as-is;
    /// relative paths are tried against the config directory, then the exe directory.
    /// </summary>
    public static string Resolve(string? path, string configDirectory, string? exeDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        path = NormalizeSeparators(path);
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var fromConfig = Path.GetFullPath(Path.Combine(configDirectory, path));
        if (File.Exists(fromConfig))
        {
            return fromConfig;
        }

        if (!string.IsNullOrWhiteSpace(exeDirectory))
        {
            var fromExe = Path.GetFullPath(Path.Combine(exeDirectory, path));
            if (File.Exists(fromExe))
            {
                return fromExe;
            }
        }

        return fromConfig;
    }

    /// <summary>
    /// Config JSON may use <c>library\file.mp3</c> or <c>library/file.mp3</c>.
    /// Normalize so <see cref="Path.Combine"/> works on Linux test hosts too.
    /// </summary>
    public static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    public static bool IsSupportedAudio(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".ogg" or ".aiff" or ".aif" or ".wma" or ".flac" or ".m4a";
    }
}
