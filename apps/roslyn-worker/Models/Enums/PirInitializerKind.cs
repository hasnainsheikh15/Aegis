namespace RoslynWorker.Models.Enums;

public enum PirInitializerKind
{
    None,
    StringLiteral,
    NumericLiteral,
    BooleanLiteral,
    NullLiteral,
    ObjectCreation,
    MethodCall,
    Other
}