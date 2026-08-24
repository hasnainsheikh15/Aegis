namespace Aegis.Graph;
using Aegis.Pir;
using Aegis.Pir.Enums;

public sealed class ProgramSlice {
    
    public List<PirNode> Nodes {get; init;} = [];

    public List<PirRelationship> Relationships {get; init;} = [];
}