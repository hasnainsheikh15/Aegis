using Aegis.Sanitizer.Models;

namespace Aegis.Sanitizer;

public sealed class SanitizedChangeDetector
{
    public List<SanitizedChange> Detect(
        string originalSanitizedSource,
        string modifiedSanitizedSource,
        string filePath
    )
    {
        if (originalSanitizedSource == modifiedSanitizedSource)
            return [];

        string[] originalLines = SplitLines(originalSanitizedSource);
        string[] modifiedLines = SplitLines(modifiedSanitizedSource);

        List<LineOperation> operations = BuildLineDiff(originalLines, modifiedLines);

        List<SanitizedChange> changes = [];

        int originalPosition = 0;

        foreach (LineOperation operation in operations)
        {
            switch (operation.Type)
            {
                case LineOperationType.Equal:
                    originalPosition += operation.OriginalText.Length;
                    break;

                case LineOperationType.Replace:
                    changes.Add(
                        new SanitizedChange
                        {
                            FilePath = filePath,
                            Start = originalPosition,
                            OriginalLength = operation.OriginalText.Length,
                            OriginalText = operation.OriginalText,
                            NewLength = operation.NewText.Length,
                            NewText = operation.NewText,
                        }
                    );

                    originalPosition += operation.OriginalText.Length;
                    break;

                case LineOperationType.Delete:
                    changes.Add(
                        new SanitizedChange
                        {
                            FilePath = filePath,
                            Start = originalPosition,
                            OriginalLength = operation.OriginalText.Length,
                            OriginalText = operation.OriginalText,
                            NewLength = 0,
                            NewText = "",
                        }
                    );

                    originalPosition += operation.OriginalText.Length;
                    break;

                case LineOperationType.Insert:
                    changes.Add(
                        new SanitizedChange
                        {
                            FilePath = filePath,
                            Start = originalPosition,
                            OriginalLength = 0,
                            OriginalText = "",
                            NewLength = operation.NewText.Length,
                            NewText = operation.NewText,
                        }
                    );

                    break;
            }
        }

        return changes;
    }

    private static string[] SplitLines(string source)
    {
        List<string> lines = [];

        int start = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != '\n')
            {
                continue;
            }

            lines.Add(source.Substring(start, i - start + 1));

            start = i + 1;
        }

        if (start < source.Length)
        {
            lines.Add(source.Substring(start));
        }

        return lines.ToArray();
    }

    private static List<LineOperation> BuildLineDiff(string[] original, string[] modified)
    {
        int n = original.Length;
        int m = modified.Length;

        int[,] dp = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
            dp[i, m] = n - i;

        for (int j = 0; j <= m; j++)
            dp[n, j] = m - j;

        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                if (original[i] == modified[j])
                {
                    dp[i, j] = dp[i + 1, j + 1];
                }
                else
                {
                    int replaceCost = 1 + dp[i + 1, j + 1];

                    int deleteCost = 1 + dp[i + 1, j];

                    int insertCost = 1 + dp[i, j + 1];

                    dp[i, j] = Math.Min(replaceCost, Math.Min(deleteCost, insertCost));
                }
            }
        }

        List<LineOperation> operations = [];

        int originalIndex = 0;
        int modifiedIndex = 0;

        while (originalIndex < n || modifiedIndex < m)
        {
            if (
                originalIndex < n
                && modifiedIndex < m
                && original[originalIndex] == modified[modifiedIndex]
            )
            {
                operations.Add(
                    new LineOperation
                    {
                        Type = LineOperationType.Equal,
                        OriginalText = original[originalIndex],
                        NewText = modified[modifiedIndex],
                    }
                );

                originalIndex++;
                modifiedIndex++;

                continue;
            }

            // Prefer replacement when it is part of an optimal edit path.
            if (
                originalIndex < n
                && modifiedIndex < m
                && dp[originalIndex, modifiedIndex] == 1 + dp[originalIndex + 1, modifiedIndex + 1]
            )
            {
                operations.Add(
                    new LineOperation
                    {
                        Type = LineOperationType.Replace,
                        OriginalText = original[originalIndex],
                        NewText = modified[modifiedIndex],
                    }
                );

                originalIndex++;
                modifiedIndex++;

                continue;
            }

            if (
                originalIndex < n
                && dp[originalIndex, modifiedIndex] == 1 + dp[originalIndex + 1, modifiedIndex]
            )
            {
                operations.Add(
                    new LineOperation
                    {
                        Type = LineOperationType.Delete,
                        OriginalText = original[originalIndex],
                        NewText = "",
                    }
                );

                originalIndex++;

                continue;
            }

            if (modifiedIndex < m)
            {
                operations.Add(
                    new LineOperation
                    {
                        Type = LineOperationType.Insert,
                        OriginalText = "",
                        NewText = modified[modifiedIndex],
                    }
                );

                modifiedIndex++;

                continue;
            }
        }

        return operations;
    }

    private sealed class LineOperation
    {
        public required LineOperationType Type { get; init; }

        public required string OriginalText { get; init; }

        public required string NewText { get; init; }
    }

    private enum LineOperationType
    {
        Equal,
        Replace,
        Insert,
        Delete,
    }
}
