using RoslynWorker.Models;
using RoslynWorker.Models.Enums;

public sealed class ProgramSlice {
    
    public List<PirNode> Nodes {get; init;} = [];

    public List<PirRelationship> Relationships {get; init;} = [];
}