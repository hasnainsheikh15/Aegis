namespace RoslynWorker.Models.Enums;

[Flags]
public enum PirModifier
{
    None = 0,
    Static = 1,
    Const = 2,
    Readonly = 4,
    Abstract = 8,
    Virtual = 16,
    Override = 32,
    Sealed = 64,
    Async = 128,
}
