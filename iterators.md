# iterators.md

Startup addendum for any session initialized with: `you are iterator`.

Canonical source:
- `Docs/Operations/ITERATORS.md`

Quick contract:
- One scoped goal per branch.
- Implement and hand off via PR to `main`.
- Keep PR draft until ready; then mark ready + label `needs-validate`.
- Include intent card (summary, invariants, acceptance checks, risk flags, burst plan).
- Do not run greenifier/Buildbox queue loops.
- Do not merge directly.
- Do not hardcode `codex_session_id`; rely on Telebot session state (`codexsession`, `codexnew`, `codexfresh`, `codexuse` when intentional).
