namespace ReelClean.Services.Naming;

public sealed class FilenameSanitizer : IFilenameSanitizer
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "episode";
        }

        var buffer = new char[name.Length];
        int length = 0;
        bool pendingSpace = false;
        foreach (char ch in name.Trim())
        {
            char current = ch < ' ' || ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                ? ' '
                : ch;
            if (current == ' ')
            {
                pendingSpace = length > 0;
                continue;
            }

            if (pendingSpace)
            {
                buffer[length++] = ' ';
                pendingSpace = false;
            }

            buffer[length++] = current;
        }

        string cleaned = new string(buffer, 0, length).Trim().TrimEnd('.', ' ');
        if (cleaned.Length == 0)
        {
            return "episode";
        }

        string extension = Path.GetExtension(cleaned);
        string stem = Path.GetFileNameWithoutExtension(cleaned);
        if (Reserved.Contains(stem))
        {
            stem += "_";
        }

        if (stem.Length + extension.Length > Constants.MaxFileNameLength)
        {
            int keep = Math.Max(1, Constants.MaxFileNameLength - extension.Length);
            stem = stem[..Math.Min(stem.Length, keep)].TrimEnd('.', ' ');
        }

        string result = stem + extension;
        return result.Length == 0 ? "episode" : result;
    }
}
