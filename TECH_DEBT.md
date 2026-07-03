# Technical Debt

## Monorepo

- Replace relative imports with workspace packages.
- Configure package exports.
- Create tsconfig.base.json.
- Configure Turbo build pipeline.
- Enable auto-imports across packages.

Priority: Medium


# Technical Debt

This document tracks architectural improvements that have been intentionally postponed. These are **not bugs** or **planned features**; they are improvements that will make the codebase cleaner, safer, or more maintainable.

---

## 1. Extract Relationship Creation Helper

**Current State**

Relationship creation logic is duplicated across multiple mapping methods.

Examples:

* `MapClass()`
* `MapMethod()`
* `MapConstructor()`
* `MapParameter()`
* `MapProperty()`
* `MapField()`

**Planned Improvement**

Introduce:

```csharp
CreateRelationship(
    PirPackage pirPackage,
    PirNode source,
    PirNode target,
    PirRelationshipType type
)
```

to eliminate duplication.

---

## 2. Improve Parent Resolution

Many mapping methods manually locate semantic owners.

Examples:

* Class → Namespace
* Method → Class
* Constructor → Class
* Property → Class
* Parameter → Method / Constructor

Later, investigate whether this can be generalized into reusable helper methods.

---

## 3. Move PIR Printing into a Dedicated Printer

`Program.cs` currently handles:

* reading files
* parsing
* mapping
* printing

Printing should eventually move into a dedicated `PirPrinter` class.

---

## 4. Introduce SemanticModel

Current implementation relies only on Roslyn's syntax tree.

Future work:

* symbol resolution
* fully-qualified type names
* inheritance resolution
* interface resolution
* accurate method call resolution

This is a major milestone and should be introduced after the structural PIR is stable.

---

## 5. Replace Metadata Types with Type Nodes (Future)

Currently, data types are stored as metadata on `PirNode`.

Future possibility:

```
Method
    │
RETURNS
    ▼
Type(User)
```

instead of:

```
ReturnType = "User"
```

This requires semantic analysis and symbol resolution.

## Refactor RoslynToPirMapper API

Current API:

MapCompilationUnit(...) -> PirPackage

Current project flow creates one temporary PirPackage per file and merges them into a project package.

Future API:

MapCompilationUnit(
    CompilationUnitSyntax root,
    SemanticModel semanticModel,
    PirPackage pirPackage
)

This will allow the mapper to write directly into the shared project PIR and avoid temporary allocations.

## ---- 

Right now we're manually specifying:

typeof(object)

typeof(Console)

Later, when Aegis analyzes real .csproj projects, we shouldn't guess the references.

Instead, we should load the project through MSBuild (via Microsoft.Build.Locator and MSBuildWorkspace in Roslyn), which automatically provides all the correct references, compilation options, analyzers, and project settings.

That will let Aegis analyze projects the same way Visual Studio does.

For now, our manual references are perfectly fine for learning and for simple sample projects, but it's worth capturing that future improvement in TECH_DEBT.md so we remember to revisit it.