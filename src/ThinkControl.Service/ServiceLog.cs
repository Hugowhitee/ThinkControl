namespace ThinkControl.Service;

internal static class ServiceLog
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ThinkControl",
        "hardware-service.log");

    internal static string PathName => LogPath;

    internal static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                string? directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}  {message}{Environment.NewLine}");

                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    string archive = LogPath + ".old";
                    try { File.Delete(archive); } catch { }
                    File.Move(LogPath, archive, overwrite: true);
                }
            }
        }
        catch
        {
            // Logging is diagnostic only and can never take the service down.
        }
    }
}
