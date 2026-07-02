using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynWorker.Mappers;

static void PrintTree(SyntaxNode node, string indent = "")
{
    string extra = "";

    switch (node)
    {
        case ClassDeclarationSyntax classNode:
            extra = $" : {classNode.Identifier.Text}";
            break;

        case MethodDeclarationSyntax methodNode:
            extra = $" : {methodNode.Identifier.Text}";
            break;

        case FileScopedNamespaceDeclarationSyntax namespaceNode:
            extra = $" : {namespaceNode.Name}";
            break;

        case NamespaceDeclarationSyntax namespaceNode:
            extra = $" : {namespaceNode.Name}";
            break;

        case IdentifierNameSyntax identifierNode:
            extra = $" : {identifierNode.Identifier.Text}";
            break;

        case LiteralExpressionSyntax literalNode:
            extra = $" : {literalNode.Token.ValueText}";
            break;
    }

    Console.WriteLine($"{indent}{node.Kind()}{extra}");

    foreach (var child in node.ChildNodes())
    {
        PrintTree(child, indent + "  ");
    }
}

if (args.Length == 0)
{
    Console.WriteLine("Usage: RoslynWorker <file-path>");
    return;
}

var filePath = args[0];

var sourceCode = File.ReadAllText(filePath);

var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

var root = syntaxTree.GetRoot();

var mapper = new RoslynToPirMapper();

var pirPackage = mapper.MapCompilationUnit(root);

foreach(var node in pirPackage.Nodes) {
    Console.WriteLine($"{node.Type} : {node.Name}");
}

