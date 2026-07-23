using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynWorker.Models;
using RoslynWorker.Models.Enums;

namespace RoslynWorker.Mappers;

public class RoslynToPirMapper
{
    private readonly Dictionary<SyntaxNode, PirNode> nodeLookup = [];

    private readonly Dictionary<string, PirNode> symbolLookup = [];

    private SemanticModel? semanticModel;

    public PirPackage MapCompilationUnit(SyntaxNode root, SemanticModel semanticModel)
    {
        this.semanticModel = semanticModel;

        PirPackage pirPackage = new();

        visitDeclaration(root, pirPackage);

        visitBehaviour(root, pirPackage);

        return pirPackage;
    }

    // private void Visit(SyntaxNode node, PirPackage pirPackage)
    // {
    //     if (node is ClassDeclarationSyntax classNode)
    //     {
    //         Mapclass(classNode, pirPackage);
    //     }

    //     if (node is MethodDeclarationSyntax methodNode)
    //     {
    //         MapMethod(methodNode, pirPackage);
    //     }

    //     if (node is FileScopedNamespaceDeclarationSyntax namespaceNode)
    //     {
    //         MapNamespace(namespaceNode, pirPackage);
    //     }

    //     if (node is ConstructorDeclarationSyntax constructorNode)
    //     {
    //         MapConstructor(constructorNode, pirPackage);
    //     }

    //     if (node is ParameterSyntax parameterNode)
    //     {
    //         MapParameter(parameterNode, pirPackage);
    //     }

    //     if (node is PropertyDeclarationSyntax propertyNode)
    //     {
    //         MapProperty(propertyNode, pirPackage);
    //     }

    //     if (node is FieldDeclarationSyntax fieldNode)
    //     {
    //         MapField(fieldNode, pirPackage);
    //     }

    //     if (node is InvocationExpressionSyntax invocationNode)
    //     {
    //         MapInvocation(invocationNode, pirPackage);
    //     }

    //     foreach (var child in node.ChildNodes())
    //     {
    //         Visit(child, pirPackage);
    //     }
    // }

    private void visitDeclaration(SyntaxNode node, PirPackage pirPackage)
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

        if (node is InterfaceDeclarationSyntax interfaceNode)
        {
            MapInterface(interfaceNode, pirPackage);
        }

        foreach (var child in node.ChildNodes())
        {
            visitDeclaration(child, pirPackage);
        }
    }

    private void visitBehaviour(SyntaxNode node, PirPackage pirPackage)
    {
        if (node is InvocationExpressionSyntax invocationNode)
        {
            MapInvocation(invocationNode, pirPackage);
        }

        if (node is ClassDeclarationSyntax classNode)
        {
            MapInheritance(classNode, pirPackage);
        }

        if (node is ClassDeclarationSyntax interfaceNode)
        {
            MapImplements(interfaceNode, pirPackage);
        }

        if (node is ObjectCreationExpressionSyntax objectCreationNode)
        {
            MapObjectCreation(objectCreationNode, pirPackage);
        }

        if (node is IdentifierNameSyntax identifierNode)
        {
            MapRead(identifierNode, pirPackage);
        }

        if (node is MemberAccessExpressionSyntax memberAccessNode)
        {
            MapRead(memberAccessNode, pirPackage);
        }

        if (node is AssignmentExpressionSyntax assignmentNode)
        {
            MapWrite(assignmentNode, pirPackage);
        }

        // if (node is AssignmentExpressionSyntax assignment)
        // {
        //     Console.WriteLine(assignment.Kind());
        // }

        if (node is PrefixUnaryExpressionSyntax prefix)
        {
            MapUnaryMutation(prefix, pirPackage);
        }

        if (node is PostfixUnaryExpressionSyntax postfix)
        {
            MapUnaryMutation(postfix, pirPackage);
        }

        foreach (var child in node.ChildNodes())
        {
            visitBehaviour(child, pirPackage);
        }
    }

    private PirNode CreateNode(
        PirPackage pirPackage,
        SyntaxNode syntaxNode,
        PirNodeType type,
        string name,
        string? dataType = null
    )
    {
        PirNode pirNode = new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Name = name,
            DataType = dataType,
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
                Type = type,
            }
        );
    }

    private void Mapclass(ClassDeclarationSyntax classNode, PirPackage pirPackage)
    {
        PirNode pirClass = CreateNode(
            pirPackage,
            classNode,
            PirNodeType.Class,
            classNode.Identifier.Text
        );

        INamedTypeSymbol? classSymbol = semanticModel?.GetDeclaredSymbol(classNode);

        string? symbolId = classSymbol?.GetDocumentationCommentId();

        if (symbolId is not null)
        {
            symbolLookup[symbolId] = pirClass;
        }

        SyntaxNode? parent = classNode.Parent;

        if (parent is FileScopedNamespaceDeclarationSyntax parentNamespace)
        {
            var parentPirNode = nodeLookup[parentNamespace];

            CreateRelationship(pirPackage, parentPirNode, pirClass, PirRelationshipType.CONTAINS);
        }
    }

    private void MapMethod(MethodDeclarationSyntax methodNode, PirPackage pirPackage)
    {
        PirNode pirMethod = CreateNode(
            pirPackage,
            methodNode,
            PirNodeType.Method,
            methodNode.Identifier.Text,
            methodNode.ReturnType.ToString()
        );

        IMethodSymbol? methodSymbol = semanticModel?.GetDeclaredSymbol(methodNode);

        string? symbolId = methodSymbol?.GetDocumentationCommentId();

        if (symbolId is not null)
        {
            symbolLookup[symbolId] = pirMethod;
        }

        SyntaxNode? parent = methodNode.Parent;

        if (parent is ClassDeclarationSyntax parentClass)
        {
            var parentPirNode = nodeLookup[parentClass];

            CreateRelationship(pirPackage, parentPirNode, pirMethod, PirRelationshipType.DECLARES);
        }

        if (parent is InterfaceDeclarationSyntax interfaceNode)
        {
            PirNode parentNode = nodeLookup[interfaceNode];

            CreateRelationship(pirPackage, parentNode, pirMethod, PirRelationshipType.DECLARES);
        }
    }

    private void MapNamespace(
        FileScopedNamespaceDeclarationSyntax namespaceNode,
        PirPackage pirPackage
    )
    {
        PirNode pirNamespace = CreateNode(
            pirPackage,
            namespaceNode,
            PirNodeType.Namespace,
            namespaceNode.Name.ToString()
        );
    }

    private void MapConstructor(ConstructorDeclarationSyntax constructorNode, PirPackage pirPackage)
    {
        PirNode pirConstructor = CreateNode(
            pirPackage,
            constructorNode,
            PirNodeType.Constructor,
            constructorNode.Identifier.Text
        );

        SyntaxNode? parent = constructorNode.Parent;

        if (parent is ClassDeclarationSyntax parentClass)
        {
            var parentPirNode = nodeLookup[parentClass];

            CreateRelationship(
                pirPackage,
                parentPirNode,
                pirConstructor,
                PirRelationshipType.DECLARES
            );
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

            CreateRelationship(pirPackage, parentNode, pirNode, PirRelationshipType.DECLARES);
        }

        if (owner is ConstructorDeclarationSyntax constructorNode)
        {
            PirNode parentNode = nodeLookup[constructorNode];

            CreateRelationship(pirPackage, parentNode, pirNode, PirRelationshipType.DECLARES);
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

        IPropertySymbol? propertySymbol = semanticModel?.GetDeclaredSymbol(propertyNode);

        if (propertySymbol is not null)
        {
            string? id = propertySymbol.GetDocumentationCommentId();

            if (id is not null)
            {
                symbolLookup[id] = pirProperty;
            }
        }
        SyntaxNode? parent = propertyNode.Parent;

        if (parent is ClassDeclarationSyntax parentClass)
        {
            var parentPirNode = nodeLookup[parentClass];

            CreateRelationship(
                pirPackage,
                parentPirNode,
                pirProperty,
                PirRelationshipType.DECLARES
            );
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

            IFieldSymbol? fieldSymbol = semanticModel?.GetDeclaredSymbol(variable) as IFieldSymbol;

            if (fieldSymbol is not null)
            {
                string? id = fieldSymbol.GetDocumentationCommentId();

                if (id is not null)
                {
                    symbolLookup[id] = pirField;
                }
            }

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

    private PirNode? FindPirNode(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        string? symbolId = symbol.GetDocumentationCommentId();

        if (symbolId is null)
        {
            return null;
        }

        symbolLookup.TryGetValue(symbolId, out PirNode? pirNode);

        return pirNode;
    }

    private void MapInterface(InterfaceDeclarationSyntax interfaceNode, PirPackage pirPackage)
    {
        PirNode pirInterface = CreateNode(
            pirPackage,
            interfaceNode,
            PirNodeType.Interface,
            interfaceNode.Identifier.Text
        );

        INamedTypeSymbol? interfaceSymbol = semanticModel?.GetDeclaredSymbol(interfaceNode);

        string? symbolId = interfaceSymbol?.GetDocumentationCommentId();

        if (symbolId is not null)
        {
            symbolLookup[symbolId] = pirInterface;
        }

        SyntaxNode? parent = interfaceNode.Parent;

        if (parent is FileScopedNamespaceDeclarationSyntax parentNamespace)
        {
            var parentPirNode = nodeLookup[parentNamespace];

            CreateRelationship(
                pirPackage,
                parentPirNode,
                pirInterface,
                PirRelationshipType.CONTAINS
            );
        }
    }

    private void MapInvocation(InvocationExpressionSyntax invocationNode, PirPackage pirPackage)
    {
        MethodDeclarationSyntax? callerMethod =
            invocationNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        if (callerMethod is null)
            return;

        IMethodSymbol? callerSymbol = semanticModel?.GetDeclaredSymbol(callerMethod);

        PirNode? callerNode = FindPirNode(callerSymbol);

        IMethodSymbol? calledMethodSymbol =
            semanticModel?.GetSymbolInfo(invocationNode).Symbol as IMethodSymbol;

        PirNode? calledNode = FindPirNode(calledMethodSymbol);

        if (callerNode is not null && calledNode is not null)
        {
            CreateRelationship(pirPackage, callerNode, calledNode, PirRelationshipType.CALLS);
        }
    }

    private void MapInheritance(ClassDeclarationSyntax classNode, PirPackage pirPackage)
    {
        INamedTypeSymbol? classSymbol = semanticModel?.GetDeclaredSymbol(classNode);

        if (classSymbol is null)
        {
            return;
        }

        INamedTypeSymbol? baseType = classSymbol.BaseType;

        PirNode? childNode = FindPirNode(classSymbol);

        PirNode? parentNode = FindPirNode(baseType);

        if (childNode is not null && parentNode is not null)
        {
            CreateRelationship(pirPackage, childNode, parentNode, PirRelationshipType.INHERITS);
        }
    }

    private void MapImplements(ClassDeclarationSyntax classNode, PirPackage pirPackage)
    {
        INamedTypeSymbol? classSymbol = semanticModel?.GetDeclaredSymbol(classNode);

        if (classSymbol is null)
        {
            return;
        }

        PirNode? childNode = FindPirNode(classSymbol);

        foreach (INamedTypeSymbol interfaceSymbol in classSymbol.AllInterfaces)
        {
            PirNode? parentNode = FindPirNode(interfaceSymbol);

            if (childNode is not null && parentNode is not null)
            {
                CreateRelationship(
                    pirPackage,
                    childNode,
                    parentNode,
                    PirRelationshipType.IMPLEMENTS
                );
            }
        }
    }

    private void MapObjectCreation(
        ObjectCreationExpressionSyntax objectCreationNode,
        PirPackage pirPackage
    )
    {
        MethodDeclarationSyntax? callerMethod =
            objectCreationNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        if (callerMethod is null)
        {
            return;
        }

        IMethodSymbol? callerSymbol = semanticModel?.GetDeclaredSymbol(callerMethod);

        PirNode? callerNode = FindPirNode(callerSymbol);

        IMethodSymbol? constructorSymbol =
            semanticModel?.GetSymbolInfo(objectCreationNode).Symbol as IMethodSymbol;

        if (constructorSymbol is null)
        {
            return;
        }

        INamedTypeSymbol classSymbol = constructorSymbol.ContainingType;

        PirNode? createdClassNode = FindPirNode(classSymbol);

        if (callerNode is not null && createdClassNode is not null)
        {
            CreateRelationship(
                pirPackage,
                callerNode,
                createdClassNode,
                PirRelationshipType.CREATES
            );
        }
    }

    private void MapRead(ExpressionSyntax expression, PirPackage pirPackage)
    {
        if (expression is IdentifierNameSyntax identifier)
        {
            if (identifier.Parent is MemberAccessExpressionSyntax)
            {
                return;
            }

            if (
                identifier.Parent is PrefixUnaryExpressionSyntax
                || identifier.Parent is PostfixUnaryExpressionSyntax
            )
            {
                return;
            }
        }
        if (
            expression.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == expression
        )
        {
            return;
        }

        // Find the enclosing method
        PirNode? callerNode = FindEnclosingMethodNode(expression);

        if (callerNode is null)
        {
            return;
        }
        // Resolve what this identifier refers to
        SymbolInfo symbolInfo = semanticModel!.GetSymbolInfo(expression);

        ISymbol? symbol = symbolInfo.Symbol;

        // We only care about fields for now
        if (symbol is not IFieldSymbol && symbol is not IPropertySymbol)
        {
            return;
        }

        PirNode? fieldNode = FindPirNode(symbol);

        if (callerNode is not null && fieldNode is not null)
        {
            CreateRelationship(pirPackage, callerNode, fieldNode, PirRelationshipType.READS);
        }
    }

    private void MapWrite(AssignmentExpressionSyntax assignmentNode, PirPackage pirPackage)
    {
        // Find the enclosing method
        PirNode? callerNode = FindEnclosingMethodNode(assignmentNode);

        if (callerNode is null)
        {
            return;
        }

        // Resolve the symbol being assigned to
        ISymbol? symbol = semanticModel?.GetSymbolInfo(assignmentNode.Left).Symbol;

        if (symbol is not IFieldSymbol && symbol is not IPropertySymbol)
        {
            return;
        }

        PirNode? memberNode = FindPirNode(symbol);
        if (memberNode is null)
            return;

        bool isCompoundAssignment = assignmentNode.Kind() != SyntaxKind.SimpleAssignmentExpression;

        if (isCompoundAssignment)
        {
            CreateRelationship(pirPackage, callerNode, memberNode, PirRelationshipType.READS);
        }

        if (memberNode is null)
        {
            return;
        }

        CreateRelationship(pirPackage, callerNode, memberNode, PirRelationshipType.WRITES);
    }

    private PirNode? FindEnclosingMethodNode(SyntaxNode node)
    {
        MethodDeclarationSyntax? method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        if (method is null)
            return null;

        IMethodSymbol? symbol = semanticModel?.GetDeclaredSymbol(method);

        return FindPirNode(symbol);
    }

    private void MapUnaryMutation(ExpressionSyntax expression, PirPackage pirPackage)
    {
        if (
            expression.Kind() != SyntaxKind.PostIncrementExpression
            && expression.Kind() != SyntaxKind.PreIncrementExpression
            && expression.Kind() != SyntaxKind.PostDecrementExpression
            && expression.Kind() != SyntaxKind.PreDecrementExpression
        )
        {
            return;
        }

        PirNode? callerNode = FindEnclosingMethodNode(expression);

        if (callerNode is null)
        {
            return;
        }

        ExpressionSyntax operand = expression switch
        {
            PrefixUnaryExpressionSyntax prefix => prefix.Operand,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand,
            _ => throw new InvalidOperationException(),
        };
        ISymbol? symbol = semanticModel?.GetSymbolInfo(operand).Symbol;

        if (symbol is not IFieldSymbol && symbol is not IPropertySymbol)
        {
            return;
        }

        PirNode? memberNode = FindPirNode(symbol);

        if (memberNode is null)
        {
            return;
        }

        CreateRelationship(pirPackage, callerNode, memberNode, PirRelationshipType.READS);

        CreateRelationship(pirPackage, callerNode, memberNode, PirRelationshipType.WRITES);
    }
}
