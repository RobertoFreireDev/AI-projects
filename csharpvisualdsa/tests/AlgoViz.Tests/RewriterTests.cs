using AlgoViz.Api;
using AlgoViz.Core.Runner;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlgoViz.Tests;

/// <summary>The guard rewriter injects guards without moving any user token to another line.</summary>
public static class RewriterTests
{
    private const string Code = """
        var a = Viz.Array("a", 1, 2, 3);
        for (int i = 0; i < 2; i++)
            a.Get(i);
        foreach (var x in new[] { 1 }) Viz.Log(x);
        int k = 0;
        while (k < 1)
        {
            k++; Viz.Log("while");
        }
        do Viz.Log("do"); while (false);
        int Twice(int n) =>
            n * 2;
        Viz.Var("twice", Twice(2));
        var doubled = new[] { 1 }.Select(v =>
            v * 2).ToList();
        Action log = () => Viz.Log("lambda");
        log();
        Viz.Log(new Box(5).Value);
        class Box(int v)
        {
            public int Value =>
                v;
        }
        """;

    public static IEnumerable<(string, Func<Task>)> All() =>
    [
        ("rewriter: keeps every token on its line", () => { KeepsLines(); return Task.CompletedTask; }),
        ("rewriter: guards every loop and body", () => { GuardsEverything(); return Task.CompletedTask; }),
        ("rewriter: recorded lines match the source", RecordedLines),
    ];

    private static (SyntaxTree Original, SyntaxTree Rewritten) Rewrite()
    {
        var output = ScriptRunner.Compile(Code);
        Check.True(output.Assembly is not null, string.Join(" | ", output.Diagnostics));
        return ScriptRunner.ParseAndRewrite(Code);
    }

    private static void KeepsLines()
    {
        var (original, rewritten) = Rewrite();
        Check.Equal(original.GetText().Lines.Count, rewritten.GetText().Lines.Count, "line count");
        var rewrittenTokens = rewritten.GetRoot().DescendantTokens().ToList();
        var cursor = 0;
        // Identifiers and literals must all stay put; punctuation like an expression body's "=>" legitimately becomes "{".
        foreach (var token in original.GetRoot().DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) || t.Parent is LiteralExpressionSyntax))
        {
            // Find the same token (by kind and text) in order; inserted tokens are skipped.
            while (cursor < rewrittenTokens.Count && !(rewrittenTokens[cursor].IsKind(token.Kind()) && rewrittenTokens[cursor].Text == token.Text))
                cursor++;
            Check.True(cursor < rewrittenTokens.Count, $"token '{token.Text}' is still there");
            Check.Equal(Line(token), Line(rewrittenTokens[cursor]), $"line of '{token.Text}'");
            cursor++;
        }

        // Inserted statements carry no newline trivia at all.
        foreach (var call in Guards(rewritten))
            Check.True(!call.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)), "guard has no newline trivia");
    }

    private static void GuardsEverything()
    {
        var (_, rewritten) = Rewrite();
        var root = rewritten.GetRoot();
        var ticks = Guards(rewritten).Count(g => g.ToString().Contains("__Tick"));
        var enters = Guards(rewritten).Count(g => g.ToString().Contains("__Enter"));
        Check.Equal(4, ticks, "for, foreach, while, do");
        // Twice, the Select lambda, the log lambda, the Value getter.
        Check.Equal(4, enters, "method, lambdas and accessor");
        Check.True(root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().All(a => a.Parent is not LocalFunctionStatementSyntax and not PropertyDeclarationSyntax));
    }

    private static async Task RecordedLines()
    {
        var result = await new ScriptRunner().RunAsync(Code);
        Check.True(result.Error is null, result.Error?.Message);
        string Lines(Func<VizOp, bool> filter) => string.Join(",", result.Ops.Where(filter).Select(o => o.Line));
        Check.Equal("3,3", Lines(o => o.Kind == OpKind.Read));
        Check.Equal("4,8,10,16,18", Lines(o => o.Kind == OpKind.Log));
        Check.Equal("13", Lines(o => o.Kind == OpKind.Var));
    }

    private static IEnumerable<ExpressionStatementSyntax> Guards(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<ExpressionStatementSyntax>()
            .Where(s => s.ToString().StartsWith("global::AlgoViz.Api.Viz.__", StringComparison.Ordinal));

    private static int Line(SyntaxToken token) => token.GetLocation().GetLineSpan().StartLinePosition.Line;
}
