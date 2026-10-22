namespace VisualDiffLab.App.Services;

/// <summary>
/// Startup arguments used by automated UI tests and local verification:
///   --a &lt;path&gt; --b &lt;path&gt;
/// They are not exposed as a user-facing feature.
/// </summary>
public sealed record StartupOptions(string? ImageAPath, string? ImageBPath, string? Language)
{
    public static StartupOptions Parse(IEnumerable<string> args)
    {
        string? a = null;
        string? b = null;
        string? language = null;
        string[] values = args.ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], "--a", StringComparison.OrdinalIgnoreCase) && i + 1 < values.Length)
            {
                a = values[++i];
            }
            else if (string.Equals(values[i], "--b", StringComparison.OrdinalIgnoreCase) && i + 1 < values.Length)
            {
                b = values[++i];
            }
            else if (string.Equals(values[i], "--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < values.Length)
            {
                language = values[++i];
            }
        }

        return new StartupOptions(a, b, language);
    }
}
