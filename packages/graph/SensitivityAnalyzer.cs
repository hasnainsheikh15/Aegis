namespace Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;

public sealed class SensitivityAnalyzer
{
    private static readonly HashSet<string> SensitiveNames =
    [
        "password",
        "passwd",
        "secret",
        "apikey",
        "apitoken",
        "accesstoken",
        "privatekey",
        "connectionstring",
        "credential",
    ];

    public SensitivityResult Analyze(PirNode node)
    {
        int score = 0;
        List<string> reasons = [];

        string normalizedName = node.Name.Replace("_", "").Replace("-", "").ToLowerInvariant();

        if (SensitiveNames.Contains(normalizedName))
        {
            score += 3;
            reasons.Add("Name matches a sensitive identifier.");
        }

        if (
            node.Accessibility == PirAccessibility.Private
            || node.Accessibility == PirAccessibility.Protected
        )
        {
            score += 1;
            reasons.Add("Node has restricted accessibility.");
        }

        if ((node.Modifiers & PirModifier.Const) != 0)
        {
            score += 1;
            reasons.Add("Node is a constant.");
        }

        if (node.HasInitializer)
        {
            switch (node.InitializerKind)
            {
                case PirInitializerKind.StringLiteral:
                    score += 2;
                    reasons.Add("Node contains a string literal initializer.");
                    break;

                case PirInitializerKind.ObjectCreation:
                    score += 1;
                    reasons.Add("Node contains an object creation initializer.");
                    break;
            }
        }

        SensitivityLevel level = score switch
        {
            >= 6 => SensitivityLevel.Secret,
            >= 4 => SensitivityLevel.Sensitive,
            >= 2 => SensitivityLevel.Internal,
            _ => SensitivityLevel.Public,
        };

        return new SensitivityResult
        {
            Node = node,
            Level = level,
            Reasons = reasons,
        };
    }

    public SliceSensitivityResult AnalyzeSlice(ProgramSlice slice)
    {
        List<SensitivityResult> results = [];

        foreach (PirNode node in slice.Nodes)
        {
            SensitivityResult result = Analyze(node);
            results.Add(result);
        }

        return new SliceSensitivityResult
        {
            Slice = slice,
            Results = results
        };
    }
}
