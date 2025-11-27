using System.Text;

namespace Lwx.Builders.MicroService;

internal static class Util
{
    /// <summary>
    /// Escape a string so it is safe to embed in a C# double-quoted string literal.
    /// This escapes backslashes and double-quotes and replaces newline variants with the escaped \n sequence.
    /// </summary>
    public static string EscapeForCSharp(this string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        // Replace backslash first to avoid double-escaping
        var sb = new StringBuilder(s.Length * 2);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\n"); break; // normalize CR to \n
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(ch); break;
            }
        }

        return sb.ToString();
    }
}

// Helper extension to fix indentation when embedding generated multi-line text
internal static class LwxGeneratedStringExtensions
{
    public static string FixIndent(this StringBuilder source, int indentLevels)
    {
        if (source == null) return string.Empty;
        return source.ToString().FixIndent(indentLevels);
    }

    /// <summary>
    /// Fix the indentation of a multi-line snippet. The parameter <paramref name="indentLevels"/>
    /// is treated as indentation levels and expanded to spaces using 4 spaces per level.
    /// For example, FixIndent(1) prefixes each non-empty line with 4 spaces.
    /// </summary>
    public static string FixIndent(this string source, int indentLevels)
    {
        if (string.IsNullOrEmpty(source)) return source;
        if (indentLevels <= 0) return source;
        // Treat indentLevels as the number of indentation units (4 spaces each)
        var spacesPerLevel = 4;
        var indent = indentLevels * spacesPerLevel;
        var indentStr = new string(' ', indent);
        // Normalize line endings to \n for predictable behavior
        var normalized = source.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
            {
                lines[i] = indentStr + lines[i];
            }
        }
        return string.Join("\n", lines);
    }
}
