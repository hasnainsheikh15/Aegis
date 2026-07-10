# Roslyn Worker Architecture

## Two-pass mapping

Pass 1
- Build declaration nodes
- Populate nodeLookup
- Populate symbolLookup

Pass 2
- Build semantic relationships
- CALLS
- (future) INHERITS
- (future) IMPLEMENTS
- (future) READS
- (future) WRITES

### Interface Relationships

Aegis records semantic interface implementation using Roslyn's
`INamedTypeSymbol.AllInterfaces`.

This means the graph contains both directly implemented interfaces
and interfaces inherited through base classes or interface inheritance.

The graph favors semantic completeness over mirroring the exact source syntax.