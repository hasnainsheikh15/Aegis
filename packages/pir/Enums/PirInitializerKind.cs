namespace Aegis.Pir.Enums;

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