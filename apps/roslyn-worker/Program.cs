using Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;
using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RoslynWorker.Mappers;
using RoslynWorker.Printers;
using RoslynWorker.Selection;
using RoslynWorker.Validation;

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

static string GetNodeName(string nodeId, PirPackage pirPackage)
{
    PirNode? node = pirPackage.Nodes.FirstOrDefault(node => node.Id == nodeId);

    return node?.Name ?? "";
}

PirPrinter.Print(pirPackage);

/*
 * Build graph
 */

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

/*
 * ============================================================
 * INTRINSIC SENSITIVITY ANALYSIS
 * ============================================================
 *
 * This must analyze the ENTIRE PIR.
 *
 * It answers:
 *
 * "How sensitive is this node by itself?"
 *
 * It must NOT be restricted to Login or any other dependency
 * slice.
 */

SensitivityAnalyzer sensitivityAnalyzer = new();

List<SensitivityResult> intrinsicResults = [];

foreach (PirNode node in pirPackage.Nodes)
{
    SensitivityResult result = sensitivityAnalyzer.Analyze(node);

    intrinsicResults.Add(result);
}

Console.WriteLine("\nSENSITIVITY ANALYSIS");

foreach (SensitivityResult result in intrinsicResults)
{
    Console.WriteLine($"{result.Node.Type} : " + $"{result.Node.Name} → " + $"{result.Level}");

    foreach (string reason in result.Reasons)
    {
        Console.WriteLine($"  Reason: {reason}");
    }
}

/*
 * ============================================================
 * DEPENDENCY SLICE + SENSITIVITY PROPAGATION
 * ============================================================
 *
 * Login is still only a TEMPORARY test root.
 *
 * The dependency slice answers:
 *
 * "What other nodes are connected to this selected/root node?"
 *
 * Propagation then answers:
 *
 * "Does sensitivity from one node flow into another node?"
 */

PirNode? loginNode = pirPackage.Nodes.FirstOrDefault(node =>
    node.Type == PirNodeType.Method && node.Name == "Login"
);

Dictionary<string, SensitivityLevel> propagated = [];

if (loginNode is not null)
{
    ProgramSlice slice = graphAnalyzer.BuildDependencySlice(loginNode, options);

    /*
     * IMPORTANT:
     *
     * Start propagation from the intrinsic sensitivity
     * results belonging to the dependency slice.
     *
     * We are NOT analyzing sensitivity here.
     * We are only propagating sensitivity that was already
     * calculated across the whole project above.
     */

    List<SensitivityResult> sliceResults = slice
        .Nodes.Select(node => intrinsicResults.First(result => result.Node.Id == node.Id))
        .ToList();

    SensitivityPropagator propagator = new(graph, pirPackage);

    propagated = propagator.Propagate(sliceResults);
}

Console.WriteLine("\nPROPAGATED SENSITIVITY");

foreach (PirNode node in pirPackage.Nodes)
{
    if (propagated.TryGetValue(node.Id, out SensitivityLevel level))
    {
        Console.WriteLine($"{node.Type} : " + $"{node.Name} → " + $"{level}");
    }
}

/*
 * ============================================================
 * SANITIZATION PLANNING
 * ============================================================
 *
 * Sanitization targets are based on INTRINSIC sensitivity,
 * not the propagated result.
 *
 * This means every intrinsically sensitive node in the project
 * can be considered for sanitization.
 */

SanitizationPlanner planner = new();

List<PirNode> sensitiveNodes = intrinsicResults
    .Where(result => result.Level >= SensitivityLevel.Sensitive)
    .Select(result => result.Node)
    .ToList();

List<SanitizationTarget> targets = planner.Plan(
    sensitiveNodes,
    node =>
    {
        if (!mapper.TryGetSyntaxNode(node, out SyntaxNode? syntaxNode))
        {
            return null;
        }

        if (syntaxNode is not VariableDeclaratorSyntax variableNode)
        {
            return null;
        }

        if (variableNode.Initializer is null)
        {
            return null;
        }

        ExpressionSyntax initializer = variableNode.Initializer.Value;

        return new SanitizationTarget
        {
            NodeId = node.Id,

            NodeType = node.Type,

            FilePath = syntaxNode.SyntaxTree.FilePath,

            Start = initializer.Span.Start,

            Length = initializer.Span.Length,

            OriginalText = initializer.ToFullString(),
        };
    }
);

Console.WriteLine("\nSANITIZATION TARGETS");

foreach (SanitizationTarget target in targets)
{
    Console.WriteLine($"{target.NodeType} : " + $"{target.NodeId}");

    Console.WriteLine($"  File: {target.FilePath}");

    Console.WriteLine($"  Start: {target.Start}");

    Console.WriteLine($"  Length: {target.Length}");

    Console.WriteLine($"  Original: {target.OriginalText}");
}

/*
 * ============================================================
 * SANITIZATION
 * ============================================================
 */

DummyValueGenerator dummyValueGenerator = new();

SourceSanitizer sourceSanitizer = new();

Dictionary<string, List<SanitizationTarget>> targetsByFile = targets
    .GroupBy(target => target.FilePath)
    .ToDictionary(group => group.Key, group => group.ToList());

foreach ((string filePath, List<SanitizationTarget> fileTargets) in targetsByFile)
{
    string source = File.ReadAllText(filePath);

    List<SanitizationMapping> mappings = sourceSanitizer.Sanitize(
        source,
        fileTargets,
        target =>
        {
            return dummyValueGenerator.Generate(
                target.OriginalText,
                GetNodeName(target.NodeId, pirPackage)
            );
        },
        out string sanitizedSource
    );

    Console.WriteLine();
    Console.WriteLine("SANITIZED CHANGES");

    SanitizedChangeDetector changeDetector = new();

    string modifiedSanitizedSource = sanitizedSource
        .Replace("string token = password;", "string token = Hash(password);")
        .Replace("string backup = token;", "string backup = Encrypt(token);")
        .Replace(
            "private string password = \"DUMMY_PASSWORD\";",
            "private string password = Hash(\"DUMMY_PASSWORD\");"
        );

    List<SanitizedChange> changes = changeDetector.Detect(
        sanitizedSource,
        modifiedSanitizedSource,
        filePath
    );

    ReverseMapper reverseMapper = new();

    List<ReverseMappedChange> reverseResults = reverseMapper.Analyze(changes, mappings);

    Console.WriteLine();
    Console.WriteLine("REVERSE MAPPING ANALYSIS");

    foreach (ReverseMappedChange result in reverseResults)
    {
        SanitizedChange change = result.SourceChange;

        Console.WriteLine($"Start: {change.Start}");
        Console.WriteLine($"Original: {change.OriginalText}");
        Console.WriteLine($"New: {change.NewText}");
        Console.WriteLine($"Safe: {result.IsSafe}");

        foreach (ProtectedRegion region in result.ProtectedRegions)
        {
            Console.WriteLine(
                $"  Protected Region: Start={region.Start}, " + $"Length={region.Length}"
            );

            Console.WriteLine($"  Dummy: {region.DummyText}");

            Console.WriteLine($"  Real: {region.OriginalText}");

            Console.WriteLine($"  Node ID: {region.NodeId}");
        }

        if (result.Reason is not null)
        {
            Console.WriteLine($"Reason: {result.Reason}");
        }

        Console.WriteLine();
    }

    PatchValidator patchValidator = new();

    Console.WriteLine();
    Console.WriteLine("REVERSE PATCH CANDIDATES");

    foreach (ReverseMappedChange result in reverseResults)
    {
        ReversePatch? patch = reverseMapper.CreatePatch(result, mappings, source);

        if (patch is null)
            continue;

        Console.WriteLine($"File: {patch.FilePath}");
        Console.WriteLine($"Start: {patch.Start}");
        Console.WriteLine($"Original: {patch.OriginalText}");
        Console.WriteLine($"Replacement: {patch.ReplacementText}");
        Console.WriteLine($"Requires Review: {patch.RequiresReview}");
        Console.WriteLine($"Reason: {patch.Reason}");

        if (patch.RequiresReview)
        {
            Console.WriteLine("Validation: SKIPPED — requires review.");
            Console.WriteLine();

            continue;
        }

        string originalSource = File.ReadAllText(patch.FilePath);

        PatchValidationResult validation = patchValidator.Validate(originalSource, patch);

        Console.WriteLine($"Validation: {(validation.IsValid ? "PASSED" : "FAILED")}");

        foreach (string diagnostic in validation.Diagnostics)
        {
            Console.WriteLine($"  {diagnostic}");
        }

        Console.WriteLine();
    }
}
