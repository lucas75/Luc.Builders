using System;
using System.Text;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Lwx.Builders.MicroService.Processors;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lwx.Builders.MicroService;

/// <summary>
/// Lightweight representation of an attribute instance discovered during syntax analysis.
/// This type is shared between the generator and processors and therefore lives at the
/// generator/root namespace so processors can reference it before their own type-level
/// abstractions are applied.
/// </summary>
internal sealed class AttributeInstance(
    string attributeName,
    ISymbol targetSymbol,
    Location location,
    AttributeData? attributeData
)
{
    public string AttributeName { get; } = attributeName;
    public ISymbol TargetSymbol { get; } = targetSymbol;
    public Location Location { get; } = location;
    public AttributeData? AttributeData { get; } = attributeData;
}

internal static class GeneratorUtils
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

    /// <summary>
    /// Resolve an AttributeInstance from a <see cref="GeneratorSyntaxContext"/> if the node
    /// represents one of the known LWX attributes. Returns null when not applicable.
    /// </summary>
    internal static AttributeInstance? ResolveAttributeInstance(GeneratorSyntaxContext ctx)
    {
        var attributeSyntax = (AttributeSyntax)ctx.Node;

        // Use the semantic model to determine the attribute type correctly
        var info = ctx.SemanticModel.GetSymbolInfo(attributeSyntax, CancellationToken.None);
        var attrType = info.Symbol switch
        {
            IMethodSymbol ms => ms.ContainingType,
            INamedTypeSymbol nts => nts,
            _ => null
        };

        if (attrType == null) return default(AttributeInstance);

        var attrName = attrType.Name;
        if (attrName.EndsWith("Attribute")) attrName = attrName.Substring(0, attrName.Length - "Attribute".Length);
        if (!Processors.LwxConstants.AttributeNames.Contains(attrName, StringComparer.Ordinal)) return default(AttributeInstance);

        var parent = attributeSyntax.Parent?.Parent;
        if (parent == null) return default(AttributeInstance);

        var declaredSymbol = ctx.SemanticModel.GetDeclaredSymbol(parent, CancellationToken.None);
        if (declaredSymbol == null) return default(AttributeInstance);

        // Find the corresponding AttributeData for the declared symbol (if any)
        var attrData = declaredSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass != null && ad.AttributeClass.ToDisplayString() == attrType.ToDisplayString());

        return new AttributeInstance(attrName, declaredSymbol, parent.GetLocation(), attrData);
    }

    /// <summary>
    /// Check whether a syntax node looks like an Lwx attribute; this is a cheap syntactic
    /// filter used by the incremental generator to limit semantic work.
    /// </summary>
    internal static bool IsPotentialAttribute(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute) return false;
        var name = attribute.Name.ToString();
        var simple = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
        if (simple.EndsWith("Attribute")) simple = simple[..^"Attribute".Length];
        return LwxConstants.AttributeNames.Contains(simple, StringComparer.Ordinal);
    }

}

// Helper extension to fix indentation when embedding generated multi-line text
internal static class LwxExtensionFunctions
{
    public static string FixIndent(this StringBuilder source, int indentLevels, bool indentFirstLine = true)
    {
        if (source == null) return string.Empty;
        return source.ToString().FixIndent(indentLevels, indentFirstLine);
    }

    /// <summary>
    /// Fix the indentation of a multi-line snippet. The parameter <paramref name="indentLevels"/>
    /// is treated as indentation levels and expanded to spaces using 4 spaces per level.
    /// For example, FixIndent(1) prefixes each non-empty line with 4 spaces.
    /// </summary>
    public static string FixIndent(this string source, int indentLevels, bool indentFirstLine = true)
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
                if (i == 0 && !indentFirstLine)
                {
                    // leave the first non-empty line unindented
                    continue;
                }
                lines[i] = indentStr + lines[i];
            }
        }
        return string.Join("\n", lines);
    }
}
