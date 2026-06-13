# Issue Tracker: GitHub

Issues live in [homosapiensagent571-design/Vestaline-ASCOMFocuser](https://github.com/homosapiensagent571-design/Vestaline-ASCOMFocuser/issues).

## Creating Issues

Use the `gh` CLI:

```bash
gh issue create --title "..." --body "..." --label "bug,needs-triage"
```

Or via the GitHub web interface.

## Reading Issues

```bash
gh issue list --label "needs-triage"
gh issue view 42
gh issue list --label "ready-for-agent"
```

## Issue Body Format

```
## Summary
[one-line description]

## Steps to reproduce (for bugs)
1.
2.
3.

## Expected behavior
[what should happen]

## Actual behavior
[what actually happens]

## Environment
- OS: Windows 11
- Driver version: 0.6.10
- NINA version: ...
```
