using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Models;
using RoslynWorker.Models.Enums;


namespace RoslynWorker.Mappers;

public class RoslynToPirMapper
{
    private readonly Dictionary<SyntaxNode, PirNode> nodeLookup = [];

    private SemanticModel? semanticModel;

    public PirPackage MapCompilationUnit(SyntaxNode root, SemanticModel semanticModel)
    {
        this.semanticModel = semanticModel;

        PirPackage pirPackage = new();

        Visit(root, pirPackage);

        return pirPackage;
    }

    private void Visit(SyntaxNode node, PirPackage pirPackage)
    {
        if (node is ClassDeclarationSyntax classNode)
        {
            Mapclass(classNode, pirPackage);
        }

        if (node is MethodDeclarationSyntax methodNode)
        {
            MapMethod(methodNode, pirPackage);
        }

        if (node is FileScopedNamespaceDeclarationSyntax namespaceNode)
        {
            MapNamespace(namespaceNode, pirPackage);
        }

        if (node is ConstructorDeclarationSyntax constructorNode)
        {
            MapConstructor(constructorNode, pirPackage);
        }

        if (node is ParameterSyntax parameterNode)
        {
            MapParameter(parameterNode, pirPackage);
        }

        if (node is PropertyDeclarationSyntax propertyNode)
        {
            MapProperty(propertyNode, pirPackage);
        }

        if (node is FieldDeclarationSyntax fieldNode)
        {
            MapField(fieldNode, pirPackage);
        }

        if (node is InvocationExpressionSyntax invocationNode)
        {
            MapInvocation(invocationNode, pirPackage);
        }

        foreach (var child in node.ChildNodes())
        {
            Visit(child, pirPackage);
        }
    }

    private PirNode CreateNode(PirPackage pirPackage, SyntaxNode syntaxNode, PirNodeType type, string name,
        string? dataType = null)
    {
        PirNode pirNode = new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Name = name,
            DataType = dataType
        };

        pirPackage.Nodes.Add(pirNode);
        nodeLookup[syntaxNode] = pirNode;

        return pirNode;
    }


    private void CreateRelationship(
        PirPackage pirPackage,
        PirNode source,
        PirNode target,
        PirRelationshipType type
    )
    {
        pirPackage.Relationships.Add(
            new PirRelationship
            {
                SourceId = source.Id,
                TargetId = target.Id,
                Type = type
            });
    }

    private void Mapclass(ClassDeclarationSyntax classNode, PirPackage pirPackage)
    {
        PirNode pirClass = CreateNode(pirPackage,
            classNode,
            PirNodeType.Class,
            classNode.Identifier.Text);

        SyntaxNode? parent = classNode.Parent;

        if (parent is FileScopedNamespaceDeclarationSyntax parentNamespace)
        {
            var parentPirNode = nodeLookup[parentNamespace];

            CreateRelationship(
                pirPackage,
                parentPirNode,
                pirClass,
                PirRelationshipType.CONTAINS);
        }
    }

    private void MapMethod(MethodDeclarationSyntax methodNode, PirPackage pirPackage)
    {
        PirNode pirNode = CreateNode(
            pirPackage,
            methodNode,
            PirNodeType.Method,
            methodNode.Identifier.Text,
            methodNode.ReturnType.ToString()
        );

        SyntaxNode? parent = methodNode.Parent;


        if (parent is ClassDeclarationSyntax parentClass)
        {
            var parentPirNode = nodeLookup[parentClass];

            CreateRelationship(
                pirPackage,
                parentPirNode,
                pirNode,
                PirRelationshipType.DECLARES
            );
        }
    }

    private void MapNamespace(FileScopedNamespaceDeclarationSyntax namespaceNode, PirPackage pirPackage)
    {
        PirNode pirNamespace = CreateNode(pirPackage,
            namespaceNode,
            PirNodeType.Namespace,
            namespaceNode.Name.ToString()
        );
    }

    private void MapConstructor(ConstructorDeclarationSyntax constructorNode, PirPackage pirPackage)
    {
        PirNode pirConstructor = CreateNode(pirPackage,
            constructorNode,
            PirNodeType.Constructor,
            constructorNode.Identifier.Text);

        SyntaxNode? parent = constructorNode.Parent;

        if (parent is ClassDeclarationSyntax parentClass)
        {
            var parentPirNode = nodeLookup[parentClass];

            CreateRelationship(pirPackage,
                parentPirNode,
                pirConstructor,
                PirRelationshipType.DECLARES);
        }
    }

    private void MapParameter(ParameterSyntax parameterNode, PirPackage pirPackage)
    {
        PirNode pirNode = CreateNode(
            pirPackage,
            parameterNode,
            PirNodeType.Parameter,
            parameterNode.Identifier.Text,
            parameterNode.Type?.ToString()
        );

        SyntaxNode? owner = parameterNode.Parent?.Parent;

        if (owner is MethodDeclarationSyntax methodNode)
        {
            PirNode parentNode = nodeLookup[methodNode];

            CreateRelationship(pirPackage,
                parentNode,
                pirNode,
                PirRelationshipType.DECLARES);
        }

        if (owner is ConstructorDeclarationSyntax constructorNode)
        {
            PirNode parentNode = nodeLookup[constructorNode];

            CreateRelationship(pirPackage,
                parentNode,
                pirNode,
                PirRelationshipType.DECLARES);
        }
    }

    private void MapProperty(PropertyDeclarationSyntax propertyNode, PirPackage pirPackage)
    {
        PirNode pirProperty = CreateNode(
            pirPackage,
            propertyNode,
            PirNodeType.Property,
            propertyNode.Identifier.Text,
            propertyNode.Type.ToString()
        );

        SyntaxNode? parent = propertyNode.Parent;

        if (parent is ClassDeclarationSyntax parentClass)
        {
            var parentPirNode = nodeLookup[parentClass];

            CreateRelationship(pirPackage,
                parentPirNode,
                pirProperty,
                PirRelationshipType.DECLARES);
        }
    }

    private void MapField(FieldDeclarationSyntax fieldNode, PirPackage pirPackage)
    {
        foreach (VariableDeclaratorSyntax variable in fieldNode.Declaration.Variables)
        {
            PirNode pirField = CreateNode(
                pirPackage,
                variable,
                PirNodeType.Field,
                variable.Identifier.Text,
                fieldNode.Declaration.Type.ToString()
            );

            SyntaxNode? parent = fieldNode.Parent;

            if (parent is ClassDeclarationSyntax classNode)
            {
                PirNode parentPirNode = nodeLookup[classNode];

                CreateRelationship(
                    pirPackage,
                    parentPirNode,
                    pirField,
                    PirRelationshipType.DECLARES
                );
            }
        }
    }

    private void MapInvocation(InvocationExpressionSyntax invocationNode, PirPackage pirPackage)
    {
        MethodDeclarationSyntax? callerMethod =
            invocationNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        
        IMethodSymbol? calledMethodSymbol =
            semanticModel?
                .GetSymbolInfo(invocationNode)
                .Symbol as IMethodSymbol;
        
        if (calledMethodSymbol is null)
        {
            Console.WriteLine("Could not resolve invocation.");
        }
        else
        {
            Console.WriteLine(
                $"{calledMethodSymbol.ContainingType.Name}.{calledMethodSymbol.Name}"
            );
        }
    }
}
