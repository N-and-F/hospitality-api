namespace Hospitality.Api.Common;

public static class EnvFile
{
    public static void LoadIfPresent()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(cwd, ".env"),
            Path.Combine(cwd, "..", ".env"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"))
        };

        foreach (var path in candidates.Distinct(StringComparer.Ordinal))
        {
            if (File.Exists(path))
            {
                Load(path);
                return;
            }
        }
    }

    private static void Load(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                (value[0] == value[^1]) &&
                (value[0] is '"' or '\''))
            {
                value = value[1..^1];
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
