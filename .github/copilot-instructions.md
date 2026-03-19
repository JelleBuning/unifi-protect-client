# Core Philosophy
- Mission: Produce maintainable, testable, evolvable C# code.
- Priority: SOLID first, Design Patterns over "quick fixes".
- Hierarchy: Correctness > Maintainability > Performance.
- Principles: KISS, DRY, YAGNI.

# Change Discipline
1. Clarify requirements (inputs, outputs, edge cases).
2. Propose a minimal design (responsibilities, abstractions, patterns).
3. Implement in small, reversible, well-tested steps.
4. Explain tradeoffs.

# Stop and Ask
Ask before: changing public APIs, adding dependencies, choosing between multiple patterns, or impacting security/financial logic.

# Copilot-specific directives
- When responding to code or design requests, actively consult the other instruction files in `.github/instructions/` (code, architecture, testing, etc.).
- Prioritize the repository’s explicit guidelines over generic advice and ask clarifying questions if the instructions seem to conflict or are incomplete.
- Mention the relevant instruction file in your response when you use its guidance, to reinforce awareness.