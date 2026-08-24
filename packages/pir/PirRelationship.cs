using Aegis.Pir.Enums;

namespace Aegis.Pir;

public class PirRelationship
{
    public string SourceId { get; set; } = "";

    public string TargetId { get; set; } = "";

    public PirRelationshipType Type { get; set; }
}
