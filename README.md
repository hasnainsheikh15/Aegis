<div align="center">

# 🛡️ Aegis

### Privacy infrastructure for AI-assisted software development.

**Sanitize proprietary code before it reaches an external LLM —  
then safely map useful changes back to the real source.**

<br />

[![Status](https://img.shields.io/badge/status-alpha-orange?style=for-the-badge)]()
[![Language](https://img.shields.io/badge/language-C%23-512BD4?style=for-the-badge&logo=csharp&logoColor=white)]()
[![Roslyn](https://img.shields.io/badge/analysis-Roslyn-68217A?style=for-the-badge)]()
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)]()
[![Tests](https://img.shields.io/badge/tests-27%20passing-2ea44f?style=for-the-badge)]()

<br />

> **Your code stays yours. Your LLM still gets the context it needs.**

</div>

---

## ⚡ The Problem

AI coding assistants are incredibly useful.

But real-world codebases contain things you may not want to send to an external LLM:

- 🔑 API keys
- 🔐 Passwords and access tokens
- 🗝️ Private keys
- 🗄️ Connection strings
- 🏢 Proprietary identifiers
- 🌐 Internal URLs
- 🧠 Confidential implementation details
- 📦 Sensitive business logic

The traditional solution is manual sanitization.

That's painful.

And simple search-and-replace isn't enough.

Consider:

```csharp
private string password = "abc123";

public void Login()
{
    string token = password;
    string backup = token;

    Validate(backup);
}
```

The sensitive value doesn't stay where it started.

It flows through the program.

A privacy layer therefore needs to understand code, not just text.

---

## 🛡️ What is Aegis?

Aegis is a developer-focused privacy layer for AI-assisted software development.

Instead of sending real source directly to an external LLM:

```
┌──────────────┐
│  Real Source  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│    Aegis     │
│   Analyze    │
│  + Sanitize  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Dummy Source  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ External LLM │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ LLM Changes  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│    Aegis     │
│ Map + Verify │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Real Patch  │
└──────────────┘
```

The important part:

Aegis does not need to know which LLM you use.

The alpha is intentionally **LLM-agnostic**.

Use ChatGPT, Claude, Gemini, a local model, or anything else.

Aegis sits between your source code and the LLM-facing representation.

---

## ✨ How It Works

### 01 — Select

Choose the source code you want to work with.

### 02 — Analyze

Aegis parses the project using Roslyn and builds a semantic representation.

It identifies:

- Declarations
- Symbols
- Relationships
- Dependencies
- Data flow
- Potentially sensitive nodes

```
password
    │
    │ FLOWS_TO
    ▼
  token
    │
    │ FLOWS_TO
    ▼
 backup
```

### 03 — Sanitize

Protected values are replaced with deterministic dummy representations.

```diff
- private string password = "abc123";
+ private string password = "DUMMY_PASSWORD";
```

The surrounding program structure remains intact.

### 04 — Use Any LLM

Send the sanitized code to whichever LLM you normally use.

Aegis does not send it automatically in the current alpha.

You remain in control.

### 05 — Bring the Result Back

Suppose the LLM changes:

```diff
- string token = password;
+ string token = Hash(password);
```

You give the modified sanitized source back to Aegis.

### 06 — Map + Validate

Aegis detects the change, maps it back to the real source, validates the resulting C# syntax, and checks that the original source hasn't changed unexpectedly.

### 07 — Apply

If the change is safe:

```diff
- string token = password;
+ string token = Hash(password);
```

If Aegis cannot safely map the change:

> ⚠ **Review required**
>
> The change cannot be safely mapped back to the protected source.
>
> The real source is left untouched.

---

## 🧠 Why Program Analysis?

Aegis isn't intended to be another regex-based secret scanner.

Consider:

```csharp
string token = password;
string backup = token;
send(backup);
```

A naive text scanner sees: `password`

Aegis can reason about the relationship:

```
password
    │
    ▼
  token
    │
    ▼
 backup
    │
    ▼
  send()
```

This is why the project contains:

```
Roslyn → PIR → Semantic Graph → Dependency Analysis
  → Sensitivity Analysis → Sanitization → Reverse Mapping
```

The analysis infrastructure exists to make the privacy transformation reliable.

---

## 🏗️ Architecture

```
┌───────────────────────────────────────────────────────────┐
│                         AEGIS                             │
├───────────────────────────────────────────────────────────┤
│                                                           │
│  ┌───────────────┐        ┌────────────────────────────┐  │
│  │    Roslyn     │───────▶│            PIR             │  │
│  │    Worker     │        │ Program Intermediate       │  │
│  └───────────────┘        │ Representation             │  │
│                           └─────────────┬──────────────┘  │
│                                         │                 │
│                                         ▼                 │
│                           ┌────────────────────────────┐  │
│                           │        Graph Engine        │  │
│                           │                            │  │
│                           │ Dependencies               │  │
│                           │ Data Flow                  │  │
│                           │ Relationships              │  │
│                           │ Sensitivity                │  │
│                           └─────────────┬──────────────┘  │
│                                         │                 │
│                                         ▼                 │
│                           ┌────────────────────────────┐  │
│                           │       Sanitizer            │  │
│                           │                            │  │
│                           │ Real → Dummy               │  │
│                           │ Mapping                    │  │
│                           │ Sessions                   │  │
│                           └─────────────┬──────────────┘  │
│                                         │                 │
│                                         ▼                 │
│                           ┌────────────────────────────┐  │
│                           │      Reverse Mapper        │  │
│                           │                            │  │
│                           │ Dummy → Real               │  │
│                           │ Change Detection           │  │
│                           │ Protected Regions          │  │
│                           └─────────────┬──────────────┘  │
│                                         │                 │
│                                         ▼                 │
│                           ┌────────────────────────────┐  │
│                           │       Patch Engine         │  │
│                           │                            │  │
│                           │ Integrity Checks           │  │
│                           │ Syntax Validation          │  │
│                           │ Safe Application           │  │
│                           └────────────────────────────┘  │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

---

## 🧩 Core Components

| Component | Responsibility |
|---|---|
| **Roslyn Worker** | C# parsing and semantic analysis |
| **PIR** | Language-independent program representation |
| **Graph** | Dependencies and semantic relationships |
| **Sensitivity** | Identifies potentially protected nodes |
| **Sanitizer** | Real → dummy transformation |
| **Session Store** | Persists mappings and source integrity data |
| **Change Detector** | Finds changes made to sanitized source |
| **Reverse Mapper** | Dummy → real source mapping |
| **Patch Engine** | Validates and applies safe patches |

---

## 🔒 Security Philosophy

Aegis follows a simple rule:

> **When Aegis cannot safely determine what a change means, it does not automatically apply it.**

The reverse-mapping pipeline includes protections such as:

- Source hashing
- Baseline verification
- Protected-region tracking
- Exact source-text verification
- Patch coordinate validation
- Syntax validation
- Review-required states
- Protected dummy-value tamper detection

For example, if an LLM changes:

```diff
- password = "DUMMY_PASSWORD";
+ password = "HACKED_VALUE";
```

Aegis does **not** assume `HACKED_VALUE → original password`.

Instead:

```
Protected value changed
         │
         ▼
    ⚠ REVIEW
         │
         ▼
Real source untouched
```

See [SECURITY.md](SECURITY.md) for the complete security model and current limitations.

---

## 🚀 Quick Start

### Requirements

- .NET 10 SDK
- Git

### Clone the repository

```bash
git clone <repository-url>
cd Aegis
```

### Run the test suite

```bash
dotnet test
```

### Build the Roslyn worker

```bash
dotnet build apps/roslyn-worker/RoslynWorker.csproj
```

---

## 🧪 Try the Alpha

The current CLI is intentionally minimal.

### Sanitize a selected region

```bash
dotnet run --project apps/roslyn-worker -- \
  sanitize \
  <project-folder> \
  <file-path> \
  <start> \
  <length>
```

**Example:**

```bash
dotnet run --project apps/roslyn-worker -- \
  sanitize \
  ./samples/sampleProject \
  ./samples/sampleProject/Program.cs \
  194 \
  24
```

Aegis creates a local session under:

```
.aegis/sessions/<session-id>/
```

The session contains the sanitized representation and the mapping information required for the return trip.

### Import changes from the LLM

After modifying the sanitized source with your LLM:

```bash
dotnet run --project apps/roslyn-worker -- \
  import \
  <path-to-session.json>
```

Aegis will detect, map, validate, and either apply or reject the resulting changes.

---

## 📦 Repository Structure

```
Aegis/
│
├── apps/
│   ├── cli/
│   └── roslyn-worker/
│
├── packages/
│   ├── graph/
│   ├── indexer/
│   ├── parser/
│   ├── pir/
│   ├── sanitizer/
│   └── shared/
│
├── samples/
│   └── sampleProject/
│
├── tests/
│   └── Sanitizer.Tests/
│
├── ARCHITECTURE.md
├── SECURITY.md
├── TECH_DEBT.md
└── objective.md
```

---

## 🧪 Testing

The project currently has **27 automated tests** covering the sanitizer and round-trip pipeline.

Important scenarios include:

- Source hashing
- Session persistence
- Change detection
- Reverse mapping
- Safe patch application
- Source modification detection
- Invalid patch rejection
- Protected-value tampering
- End-to-end round trip

The intended invariant is:

| Scenario | Outcome |
|---|---|
| Safe change | Map → Validate → **Apply** |
| Unsafe / ambiguous change | Review → **Real source remains unchanged** |

---

## ⚠️ Current Alpha Limitations

Aegis is currently an experimental alpha.

The current implementation intentionally does **not** include:

- Direct LLM integrations
- VS Code extension
- Automatic LLM requests
- Multi-language support
- Full MSBuild project loading
- Complete secret detection
- Complete proprietary-code detection
- Formal semantic-equivalence verification
- Cloud infrastructure
- Enterprise policy management

The current focus is proving the core round-trip:

**Real → Sanitize → External LLM → Import → Map → Validate → Apply**

---

## 🗺️ Roadmap

### ✅ Alpha Foundation

- [x] Roslyn parsing
- [x] Semantic symbol resolution
- [x] PIR
- [x] Semantic relationships
- [x] Dependency graph
- [x] Data-flow tracking
- [x] Sensitivity analysis
- [x] Sanitization
- [x] Session persistence
- [x] Reverse mapping
- [x] Patch validation
- [x] Safe patch application
- [x] End-to-end round trip

### 🔭 Next

- [ ] Better project loading through MSBuild
- [ ] More sophisticated sensitivity detection
- [ ] Stronger semantic mapping
- [ ] Better sanitized representations
- [ ] Improved CLI UX
- [ ] More realistic project fixtures
- [ ] Expanded security testing

### 🌐 Future

- [ ] VS Code integration
- [ ] Multi-language analysis
- [ ] LLM provider integrations
- [ ] Policy controls
- [ ] Team/enterprise workflows

---

## 💡 Design Principles

**Privacy over convenience**
Protected information should not be exposed simply because an LLM workflow is convenient.

**Preserve context**
Sanitization should remove what needs protection without destroying the information required for useful development work.

**Analyze code, not strings**
Program structure, symbols, relationships, and data flow matter.

**Fail closed**
Ambiguous reverse mappings should require review rather than being guessed.

**LLM agnostic**
Aegis should not lock developers into a specific AI provider.

**Local-first**
The current alpha keeps the sanitization and mapping workflow local to the developer's environment.

---

## 📚 Documentation

- [objective.md](objective.md) — Product objective and design goals
- [ARCHITECTURE.md](ARCHITECTURE.md) — Current Roslyn/PIR architecture
- [SECURITY.md](SECURITY.md) — Security model and limitations
- [TECH_DEBT.md](TECH_DEBT.md) — Deferred architectural improvements

---

## 🤝 Contributing

Aegis is currently in alpha and the architecture is still evolving.

If you're experimenting with the project:

1. Create a branch.
2. Keep changes focused.
3. Add tests for security-sensitive behavior.
4. Run the complete test suite.
5. Avoid committing `.aegis/` session data.

Before opening a pull request:

```bash
dotnet test
dotnet build
```

---

## ⚖️ Security Notice

> **Aegis should not currently be treated as a guarantee that arbitrary source code is safe to send to an external LLM.**
>
> The current sensitivity analyzer is heuristic and incomplete.
>
> Always review the sanitized output before sharing it externally.

See [SECURITY.md](SECURITY.md).

---

<div align="center">

🛡️ **Build with AI without blindly exposing your code.**

**Aegis — Sanitize. Delegate. Map back.**

<br />

<sub>Experimental alpha • Built with C# + Roslyn</sub>

</div>