# Coding Standards
- Modern C#: Use records, pattern matching, init-only properties.
- Immutability: Prefer by default.
- Collections: Use IReadOnlyList/Collection for public APIs.
- Async: No .Result or .Wait(). Use CancellationToken.
- Guard Clauses: Validate arguments.

# Patterns & Error Handling
- Use patterns (Strategy, Factory, Decorator, etc.) intentionally.
- Results: Use explicit Result<T> or ErrorOr<T> types.
- Exceptions: Only for truly exceptional conditions.

# Performance
- No micro-optimization.
- Use Span<T> or IAsyncEnumerable<T> only when justified and readable.