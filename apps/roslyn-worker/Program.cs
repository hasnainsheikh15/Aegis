using Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;
using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Mappers;
using RoslynWorker.Printers;
using RoslynWorker.Selection;
using RoslynWorker.Validation;

if (args.Length < 4)
{
    Console.WriteLine(
        "Usage: RoslynWorker <project-folder> <file-path> <start> <length>"
    );

    return;
}

string projectPath =
    Path.GetFullPath(args[0]);

string selectedFilePath =
    Path.GetFullPath(args[1]);

if (!Directory.Exists(projectPath))
{
    Console.WriteLine(
        $"Project directory not found: {projectPath}"
    );

    return;
}

if (!File.Exists(selectedFilePath))
{
    Console.WriteLine(
        $"Selected file not found: {selectedFilePath}"
    );

    return;
}

if (!int.TryParse(args[2], out int selectionStart))
{
    Console.WriteLine(
        "Selection start must be an integer."
    );

    return;
}

if (!int.TryParse(args[3], out int selectionLength))
{
    Console.WriteLine(
        "Selection length must be an integer."
    );

    return;
}

if (selectionStart < 0)
{
    Console.WriteLine(
        "Selection start cannot be negative."
    );

    return;
}

if (selectionLength < 0)
{
    Console.WriteLine(
        "Selection length cannot be negative."
    );

    return;
}

/*
 * ============================================================
 * READ PROJECT
 * ============================================================
 */

string[] files =
    Directory.GetFiles(
        projectPath,
        "*.cs",
        SearchOption.AllDirectories
    );

if (files.Length == 0)
{
    Console.WriteLine(
        "No C# files found in the project."
    );

    return;
}

/*
 * ============================================================
 * PARSE PROJECT
 * ============================================================
 */

List<SyntaxTree> syntaxTrees = [];

foreach (string file in files)
{
    string sourceCode =
        File.ReadAllText(file);

    SyntaxTree syntaxTree =
        CSharpSyntaxTree.ParseText(
            sourceCode,
            path: file
        );

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
    MetadataReference.CreateFromFile(
        typeof(object).Assembly.Location
    ),

    MetadataReference.CreateFromFile(
        typeof(Console).Assembly.Location
    ),
];

CSharpCompilation compilation =
    CSharpCompilation.Create(
        assemblyName: "AegisAnalysis",
        syntaxTrees: syntaxTrees,
        references: references
    );

/*
 * ============================================================
 * BUILD PIR
 * ============================================================
 */

RoslynToPirMapper mapper =
    new();

PirPackage pirPackage =
    new();

foreach (SyntaxTree syntaxTree in syntaxTrees)
{
    CompilationUnitSyntax root =
        syntaxTree.GetCompilationUnitRoot();

    SemanticModel semanticModel =
        compilation.GetSemanticModel(syntaxTree);

    PirPackage filePackage =
        mapper.MapCompilationUnit(
            root,
            semanticModel
        );

    pirPackage.Nodes.AddRange(
        filePackage.Nodes
    );

    pirPackage.Relationships.AddRange(
        filePackage.Relationships
    );
}

/*
 * ============================================================
 * SELECTION
 * ============================================================
 */

SyntaxTree? selectedSyntaxTree =
    syntaxTrees.FirstOrDefault(
        tree =>
            string.Equals(
                Path.GetFullPath(tree.FilePath),
                selectedFilePath,
                StringComparison.OrdinalIgnoreCase
            )
    );

if (selectedSyntaxTree is null)
{
    Console.WriteLine(
        $"Selected file is not part of the analyzed project: "
        + $"{selectedFilePath}"
    );

    return;
}

string selectedSource =
    File.ReadAllText(selectedFilePath);

if (
    selectionStart > selectedSource.Length
    || selectionLength > selectedSource.Length - selectionStart
)
{
    Console.WriteLine(
        "Selection range is outside the selected file."
    );

    return;
}

SourceSelection selection =
    new()
    {
        FilePath = selectedFilePath,
        Start = selectionStart,
        Length = selectionLength,
    };

SemanticModel selectedSemanticModel =
    compilation.GetSemanticModel(
        selectedSyntaxTree
    );

SelectionResolver selectionResolver =
    new();

List<PirNode> selectedNodes =
    selectionResolver.Resolve(
        selectedSyntaxTree,
        selectedSemanticModel,
        selection,
        mapper
    );

Console.WriteLine();
Console.WriteLine("SELECTION");

Console.WriteLine(
    $"File: {selection.FilePath}"
);

Console.WriteLine(
    $"Start: {selection.Start}"
);

Console.WriteLine(
    $"Length: {selection.Length}"
);

Console.WriteLine(
    $"Selected nodes: {selectedNodes.Count}"
);

foreach (PirNode node in selectedNodes)
{
    Console.WriteLine(
        $"  {node.Type} : {node.Name}"
    );
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
    Console.WriteLine(
        "No PIR node could be resolved from the selection."
    );

    return;
}

if (selectedNodes.Count > 1)
{
    Console.WriteLine();
    Console.WriteLine(
        "Selection resolved to multiple PIR nodes."
    );

    Console.WriteLine(
        "The alpha currently requires exactly one selected node."
    );

    return;
}

PirNode selectedNode =
    selectedNodes[0];

Console.WriteLine();
Console.WriteLine(
    $"Selected root: {selectedNode.Type} : {selectedNode.Name}"
);

/*
 * ============================================================
 * BUILD GRAPH
 * ============================================================
 */

PirGraph graph =
    new(pirPackage);

GraphAnalyzer graphAnalyzer =
    new(graph);

DependencyOptions options =
    new()
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

SensitivityAnalyzer sensitivityAnalyzer =
    new();

List<SensitivityResult> intrinsicResults = [];

foreach (PirNode node in pirPackage.Nodes)
{
    SensitivityResult result =
        sensitivityAnalyzer.Analyze(node);

    intrinsicResults.Add(result);
}

Console.WriteLine();
Console.WriteLine(
    "SENSITIVITY ANALYSIS"
);

foreach (SensitivityResult result in intrinsicResults)
{
    Console.WriteLine(
        $"{result.Node.Type} : "
        + $"{result.Node.Name} → "
        + $"{result.Level}"
    );

    foreach (string reason in result.Reasons)
    {
        Console.WriteLine(
            $"  Reason: {reason}"
        );
    }
}

/*
 * ============================================================
 * DEPENDENCY SLICE + SENSITIVITY PROPAGATION
 * ============================================================
 *
 * The dependency root is now the ACTUAL USER SELECTION.
 */

ProgramSlice slice =
    graphAnalyzer.BuildDependencySlice(
        selectedNode,
        options
    );

Console.WriteLine();
Console.WriteLine(
    "DEPENDENCY SLICE"
);

foreach (PirNode node in slice.Nodes)
{
    Console.WriteLine(
        $"  {node.Type} : {node.Name}"
    );
}

/*
 * Start propagation from the intrinsic sensitivity results
 * belonging to the selected node's dependency slice.
 */

List<SensitivityResult> sliceResults =
    slice.Nodes
        .Select(
            node =>
                intrinsicResults.First(
                    result =>
                        result.Node.Id == node.Id
                )
        )
        .ToList();

SensitivityPropagator propagator =
    new(
        graph,
        pirPackage
    );

Dictionary<string, SensitivityLevel> propagated =
    propagator.Propagate(
        sliceResults
    );

Console.WriteLine();
Console.WriteLine(
    "PROPAGATED SENSITIVITY"
);

foreach (PirNode node in slice.Nodes)
{
    if (
        propagated.TryGetValue(
            node.Id,
            out SensitivityLevel level
        )
    )
    {
        Console.WriteLine(
            $"{node.Type} : "
            + $"{node.Name} → "
            + $"{level}"
        );
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

HashSet<string> sliceNodeIds =
    slice.Nodes
        .Select(node => node.Id)
        .ToHashSet();

List<PirNode> sensitiveNodes =
    intrinsicResults
        .Where(
            result =>
                result.Level >= SensitivityLevel.Sensitive
                && sliceNodeIds.Contains(result.Node.Id)
        )
        .Select(
            result => result.Node
        )
        .ToList();

SanitizationPlanner planner =
    new();

List<SanitizationTarget> targets =
    planner.Plan(
        sensitiveNodes,
        node =>
        {
            if (
                !mapper.TryGetSyntaxNode(
                    node,
                    out SyntaxNode? syntaxNode
                )
            )
            {
                return null;
            }

            if (
                syntaxNode
                    is not VariableDeclaratorSyntax variableNode
            )
            {
                return null;
            }

            if (variableNode.Initializer is null)
            {
                return null;
            }

            ExpressionSyntax initializer =
                variableNode.Initializer.Value;

            return new SanitizationTarget
            {
                NodeId = node.Id,

                NodeType = node.Type,

                FilePath =
                    syntaxNode.SyntaxTree.FilePath,

                Start =
                    initializer.Span.Start,

                Length =
                    initializer.Span.Length,

                OriginalText =
                    initializer.ToFullString(),
            };
        }
    );

Console.WriteLine();
Console.WriteLine(
    "SANITIZATION TARGETS"
);

foreach (SanitizationTarget target in targets)
{
    Console.WriteLine(
        $"{target.NodeType} : {target.NodeId}"
    );

    Console.WriteLine(
        $"  File: {target.FilePath}"
    );

    Console.WriteLine(
        $"  Start: {target.Start}"
    );

    Console.WriteLine(
        $"  Length: {target.Length}"
    );

    Console.WriteLine(
        $"  Original: {target.OriginalText}"
    );
}

/*
 * ============================================================
 * SANITIZATION
 * ============================================================
 */

DummyValueGenerator dummyValueGenerator =
    new();

SourceSanitizer sourceSanitizer =
    new();

Dictionary<string, List<SanitizationTarget>> targetsByFile =
    targets
        .GroupBy(
            target => target.FilePath
        )
        .ToDictionary(
            group => group.Key,
            group => group.ToList()
        );

foreach (
    (
        string filePath,
        List<SanitizationTarget> fileTargets
    )
    in targetsByFile
)
{
    string source =
        File.ReadAllText(filePath);

    List<SanitizationMapping> mappings =
        sourceSanitizer.Sanitize(
            source,
            fileTargets,
            target =>
            {
                return dummyValueGenerator.Generate(
                    target.OriginalText,
                    GetNodeName(
                        target.NodeId,
                        pirPackage
                    )
                );
            },
            out string sanitizedSource
        );

    Console.WriteLine();
    Console.WriteLine(
        "SANITIZED SOURCE"
    );

    Console.WriteLine(
        sanitizedSource
    );

    Console.WriteLine();
    Console.WriteLine(
        "SANITIZED CHANGES"
    );

    /*
     * ========================================================
     * TEMPORARY LLM SIMULATION
     * ========================================================
     *
     * This remains a test harness.
     *
     * We will replace this with actual external LLM output
     * after the local round-trip pipeline is stable.
     */

    SanitizedChangeDetector changeDetector =
        new();

    string modifiedSanitizedSource =
        sanitizedSource
            .Replace(
                "string token = password;",
                "string token = Hash(password);"
            )
            .Replace(
                "string backup = token;",
                "string backup = Encrypt(token);"
            )
            .Replace(
                "private string password = \"DUMMY_PASSWORD\";",
                "private string password = Hash(\"DUMMY_PASSWORD\");"
            );

    List<SanitizedChange> changes =
        changeDetector.Detect(
            sanitizedSource,
            modifiedSanitizedSource,
            filePath
        );

    ReverseMapper reverseMapper =
        new();

    List<ReverseMappedChange> reverseResults =
        reverseMapper.Analyze(
            changes,
            mappings
        );

    Console.WriteLine();
    Console.WriteLine(
        "REVERSE MAPPING ANALYSIS"
    );

    foreach (ReverseMappedChange result in reverseResults)
    {
        SanitizedChange change =
            result.SourceChange;

        Console.WriteLine(
            $"Start: {change.Start}"
        );

        Console.WriteLine(
            $"Original: {change.OriginalText}"
        );

        Console.WriteLine(
            $"New: {change.NewText}"
        );

        Console.WriteLine(
            $"Safe: {result.IsSafe}"
        );

        foreach (ProtectedRegion region in result.ProtectedRegions)
        {
            Console.WriteLine(
                $"  Protected Region: "
                + $"Start={region.Start}, "
                + $"Length={region.Length}"
            );

            Console.WriteLine(
                $"  Dummy: {region.DummyText}"
            );

            Console.WriteLine(
                $"  Real: {region.OriginalText}"
            );

            Console.WriteLine(
                $"  Node ID: {region.NodeId}"
            );
        }

        if (result.Reason is not null)
        {
            Console.WriteLine(
                $"Reason: {result.Reason}"
            );
        }

        Console.WriteLine();
    }

    /*
     * ========================================================
     * REVERSE PATCH
     * ========================================================
     */

    PatchValidator patchValidator =
        new();

    Console.WriteLine();
    Console.WriteLine(
        "REVERSE PATCH CANDIDATES"
    );

    foreach (ReverseMappedChange result in reverseResults)
    {
        ReversePatch? patch =
            reverseMapper.CreatePatch(
                result,
                mappings,
                source
            );

        if (patch is null)
        {
            continue;
        }

        Console.WriteLine(
            $"File: {patch.FilePath}"
        );

        Console.WriteLine(
            $"Start: {patch.Start}"
        );

        Console.WriteLine(
            $"Original: {patch.OriginalText}"
        );

        Console.WriteLine(
            $"Replacement: {patch.ReplacementText}"
        );

        Console.WriteLine(
            $"Requires Review: {patch.RequiresReview}"
        );

        Console.WriteLine(
            $"Reason: {patch.Reason}"
        );

        if (patch.RequiresReview)
        {
            Console.WriteLine(
                "Validation: SKIPPED — requires review."
            );

            Console.WriteLine();

            continue;
        }

        string originalSource =
            File.ReadAllText(
                patch.FilePath
            );

        PatchValidationResult validation =
            patchValidator.Validate(
                originalSource,
                patch
            );

        Console.WriteLine(
            $"Validation: "
            + $"{(validation.IsValid ? "PASSED" : "FAILED")}"
        );

        foreach (string diagnostic in validation.Diagnostics)
        {
            Console.WriteLine(
                $"  {diagnostic}"
            );
        }

        Console.WriteLine();
    }
}

/*
 * ============================================================
 * HELPERS
 * ============================================================
 */

static string GetNodeName(
    string nodeId,
    PirPackage pirPackage
)
{
    PirNode? node =
        pirPackage.Nodes.FirstOrDefault(
            node => node.Id == nodeId
        );

    return node?.Name ?? "";
}