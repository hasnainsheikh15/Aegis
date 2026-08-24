using Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Mappers;
using RoslynWorker.Printers;

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

string[] files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);

List<SyntaxTree> syntaxTrees = [];

foreach (string file in files)
{
    string sourceCode = File.ReadAllText(file);

    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: file);

    syntaxTrees.Add(syntaxTree);
}

MetadataReference[] references =
[
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
];

var compilation = CSharpCompilation.Create(
    assemblyName: "AegisAnalysis",
    syntaxTrees: syntaxTrees,
    references: references
);

var mapper = new RoslynToPirMapper();

PirPackage pirPackage = new();

foreach (SyntaxTree syntaxTree in syntaxTrees)
{
    CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

    SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

    PirPackage filePackage = mapper.MapCompilationUnit(root, semanticModel);

    pirPackage.Nodes.AddRange(filePackage.Nodes);

    pirPackage.Relationships.AddRange(filePackage.Relationships);
}

PirPrinter.Print(pirPackage);

PirGraph graph = new(pirPackage);

GraphAnalyzer graphAnalyzer = new(graph);

DependencyOptions options = new()
{
    RelationshipTypes =
    [
        PirRelationshipType.CALLS,
        PirRelationshipType.READS,
        PirRelationshipType.WRITES,
        PirRelationshipType.CREATES,
        PirRelationshipType.FLOWS_TO,
    ],
};

PirNode? loginNode = pirPackage.Nodes.FirstOrDefault(node =>
    node.Type == PirNodeType.Method && node.Name == "Login"
);

if (loginNode is not null)
{
    ProgramSlice slice = graphAnalyzer.BuildDependencySlice(loginNode, options);

    SensitivityAnalyzer sensitivityAnalyzer = new();

    SliceSensitivityResult analysis = sensitivityAnalyzer.AnalyzeSlice(slice);

    SensitivityPropagator propagator = new(graph, pirPackage);

    Dictionary<string, SensitivityLevel> propagated = propagator.Propagate(analysis.Results);

    Console.WriteLine("\nSENSITIVITY ANALYSIS");

    foreach (SensitivityResult result in analysis.Results)
    {
        Console.WriteLine($"{result.Node.Type} : " + $"{result.Node.Name} → " + $"{result.Level}");

        foreach (string reason in result.Reasons)
        {
            Console.WriteLine($"  Reason: {reason}");
        }
    }

    Console.WriteLine("\nPROPAGATED SENSITIVITY");

    foreach (PirNode node in pirPackage.Nodes)
    {
        if (propagated.TryGetValue(node.Id, out SensitivityLevel level))
        {
            Console.WriteLine($"{node.Type} : {node.Name} → {level}");
        }
    }
}
