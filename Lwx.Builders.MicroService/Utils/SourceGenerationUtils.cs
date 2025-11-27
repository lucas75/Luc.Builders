using System.Text;

namespace Lwx.Builders.MicroService.Utils;

internal static class SourceGenerationUtils
{
    /// <summary>
    /// Escape a string so it is safe to embed in a C# double-quoted string literal.
    /// This escapes backslashes and double-quotes and replaces newline variants with the escaped \n sequence.
    /// </summary>
    public static string EscapeForCSharp(string? s)
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
