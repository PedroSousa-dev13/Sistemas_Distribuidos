namespace SharedProtocol;

public static class LogHelper
{
    private static readonly object Lock = new();
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static void Write(string filePath, string message)
    {
        lock (Lock)
        {
            try
            {
                RotateIfNeeded(filePath);
                File.AppendAllText(filePath, $"{DateTime.Now:o}: {message}\n");
            }
            catch { }
        }
    }

    private static void RotateIfNeeded(string filePath)
    {
        try
        {
            var fi = new FileInfo(filePath);
            if (fi.Exists && fi.Length > MaxFileSizeBytes)
            {
                string backup = $"{filePath}.{DateTime.Now:yyyyMMddHHmmss}.bak";
                File.Move(filePath, backup);
            }
        }
        catch { }
    }
}
