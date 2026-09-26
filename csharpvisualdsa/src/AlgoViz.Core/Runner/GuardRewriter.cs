using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlgoViz.Core.Runner;

/// <summary>
/// Injects <c>Viz.__Tick()</c> at the start of every loop body and <c>Viz.__Enter()</c> at the start of every
/// method, accessor, local function and lambda. Expression bodies become block bodies so they get the guard too.
/// Inserted syntax carries no newline trivia, so every user token stays on its original line.
/// The semantic model must belong to the tree being rewritten (it is queried with original nodes).
/// </summary>
internal sealed class GuardRewriter(SemanticModel model) : CSharpSyntaxRewriter
{
    private static StatementSyntax Tick() => ParseStatement("global::AlgoViz.Api.Viz.__Tick();");

    private static StatementSyntax Enter() => ParseStatement("global::AlgoViz.Api.Viz.__Enter();");

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var n = (ForStatementSyntax)base.VisitForStatement(node)!;
        return n.WithStatement(Prepend(n.Statement, Tick()));
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var n = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return n.WithStatement(Prepend(n.Statement, Tick()));
    }

    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        var n = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return n.WithStatement(Prepend(n.Statement, Tick()));
    }

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var n = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return n.WithStatement(Prepend(n.Statement, Tick()));
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var n = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return n.WithStatement(Prepend(n.Statement, Tick()));
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var n = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (n.Body is not null) return n.WithBody(PrependEnter(n.Body));
        if (n.ExpressionBody is null) return n;
        return n.WithBody(ArrowToBlock(n.ExpressionBody, n.SemicolonToken, IsVoid(n.ReturnType)))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var n = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
        if (n.Body is not null) return n.WithBody(PrependEnter(n.Body));
        if (n.ExpressionBody is null) return n;
        return n.WithBody(ArrowToBlock(n.ExpressionBody, n.SemicolonToken, IsVoid(n.ReturnType)))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var n = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (n.Body is not null) return n.WithBody(PrependEnter(n.Body));
        if (n.ExpressionBody is null) return n;
        return n.WithBody(ArrowToBlock(n.ExpressionBody, n.SemicolonToken, isVoid: true))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        var n = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
        if (n.Body is not null) return n.WithBody(PrependEnter(n.Body));
        if (n.ExpressionBody is null) return n;
        return n.WithBody(ArrowToBlock(n.ExpressionBody, n.SemicolonToken, IsVoid(n.ReturnType)))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        var n = (ConversionOperatorDeclarationSyntax)base.VisitConversionOperatorDeclaration(node)!;
        if (n.Body is not null) return n.WithBody(PrependEnter(n.Body));
        if (n.ExpressionBody is null) return n;
        return n.WithBody(ArrowToBlock(n.ExpressionBody, n.SemicolonToken, isVoid: false))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        var n = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
        if (n.Body is not null) return n.WithBody(PrependEnter(n.Body));
        if (n.ExpressionBody is null) return n;
        var isVoid = !n.IsKind(SyntaxKind.GetAccessorDeclaration);
        return n.WithBody(ArrowToBlock(n.ExpressionBody, n.SemicolonToken, isVoid))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var n = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        if (n.ExpressionBody is null) return n;
        return n.WithAccessorList(ArrowToGetter(n.ExpressionBody, n.SemicolonToken))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        var n = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;
        if (n.ExpressionBody is null) return n;
        return n.WithAccessorList(ArrowToGetter(n.ExpressionBody, n.SemicolonToken))
            .WithExpressionBody(null).WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        var isVoid = LambdaReturnsVoid(node);
        var n = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;
        if (n.Block is not null) return n.WithBlock(PrependEnter(n.Block));
        if (n.ExpressionBody is null || isVoid is null) return n;
        return n.WithBlock(LambdaBlock(n.ExpressionBody, isVoid.Value)).WithExpressionBody(null);
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var isVoid = LambdaReturnsVoid(node);
        var n = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
        if (n.Block is not null) return n.WithBlock(PrependEnter(n.Block));
        if (n.ExpressionBody is null || isVoid is null) return n;
        return n.WithBlock(LambdaBlock(n.ExpressionBody, isVoid.Value)).WithExpressionBody(null);
    }

    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        var n = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return n.WithBlock(PrependEnter(n.Block));
    }

    private bool? LambdaReturnsVoid(LambdaExpressionSyntax node) =>
        (model.GetSymbolInfo(node).Symbol as IMethodSymbol)?.ReturnsVoid;

    private static bool IsVoid(TypeSyntax type) =>
        type is PredefinedTypeSyntax p && p.Keyword.IsKind(SyntaxKind.VoidKeyword);

    private static StatementSyntax Prepend(StatementSyntax body, StatementSyntax guard) => body switch
    {
        BlockSyntax block => block.WithStatements(block.Statements.Insert(0, guard)),
        _ => Block(Token(SyntaxKind.OpenBraceToken), List([guard, body]), Token(SyntaxKind.CloseBraceToken)),
    };

    private static BlockSyntax PrependEnter(BlockSyntax block) => block.WithStatements(block.Statements.Insert(0, Enter()));

    /// <summary><c>=&gt; expr;</c> becomes <c>{ __Enter(); return expr; }</c>, keeping the arrow and semicolon trivia.</summary>
    private static BlockSyntax ArrowToBlock(ArrowExpressionClauseSyntax arrow, SyntaxToken semicolon, bool isVoid) =>
        Block(
            Token(arrow.ArrowToken.LeadingTrivia, SyntaxKind.OpenBraceToken, arrow.ArrowToken.TrailingTrivia),
            List([Enter(), BodyStatement(arrow.Expression, isVoid)]),
            Token(semicolon.LeadingTrivia, SyntaxKind.CloseBraceToken, semicolon.TrailingTrivia));

    /// <summary><c>=&gt; expr;</c> on a property/indexer becomes <c>{ get { __Enter(); return expr; } }</c>.</summary>
    private static AccessorListSyntax ArrowToGetter(ArrowExpressionClauseSyntax arrow, SyntaxToken semicolon) =>
        AccessorList(
            Token(arrow.ArrowToken.LeadingTrivia, SyntaxKind.OpenBraceToken, arrow.ArrowToken.TrailingTrivia),
            SingletonList(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(Enter(), BodyStatement(arrow.Expression, isVoid: false)))),
            Token(semicolon.LeadingTrivia, SyntaxKind.CloseBraceToken, semicolon.TrailingTrivia));

    private static BlockSyntax LambdaBlock(ExpressionSyntax body, bool isVoid) =>
        Block(Enter(), BodyStatement(body, isVoid));

    private static StatementSyntax BodyStatement(ExpressionSyntax expression, bool isVoid) => expression switch
    {
        ThrowExpressionSyntax t => ThrowStatement(t.ThrowKeyword, t.Expression, Token(SyntaxKind.SemicolonToken)),
        _ when isVoid => ExpressionStatement(expression),
        _ => ReturnStatement(Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(Space), expression, Token(SyntaxKind.SemicolonToken)),
    };
}
