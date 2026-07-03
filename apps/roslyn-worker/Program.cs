using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Mappers;
using RoslynWorker.Models;
using RoslynWorker.Printers;


// static void PrintTree(SyntaxNode node, string indent = "")
// {
//     string extra = "";

//     switch (node)
//     {
//         case ClassDeclarationSyntax classNode:
//             extra = $" : {classNode.Identifier.Text}";
//             break;

//         case MethodDeclarationSyntax methodNode:
//             extra = $" : {methodNode.Identifier.Text}";
//             break;

//         case FileScopedNamespaceDeclarationSyntax namespaceNode:
//             extra = $" : {namespaceNode.Name}";
//             break;

//         case NamespaceDeclarationSyntax namespaceNode:
//             extra = $" : {namespaceNode.Name}";
//             break;

//         case IdentifierNameSyntax identifierNode:
//             extra = $" : {identifierNode.Identifier.Text}";
//             break;

//         case LiteralExpressionSyntax literalNode:
//             extra = $" : {literalNode.Token.ValueText}";
//             break;
//     }

//     Console.WriteLine($"{indent}{node.Kind()}{extra}");

//     foreach (var child in node.ChildNodes())
//     {
//         PrintTree(child, indent + "  ");
//     }
// }

// if (args.Length == 0)
// {
//     Console.WriteLine("Usage: RoslynWorker <file-path>");
//     return;
// }

// var projectPath = args[0];

// if (!Directory.Exists(projectPath))
// {
//     Console.WriteLine("Project directory not found.");
//     return;
// }

// // var sourceCode = File.ReadAllText(filePath);

// string[] files = Directory.GetFiles(
//     projectPath,
//     "*.cs",
//     SearchOption.AllDirectories
// );

// foreach (string file in files)
// {
//     Console.WriteLine(file);
// }

// var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

// var root = syntaxTree.GetRoot();

// var compilation = CSharpCompilation.Create(
//     "AegisAnalysis",
//     syntaxTrees: new[] { syntaxTree }
// );

// var semanticModel = compilation.GetSemanticModel(syntaxTree);

// var mapper = new RoslynToPirMapper();

// var pirPackage = mapper.MapCompilationUnit(root,semanticModel);

// Console.WriteLine(semanticModel != null);

// PirPrinter.Print(pirPackage);




if (args.Length == 0)
{
    Console.WriteLine("Usage: RoslynWorker <project-folder>");
    return;
}

var projectPath = args[0];

if (!Directory.Exists(projectPath))
{
    Console.WriteLine("Project directory not found.");
    return;
}

string[] files = Directory.GetFiles(
    projectPath,
    "*.cs",
    SearchOption.AllDirectories
);

List<SyntaxTree> syntaxTrees = [];

foreach (string file in files)
{
    string sourceCode = File.ReadAllText(file);

    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
        sourceCode,
        path: file
    );

    syntaxTrees.Add(syntaxTree);
}

MetadataReference[] references =
[
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
];

var compilation = CSharpCompilation.Create(
    assemblyName: "AegisAnalysis",
    syntaxTrees: syntaxTrees,
    references : references
);

// foreach (Diagnostic diagnostic in compilation.GetDiagnostics())
// {
//     Console.WriteLine(diagnostic);
// }

var mapper = new RoslynToPirMapper();

PirPackage pirPackage = new();

foreach (SyntaxTree syntaxTree in syntaxTrees)
{
    CompilationUnitSyntax root =
        syntaxTree.GetCompilationUnitRoot();

    SemanticModel semanticModel =
        compilation.GetSemanticModel(syntaxTree);

    PirPackage filePackage =
        mapper.MapCompilationUnit(root, semanticModel);

    pirPackage.Nodes.AddRange(filePackage.Nodes);

    pirPackage.Relationships.AddRange(filePackage.Relationships);
}

PirPrinter.Print(pirPackage);