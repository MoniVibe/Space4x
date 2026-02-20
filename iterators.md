# iterators.md

Startup addendum for any session initialized with: `you are iterator`.

Canonical source:
- `Docs/Operations/ITERATORS.md`

Quick contract:
- One scoped goal per branch.
- Implement and hand off via PR to `main`.
- Keep PR draft until ready; then mark ready + label `needs-validate`.
- After `needs-validate`, freeze that PR branch (desktop + laptop iterators) unless validator asks for targeted follow-up on that same branch.
- Include intent card (summary, invariants, acceptance checks, risk flags, burst plan).
- Do not run greenifier/Buildbox queue loops.
- Do not merge directly.
- Do not hardcode `codex_session_id`; rely on Telebot session state (`codexsession`, `codexnew`, `codexfresh`, `codexuse` when intentional).
- Validator pickup is strict: base must be `main` (stacked `feat/* -> feat/*` PRs are ignored until retargeted to `main`).
- Skill-aware behavior:
  - Run `skills list` at startup and use matching skills when available.
  - Do not assume only two skills; also check:
    - `codexbridge/skills.ps1` root (`CODEX_SKILLS_ROOT` or `%USERPROFILE%\.codex\skills`)
    - `C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills\SKILLS_INDEX.md`
  - Telebot tip: set `CODEX_SKILLS_ROOT=C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills` if you want `skills list` to include headless skills directly.
  - For buildbox/headless/queue work, route through HeadlessRebuildTool `.agents/skills/*`.
  - If a named/matching skill is unavailable in-session, state it and continue with fallback.
- Apply the PureDOTS promotion gate from `Docs/Operations/ITERATORS.md` before coding each slice, and include `Placement` (`shared`, `game-side`, `deferred-promote`) in the PR intent card.
