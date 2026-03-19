# Testing Strategy
- Unit tests: Business logic.
- Integration tests: Persistence/HTTP layers.
- Pattern: Arrange-Act-Assert (AAA).
- Goal: Test behavior, not implementation.
- Abstractions: Inject interfaces for ISystemClock, IRandom, and IO.
- TDD: Add/adjust tests before implementation when feasible.