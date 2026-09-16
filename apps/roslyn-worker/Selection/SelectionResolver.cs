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
        TextSpan selectionSpan = new(selection.Start, selection.Length);

        SyntaxNode root = syntaxTree.GetRoot();

        List<PirNode> selectedNodes = [];

        // First, find the smallest syntax node that contains
        // the complete selection.
        SyntaxNode? smallestNode = root.DescendantNodesAndSelf()
            .Where(node => node.Span.Contains(selectionSpan))
            .OrderBy(node => node.Span.Length)
            .FirstOrDefault();

        if (smallestNode is not null)
        {
            // If the selection represents a semantic reference,
            // resolve the referenced symbol first.
            ResolveSemanticNode(smallestNode, semanticModel, mapper, selectedNodes);

            if (selectedNodes.Count > 0)
            {
                return DistinctNodes(selectedNodes);
            }

            // If it wasn't a semantic reference, try it as a declaration.
            if (IsMeaningfulDeclaration(smallestNode))
            {
                ResolveDeclaration(smallestNode, mapper, selectedNodes);

                return DistinctNodes(selectedNodes);
            }
        }

        // Fallback for selections that span multiple syntax nodes.
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            if (!node.Span.IntersectsWith(selectionSpan))
                continue;

            ResolveSemanticNode(node, semanticModel, mapper, selectedNodes);
        }

        return DistinctNodes(selectedNodes);
    }

    private static void ResolveDeclaration(
        SyntaxNode declaration,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        /*
         * Fields are represented in PIR by their individual
         * VariableDeclaratorSyntax nodes.
         *
         * Therefore:
         *
         * FieldDeclarationSyntax
         *          ↓
         * VariableDeclaratorSyntax
         *          ↓
         * PIR Field
         */
        if (declaration is FieldDeclarationSyntax field)
        {
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                AddPirNode(variable, mapper, selectedNodes);
            }

            return;
        }

        /*
         * Local declarations are also represented by their
         * VariableDeclaratorSyntax nodes.
         */
        if (declaration is LocalDeclarationStatementSyntax local)
        {
            foreach (VariableDeclaratorSyntax variable in local.Declaration.Variables)
            {
                AddPirNode(variable, mapper, selectedNodes);
            }

            return;
        }

        /*
         * All other declaration types map directly to their
         * syntax declaration node.
         */
        AddPirNode(declaration, mapper, selectedNodes);
    }

    private static void ResolveSemanticNode(
        SyntaxNode node,
        SemanticModel semanticModel,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        /*
         * password
         * token
         * secret
         */
        if (node is IdentifierNameSyntax identifier)
        {
            ISymbol? symbol = semanticModel.GetSymbolInfo(identifier).Symbol;

            AddSymbol(symbol, mapper, selectedNodes);

            return;
        }

        /*
         * Validate()
         */
        if (node is InvocationExpressionSyntax invocation)
        {
            ISymbol? symbol = semanticModel.GetSymbolInfo(invocation).Symbol;

            AddSymbol(symbol, mapper, selectedNodes);

            return;
        }

        /*
         * new AuthService()
         */
        if (node is ObjectCreationExpressionSyntax objectCreation)
        {
            ISymbol? symbol = semanticModel.GetSymbolInfo(objectCreation).Symbol;

            AddSymbol(symbol, mapper, selectedNodes);
        }
    }

    private static void AddSymbol(
        ISymbol? symbol,
        RoslynToPirMapper mapper,
        List<PirNode> selectedNodes
    )
    {
        if (symbol is null)
        {
            return;
        }

        if (mapper.TryGetPirNode(symbol, out PirNode? pirNode) && pirNode is not null)
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
        if (mapper.TryGetPirNode(node, out PirNode? pirNode) && pirNode is not null)
        {
            selectedNodes.Add(pirNode);
        }
    }

    private static List<PirNode> DistinctNodes(List<PirNode> nodes)
    {
        return nodes.GroupBy(node => node.Id).Select(group => group.First()).ToList();
    }

    private static bool IsMeaningfulDeclaration(SyntaxNode node)
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
