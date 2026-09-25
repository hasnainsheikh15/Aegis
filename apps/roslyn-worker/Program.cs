using Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;
using Aegis.Sanitizer;
using Aegis.Sanitizer.Models;
using Aegis.Sanitizer.Models.Import;
using Aegis.Sanitizer.Models.Session;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Mappers;
using RoslynWorker.Models;
using RoslynWorker.Selection;
using RoslynWorker.Validation;

if (args.Length == 0)
{
    PrintUsage();
    return;
}

string command = args[0].ToLowerInvariant();

switch (command)
{
    case "sanitize":
        await Sanitize(args);
        break;
    case "apply":
        Apply(args);
        break;

    case "import":
        Import(args);
        break;

    case "status":
        Status(args);
        break;

    default:
        Console.WriteLine($"Unknown command: {args[0]}");
        Console.WriteLine();
        PrintUsage();
        break;
}

static async Task Sanitize(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine(
            "Usage: RoslynWorker sanitize <project-folder> <file-path> [<start> <length>]"
        );

        return;
    }

    bool interactiveSelection = args.Length == 3;

    if (!interactiveSelection && args.Length < 5)
    {
        Console.WriteLine(
            "Usage: RoslynWorker sanitize <project-folder> <file-path> [<start> <length>]"
        );

        return;
    }
    string projectPath = Path.GetFullPath(args[1]);
    string selectedFilePath = Path.GetFullPath(args[2]);

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

    string activeSessionsDirectory = Path.Combine(projectPath, ".aegis", "sessions");

    if (Directory.Exists(activeSessionsDirectory))
    {
        string[] existingSessionFiles = Directory.GetFiles(
            activeSessionsDirectory,
            "session.json",
            SearchOption.AllDirectories
        );

        SessionStore activeSessionStore = new();

        List<(string Path, AegisSession Session)> existingReadySessions = [];

        foreach (string existingSessionFile in existingSessionFiles)
        {
            AegisSession existingSession = activeSessionStore.Load(existingSessionFile);

            if (existingSession.Status == SessionStatus.Ready)
            {
                existingReadySessions.Add((existingSessionFile, existingSession));
            }
        }

        if (existingReadySessions.Count > 0)
        {
            Console.WriteLine("An active Aegis session already exists.");

            Console.WriteLine($"Session: {existingReadySessions[0].Session.SessionId}");

            Console.WriteLine();

            Console.WriteLine("Run aegis apply before starting another session.");

            return;
        }
    }

    int selectionStart;
    int selectionLength;

    if (interactiveSelection)
    {
        string source = File.ReadAllText(selectedFilePath);

        string[] lines = source.Split('\n');

        Console.WriteLine();
        Console.WriteLine($"File: {selectedFilePath}");
        Console.WriteLine();

        for (int i = 0; i < lines.Length; i++)
        {
            Console.WriteLine($"{i + 1, 4} | {lines[i].TrimEnd('\r')}");
        }

        Console.WriteLine();

        Console.Write("Start line: ");

        if (!int.TryParse(Console.ReadLine(), out int startLine))
        {
            Console.WriteLine("Start line must be an integer.");
            return;
        }

        Console.Write("End line: ");

        if (!int.TryParse(Console.ReadLine(), out int endLine))
        {
            Console.WriteLine("End line must be an integer.");
            return;
        }

        if (startLine < 1 || endLine < startLine || endLine > lines.Length)
        {
            Console.WriteLine("Invalid line range.");
            return;
        }

        selectionStart = 0;

        for (int i = 0; i < startLine - 1; i++)
        {
            selectionStart += lines[i].Length + 1;
        }

        selectionLength = 0;

        for (int i = startLine - 1; i < endLine; i++)
        {
            selectionLength += lines[i].Length;

            if (i < endLine - 1)
            {
                selectionLength += 1;
            }
        }
    }
    else
    {
        if (!int.TryParse(args[3], out selectionStart))
        {
            Console.WriteLine("Selection start must be an integer.");
            return;
        }

        if (!int.TryParse(args[4], out selectionLength))
        {
            Console.WriteLine("Selection length must be an integer.");
            return;
        }
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

    CSharpCompilation compilation = CSharpCompilation.Create(
        assemblyName: "AegisAnalysis",
        syntaxTrees: syntaxTrees,
        references: references
    );

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

    SyntaxTree? selectedSyntaxTree = syntaxTrees.FirstOrDefault(tree =>
        string.Equals(
            Path.GetFullPath(tree.FilePath),
            selectedFilePath,
            StringComparison.OrdinalIgnoreCase
        )
    );

    if (selectedSyntaxTree is null)
    {
        Console.WriteLine("Selected file is not part of the analyzed project.");

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

    List<PirNode> selectedNodes;

    if (interactiveSelection)
    {
        selectedNodes = selectionResolver.ResolveRange(selectedSyntaxTree, selection, mapper);
    }
    else
    {
        selectedNodes = selectionResolver.Resolve(
            selectedSyntaxTree,
            selectedSemanticModel,
            selection,
            mapper
        );
    }

    if (selectedNodes.Count == 0)
    {
        Console.WriteLine("No PIR nodes could be resolved from the selection.");

        return;
    }

    if (!interactiveSelection && selectedNodes.Count > 1)
    {
        Console.WriteLine("Selection resolved to multiple PIR nodes.");

        Console.WriteLine(
            "The precise selection mode currently requires exactly one selected node."
        );

        return;
    }

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

    SensitivityAnalyzer sensitivityAnalyzer = new();

    List<SensitivityResult> intrinsicResults = [];

    foreach (PirNode node in pirPackage.Nodes)
    {
        intrinsicResults.Add(sensitivityAnalyzer.Analyze(node));
    }

    SensitivityPropagator sensitivityPropagator = new(graph, pirPackage);

    Dictionary<string, SensitivityLevel> propagatedSensitivity = sensitivityPropagator.Propagate(
        intrinsicResults
    );

    List<PirNode> sensitiveNodes;

    if (interactiveSelection)
    {
        sensitiveNodes = selectedNodes
            .Where(node =>
                propagatedSensitivity.TryGetValue(node.Id, out SensitivityLevel level)
                && level >= SensitivityLevel.Sensitive
            )
            .ToList();
    }
    else
    {
        PirNode selectedNode = selectedNodes[0];

        ProgramSlice slice = graphAnalyzer.BuildDependencySlice(selectedNode, options);

        sensitiveNodes = slice
            .Nodes.Where(node =>
                propagatedSensitivity.TryGetValue(node.Id, out SensitivityLevel level)
                && level >= SensitivityLevel.Sensitive
            )
            .ToList();
    }

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

            LiteralExpressionSyntax? literal = initializer
                .DescendantNodesAndSelf()
                .OfType<LiteralExpressionSyntax>()
                .FirstOrDefault(literal => literal.IsKind(SyntaxKind.StringLiteralExpression));

            if (literal is null)
            {
                return null;
            }

            return new SanitizationTarget
            {
                NodeId = node.Id,
                NodeType = node.Type,
                FilePath = syntaxNode.SyntaxTree.FilePath,
                Start = literal.Span.Start,
                Length = literal.Span.Length,
                OriginalText = literal.ToFullString(),
            };
        }
    );

    if (targets.Count == 0)
    {
        Console.WriteLine("No sanitization targets were found.");

        return;
    }

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
                dummyValueGenerator.Generate(
                    target.OriginalText,
                    GetNodeName(target.NodeId, pirPackage)
                ),
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
                        "Could not resolve PIR node for "
                            + $"sanitization mapping: {mapping.NodeId}"
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

    WriteSessionReadme(sessionDirectory, session);

    string readmePath = Path.Combine(sessionDirectory, "README.md");

    Console.WriteLine("Aegis session created.");
    Console.WriteLine();
    Console.WriteLine($"Session:   {sessionFilePath}");
    Console.WriteLine($"Sanitized: {sanitizedDirectory}");
    Console.WriteLine($"README:    {readmePath}");
    Console.WriteLine();
    Console.WriteLine("Next:");
    Console.WriteLine("  1. Review the sanitized files.");
    Console.WriteLine("  2. Send the sanitized source to your preferred LLM.");
    Console.WriteLine("  3. Modify the sanitized files with the requested changes.");
    Console.WriteLine("  4. Run:");
    Console.WriteLine();
    Console.WriteLine($"       aegis apply");
}

static void Status(string[] args)
{
    string projectPath =
        args.Length >= 2 ? Path.GetFullPath(args[1]) : Directory.GetCurrentDirectory();

    string sessionsDirectory = Path.Combine(projectPath, ".aegis", "sessions");

    if (!Directory.Exists(sessionsDirectory))
    {
        Console.WriteLine("""{"status":"none"}""");
        return;
    }

    string[] sessionFiles = Directory.GetFiles(
        sessionsDirectory,
        "session.json",
        SearchOption.AllDirectories
    );

    if (sessionFiles.Length == 0)
    {
        Console.WriteLine("""{"status":"none"}""");
        return;
    }

    SessionStore sessionStore = new SessionStore();

    List<string> readySessions = [];

    foreach (string sessionFile in sessionFiles)
    {
        AegisSession session = sessionStore.Load(sessionFile);

        if (session.Status == SessionStatus.Ready)
        {
            readySessions.Add(sessionFile);
        }
    }

    if (readySessions.Count == 0)
    {
        Console.WriteLine("""{"status":"none"}""");
        return;
    }

    if (readySessions.Count > 1)
    {
        Console.WriteLine("""{"status":"multiple"}""");
        return;
    }

    string activeSessionPath = readySessions[0];

    AegisSession activeSession = sessionStore.Load(activeSessionPath);

    string filesJson = string.Join(
        ",",
        activeSession.Files.Select(file =>
            $$"""{"name":"{{Path.GetFileName(file.OriginalFilePath)}}","status":"protected"}"""
        )
    );

    Console.WriteLine(
        $$"""
        {
          "status": "ready",
          "sessionId": "{{activeSession.SessionId}}",
          "sessionPath": "{{activeSessionPath.Replace("\\", "\\\\")}}",
          "files": [{{filesJson}}]
        }
        """
    );
}

static void Apply(string[] args)
{
    string projectPath =
        args.Length >= 2 ? Path.GetFullPath(args[1]) : Directory.GetCurrentDirectory();

    // Console.WriteLine($"Project: {projectPath}");

    string sessionsDirectory = Path.Combine(projectPath, ".aegis", "sessions");

    // Console.WriteLine($"Sessions: {sessionsDirectory}");

    if (!Directory.Exists(sessionsDirectory))
    {
        Console.WriteLine("No Aegis sessions found.");
        return;
    }

    string[] sessionFiles = Directory.GetFiles(
        sessionsDirectory,
        "session.json",
        SearchOption.AllDirectories
    );

    if (sessionFiles.Length == 0)
    {
        Console.WriteLine("No Aegis sessions found.");
        return;
    }

    if (sessionFiles.Length > 1)
    {
        Console.WriteLine("Multiple Aegis sessions found.");
        Console.WriteLine("The alpha requires exactly one session.");
        return;
    }

    SessionStore sessionStore = new();

    List<(string Path, AegisSession Session)> readySessions = [];

    foreach (string sessionFile in sessionFiles)
    {
        AegisSession session = sessionStore.Load(sessionFile);

        if (session.Status == SessionStatus.Ready)
        {
            readySessions.Add((sessionFile, session));
        }
    }

    if (readySessions.Count == 0)
    {
        Console.WriteLine("No Aegis sessions found.");

        return;
    }

    if (readySessions.Count > 1)
    {
        Console.WriteLine(
            "Multiple Aegis sessions found. The alpha requires exactly one active session."
        );

        return;
    }

    string sessionFilePath = readySessions[0].Path;
    AegisSession activeSession = readySessions[0].Session;

    Console.WriteLine($"Session: {activeSession.SessionId}");

    Import(["import", sessionFilePath]);
}
static void Import(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: RoslynWorker import <session.json>");

        return;
    }

    string sessionFilePath = Path.GetFullPath(args[1]);

    if (!File.Exists(sessionFilePath))
    {
        Console.WriteLine($"Session file not found: {sessionFilePath}");

        return;
    }

    SessionStore sessionStore = new();

    AegisSession session = sessionStore.Load(sessionFilePath);

    if (session.Status == SessionStatus.Consumed)
    {
        Console.WriteLine("Session has already been consumed.");

        return;
    }

    SessionImporter importer = new(
        sessionStore,
        new SourceHasher(),
        new SanitizedChangeDetector(),
        new ReverseMapper()
    );

    ImportResult importResult = importer.Import(sessionFilePath);

    PatchApplicationService patchApplicationService = new(
        new PatchApplier(),
        new PatchValidator(),
        new SourceHasher()
    );

    bool hadChanges = false;
    bool hadReview = false;

    foreach (ImportedFileResult fileResult in importResult.Files)
    {
        if (fileResult.RequiresReview)
        {
            hadReview = true;
            hadChanges = true;

            Console.WriteLine($"Review required: {fileResult.OriginalFilePath}");

            foreach (string reason in fileResult.ReviewReasons)
            {
                Console.WriteLine($"  {reason}");
            }

            foreach (ReversePatch patch in fileResult.Patches)
            {
                if (patch.RequiresReview && !string.IsNullOrWhiteSpace(patch.Reason))
                {
                    Console.WriteLine($"  {patch.Reason}");
                }
            }

            continue;
        }

        if (fileResult.Changes.Count == 0)
        {
            continue;
        }

        hadChanges = true;

        SessionFile? sessionFile = session.Files.FirstOrDefault(file =>
            string.Equals(
                file.OriginalFilePath,
                fileResult.OriginalFilePath,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (sessionFile is null)
        {
            Console.WriteLine(
                $"Could not resolve session metadata for: " + fileResult.OriginalFilePath
            );

            hadReview = true;
            continue;
        }

        PatchApplicationResult applicationResult = patchApplicationService.Apply(
            fileResult.OriginalFilePath,
            sessionFile.OriginalSourceHash,
            fileResult.Patches
        );

        if (!applicationResult.Applied)
        {
            hadReview = true;

            Console.WriteLine($"Review required: {fileResult.OriginalFilePath}");

            Console.WriteLine($"  {applicationResult.Message}");

            foreach (string diagnostic in applicationResult.Diagnostics)
            {
                Console.WriteLine($"  {diagnostic}");
            }

            continue;
        }

        Console.WriteLine($"Applied: {fileResult.OriginalFilePath}");
    }

    if (!hadChanges)
    {
        Console.WriteLine("No changes were detected.");

        return;
    }

    if (hadReview)
    {
        Console.WriteLine("Import completed with files requiring review.");

        return;
    }

    // Every file was processed successfully.
    session.Status = SessionStatus.Consumed;

    sessionStore.Save(session, sessionFilePath);

    Console.WriteLine("Session marked as consumed.");

    Console.WriteLine("Import completed successfully.");
}

static string GetNodeName(string nodeId, PirPackage pirPackage)
{
    PirNode? node = pirPackage.Nodes.FirstOrDefault(node => node.Id == nodeId);

    return node?.Name ?? "";
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");

    Console.WriteLine("  RoslynWorker sanitize <project-folder> <file-path> <start> <length>");

    Console.WriteLine("  RoslynWorker import <session.json>");
}

static void WriteSessionReadme(string sessionDirectory, AegisSession session)
{
    string readmePath = Path.Combine(sessionDirectory, "README.md");

    string sessionFilePath = Path.Combine(sessionDirectory, "session.json");

    List<string> lines =
    [
        "# Aegis Sanitized Session",
        "",
        $"Session ID: `{session.SessionId}`",
        "",
        "## Purpose",
        "",
        "This session contains a sanitized copy of selected source code.",
        "Sensitive values identified by Aegis have been replaced with dummy values.",
        "",
        "The original source remains outside this session.",
        "",
        "## Workflow",
        "",
        "1. Review the sanitized files in the `sanitized` directory.",
        "2. Send the sanitized source to your preferred external LLM.",
        "3. Ask the LLM to make the desired changes.",
        "4. Apply or paste the LLM's changes into the sanitized files.",
        "5. Review the modified sanitized files.",
        "6. Import the session with:",
        "",
        "```bash",
        "aegis apply",
        "```",
        "",
        "Aegis compares the modified sanitized source against the trusted baseline.",
        "It then validates the changes before modifying the original source files.",
        "",
        "## Security rules",
        "",
        "- Do not place original sensitive values into the sanitized files.",
        "- Do not replace, modify, duplicate, or move protected dummy values.",
        "- Aegis may require manual review when a change cannot be safely mapped back.",
        "- A session can be successfully imported only once.",
        "- If the original source changes after the session is created, the import requires review.",
        "",
        "## If import requires review",
        "",
        "Aegis will not automatically apply an unsafe change to the original source.",
        "Review the reported reason and inspect the proposed change manually.",
        "",
        "## Files",
        "",
    ];

    foreach (SessionFile file in session.Files)
    {
        lines.Add($"- `{Path.GetFileName(file.SanitizedFilePath)}`");
    }

    lines.Add("");
    lines.Add(
        "The `baseline` directory contains the trusted sanitized snapshot used for change detection."
    );
    lines.Add("");
    lines.Add("The `session.json` file contains the session metadata and sanitization mappings.");
    lines.Add("");

    File.WriteAllLines(readmePath, lines);
}
