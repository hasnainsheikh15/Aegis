using Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;
using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using Aegis.Sanitizer.Models.Session;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Mappers;
using RoslynWorker.Printers;
using RoslynWorker.Selection;
using RoslynWorker.Validation;

if (args.Length < 4)
{
    Console.WriteLine("Usage: RoslynWorker <project-folder> <file-path> <start> <length>");

    return;
}

string projectPath = Path.GetFullPath(args[0]);

string selectedFilePath = Path.GetFullPath(args[1]);

if (!Directory.Exists(projectPath))
{
    Console.WriteLine($"Project directory not found: {projectPath}");

    return;
}

if (!File.Exists(selectedFilePath))
{
    Console.WriteLine($"Selected file not found: {selectedFilePath}");

    return;
}

if (!int.TryParse(args[2], out int selectionStart))
{
    Console.WriteLine("Selection start must be an integer.");

    return;
}

if (!int.TryParse(args[3], out int selectionLength))
{
    Console.WriteLine("Selection length must be an integer.");

    return;
}

if (selectionStart < 0)
{
    Console.WriteLine("Selection start cannot be negative.");

    return;
}

if (selectionLength < 0)
{
    Console.WriteLine("Selection length cannot be negative.");

    return;
}

/*
 * ============================================================
 * READ PROJECT
 * ============================================================
 */

string[] files = Directory
    .GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
    .Where(file =>
        !Path.GetFullPath(file)
            .StartsWith(
                Path.Combine(projectPath, ".aegis") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
    )
    .ToArray();

/*
 * ============================================================
 * PARSE PROJECT
 * ============================================================
 */

List<SyntaxTree> syntaxTrees = [];

foreach (string file in files)
{
    string sourceCode = File.ReadAllText(file);

    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: file);

    syntaxTrees.Add(syntaxTree);
}

/*
 * ============================================================
 * COMPILATION
 * ============================================================
 *
 * This is still the minimal compilation used by the current
 * alpha analysis pipeline.
 */

MetadataReference[] references =
[
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
];

CSharpCompilation compilation = CSharpCompilation.Create(
    assemblyName: "AegisAnalysis",
    syntaxTrees: syntaxTrees,
    references: references
);

/*
 * ============================================================
 * BUILD PIR
 * ============================================================
 */

RoslynToPirMapper mapper = new();

PirPackage pirPackage = new();

foreach (SyntaxTree syntaxTree in syntaxTrees)
{
    CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

    SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

    PirPackage filePackage = mapper.MapCompilationUnit(root, semanticModel);

    pirPackage.Nodes.AddRange(filePackage.Nodes);

    pirPackage.Relationships.AddRange(filePackage.Relationships);
}

/*
 * ============================================================
 * SELECTION
 * ============================================================
 */

SyntaxTree? selectedSyntaxTree = syntaxTrees.FirstOrDefault(tree =>
    string.Equals(
        Path.GetFullPath(tree.FilePath),
        selectedFilePath,
        StringComparison.OrdinalIgnoreCase
    )
);

if (selectedSyntaxTree is null)
{
    Console.WriteLine(
        $"Selected file is not part of the analyzed project: " + $"{selectedFilePath}"
    );

    return;
}

string selectedSource = File.ReadAllText(selectedFilePath);

if (
    selectionStart > selectedSource.Length
    || selectionLength > selectedSource.Length - selectionStart
)
{
    Console.WriteLine("Selection range is outside the selected file.");

    return;
}

SourceSelection selection = new()
{
    FilePath = selectedFilePath,
    Start = selectionStart,
    Length = selectionLength,
};

SemanticModel selectedSemanticModel = compilation.GetSemanticModel(selectedSyntaxTree);

SelectionResolver selectionResolver = new();

List<PirNode> selectedNodes = selectionResolver.Resolve(
    selectedSyntaxTree,
    selectedSemanticModel,
    selection,
    mapper
);

Console.WriteLine();
Console.WriteLine("SELECTION");

Console.WriteLine($"File: {selection.FilePath}");

Console.WriteLine($"Start: {selection.Start}");

Console.WriteLine($"Length: {selection.Length}");

Console.WriteLine($"Selected nodes: {selectedNodes.Count}");

foreach (PirNode node in selectedNodes)
{
    Console.WriteLine($"  {node.Type} : {node.Name}");
}

/*
 * ============================================================
 * SELECTION VALIDATION
 * ============================================================
 *
 * The alpha currently expects exactly one semantic root.
 *
 * Multi-node selection can be added later once the core
 * round-trip workflow is stable.
 */

if (selectedNodes.Count == 0)
{
    Console.WriteLine();
    Console.WriteLine("No PIR node could be resolved from the selection.");

    return;
}

if (selectedNodes.Count > 1)
{
    Console.WriteLine();
    Console.WriteLine("Selection resolved to multiple PIR nodes.");

    Console.WriteLine("The alpha currently requires exactly one selected node.");

    return;
}

PirNode selectedNode = selectedNodes[0];

Console.WriteLine();
Console.WriteLine($"Selected root: {selectedNode.Type} : {selectedNode.Name}");

/*
 * ============================================================
 * BUILD GRAPH
 * ============================================================
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
 * Analyze the entire PIR.
 */

SensitivityAnalyzer sensitivityAnalyzer = new();

List<SensitivityResult> intrinsicResults = [];

foreach (PirNode node in pirPackage.Nodes)
{
    SensitivityResult result = sensitivityAnalyzer.Analyze(node);

    intrinsicResults.Add(result);
}

Console.WriteLine();
Console.WriteLine("SENSITIVITY ANALYSIS");

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
 * The dependency root is now the ACTUAL USER SELECTION.
 */

ProgramSlice slice = graphAnalyzer.BuildDependencySlice(selectedNode, options);

Console.WriteLine();
Console.WriteLine("DEPENDENCY SLICE");

foreach (PirNode node in slice.Nodes)
{
    Console.WriteLine($"  {node.Type} : {node.Name}");
}

/*
 * Start propagation from the intrinsic sensitivity results
 * belonging to the selected node's dependency slice.
 */

List<SensitivityResult> sliceResults = slice
    .Nodes.Select(node => intrinsicResults.First(result => result.Node.Id == node.Id))
    .ToList();

SensitivityPropagator propagator = new(graph, pirPackage);

Dictionary<string, SensitivityLevel> propagated = propagator.Propagate(sliceResults);

Console.WriteLine();
Console.WriteLine("PROPAGATED SENSITIVITY");

foreach (PirNode node in slice.Nodes)
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
 * IMPORTANT:
 *
 * Only intrinsically sensitive nodes that belong to the
 * selected dependency slice are candidates for sanitization.
 *
 * We analyze the whole project, but the alpha only sanitizes
 * the context relevant to the user's selected root.
 */

HashSet<string> sliceNodeIds = slice.Nodes.Select(node => node.Id).ToHashSet();

List<PirNode> sensitiveNodes = intrinsicResults
    .Where(result =>
        result.Level >= SensitivityLevel.Sensitive && sliceNodeIds.Contains(result.Node.Id)
    )
    .Select(result => result.Node)
    .ToList();

SanitizationPlanner planner = new();

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

Console.WriteLine();
Console.WriteLine("SANITIZATION TARGETS");

foreach (SanitizationTarget target in targets)
{
    Console.WriteLine($"{target.NodeType} : {target.NodeId}");

    Console.WriteLine($"  File: {target.FilePath}");

    Console.WriteLine($"  Start: {target.Start}");

    Console.WriteLine($"  Length: {target.Length}");

    Console.WriteLine($"  Original: {target.OriginalText}");
}

/*
 * ============================================================
 * SANITIZATION + SESSION EXPORT
 * ============================================================
 */

DummyValueGenerator dummyValueGenerator = new();

SourceSanitizer sourceSanitizer = new();

SourceHasher sourceHasher = new();

Dictionary<string, List<SanitizationTarget>> targetsByFile = targets
    .GroupBy(target => target.FilePath)
    .ToDictionary(group => group.Key, group => group.ToList());

string sessionId = Guid.NewGuid().ToString("N");

string sessionDirectory = Path.Combine(projectPath, ".aegis", "sessions", sessionId);

string sanitizedDirectory = Path.Combine(sessionDirectory, "sanitized");

string baselineDirectory = Path.Combine(sessionDirectory, "baseline");

Directory.CreateDirectory(sanitizedDirectory);

Directory.CreateDirectory(baselineDirectory);

List<SessionFile> sessionFiles = [];

foreach ((string filePath, List<SanitizationTarget> fileTargets) in targetsByFile)
{
    string source = File.ReadAllText(filePath);

    string originalSourceHash = sourceHasher.Compute(source);

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

    string sanitizedSourceHash = sourceHasher.Compute(sanitizedSource);

    string relativeFilePath = Path.GetRelativePath(projectPath, filePath);

    string sanitizedFilePath = Path.Combine(sanitizedDirectory, relativeFilePath);

    string baselineSanitizedFilePath = Path.Combine(baselineDirectory, relativeFilePath);

    string? sanitizedFileDirectory = Path.GetDirectoryName(sanitizedFilePath);

    if (!string.IsNullOrWhiteSpace(sanitizedFileDirectory))
    {
        Directory.CreateDirectory(sanitizedFileDirectory);
    }

    string? baselineFileDirectory = Path.GetDirectoryName(baselineSanitizedFilePath);

    if (!string.IsNullOrWhiteSpace(baselineFileDirectory))
    {
        Directory.CreateDirectory(baselineFileDirectory);
    }

    File.WriteAllText(sanitizedFilePath, sanitizedSource);

    File.WriteAllText(baselineSanitizedFilePath, sanitizedSource);

    List<SessionMapping> sessionMappings = mappings
        .Select(mapping =>
        {
            PirNode? node = pirPackage.Nodes.FirstOrDefault(candidate =>
                candidate.Id == mapping.NodeId
            );

            if (node is null)
            {
                throw new InvalidOperationException(
                    "Could not resolve PIR node for " + $"sanitization mapping: {mapping.NodeId}"
                );
            }

            return new SessionMapping
            {
                NodeId = mapping.NodeId,
                NodeType = node.Type,
                OriginalText = mapping.OriginalText,
                DummyText = mapping.DummyText,
                OriginalStart = mapping.OriginalStart,
                OriginalLength = mapping.OriginalLength,
                SanitizedStart = mapping.SanitizedStart,
                SanitizedLength = mapping.SanitizedLength,
            };
        })
        .ToList();

    sessionFiles.Add(
        new SessionFile
        {
            OriginalFilePath = filePath,
            SanitizedFilePath = sanitizedFilePath,
            BaselineSanitizedFilePath = baselineSanitizedFilePath,
            OriginalSourceHash = originalSourceHash,
            SanitizedSourceHash = sanitizedSourceHash,
            Mappings = sessionMappings,
        }
    );

    Console.WriteLine();
    Console.WriteLine("SANITIZED FILE");

    Console.WriteLine($"Original:  {filePath}");

    Console.WriteLine($"Sanitized: {sanitizedFilePath}");

    Console.WriteLine($"Original SHA-256:  {originalSourceHash}");

    Console.WriteLine($"Sanitized SHA-256: {sanitizedSourceHash}");

    Console.WriteLine($"Mappings: {sessionMappings.Count}");

    Console.WriteLine();
    Console.WriteLine("SANITIZED SOURCE");

    Console.WriteLine(sanitizedSource);
}

if (sessionFiles.Count == 0)
{
    Console.WriteLine();
    Console.WriteLine("No sanitization targets were found.");

    return;
}

AegisSession session = new()
{
    Version = 1,
    SessionId = sessionId,
    Files = sessionFiles,
};

string sessionFilePath = Path.Combine(sessionDirectory, "session.json");

SessionStore sessionStore = new();

sessionStore.Save(session, sessionFilePath);

Console.WriteLine();
Console.WriteLine("============================================================");

Console.WriteLine("AEGIS SESSION CREATED");

Console.WriteLine("============================================================");

Console.WriteLine($"Session ID: {sessionId}");

Console.WriteLine($"Session:    {sessionFilePath}");

Console.WriteLine($"Sanitized:  {sanitizedDirectory}");

Console.WriteLine(
    $"Baseline:   {baselineDirectory}"
);

Console.WriteLine();
Console.WriteLine("The sanitized files can now be sent to any external LLM.");

Console.WriteLine("Bring the modified sanitized files back to Aegis " + "for reverse mapping.");

/*
 * ============================================================
 * HELPERS
 * ============================================================
 */

static string GetNodeName(string nodeId, PirPackage pirPackage)
{
    PirNode? node = pirPackage.Nodes.FirstOrDefault(node => node.Id == nodeId);

    return node?.Name ?? "";
}
