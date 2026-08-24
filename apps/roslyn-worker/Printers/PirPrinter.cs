using Aegis.Pir;

namespace RoslynWorker.Printers;

public static class PirPrinter
{
    public static void Print(PirPackage pirPackage)
    {
        foreach (PirNode node in pirPackage.Nodes)
        {
            // if (!string.IsNullOrWhiteSpace(node.DataType))
            // {
            //     Console.WriteLine($"{node.Type} : {node.Name} ({node.DataType})");
            // }
            // else
            // {
            //     Console.WriteLine($"{node.Type} : {node.Name}");
            // }

            // Console.WriteLine(
            //     $"{node.Type} : {node.Name} ({node.DataType}) [{node.Accessibility}]"
            // );

            // Console.WriteLine(
            //     $"{node.Type} : {node.Name} [{node.Accessibility}] [{node.Modifiers}]"
            // );

            Console.WriteLine(
                $"{node.Type} : {node.Name} "
                    + $"[{node.Accessibility}] "
                    + $"[{node.Modifiers}] "
                    + $"[Initializer: {node.HasInitializer}, "
                    + $"Kind: {node.InitializerKind}]"
            );
        }

        // Console.WriteLine();
        // Console.WriteLine("Relationships");

        Dictionary<string, PirNode> nodeLookup = pirPackage.Nodes.ToDictionary(
            node => node.Id,
            node => node
        );

        foreach (PirRelationship relationship in pirPackage.Relationships)
        {
            PirNode source = nodeLookup[relationship.SourceId];
            PirNode target = nodeLookup[relationship.TargetId];

            // Console.WriteLine(
            //     $"{source.Type}({source.Name}) --{relationship.Type}--> {target.Type}({target.Name})"
            // );
        }
    }
}
