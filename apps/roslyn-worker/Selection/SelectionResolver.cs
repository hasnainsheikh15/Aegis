using Aegis.Pir;
using Aegis.Sanitizer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RoslynWorker.Mappers;

namespace RoslynWorker.Selection;

public sealed class SelectionResolver
{
    public List<PirNode> Resolve(
    SyntaxTree syntaxTree,
    SemanticModel semanticModel,
    SourceSelection selection,
    RoslynToPirMapper mapper
)
{
    TextSpan selectionSpan = new(
        selection.Start,
        selection.Length
    );

    SyntaxNode root = syntaxTree.GetRoot();

    List<PirNode> selectedNodes = [];

    /*
     * ------------------------------------------------------------
     * 1. Exact/partial local declaration selection
     * ------------------------------------------------------------
     *
     * Example:
     *
     *     string token = password;
     *
     * If the user's selection lies inside this declaration,
     * resolve the individual VariableDeclaratorSyntax nodes.
     *
     * We do this before generic semantic resolution because the
     * declaration itself is what the user selected.
     */
    LocalDeclarationStatementSyntax? localDeclaration =
        root.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .Where(node => node.Span.IntersectsWith(selectionSpan))
            .OrderBy(node => node.Span.Length)
            .FirstOrDefault();

    if (localDeclaration is not null)
    {
        foreach (
            VariableDeclaratorSyntax variable
                in localDeclaration.Declaration.Variables
        )
        {
            if (!variable.Span.IntersectsWith(selectionSpan))
                continue;

            AddPirNode(
                variable,
                mapper,
                selectedNodes
            );
        }

        if (selectedNodes.Count > 0)
        {
            return DistinctNodes(selectedNodes);
        }
    }

    /*
     * ------------------------------------------------------------
     * 2. Other meaningful declarations
     * ------------------------------------------------------------
     *
     * Fields, methods, constructors, properties, classes, etc.
     */
    SyntaxNode? declaration = root
        .DescendantNodesAndSelf()
        .Where(node =>
            IsMeaningfulDeclaration(node)
            && node is not LocalDeclarationStatementSyntax
            && node.Span.Contains(selectionSpan)
        )
        .OrderBy(node => node.Span.Length)
        .FirstOrDefault();

    if (declaration is not null)
    {
        ResolveDeclaration(
            declaration,
            mapper,
            selectedNodes
        );

        if (selectedNodes.Count > 0)
        {
            return DistinctNodes(selectedNodes);
        }
    }

    /*
     * ------------------------------------------------------------
     * 3. Semantic selection
     * ------------------------------------------------------------
     *
     * Used for things such as:
     *
     *     password
     *     Validate();
     *     SomeClass(...)
     */
    SyntaxNode? smallestNode = root
        .DescendantNodesAndSelf()
        .Where(node => node.Span.Contains(selectionSpan))
        .OrderBy(node => node.Span.Length)
        .FirstOrDefault();

    if (smallestNode is not null)
    {
        ResolveSemanticNode(
            smallestNode,
            semanticModel,
            mapper,
            selectedNodes
        );

        if (selectedNodes.Count > 0)
        {
            return DistinctNodes(selectedNodes);
        }
    }

    /*
     * ------------------------------------------------------------
     * 4. Partial semantic selection fallback
     * ------------------------------------------------------------
     */
    foreach (SyntaxNode node in root.DescendantNodes())
    {
        if (!node.Span.IntersectsWith(selectionSpan))
            continue;

        ResolveSemanticNode(
            node,
            semanticModel,
            mapper,
            selectedNodes
        );
    }

    return DistinctNodes(selectedNodes);
}

    private static void ResolveDeclaration(
        SyntaxNode declaration,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        if (declaration is FieldDeclarationSyntax field)
        {
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                AddPirNode(
                    variable,
                    mapper,
                    selectedNodes
                );
            }

            return;
        }

        if (declaration is LocalDeclarationStatementSyntax local)
        {
            foreach (VariableDeclaratorSyntax variable in local.Declaration.Variables)
            {
                AddPirNode(
                    variable,
                    mapper,
                    selectedNodes
                );
            }

            return;
        }

        AddPirNode(
            declaration,
            mapper,
            selectedNodes
        );
    }

    private static void ResolveSemanticNode(
        SyntaxNode node,
        SemanticModel semanticModel,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        if (node is IdentifierNameSyntax identifier)
        {
            ISymbol? symbol =
                semanticModel
                    .GetSymbolInfo(identifier)
                    .Symbol;

            AddSymbol(
                symbol,
                mapper,
                selectedNodes
            );

            return;
        }

        if (node is InvocationExpressionSyntax invocation)
        {
            ISymbol? symbol =
                semanticModel
                    .GetSymbolInfo(invocation)
                    .Symbol;

            AddSymbol(
                symbol,
                mapper,
                selectedNodes
            );

            return;
        }

        if (node is ObjectCreationExpressionSyntax objectCreation)
        {
            ISymbol? symbol =
                semanticModel
                    .GetSymbolInfo(objectCreation)
                    .Symbol;

            AddSymbol(
                symbol,
                mapper,
                selectedNodes
            );
        }
    }

    private static void AddSymbol(
        ISymbol? symbol,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        if (symbol is null)
            return;

        if (
            mapper.TryGetPirNode(
                symbol,
                out PirNode? pirNode
            )
            && pirNode is not null
        )
        {
            selectedNodes.Add(pirNode);
        }
    }

    private static void AddPirNode(
        SyntaxNode node,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        if (
            mapper.TryGetPirNode(
                node,
                out PirNode? pirNode
            )
            && pirNode is not null
        )
        {
            selectedNodes.Add(pirNode);
        }
    }

    private static List<PirNode> DistinctNodes(
        List<PirNode> nodes
    )
    {
        return nodes
            .GroupBy(node => node.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsMeaningfulDeclaration(
        SyntaxNode node
    )
    {
        return node switch
        {
            NamespaceDeclarationSyntax => true,
            FileScopedNamespaceDeclarationSyntax => true,
            ClassDeclarationSyntax => true,
            InterfaceDeclarationSyntax => true,
            MethodDeclarationSyntax => true,
            ConstructorDeclarationSyntax => true,
            PropertyDeclarationSyntax => true,
            FieldDeclarationSyntax => true,
            LocalDeclarationStatementSyntax => true,
            _ => false,
        };
    }
}