# Architecture: Clean / Onion
- Domain: entities, value objects, domain services, events (no infrastructure dependencies).
- Application: use-cases, orchestration, ports (interfaces), DTOs, handlers.
- Infrastructure: EF Core, HTTP clients, implementations (hide these from domain).
- Presentation: Web API / UI.

# DDD Standards
- Ubiquitous Language.
- Aggregates: Use root entities to maintain consistency boundaries.
- Value Objects: Prefer immutable objects for identity-less data.
- Domain Services: For logic that doesn't fit in an entity.
- Bounded Contexts: Clear separation using ACLs if necessary.