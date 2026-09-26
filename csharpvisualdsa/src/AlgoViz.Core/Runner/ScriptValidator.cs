using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlgoViz.Core.Runner;

/// <summary>
/// Rejects anything outside the script allowlist, using the semantic model rather than string matching.
/// This is a guardrail for a practice tool, not a security sandbox.
/// </summary>
internal sealed class ScriptValidator(SemanticModel model)
{
    private static readonly HashSet<string> AllowedNamespaces =
        ["System", "System.Collections.Generic", "System.Linq", "System.Text", "AlgoViz.Api"];

    private const string ReflectionMessage = "Reflection is not allowed in scripts.";
    private const string ThreadingMessage = "Scripts are single-threaded: threads, tasks, async/await and locks are not allowed.";

    private static readonly Dictionary<string, string> BannedSystemTypes = new()
    {
        ["Console"] = "Console is not available. Use Viz.Log(...) instead.",
        ["Environment"] = "Environment is not available in scripts.",
        ["AppDomain"] = "AppDomain is not available in scripts.",
        ["AppContext"] = "AppContext is not available in scripts.",
        ["Activator"] = ReflectionMessage,
        ["Type"] = ReflectionMessage,
        ["Delegate"] = ReflectionMessage,
        ["MulticastDelegate"] = ReflectionMessage,
        ["RuntimeTypeHandle"] = ReflectionMessage,
        ["RuntimeMethodHandle"] = ReflectionMessage,
        ["RuntimeFieldHandle"] = ReflectionMessage,
        ["ModuleHandle"] = ReflectionMessage,
        ["TypedReference"] = ReflectionMessage,
        ["ArgIterator"] = ReflectionMessage,
        ["RuntimeArgumentHandle"] = ReflectionMessage,
        ["GC"] = "GC is not available in scripts.",
        ["Buffer"] = "Buffer is not available in scripts.",
        ["MarshalByRefObject"] = "MarshalByRefObject is not available in scripts.",
        ["TimeProvider"] = ThreadingMessage,
    };

    private static readonly Dictionary<SyntaxKind, string> BannedTokens = new()
    {
        [SyntaxKind.UnsafeKeyword] = "unsafe code is not allowed.",
        [SyntaxKind.FixedKeyword] = "fixed is not allowed.",
        [SyntaxKind.StackAllocKeyword] = "stackalloc is not allowed.",
        [SyntaxKind.ExternKeyword] = "extern is not allowed.",
        [SyntaxKind.GotoKeyword] = "goto is not allowed. Use loops, break and continue.",
        [SyntaxKind.AsyncKeyword] = ThreadingMessage,
        [SyntaxKind.AwaitKeyword] = ThreadingMessage,
        [SyntaxKind.LockKeyword] = ThreadingMessage,
        [SyntaxKind.ArgListKeyword] = "__arglist is not allowed.",
        [SyntaxKind.MakeRefKeyword] = "__makeref is not allowed.",
        [SyntaxKind.RefTypeKeyword] = "__reftype is not allowed.",
        [SyntaxKind.RefValueKeyword] = "__refvalue is not allowed.",
    };

    /// <summary>Names that don't resolve because their assembly isn't referenced, and what to say instead.</summary>
    private static readonly Dictionary<string, string> UnresolvedNames = new()
    {
        ["Console"] = BannedSystemTypes["Console"],
        ["Thread"] = ThreadingMessage,
        ["Task"] = ThreadingMessage,
        ["Parallel"] = ThreadingMessage,
        ["ThreadPool"] = ThreadingMessage,
        ["Monitor"] = ThreadingMessage,
        ["File"] = "File and stream I/O is not allowed.",
        ["Directory"] = "File and stream I/O is not allowed.",
        ["Process"] = "System.Diagnostics is not available in scripts.",
        ["Stopwatch"] = "System.Diagnostics is not available in scripts.",
        ["Assembly"] = ReflectionMessage,
    };

    private readonly List<ScriptDiagnostic> diagnostics = [];
    private readonly HashSet<(int, string)> reported = [];

    public IReadOnlyList<ScriptDiagnostic> Validate(SyntaxNode root)
    {
        foreach (var token in root.DescendantTokens())
            if (BannedTokens.TryGetValue(token.Kind(), out var message))
                Report(token.GetLocation(), message);

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case AttributeListSyntax:
                    Report(node, "Attributes are not allowed.");
                    continue;
                case RecordDeclarationSyntax:
                    Report(node, "Records are not allowed. Use a class instead.");
                    break;
                case DestructorDeclarationSyntax:
                    Report(node, "Finalizers are not allowed.");
                    break;
                case FunctionPointerTypeSyntax or PointerTypeSyntax:
                    Report(node, "Pointers are not allowed.");
                    break;
            }

            if (node is SimpleNameSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
                or ElementAccessExpressionSyntax or InvocationExpressionSyntax)
            {
                var info = model.GetSymbolInfo(node);
                if (info.Symbol is { } symbol) CheckSymbol(symbol, node);
                else foreach (var candidate in info.CandidateSymbols) CheckSymbol(candidate, node);
            }

            if (node is ExpressionSyntax && model.GetTypeInfo(node).Type is { } type)
                CheckType(type, node);
        }

        return diagnostics;
    }

    private void CheckSymbol(ISymbol symbol, SyntaxNode node)
    {
        switch (symbol)
        {
            case INamespaceSymbol ns:
                var name = ns.ToDisplayString();
                if (!ns.IsGlobalNamespace && !AllowedNamespaces.Any(a => a == name || a.StartsWith(name + ".", StringComparison.Ordinal)))
                    Report(node, NamespaceMessage(name));
                break;
            case ITypeSymbol type:
                CheckType(type, node);
                break;
            case IMethodSymbol method:
                if (method.Name == nameof(GetType) && method.ContainingType?.SpecialType == SpecialType.System_Object)
                    Report(node, ReflectionMessage);
                CheckMember(method.ReducedFrom ?? method, node);
                foreach (var arg in method.TypeArguments) CheckType(arg, node);
                break;
            case IPropertySymbol or IFieldSymbol or IEventSymbol:
                CheckMember(symbol, node);
                break;
            case ILocalSymbol local:
                CheckType(local.Type, node);
                break;
            case IAliasSymbol alias:
                CheckSymbol(alias.Target, node);
                break;
        }
    }

    private void CheckMember(ISymbol member, SyntaxNode node)
    {
        if (member.ContainingType is { } type) CheckType(type, node);
    }

    private void CheckType(ITypeSymbol type, SyntaxNode node)
    {
        switch (type)
        {
            case IDynamicTypeSymbol:
                Report(node, "dynamic is not allowed.");
                return;
            case IPointerTypeSymbol or IFunctionPointerTypeSymbol:
                Report(node, "Pointers are not allowed.");
                return;
            case IArrayTypeSymbol array:
                CheckType(array.ElementType, node);
                return;
            case INamedTypeSymbol named when named.TypeKind != TypeKind.Error:
                foreach (var arg in named.TypeArguments) CheckType(arg, node);
                var outer = named;
                while (outer.ContainingType is { } containing) outer = containing;
                if (outer.Locations.Any(l => l.IsInSource)) return;
                var ns = outer.ContainingNamespace?.ToDisplayString() ?? "";
                if (!AllowedNamespaces.Contains(ns)) Report(node, NamespaceMessage(ns, outer.Name));
                else if (ns == "System" && BannedSystemTypes.TryGetValue(outer.Name, out var message)) Report(node, message);
                return;
        }
    }

    /// <summary>A friendlier message for "name not found" errors on names scripts can't use, if one applies.</summary>
    public static string? FriendlyMessage(Diagnostic diagnostic)
    {
        if (diagnostic.Id is not ("CS0103" or "CS0246" or "CS0234")) return null;
        if (diagnostic.Location.SourceTree?.GetRoot().FindNode(diagnostic.Location.SourceSpan) is not { } node) return null;
        SimpleNameSyntax? name = node switch
        {
            QualifiedNameSyntax q => q.Right,
            MemberAccessExpressionSyntax m => m.Name,
            SimpleNameSyntax s => s,
            _ => null,
        };
        if (name is not null && UnresolvedNames.TryGetValue(name.Identifier.ValueText, out var message))
            return message;
        var text = node.ToString();
        return text.StartsWith("System.Threading", StringComparison.Ordinal) || text.StartsWith("System.IO", StringComparison.Ordinal)
            || text.StartsWith("System.Reflection", StringComparison.Ordinal) || text.StartsWith("System.Diagnostics", StringComparison.Ordinal)
            ? NamespaceMessage(text)
            : null;
    }

    private static string NamespaceMessage(string ns, string? typeName = null)
    {
        if (ns.StartsWith("System.Threading", StringComparison.Ordinal)) return ThreadingMessage;
        if (ns.StartsWith("System.Reflection", StringComparison.Ordinal) || ns.StartsWith("System.Runtime", StringComparison.Ordinal))
            return ReflectionMessage;
        if (ns.StartsWith("System.IO", StringComparison.Ordinal)) return "File and stream I/O is not allowed.";
        if (ns.StartsWith("System.Diagnostics", StringComparison.Ordinal)) return "System.Diagnostics is not available in scripts.";
        var what = typeName is null ? $"Namespace '{ns}'" : $"'{typeName}' (namespace {ns})";
        return $"{what} is not available. Scripts can use System, System.Collections.Generic, System.Linq, System.Text and the Viz API.";
    }

    private void Report(SyntaxNode node, string message) => Report(node.GetLocation(), message);

    private void Report(Location location, string message)
    {
        var span = location.GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        if (reported.Add((line, message)))
            diagnostics.Add(new ScriptDiagnostic(line, span.StartLinePosition.Character + 1, message, ScriptSeverity.Error));
    }
}
