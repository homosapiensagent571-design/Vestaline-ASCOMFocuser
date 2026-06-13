# Triage Labels

All labels use the default canonical names.

## State Labels

| Label | Meaning |
|-------|---------|
| `needs-triage` | Maintainer needs to evaluate |
| `needs-info` | Waiting on reporter for more information |
| `ready-for-agent` | Fully specified, safe for AI agent to implement |
| `ready-for-human` | Needs human implementation (judgment, hardware, design) |
| `wontfix` | Will not be actioned |

## Category Labels

| Label | Meaning |
|-------|---------|
| `bug` | Something is broken |
| `enhancement` | New feature or improvement |

## Rules

- Every issue carries exactly one category label and one state label
- New, unlabeled issues start as `needs-triage`
- From `needs-triage`, move to `needs-info`, `ready-for-agent`, `ready-for-human`, or `wontfix`
- `needs-info` returns to `needs-triage` once reporter replies
