# Security Model

Aegis is designed to reduce the amount of sensitive and proprietary source code exposed to external LLMs during AI-assisted software development.

The core security principle is:

> **If Aegis cannot safely determine how a change maps back to the real source, it must not automatically apply that change.**

---

## Threat Model

Aegis addresses this workflow:

```
Developer
    │
    │ Real source code
    ▼
┌───────────┐
│   Aegis   │
└─────┬─────┘
      │
      │ Sanitized source
      ▼
┌───────────┐
│ External  │
│    LLM    │
└───────────┘
```

The goal is to prevent protected source values from unnecessarily crossing the boundary between the developer's environment and the external LLM.

Examples of information Aegis may protect include:

- Passwords
- API keys
- Access tokens
- Private keys
- Connection strings
- Confidential identifiers
- Proprietary values

---

## Human-in-the-Loop Design

The current alpha does **not** directly communicate with external LLM providers.

The workflow is intentionally:

```
Real Source
     │
     ▼
   Aegis
     │
     ▼
Sanitized Source
     │
     ▼
  Developer        ◄── human decision point
     │
     ▼
 External LLM
     │
     ▼
Modified Sanitized Source
     │
     ▼
  Developer        ◄── human decision point
     │
     ▼
   Aegis
     │
     ▼
Mapped + Validated Patch
```

The developer remains responsible for deciding:

1. Which source is sanitized.
2. Which external LLM is used.
3. What prompt is provided.
4. Whether the sanitized representation is appropriate to share.
5. Whether the returned result should be trusted.

---

## Sanitization

Aegis replaces identified protected values with deterministic dummy representations.

For example:

```csharp
private string password = "abc123";
```

becomes:

```csharp
private string password = "DUMMY_PASSWORD";
```

The surrounding program structure is intentionally preserved so that the sanitized code remains useful to an LLM.

The mapping between the real and sanitized representation is maintained locally.

---

## Program Analysis

Aegis does not rely exclusively on text matching.

Sensitive information may propagate through program relationships.

For example:

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

The current analysis infrastructure includes:

- Roslyn syntax analysis
- Semantic symbol resolution
- PIR (Program Intermediate Representation)
- Dependency relationships
- Data-flow relationships
- Sensitivity analysis
- Multi-hop dependency traversal

This allows the sanitizer to reason about relationships between program entities rather than only matching strings.

---

## Protected Values

Aegis treats modifications to protected dummy values **conservatively**.

For example, suppose the sanitized source contains:

```csharp
password = "DUMMY_PASSWORD";
```

and the imported result changes it to:

```csharp
password = "HACKED_VALUE";
```

Aegis does **not** automatically assume that `HACKED_VALUE` should replace the original protected value.

Instead, the change is treated as requiring review:

```
Protected value changed
        │
        ▼
   REVIEW REQUIRED
        │
        ▼
Real source remains unchanged
```

This prevents arbitrary modifications to protected regions from becoming automatic replacements in the real source.

---

## Source Integrity

Every sanitization session records the hash of the original source.

Before applying patches, Aegis verifies that the original source has not changed since the session was created.

```
Session created
      │
      ▼
Hash(original source)
      │
      ▼
Developer works with sanitized source
      │
      ▼
Import result
      │
      ▼
Hash(current source)
      │
      ├── mismatch ──── ⚠ REVIEW
      │
      ▼
   Continue
```

If the source has changed, Aegis refuses automatic patch application.

This prevents stale session mappings from being applied to a modified source file.

---

## Patch Safety

Before a patch is applied, Aegis validates:

### 1. Source integrity

The current source must match the source recorded when the session was created.

### 2. Patch coordinates

Patch positions must fall within the source range.

### 3. Expected source text

The text at the patch location must match the original text recorded by the patch.

> This prevents a patch from silently being applied to an unexpected piece of source.

### 4. Protected regions

Changes involving protected sanitized values are evaluated separately. Unsafe changes require review.

### 5. Syntax

The resulting C# source is parsed after applying the patch in memory. If syntax errors are detected, the patch is rejected.

---

## Safe Failure

Aegis follows a conservative failure model:

```
        Can the change
       be mapped safely?
              │
        ┌─────┴─────┐
        │           │
       YES          NO
        │           │
        ▼           ▼
     Validate     Review
        │
        ▼
      Apply
```

The system should prefer requiring human review over guessing.

---

## Local Session Data

Aegis creates local session data required for the sanitization round trip.

A session can contain:

- Original source hash
- Sanitized source hash
- Sanitized source
- Baseline sanitized source
- Source mappings
- Source coordinates
- Session metadata

Sessions are stored under:

```
.aegis/
```

The repository `.gitignore` excludes this directory so that local session data is not accidentally committed to source control.

> **However:** `.aegis/` should still be treated as sensitive local development data.
>
> The session mapping can contain information that relates sanitized values back to their original source values.

---

## Important Limitations

### ⚠️ Detection Is Not Perfect

The current sensitivity analyzer is **heuristic**.

It uses signals such as:

- Sensitive identifier names
- Accessibility
- Constants
- Initializers
- String literals
- Object creation

It does **not** guarantee detection of every secret or proprietary value.

For example, arbitrary confidential business logic may not be detected.

> **Aegis alpha should not be treated as a complete secret scanner or formal information-flow security system.**

### ⚠️ Sanitized Code Can Still Leak Information

Replacing secret values does not automatically make source code completely private.

Source structure itself can contain sensitive information. Examples include:

- Class names
- Method names
- API shapes
- Internal architecture
- Algorithms
- Control flow
- Proprietary implementation details

Aegis intentionally preserves much of this information because the LLM needs useful program context.

> **Developers should review the generated sanitized representation before sending it to an external service.**

### ⚠️ External LLM Responsibility

Once sanitized source is sent to an external LLM, its handling is governed by that provider.

Aegis does **not** control:

- Provider data retention
- Provider logging
- Provider training policies
- Provider infrastructure
- Provider access controls
- Provider terms of service

Developers should independently evaluate the policies and configuration of the external service they choose.

### ⚠️ Syntax Validation Is Not Correctness Validation

A patch successfully passing syntax validation does **not** mean that it is correct.

A syntactically valid patch may still contain:

- Bugs
- Behavioral regressions
- Security vulnerabilities
- Incorrect business logic
- Unintended behavior

Aegis currently validates source syntax and patch integrity. It does **not** prove semantic correctness.

---

## Security Boundary

The intended boundary is:

```
                    SECURITY BOUNDARY
                           │
                           ▼

┌──────────────┐    ┌───────────┐    ┌──────────────────┐
│  Real Source  │ ──▶│   Aegis   │──▶ │ Sanitized Code   │
└──────────────┘    └───────────┘    └────────┬─────────┘
                                              │
                                              ▼
                                     ┌──────────────┐
                                     │ External LLM │
                                     └──────────────┘
```

Aegis attempts to ensure that protected values are replaced **before** the sanitized representation crosses this boundary.

---

## Security Invariants

The current implementation aims to maintain these invariants:

| Invariant | Description |
|---|---|
| **Protected values should not be intentionally exposed** | Sensitive values identified by the sanitizer should be replaced before the sanitized source is produced. |
| **Unsafe reverse mappings should not be applied automatically** | Ambiguous or protected changes must require review. |
| **Stale source should not receive patches** | A session created against one version of a file should not automatically modify a different version. |
| **Invalid patches should not modify the real source** | Patch validation occurs before the file is written. |
| **Failed validation should leave the real source unchanged** | The patch pipeline should fail closed. |

---

## Security Testing

The automated test suite currently covers security-sensitive behavior including:

- Source hashing
- Session persistence
- Reverse mapping
- Patch validation
- Source modification detection
- Protected-value tampering
- Safe patch application
- End-to-end sanitized-to-real round trips

> Security-sensitive behavior should continue to receive automated tests as the project evolves.

---

## Reporting Security Issues

If you discover a security vulnerability, please report it **privately** to the project maintainers rather than publicly disclosing it immediately.

A useful report should contain:

1. Description of the vulnerability
2. Steps to reproduce
3. Relevant source-code shape
4. Expected behavior
5. Actual behavior
6. Potential security impact

> **Do not include real production secrets in bug reports. Use synthetic values instead.**

---

## Alpha Security Status

Aegis is currently an **experimental alpha**.

The current implementation provides a security-oriented sanitization and reverse-mapping pipeline, but it should **not** be considered a complete enterprise security product.

In particular:

- Detection is incomplete.
- Sanitization is heuristic.
- Source structure may remain sensitive.
- Semantic correctness is not guaranteed.
- External LLM behavior is outside Aegis's control.

> The alpha goal is to establish a conservative and testable foundation for privacy-preserving AI-assisted development.
