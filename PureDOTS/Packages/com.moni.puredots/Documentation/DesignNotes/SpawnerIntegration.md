# Spawner Integration Concepts

- Queue spawn requests by initiator type (`Building`, `Transport`, `NaturalCycle`) inside `SpawnerRegistrySystem`. Each request stores context so domain-specific systems decide when to instantiate instead of immediate creation.
- Housing births: add a `BirthQueue` buffer to housing entities and a `VillageBirthSystem` in `VillagerSystemGroup` that consumes the queue, validates capacity/stats, and forwards spawn requests through the shared spawner.
- Fleet arrivals: emit `ShipArrivalEvent` records from transport adapters, consume them in a `DisembarkationSystem`, and create queued spawns flagged as `Transport` initiators so villagers emerge at docks with predefined roles.
- Raiding parties: mirror the fleet flow with `RaidIntent` data; delay spawn release until raid conditions trigger, and tag new units for hostile AI routing.
- Fauna and vegetation: drive “pop-in” through `EnvironmentSpawnCycle` data on regions; vegetation/creature systems spend spawn budget over time and instantiate only when terrain slots satisfy environmental requirements.
