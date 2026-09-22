# Technical Debt

This document tracks architectural improvements that have been intentionally postponed.

These items are **not current bugs** and are **not required to complete the current alpha**.

The goal is to keep the alpha focused while recording decisions that should be revisited as Aegis grows.

---

## 1. Roslyn Project Loading

### Current

The Roslyn worker manually creates a `CSharpCompilation` and supplies a limited set of metadata references.

This is sufficient for the current alpha fixtures and analysis pipeline.

### Future

Move toward project-aware loading through MSBuild/Roslyn workspace infrastructure.

This would allow Aegis to understand:

- Project references
- NuGet dependencies
- Target frameworks
- Global usings
- Nullable configuration
- Language version
- Compilation options
- Analyzer configuration

### Why Deferred

The current alpha is proving the sanitization and reverse-mapping workflow.

Full project loading would significantly expand the scope without being required to validate that workflow.

**Priority:** `High` — after alpha

---

## 2. Roslyn Mapper State

### Current

`RoslynToPirMapper` maintains several internal lookup structures, including:

```
SemanticModel
nodeLookup
symbolLookup
localSymbolLookup
```

This works for the current mapper.

### Future

Introduce a dedicated mapping context if the mapper continues to accumulate state.

Possible structure:

```
MappingContext
    │
    ├── SemanticModel
    ├── NodeLookup
    ├── SymbolLookup
    ├── LocalSymbolLookup
    ├── Diagnostics
    └── Future analysis state
```

### Benefits

- Cleaner mapper API
- Easier testing
- More explicit state ownership
- Reduced shared mutable state

**Priority:** `Medium`

---

## 3. Project-Level PIR Construction

### Current

Individual source files can produce temporary PIR packages that are later combined into the project-level representation.

### Future

Refactor the mapper so that project analysis writes directly into a shared `PirPackage`.

Conceptually:

```csharp
MapCompilationUnit(
    CompilationUnitSyntax root,
    SemanticModel semanticModel,
    PirPackage pirPackage
)
```

### Benefits

- Fewer temporary allocations
- Simpler project-level mapping
- Clearer ownership of the PIR graph
- Better alignment with project-wide analysis

**Priority:** `Medium`

---

## 4. Relationship Creation Helper

### Current

Relationship creation logic is distributed across multiple mapping methods.

Examples include: `MapClass()`, `MapMethod()`, `MapConstructor()`, `MapParameter()`, `MapProperty()`, `MapField()`.

### Future

Introduce a centralized helper:

```csharp
CreateRelationship(
    PirPackage pirPackage,
    PirNode source,
    PirNode target,
    PirRelationshipType type
)
```

### Benefits

- Less duplicated logic
- Centralized relationship creation
- Easier validation
- Simpler future relationship types

**Priority:** `Medium`

---

## 5. Parent Resolution

### Current

Several mapping operations manually locate semantic parents.

Examples:

| Child | Parent |
|---|---|
| Class | Namespace |
| Method | Class |
| Constructor | Class |
| Property | Class |
| Parameter | Method / Constructor |

### Future

Introduce reusable helpers for semantic parent resolution.

### Benefits

- Less duplicated traversal
- More consistent ownership resolution
- Easier mapper maintenance

**Priority:** `Low / Medium`

---

## 6. PIR Type Representation

### Current

Type information is stored as metadata on PIR nodes.

Example:

```
Method
  ReturnType = "User"
```

### Future

Represent important types as first-class PIR nodes.

Conceptually:

```
Method
   │
   │ RETURNS
   ▼
Type(User)
```

### Benefits

- Richer dependency graph
- Better type analysis
- More expressive cross-language representation
- Better foundation for semantic transformations

**Priority:** `Medium`

---

## 7. Formal PIR Specification

### Current

The PIR model exists in code but is not yet formalized as a standalone specification.

### Future

Create: `docs/pir-spec.md`

The specification should define:

**Node Types:**

| Node Type |
|---|
| `Namespace` |
| `Class` |
| `Interface` |
| `Constructor` |
| `Method` |
| `Parameter` |
| `Property` |
| `Field` |
| `LocalVariable` |
| `Type` |

**Relationship Types:**

| Relationship |
|---|
| `CONTAINS` |
| `DECLARES` |
| `CALLS` |
| `IMPLEMENTS` |
| `INHERITS` |
| `CREATES` |
| `READS` |
| `WRITES` |
| `FLOWS_TO` |

> The specification should define the semantic meaning and invariants of each node and relationship.

**Priority:** `Medium`

---

## 8. Relationship Deduplication

### Current

The exact representation of repeated relationships has not been formally defined.

Example:

```csharp
Validate();
Validate();
```

Possible representations include:

**Option A** — separate relationships:

```
Login ── CALLS ──▶ Validate
Login ── CALLS ──▶ Validate
```

**Option B** — single relationship with occurrence data:

```
Login ── CALLS(count = 2) ──▶ Validate
```

### Future

Define the graph's relationship multiplicity semantics.

> This becomes important as the graph is used for deeper analysis.

**Priority:** `Medium`

---

## 9. Unified Symbol-to-PIR Lookup

### Current

Different symbol categories use different lookup mechanisms.

```
symbolLookup
    │
    ├── Classes
    ├── Methods
    ├── Fields
    ├── Properties
    └── Parameters

localSymbolLookup
    │
    └── LocalVariables
```

### Future

Investigate whether symbol-to-PIR resolution can be unified without losing the special handling required for local symbols.

### Benefits

- Simpler lookup API
- Fewer special cases
- Easier mapper maintenance

**Priority:** `Medium`

---

## 10. Sanitization Model

### Current

The sanitizer primarily replaces identified protected values with deterministic dummy representations.

| Original | Dummy |
|---|---|
| `password` | `DUMMY_PASSWORD` |
| `apiKey` | `DUMMY_API_KEY` |
| `privateKey` | `DUMMY_PRIVATE_KEY` |

### Future

Develop richer context-aware sanitization strategies.

Potential areas include:

- Structured identifiers
- Internal URLs
- Connection strings
- Customer information
- Proprietary identifiers
- Context-dependent dummy values
- Type-aware replacement

> [!IMPORTANT]
> Future sanitization must preserve enough context for the external LLM to remain useful. More aggressive sanitization is not automatically better.

**Priority:** `High`

---

## 11. Sensitivity Analysis

### Current

Sensitivity detection is heuristic.

Signals currently include:

- Sensitive names
- Accessibility
- Constants
- String literal initializers
- Object creation initializers
- Dependency-based propagation

### Future

Improve detection using richer semantic and data-flow analysis.

Potential areas include:

- More expression types
- Interprocedural flow
- Collection propagation
- Return-value propagation
- Parameter propagation
- Field/property propagation
- Sink analysis
- Configuration analysis

> [!IMPORTANT]
> Sensitivity analysis should remain **explainable**. Aegis should be able to explain why a node was classified as sensitive.

**Priority:** `High`

---

## 12. Change Detection

### Current

Sanitized changes are detected primarily at **line granularity**.

Supported operations include: `Equal`, `Replace`, `Delete`, `Insert`.

### Future

Move toward more precise syntax-aware or token-aware change detection.

Potential approaches include:

- Token-level diffing
- Roslyn syntax-tree diffing
- Syntax-aware edit extraction
- Semantic edit classification

### Benefits

- More precise reverse mapping
- Better handling of small edits
- Reduced false review cases
- Better support for structural changes

**Priority:** `Medium`

---

## 13. Reverse Mapping Precision

### Current

The reverse mapper performs conservative coordinate translation and protected-region checks.

Unsafe or ambiguous mappings require review.

### Future

Improve mapping precision for:

- Multiple edits in one region
- Structural edits
- Moved code
- Renamed entities
- Inserted statements
- Deleted statements
- Changes spanning multiple mappings

> The system should remain **conservative** even as mapping capabilities become more sophisticated.

**Priority:** `High`

---

## 14. Patch Validation

### Current

The patch validator performs syntax validation using Roslyn. It does **not** currently prove semantic correctness.

### Future

Add stronger validation layers where practical.

Potential validation stages:

```
Patch
  │
  ▼
Syntax validation
  │
  ▼
Compilation validation
  │
  ▼
Semantic checks
  │
  ▼
Optional project tests
```

> [!WARNING]
> Compilation success must not be treated as proof that an LLM-generated change is correct.

**Priority:** `Medium`

---

## 15. CLI Architecture

### Current

The working alpha pipeline is exposed through the Roslyn worker command surface.

The repository also contains an older TypeScript CLI structure that is not yet the primary alpha interface.

### Future

Create a dedicated user-facing Aegis CLI:

```bash
aegis sanitize ...
aegis import ...
```

The CLI should become a thin interface over the analysis and sanitization engine rather than containing business logic itself.

```
CLI
 │
 ▼
Application Layer
 │
 ├── Analysis
 ├── Sanitization
 ├── Session
 ├── Import
 └── Patch Application
```

**Priority:** `High`

---

## 16. Direct LLM Integrations

### Current

Aegis is deliberately **LLM-agnostic**.

The developer manually transfers sanitized source to an external LLM and brings the result back.

### Future

Optional integrations may support providers or local models.

Potential integrations include:

- OpenAI
- Anthropic
- Google
- Local inference
- Other compatible providers

> [!IMPORTANT]
> LLM integrations must remain separate from the core sanitization and analysis engine. The core engine must not depend on a specific provider.

**Priority:** `Post-alpha`

---

## 17. IDE Integration

### Current

No IDE integration exists.

### Future

Potentially provide a **VS Code extension**.

Possible workflow:

```
Select code → Right click → Sanitize with Aegis → Open sanitized representation
```

> Eventually the full workflow could happen inside the developer's editor.

**Priority:** `Post-alpha`

---

## 18. Multi-Language Support

### Current

The alpha focuses on **C#** through Roslyn.

### Future

Additional language frontends can produce the same PIR abstraction.

```
C#           → Roslyn
Java         → Java frontend
Python       → Python frontend
TypeScript   → TypeScript frontend
                    │
                    ▼
                   PIR
                    │
                    ▼
           Shared analysis engine
```

> [!IMPORTANT]
> Language-specific parsing should remain separate from the language-independent analysis and sanitization layers.

**Priority:** `Post-alpha`

---

## 19. Session Security

### Current

Aegis stores session information locally under `.aegis/`.

The directory is ignored by Git.

Sessions can contain mappings between real and sanitized source values.

### Future

Consider additional protection mechanisms such as:

- Encrypted session storage
- Explicit session expiration
- Secure deletion
- Permission checks
- Configurable session locations
- Session locking

> These should be evaluated when the threat model expands beyond local developer workflows.

**Priority:** `Post-alpha`

---

## 20. What Is Explicitly Not Technical Debt

The following are intentionally **outside the current alpha** rather than unfinished implementation:

- ❌ Direct LLM integrations
- ❌ VS Code extension
- ❌ Multi-language support
- ❌ Cloud infrastructure
- ❌ Enterprise dashboards
- ❌ Enterprise policy management
- ❌ Automatic LLM orchestration

> These belong to future product scope. They should not be treated as bugs in the current alpha.

---

## 21. Priority Summary

| Area | Priority | Reason |
|---|---|---|
| Roslyn project loading | `High` | Real project analysis |
| Sensitivity analysis | `High` | Core privacy capability |
| Sanitization model | `High` | Core privacy capability |
| Reverse mapping | `High` | Core round-trip reliability |
| CLI | `High` | Developer-facing entry point |
| Mapper context | `Medium` | Maintainability |
| Relationship helpers | `Medium` | Maintainability |
| PIR type nodes | `Medium` | Richer analysis |
| Change detection | `Medium` | Better mapping precision |
| Patch validation | `Medium` | Stronger safety |
| PIR specification | `Medium` | Cross-component contract |
| Relationship deduplication | `Medium` | Graph semantics |
| Parent resolution | `Low/Medium` | Code quality |
| Unified symbol lookup | `Medium` | Mapper simplification |
| LLM integrations | `Post-alpha` | Product expansion |
| IDE integration | `Post-alpha` | Product expansion |
| Multi-language support | `Post-alpha` | Product expansion |
| Session encryption | `Post-alpha` | Expanded threat model |

---

## 22. Guiding Rule

Technical debt should be paid when it improves one of:

- ✅ Correctness
- ✅ Security
- ✅ Maintainability
- ✅ Extensibility
- ✅ Developer Experience

> Aegis should not prematurely optimize architecture for requirements that do not yet exist.

The current priority is to make the core workflow reliable:

**`Analyze → Sanitize → Use external LLM → Import → Map → Validate → Apply`**