# Requirements: Structure, Status, Milestones and Use Cases

This document defines where requirements belong in this repository, how they
are written and identified, and how requirements, use cases and milestones are
approved. It is compatible with other repositories that use the same standard,
so requirements can be referenced across repository boundaries.

## 1. Documentation Levels

| Level | General documentation | Requirements |
| --- | --- | --- |
| Repository | `docs/` | `docs/requirements/` for cross-component contracts and principles |
| Component or module | `<component>/docs/` | `<component>/docs/requirements/` |
| Plugin or extension | `<plugins>/<plugin>/docs/` | `<plugins>/<plugin>/docs/requirements/` |
| Tool or utility | `<tools>/<tool>/docs/` | `<tools>/<tool>/docs/requirements/` |

Adapt the parent folder names to the repository layout, for example
`src/<module>`. Always keep requirements in `docs/requirements` under the
owner.

- Store a requirement at the lowest owner that can maintain and verify it.
  A principle that applies across components belongs at repository level. The
  mechanisms and classes that realize it belong to their owning component.
- A higher layer may reference lower-layer requirement IDs. A lower layer shall
  not depend on higher-layer requirements; this mirrors the dependency
  direction between components.
- Reference an existing requirement instead of copying its text. This also
  applies to requirements owned by other repositories (section 11).
- Descriptive material belongs in general documentation, not in requirement
  documents. This includes usage guides, tutorials, observed behavior and
  design narratives.
- Create a requirements folder only when the owner has real content. Do not add
  empty placeholder folders.

## 2. Requirements Folder Layout

```text
<owner>/docs/requirements/
  README.md      index: scope, ID codes, topics, document map, milestones and use cases
  milestones.md  milestone definitions M1, M2, M3 ...
  architecture/  architecture and design requirements: principles, mechanisms, classes
  functional/    functional requirements: behaviors
  use-cases/     use cases, one file per use case or closely related group
```

Within `architecture/` and `functional/`, name documents after their topic or
class, for example `architecture/object-lifetime.md` or
`functional/persistence.md`.

An owner may add an implementation-status ledger, for example
`implementation-status.md`. It maps requirements to source code and
verification evidence.

Existing topic folders may remain. Documents in those folders state the kind of
each requirement.

## 3. Document Template

```md
# Requirements: <Area>

## Purpose
Short reason for the requirement area.

## Scope
Modules, classes, file formats, APIs or runtime systems covered.

## Requirements
Requirements in the heading or compact form of section 6.

## Design Consequences
Implementation consequences, allowed patterns, forbidden shortcuts, ownership
rules or layering effects.

## Verification
Tests, benchmarks, review checks, profiling scenarios, validation data or manual
checks needed to verify the requirements.

## Related Documentation
Relative links to guides, public headers or interfaces, tests and use cases.
```

## 4. Requirement Kinds

Every requirement has exactly one kind. The kind determines the category and
the folder.

| Kind | Specifies | Category | Folder |
| --- | --- | --- | --- |
| **Principle** | A cross-cutting rule that all affected code follows. Examples: platform independence, layering and dependency direction, ownership rules, coupling direction. | Architecture | `architecture/` |
| **Mechanism** | A reusable solution with defined participants, protocol and invariants. Examples: object lifetime management, serialization, type registration, plugin loading, notification, locking. | Design | `architecture/` |
| **Class** | The contract of one class, type or template. Covers responsibility, invariants, construction, copying, ownership, thread safety, persistence and extension points. | Design | `architecture/` |
| **Behavior** | Observable results: return values, errors, ordering, side effects, and file or wire compatibility. | Functional | `functional/` |

Express the observable API results of a class as separate Behavior requirements
that reference the Class requirement.

## 5. Topics

The repository README in `docs/requirements` lists the topics the repository
specifies, with their owners and area codes. Reuse these area codes when a
topic applies so that IDs remain comparable across repositories:

| Topic | Typical requirements | Area code |
| --- | --- | --- |
| Platform independence and basic types | Portable types, OS and compiler abstraction, byte order, strings, file and time abstractions, consistent behavior across platforms | `PLAT` |
| Containers and generic types | Generic containers and algorithms, complexity, iteration, growth, copy and move semantics | `TMPL` |
| Memory and resource management | Allocation, pools, bounded memory, cleanup, diagnostics | `MEM` |
| Object lifetime and ownership | Reference counting or other lifetime models, ownership transfer, borrowing, lifetime across module and language boundaries | `REF` |
| Type handling | Runtime type information, registration, factories, dynamic values, conversions | `TYPE` |
| Serialization and persistence | Formats, adapters, versioning, backward reading, streaming and size limits | `SER` |
| Coupling and integration | Module and plugin loading, registries, notifications and callbacks, language bindings, dependency direction | `COUP` |
| Concurrency | Threads, locks, thread-local data, lock order, callback threads | `THR` |
| Distributed communication | Remote objects, sessions, channels, transports, events, transactions, ordering, reliability | `DIST`, `TRANS` |
| Test and verification | Test framework contracts, test kinds, capability gating, required verification per owner | `TEST` |

Add repository-specific topics as needed. When a document is not tied to one
topic, use `ARCH`, `DES` and `FUNC`.

## 6. Requirement IDs and Fields

The ID form is `REQ-<CODE>-<AREA>-NNN`.

- **Owner code:** each requirements root registers one unique short code in its
  README, for example `CORE`, `UI` or `NET`. Choose codes that do not collide
  with the owner codes of repositories you reference.
- **Area:** a topic code from section 5, or an established class or format code.
- **Numbers:** three digits or more. Never reuse or renumber an ID.

Use the heading form for new documents:

```md
### REQ-CORE-REF-012: Ownership transfer on factory creation

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CORE-002
- **Depends on:** REQ-CORE-ARCH-001
- **Verification:** Unit test proving balanced ownership after creation and release

Factory methods shall return newly created objects without retaining an
internal owner ...
```

An accepted item records the date, the approver and a decision reference:

```md
- **Status:** Accepted (2026-10-28, <approver>, PR #123)
```

Short documents may use the compact list form:

```md
- REQ-CORE-ARCH-003 [Accepted, Principle, M1]: Public APIs shall ...
- REQ-CORE-SER-004 [Pending, Behavior]: Reading shall ...
```

Requirement language:

- **Shall:** a required behavior or contract.
- **Should:** a preferred behavior that allows a justified exception.
- **May:** an allowed implementation choice.

Each requirement states one verifiable contract.

## 7. Status: Pending and Accepted

The same two statuses apply to requirements, use cases and milestones.

| Status | Meaning |
| --- | --- |
| **Pending** | Proposed, under discussion or under revision. Not binding. The content may change. It shall not be cited as an accepted contract, used to freeze a public API or used for acceptance claims. |
| **Accepted** | Explicitly approved by the owner. Binding for implementation, review and verification. Changes follow the rules below. |

Rules:

1. New items start as **Pending**.
2. Only an explicit decision by the owner or a designated approver changes an
   item to **Accepted**. Record the date, the approver and a decision reference.
   Contributors and AI agents may create and edit Pending items. They shall not
   set Accepted on their own initiative.
3. A requirement can become Accepted only when all of the following hold:
   - its wording is verifiable;
   - its owner, kind and ID are fixed;
   - a verification method is stated;
   - no unresolved open decision affects it;
   - the requirements it depends on are Accepted.
4. Status records approval, not implementation. Accepted does not mean
   implemented or verified, and an implementation does not make a requirement
   Accepted. Record implementation and verification progress separately, for
   example in an implementation-status ledger using Realized, Partly realized,
   Planned, Deferred and Unverified.
5. Editorial changes keep the current status. These include spelling, links and
   clarifications that do not change the meaning.
6. A semantic change to an Accepted item returns it to Pending until it is
   accepted again. An Accepted milestone that contains the item also returns to
   Pending. Alternatively, create a new ID and mark the old one as superseded.
7. Mark a replaced item with `**Superseded by:** <ID>` and a withdrawn item with
   `**Withdrawn:** <reason> (<date>)`. Keep the ID and text in both cases. These
   are annotations, not additional statuses, and the item no longer counts
   toward milestone scope.
8. Existing documents without an explicit status remain valid. Add an explicit
   status when a document is materially changed or referenced by a milestone.
   Earlier prose such as "proposed", "target" or "planned" maps to Pending.
   "Confirmed" or "normative" may become Accepted only after owner confirmation.

## 8. Open Decisions

Record each unresolved design choice as `OD-<CODE>-NNN` in an "Open Decisions"
table in the relevant architecture document. A Pending requirement may reference
an open decision. A requirement that depends on an unresolved open decision
cannot become Accepted.

## 9. Milestones

Define each owner's milestones in `<owner>/docs/requirements/milestones.md`.

- **IDs:** use `M1`, `M2`, `M3` and so on within the owner. Outside the owner,
  use the qualified form `<CODE>-M<n>`, for example `CORE-M2`. Numbers are
  identities, not dates. Never reuse or renumber them. Express order through
  explicit dependencies.
- **Status:** Pending means the scope is proposed. Accepted means the owner has
  approved the scope; changing it requires a new acceptance.
- **Scope:** an Accepted milestone contains only Accepted requirements and use
  cases. Pending items may be listed as candidates but are not part of the
  scope.
- **Single source of truth:** the milestone document is authoritative for
  scope. Each requirement's `Milestone` field shall match it.
- **Completion** is not a status. Record delivery evidence, such as a commit,
  tag, version, test run or demonstration, under "Completion evidence". A
  milestone is complete only when every requirement in its scope has recorded
  verification evidence.

```md
## M2: <Milestone name>

- **Status:** Pending
- **Goal:** One-sentence outcome.
- **Depends on:** M1, <Repository>:<CODE>-M1
- **Requirements:** REQ-CORE-SER-010, REQ-CORE-SER-011, REQ-CORE-MEM-004
- **Use cases:** UC-CORE-003
- **Candidates (not in scope):** REQ-CORE-SER-020
- **Exit criteria:** Named tests, platforms, configurations and demonstrations.
- **Out of scope:** ...
- **Completion evidence:** none yet
```

The requirements README contains a summary table:

```md
| Milestone | Status | Goal | Depends on |
| --- | --- | --- | --- |
| M1 | Accepted | ... | - |
| M2 | Pending | ... | M1 |
```

## 10. Use Cases

Store use cases in `use-cases/` with IDs of the form `UC-<CODE>-NNN`. Use one
file per use case, named `UC-<CODE>-NNN-short-name.md`, or group closely related
use cases in one file.

A use case describes an actor's goal and the interaction that achieves it. The
actor may be a user, an application developer, a plugin author, a tool or a
remote system. A use case motivates requirements and supplies acceptance
scenarios, but it contains no "shall" contracts of its own. Each requirement
references the use cases it serves, and each use case lists its related
requirements. Use cases follow the status rules in section 7.

```md
# UC-CORE-002: <Use case name>

- **Status:** Pending
- **Milestone:** M1
- **Primary actor:** <Actor>
- **Related requirements:** REQ-CORE-REF-012, REQ-CORE-SER-004

## Goal
## Preconditions
## Trigger
## Main Flow
1. ...
## Alternative and Error Flows
- 3a. ...
## Postconditions
## Acceptance Scenario
Concrete data, platforms, configuration and expected observable result.
```

## 11. Cross-Repository References

- Reference a requirement, use case or milestone in another repository with
  the qualified form `<Repository>:<ID>`, for example
  `<Repository>:REQ-CORE-REF-003` or `<Repository>:<CODE>-M2`.
- Link to the owning document with a stable URL or a relative path in a shared
  checkout. Do not copy the requirement text.
- A dependency on another repository's requirement counts as satisfied for
  acceptance only when that requirement is Accepted in its own repository.
- Requirements that the repository adds on top of a dependency are written as
  its own requirements and reference the dependency's IDs. They do not
  redefine the dependency's contract.

## 12. Requirements README

Each requirements root contains a README with:

- its purpose and scope;
- the owner code and the topics and area codes it uses;
- a document map listing each document, its kinds and its ID ranges;
- the milestone summary table;
- a use-case index;
- links to related higher- and lower-level requirement roots, to external
  repositories it depends on, and to any implementation-status ledger.

## 13. Writing Rules

Requirements are:

- specific enough to verify;
- owned by one owner;
- written as contracts, not as implementation notes;
- separate from tutorial and usage text;
- linked to tests or explicit verification where practical;
- updated when architecture decisions change.

Write requirement documents in English Markdown, encoded as UTF-8 without BOM,
with relative links for references within the repository.

## 14. Review Checks

- Each ID is unique within its owner and never reused.
- Every requirement states its kind.
- Every Accepted item records its acceptance date, approver and decision
  reference.
- Every Accepted requirement states a verification method.
- An Accepted milestone references only Accepted requirements and use cases.
- Milestone scope matches the requirements' `Milestone` fields.
- Cross-component principles live at repository level. Mechanisms and classes
  live in their owning component.
- Cross-repository references are qualified and are not copied.
- All cross-references resolve.
- No requirement text is duplicated across owners.
- Implementation claims appear only in implementation-status records that cite
  evidence.
