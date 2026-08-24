using Aegis.Pir.Enums;

namespace Aegis.Pir;

public class PirNode
{
    public string Id { get; set; } = "";

    public PirNodeType Type { get; set; }

    public string Name { get; set; } = "";

    public string? DataType { get; set; }

    public PirAccessibility Accessibility { get; set; }

    public PirModifier Modifiers { get; set; } = PirModifier.None;

    public bool HasInitializer { get; set; }

    public PirInitializerKind InitializerKind { get; set; } = PirInitializerKind.None;
}
