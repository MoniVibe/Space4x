# Economy Spine & Principles

> **Note:** This is a reference copy. The canonical source is [`Godgame/Docs/Concepts/Economy/Economy_Spine_And_Principles.md`](../../../../Godgame/Docs/Concepts/Economy/Economy_Spine_And_Principles.md).  
> **When updating:** Update the Godgame version first, then sync this copy.

**Type:** Foundation Document  
**Status:** Foundation – applies to all economy work  
**Version:** Chunk 0  
**Scope:** Wealth, resources, businesses, trade, markets, policies (Godgame + Space4X)  
**Dependencies:** None (this is the foundation)  
**Last Updated:** 2025-01-27

---

## Purpose

The economic layer has grown complex: personal wealth, businesses, families, guilds, villages, resources with mass, trade routes, sanctions, etc.

**Chunk 0 exists to:**

- **Prevent "ghost money" and "ghost goods"** – All wealth and items must exist in explicit components, never as hidden floats or magical numbers
- **Avoid tangled dependencies** – Clear layer boundaries prevent wealth, items, trade, politics, and AI from becoming unmaintainable spaghetti
- **Make the economy moddable** – Behavior described in data catalogs, not hardcoded magic numbers
- **Make the economy predictable** – Deterministic, rewind-safe, observable
- **Make the economy debuggable** – Events and logs for all significant changes
- **Provide a simple checklist** – "Is this new system legal?" validation for future work

**This document has no code.** It is a set of rules and constraints that every later system (Chunks 1–6) must respect.

---

> **Full content:** See the canonical source document at [`Godgame/Docs/Concepts/Economy/Economy_Spine_And_Principles.md`](../../../../Godgame/Docs/Concepts/Economy/Economy_Spine_And_Principles.md) for complete principles, examples, and guidelines.

---

**Last Updated:** 2025-01-27  
**Maintainer:** Economy Architecture Team  
**Status:** Foundation – All future economy work must comply with these principles












