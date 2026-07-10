# Technical Debt

This document tracks architectural improvements that have been intentionally postponed. These are **not bugs** or **planned features**. They are improvements that will make the codebase cleaner, more maintainable, or easier to extend as Aegis grows.

---

# Architecture

## 1. Replace Relative Imports with Workspace Packages

### Current

Some packages still reference each other using relative imports.

### Future

* Replace relative imports with workspace packages.
* Configure package exports.
* Create `tsconfig.base.json`.
* Configure the Turbo build pipeline.
* Enable auto-imports across packages.

**Priority:** Medium

---

## 2. Refactor `RoslynToPirMapper` API

### Current

Each source file creates its own temporary `PirPackage`.

```csharp
MapCompilationUnit(...) -> PirPackage
```

The Roslyn worker later merges these temporary packages into a single project package.

### Future

Refactor the mapper so it writes directly into the project's shared `PirPackage`.

```csharp
MapCompilationUnit(
    CompilationUnitSyntax root,
    SemanticModel semanticModel,
    PirPackage pirPackage
)
```

Benefits:

* Eliminates temporary allocations.
* Simplifies project-level mapping.
* Better reflects the fact that a project produces one PIR graph.

---

## 3. Introduce `MappingContext`

### Current

`RoslynToPirMapper` stores mapping state in several private fields:

* `SemanticModel`
* `nodeLookup`
* `symbolLookup`

### Future

If additional mapper state is introduced (diagnostics, options, type lookups, etc.), replace these individual fields with a dedicated `MappingContext` object.

Benefits:

* Cleaner mapper implementation.
* Easier unit testing.
* Reduces shared mutable state.

---

## 4. Improve Parent Resolution

### Current

Many mapping methods manually locate semantic owners.

Examples:

* Class → Namespace
* Method → Class
* Constructor → Class
* Property → Class
* Parameter → Method / Constructor

### Future

Investigate reusable helper methods for locating semantic parents to reduce duplicated traversal logic.

---

## 5. Extract Relationship Creation Helper

### Current

Relationship creation logic is duplicated across multiple mapping methods.

Examples:

* `MapClass()`
* `MapMethod()`
* `MapConstructor()`
* `MapParameter()`
* `MapProperty()`
* `MapField()`
* Future semantic mapping methods

### Future

Introduce a reusable helper:

```csharp
CreateRelationship(
    PirPackage pirPackage,
    PirNode source,
    PirNode target,
    PirRelationshipType type
)
```

Benefits:

* Eliminates duplication.
* Centralizes relationship creation.
* Makes future semantic mappings simpler.

---

# Roslyn Integration

## 6. Load Projects Through MSBuild

### Current

The Roslyn worker manually creates a `CSharpCompilation` and manually supplies metadata references.

```csharp
typeof(object)

typeof(Console)
```

This works for sample projects but does not fully reproduce how real C# projects are compiled.

### Future

Replace manual compilation with `MSBuildWorkspace`.

Benefits:

* Correct project references.
* NuGet package resolution.
* Implicit/global usings.
* Nullable context.
* Language version.
* Analyzer support.
* Compilation options.

This will allow Aegis to analyze projects exactly as Visual Studio does.

---

# PIR Evolution

## 7. Replace Metadata Types with Type Nodes

### Current

Data types are stored as metadata.

Example:

```text
ReturnType = "User"
```

### Future

Represent types as graph nodes.

```text
Method
    │
RETURNS
    ▼
Type(User)
```

Benefits:

* Richer type graph.
* Easier dependency analysis.
* Better cross-language representation.

---

## 8. Formalize PIR Specification

Create:

```text
docs/pir-spec.md
```

The specification should define:

### Node Types

* Namespace
* Class
* Constructor
* Method
* Parameter
* Property
* Field
* (Future) Type

### Relationship Types

* CONTAINS
* DECLARES
* CALLS
* INHERITS
* IMPLEMENTS
* CREATES
* READS
* WRITES

This document will become the contract between every language frontend and the Aegis analysis engine.

---

# Future Decisions

## 9. Relationship Deduplication Strategy

Decide how repeated semantic relationships should be represented.

Example:

```csharp
Validate();
Validate();
```

Should this produce:

```text
Login
    │
 CALLS
    ▼
Validate

Login
    │
 CALLS
    ▼
Validate
```

or

```text
Login
    │
 CALLS (count = 2)
    ▼
Validate
```

Current behavior is intentionally undefined and should be decided before large-scale graph analysis is implemented.
