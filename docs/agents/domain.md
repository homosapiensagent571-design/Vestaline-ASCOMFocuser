# Domain Docs

## Layout: Single-Context

This repo has one global domain:
- **Glossary**: `CONTEXT.md` at repo root
- **Architecture Decision Records**: `docs/adr/`

## Consumer Rules

Skills that read these files:
- `mp-architecture` — uses glossary to name modules, ADRs to avoid re-litigating past decisions
- `mp-diagnose` — uses glossary for mental model of relevant modules
- `mp-tdd` — uses glossary to name tests with domain language
- `mp-grill-docs` — challenges against glossary, sharpens terms, creates ADRs

## Creating Docs

- **CONTEXT.md**: Create when the first domain term is resolved during a `/mp-grill-docs` session. Contains only a glossary — no implementation details, no specs.
- **ADRs**: Create when a decision is (1) hard to reverse, (2) surprising without context, and (3) the result of a real trade-off.

## File Structure

```
/
├── CONTEXT.md              # Domain glossary
├── docs/
│   ├── adr/                # Architecture decisions
│   │   ├── 0001-xxx.md
│   │   └── ...
│   └── agents/             # Agent configuration
│       ├── issue-tracker.md
│       ├── triage-labels.md
│       └── domain.md
└── AGENTS.md               # Project context + Agent skills block
```
