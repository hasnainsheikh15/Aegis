# Aegis — Objective

## 1. Core Objective

Aegis is a developer-focused privacy layer for AI-assisted software development.

The core objective is:

> Allow developers to use external LLMs with their real code without exposing sensitive or proprietary parts of that code to the LLM.

Aegis achieves this by transforming code selected by the developer into a safe dummy representation before it is given to an LLM, while preserving enough context, structure, semantics, and relationships for the LLM to perform the requested task correctly.

After the LLM responds, Aegis maps the useful result back onto the developer's real code.

The fundamental workflow is:

```text
Developer selects code/file
        |
        v
      Aegis
        |
        | Analyze sensitive/proprietary content
        | Create dummy representation
        | Preserve useful context
        v
Sanitized / dummy code
        |
        | Developer gives this to the LLM
        v
       LLM
        |
        | Write / review / debug / test / refactor
        v
     LLM result
        |
        v
      Aegis
        |
        | Map dummy entities/results to real entities
        v
Useful result applied to real code
```

---

## 2. Real Developer Use Case

Developers increasingly use LLMs throughout software development for:

- Writing code
- Debugging
- Code review
- Refactoring
- Test generation
- Writing tests
- Code explanation
- Optimization
- Documentation
- Understanding unfamiliar code

A developer may want to give an LLM a selected portion of code or an entire file.

That code may contain information that should not be exposed to the LLM, such as:

- API keys
- Passwords
- Access tokens
- Private keys
- Connection strings
- Internal API formats
- Internal URLs
- Customer information
- Proprietary identifiers
- Confidential business logic
- Proprietary implementation details
- Other sensitive values embedded in source code

Aegis should remove the need for the developer to manually sanitize that code before sending it to an LLM.

---

## 3. How Aegis Is Used

### Step 1 — Select

The developer selects a specific piece of code or an entire file.

### Step 2 — Analyze

Aegis analyzes the selected source code and identifies sensitive or protected content and data derived from it.

### Step 3 — Sanitize

Aegis converts the real code into a dummy/safe representation.

Example:

### Real code

```csharp
public class AuthService
{
    private string password = "abc123";

    public void Login()
    {
        string token = password;
        Validate(token);
    }
}
```

### Dummy code

```csharp
public class AuthService
{
    private string password = "DUMMY_PASSWORD";

    public void Login()
    {
        string token = password;
        Validate(token);
    }
}
```

The goal is not to destroy the code's meaning.

The LLM should still understand:

- Program structure
- Types
- Control flow
- Method signatures
- Relationships
- Relevant behavior
- Data relationships necessary for the requested task

Aegis should remove protected information while preserving useful context.

### Step 4 — Developer sends the dummy code to the LLM

The developer can now use the sanitized representation for the normal LLM task.

### Step 5 — LLM responds

The LLM may return:

- New code
- Refactored code
- Bug fixes
- Tests
- Suggestions
- Documentation
- Explanations

### Step 6 — Map the useful result back

Aegis uses its mapping between real and dummy representations to apply useful changes/results to the developer's actual code.

---

## 4. Core Product Principle

Aegis is **not primarily a blocking system**.

The primary goal is not:

```text
Sensitive data detected
        |
        v
BLOCK LLM
```

The primary goal is:

```text
Real code
    |
    v
Sanitize
    |
    v
Dummy/safe code
    |
    v
LLM
    |
    v
Useful result
    |
    v
Map back to real code
```

Blocking may exist as a policy option, but the central product experience is safe transformation.

---

## 5. Why Program Analysis Is Required

Simple text replacement is insufficient.

Consider:

```csharp
string token = password;
string backup = token;
```

The sensitive value originated from `password`, but the value is later represented by `backup`.

Aegis therefore needs to understand data flow:

```text
password
    |
    | FLOWS_TO
    v
token
    |
    | FLOWS_TO
    v
backup
```

This allows Aegis to identify data that is sensitive because of where it came from, even when the final variable or expression does not have an obviously sensitive name.

The PIR, graph, semantic analysis, sensitivity analysis, and data-flow analysis exist to support the sanitization workflow.

They are implementation mechanisms, not the product objective themselves.

---

## 6. Sanitization Requirements

### 6.1 Sensitive Content Identification

Aegis should identify sensitive/protected content using multiple signals, including:

- Sensitive identifiers
- Accessibility
- Constants
- Literal values
- Initializers
- Program/data flow
- Relevant semantic information

### 6.2 Context-Aware Replacement

Aegis should not blindly replace arbitrary text.

Different sensitive elements may require different dummy representations.

Examples:

```text
password           -> DUMMY_PASSWORD
apiKey             -> DUMMY_API_KEY
connectionString   -> DUMMY_CONNECTION_STRING
privateKey         -> DUMMY_PRIVATE_KEY
customerEmail      -> dummy@example.com
```

### 6.3 Structural Preservation

The sanitized code should remain useful to an LLM.

Where possible, preserve:

- Program structure
- Types
- Control flow
- Method signatures
- Relationships
- Non-sensitive names
- Relevant semantic context

### 6.4 Deterministic Mapping

Aegis must maintain a reliable mapping between real and dummy representations.

Conceptually:

```text
Real entity/value       Dummy entity/value
------------------------------------------------
password                DUMMY_PASSWORD
real connection string  DUMMY_CONNECTION_STRING
real API key            DUMMY_API_KEY
```

The mapping must be sufficient to identify which dummy representation corresponds to which real source element.

### 6.5 Result Mapping

When the LLM returns a result based on the dummy code, Aegis must be able to map useful changes/results back to the real source.

The real-to-dummy mapping is therefore a fundamental part of the product.

---

## 7. Target Audience

### Primary Target Audience

> Software developers who regularly use LLMs for software development and need to work with code that they cannot safely expose to external LLM providers.

This includes developers using LLMs for:

- Coding
- Debugging
- Refactoring
- Code review
- Testing
- Documentation
- Code explanation
- Optimization

### Secondary Audience

Engineering teams and organizations that want developers to use external LLMs while maintaining control over sensitive and proprietary source code.

Examples include:

- Startups with proprietary products
- Software companies
- Enterprise engineering teams
- Security-conscious development teams
- Organizations with confidential codebases
- Teams handling sensitive customer or business information

---

## 8. What Aegis Is Not Primarily

Aegis is not primarily:

- A generic secret scanner
- A vulnerability scanner
- A code-quality analyzer
- A generic DLP platform
- A generic LLM firewall
- A general-purpose static-analysis platform

These capabilities may support Aegis, but the central product remains:

> Sanitize selected real code before an LLM sees it, preserve enough context for the LLM to be useful, and map the useful LLM result back to the developer's real code.

---

## 9. V1 Objective

Aegis V1 should prove this complete developer workflow:

```text
Developer selects code/file
          |
          v
Aegis analyzes it
          |
          v
Sensitive/proprietary content identified
          |
          v
Sensitive content transformed into dummy representations
          |
          v
Sanitized code provided to developer
          |
          v
Developer sends sanitized code to LLM
          |
          v
LLM performs requested task
          |
          v
LLM returns result
          |
          v
Aegis maps useful result back to real entities/source
          |
          v
Developer receives/applies the useful result
```

The V1 success criterion is:

> A developer can select real code containing sensitive information, use an external LLM on a sanitized representation of that code, and apply the useful result back to the real code without exposing the protected information to the LLM.

---

## 10. Current Engineering Foundation

The following infrastructure exists to support this objective:

- Roslyn source parsing
- Semantic symbol resolution
- PIR representation
- PIR relationships
- Dependency graph
- Sensitivity analysis
- Local-variable analysis
- Data-flow relationships
- Multi-hop flow tracking
- Sensitivity propagation

These components should now be treated as the foundation for the sanitizer rather than as the final product.

---

## 11. Current Missing Product Layer

The next major implementation is the sanitizer itself:

```text
Real selected source
        |
        v
Sensitive PIR/sensitivity information
        |
        v
Sanitization engine
        |
        +--> Dummy representation
        |
        +--> Real <-> Dummy mapping
        |
        v
Sanitized source
```

After that, the next product layer is result remapping:

```text
Sanitized source
        |
        v
       LLM
        |
        v
LLM result
        |
        v
Mapping layer
        |
        v
Real source/result
```

---

## 12. Guiding Principle

Every Aegis feature should ultimately support one question:

> Can we give the LLM enough information to perform the developer's requested task while keeping information that should remain private out of the LLM's view?

Aegis should optimize for:

1. **Privacy** — protected information must not be unnecessarily exposed.
2. **Context preservation** — sanitization must not destroy information required by the LLM.
3. **Useful LLM results** — dummy code must remain useful for real development tasks.
4. **Reliable mapping** — dummy entities/results must map reliably back to real source entities.
5. **Minimal developer friction** — the workflow should fit naturally into the developer's existing LLM workflow.

The intended product experience should feel simple:

```text
SELECT
  ↓
SANITIZE
  ↓
SEND TO LLM
  ↓
GET RESULT
  ↓
MAP/APPLY TO REAL CODE
```

The complexity of Roslyn, PIR, graph analysis, sensitivity detection, data-flow analysis, and mapping should remain underneath this workflow.
