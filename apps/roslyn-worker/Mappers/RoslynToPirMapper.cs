using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynWorker.Models;

namespace RoslynWorker.Mappers;

public class RoslynToPirMapper {

    public PirPackage MapCompilationUnit(SyntaxNode root)
     {
        PirPackage pirPackage = new();

        Visit(root,pirPackage);
        
        return pirPackage;

     }

     private void Visit(SyntaxNode node , PirPackage pirPackage) {

        if(node is ClassDeclarationSyntax classNode) {
            pirPackage.Nodes.Add(
                new PirNode
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "Class",
                        Name = classNode.Identifier.Text
                    }
                
            );
        }

        foreach(var child in node.ChildNodes()) {
            Visit(child,pirPackage);
        }
     }
}
