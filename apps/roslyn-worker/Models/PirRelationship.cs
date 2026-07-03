using RoslynWorker.Models.Enums;

namespace RoslynWorker.Models;

public class PirRelationship {
    public string SourceId {get; set;} = "";

    // public string SourceName { get; set; } = "";

    public string TargetId {get; set;} = "";

    // public string TargetName { get; set; } = "";
    

    public PirRelationshipType Type {get; set;}

}