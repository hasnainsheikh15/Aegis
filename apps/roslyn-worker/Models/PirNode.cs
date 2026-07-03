using RoslynWorker.Models.Enums;

namespace RoslynWorker.Models;

public class PirNode {
    public string Id {get ; set ; } = "";
    public PirNodeType Type {get; set;}
    public string Name {get; set;} = "";
    public string? DataType { get; set; }
}
