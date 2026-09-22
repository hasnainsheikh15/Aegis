using Aegis.Sanitizer.Models;
using Aegis.Sanitizer.Models.Import;
using Aegis.Sanitizer.Models.Session;

namespace Aegis.Sanitizer;

public sealed class SessionImporter
{
    private readonly SessionStore sessionStore;
    private readonly SourceHasher sourceHasher;
    private readonly SanitizedChangeDetector changeDetector;
    private readonly ReverseMapper reverseMapper;

    public SessionImporter(
        SessionStore sessionStore,
        SourceHasher sourceHasher,
        SanitizedChangeDetector changeDetector,
        ReverseMapper reverseMapper
    )
    {
        this.sessionStore = sessionStore;
        this.sourceHasher = sourceHasher;
        this.changeDetector = changeDetector;
        this.reverseMapper = reverseMapper;
    }

    public ImportResult Import(string sessionFilePath)
    {
        AegisSession session = sessionStore.Load(sessionFilePath);

        List<ImportedFileResult> files = [];

        foreach (SessionFile sessionFile in session.Files)
        {
            files.Add(ImportFile(sessionFile));
        }

        return new ImportResult { SessionId = session.SessionId, Files = files };
    }

    private ImportedFileResult ImportFile(SessionFile sessionFile)
    {
        List<string> reviewReasons = [];

        if (!File.Exists(sessionFile.OriginalFilePath))
        {
            reviewReasons.Add("Original source file no longer exists.");

            return CreateReviewResult(sessionFile, reviewReasons);
        }

        if (!File.Exists(sessionFile.BaselineSanitizedFilePath))
        {
            reviewReasons.Add("Baseline sanitized file is missing.");

            return CreateReviewResult(sessionFile, reviewReasons);
        }

        if (!File.Exists(sessionFile.SanitizedFilePath))
        {
            reviewReasons.Add("Modified sanitized file is missing.");

            return CreateReviewResult(sessionFile, reviewReasons);
        }

        string originalSource = File.ReadAllText(sessionFile.OriginalFilePath);

        string baselineSanitizedSource = File.ReadAllText(sessionFile.BaselineSanitizedFilePath);

        string modifiedSanitizedSource = File.ReadAllText(sessionFile.SanitizedFilePath);

        /*
         * Verify that the real source has not changed
         * since the session was created.
         */
        string currentOriginalHash = sourceHasher.Compute(originalSource);

        if (
            !string.Equals(
                currentOriginalHash,
                sessionFile.OriginalSourceHash,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            reviewReasons.Add("Original source changed after the session was created.");
        }

        /*
         * Verify that the trusted baseline has not changed.
         */
        string currentBaselineHash = sourceHasher.Compute(baselineSanitizedSource);

        if (
            !string.Equals(
                currentBaselineHash,
                sessionFile.SanitizedSourceHash,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            reviewReasons.Add("Baseline sanitized source does not match the session hash.");
        }

        if (reviewReasons.Count > 0)
        {
            return CreateReviewResult(sessionFile, reviewReasons);
        }

        /*
         * Compare the trusted baseline with the
         * LLM-modified sanitized source.
         */
        List<SanitizedChange> changes = changeDetector.Detect(
            baselineSanitizedSource,
            modifiedSanitizedSource,
            sessionFile.SanitizedFilePath
        );

        /*
         * No changes means there is nothing to import.
         */
        if (changes.Count == 0)
        {
            return new ImportedFileResult
            {
                OriginalFilePath = sessionFile.OriginalFilePath,

                Changes = [],

                Patches = [],

                ReviewReasons = [],
            };
        }

        /*
         * Convert the session mappings into the
         * SanitizationMapping model used by ReverseMapper.
         */
        List<SanitizationMapping> mappings = sessionFile
            .Mappings.Select(mapping => new SanitizationMapping
            {
                NodeId = mapping.NodeId,
                OriginalText = mapping.OriginalText,
                DummyText = mapping.DummyText,
                FilePath = sessionFile.OriginalFilePath,
                OriginalStart = mapping.OriginalStart,
                OriginalLength = mapping.OriginalLength,
                SanitizedStart = mapping.SanitizedStart,
                SanitizedLength = mapping.SanitizedLength,
            })
            .ToList();

        /*
         * Determine which changes overlap protected values.
         */
        List<ReverseMappedChange> mappedChanges = reverseMapper.Analyze(changes, mappings);

        /*
         * Convert every analyzed change into a patch
         * against the real source.
         */
        List<ReversePatch> patches = [];

        foreach (ReverseMappedChange mappedChange in mappedChanges)
        {
            ReversePatch? patch = reverseMapper.CreatePatch(mappedChange, mappings, originalSource);

            if (patch is null)
            {
                reviewReasons.Add("A reverse patch could not be created.");

                continue;
            }

            patches.Add(patch);
        }

        return new ImportedFileResult
        {
            OriginalFilePath = sessionFile.OriginalFilePath,

            Changes = mappedChanges,

            Patches = patches,

            ReviewReasons = reviewReasons,
        };
    }

    private static ImportedFileResult CreateReviewResult(
        SessionFile sessionFile,
        List<string> reviewReasons
    )
    {
        return new ImportedFileResult
        {
            OriginalFilePath = sessionFile.OriginalFilePath,

            Changes = [],

            Patches = [],

            ReviewReasons = reviewReasons,
        };
    }
}
