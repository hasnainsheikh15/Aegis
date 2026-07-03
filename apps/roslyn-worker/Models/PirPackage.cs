namespace RoslynWorker.Models;

public class PirPackage {
    public List<PirNode> Nodes {get; set;} = [];
    public List<PirRelationship> Relationships {get; set;} = [];
}