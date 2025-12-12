# TRI Stub Catalog

Track ahead-of-time stub assets so agents know what exists and what they replace later.

- **File**: `Assets/Scripts/Space4x/Stubs/Space4XConceptStubComponents.cs`
  - **Module**: Carrier loops, logistics, tech diffusion, sensors/perception, formations, nav/path requests, economy/crafting hooks, behavior/needs, aggregates (fleets/guilds), time/narrative, telemetry/save references, interception.
  - **Types**: `MiningOrderTag`, `HaulOrderTag`, `ExplorationProbeTag`, `FleetInterceptIntent`, `ComplianceBreachEvent`, `TechDiffusionNode`, `StationConstructionSite`, `AnomalyTag`, `TradeContractHandle`, `CarrierBehaviorTreeRef`, `CarrierBehaviorState`, `CarrierBehaviorNodeState`, `CarrierPerceptionConfig`, `CarrierPerceptionStimulus`, `CarrierHangarState`, `CarrierBehaviorModifier`, `CarrierInitiativeState`, `CarrierNeedElement`, `StrikeCraftSpawnRequest`, `FormationAnchor`, `AttackRunIntent`, `AttackRunProgress`, `TravelRequest`, `ArrivalWaypoint`, `NavigationTicketRef`, `MovementProfileId`, `CarrierSensorRig`, `CarrierInterruptTicket`, `Space4XTimeCommand`, `Space4XBookmark`, `Space4XSituationAnchor`, `TelemetryStreamHook`, `SaveSlotRequest`, `StationProductionSlot`, `RefineryJobRequest`, `TradeLedgerEntry`, `CarrierDeliveryManifest`, `FleetHandle`, `FleetMembershipElement`, `GuildHandle`, `ModuleCraftJob`, `CraftedItemHandle`, `CraftQualityState`, `InterceptState`.
  - **Intent**: Provide DOTS-safe tags/handles for mining/haul/exploration/compliance/tech systems plus future behavior-tree/perception, fighter launch/attack-run wiring, navigation ticketing, sensor/time/narrative wiring, telemetry streams, and economy slots/ledger hooks.

- **File**: `Assets/Scripts/Space4x/Stubs/Space4XServiceBridges.cs`
  - **Module**: Game-side bridges into PureDOTS services (nav, combat, economy, diplomacy, telemetry, sensors, time, narrative, persistence, behavior, decision, ambition, interception, communication, trade).
  - **Types**: `Space4XNavigationBridgeStub`, `Space4XCombatBridgeStub`, `Space4XEconomyBridgeStub`, `Space4XDiplomacyBridgeStub`, `Space4XTelemetryBridgeStub`, `Space4XSensorBridgeStub`, `Space4XTimeBridgeStub`, `Space4XNarrativeBridgeStub`, `Space4XSaveLoadBridgeStub`, `Space4XBehaviorBridgeStub`, `Space4XDecisionBridgeStub`, `Space4XAmbitionBridgeStub`, `Space4XInterceptBridgeStub`, `Space4XCommunicationBridgeStub`, `Space4XTradeBridgeStub`.
  - **Intent**: Provide canonical touchpoints for gameplay code to call PureDOTS APIs while implementations are stubbed.
  - **Owner**: Space4X gameplay.
