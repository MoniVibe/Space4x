# Scene Lockdown Checklist (Shared)

Hard invariants enforced across PureDOTS games when preparing scenes/subscenes:

- **Missing Scripts**: Prefabs, scenes, and subscenes must be free of Missing Script references. Run automated sweeps before check in.
- **Component Types**: Do not assign ScriptableObject types into ECS component slots; filenames and classes must make this impossible (authoring vs runtime split stays clean).
- **Bootstrap Location**: Gameplay subscenes host all bootstrap authoring. The main scene cannot contain bootstrap authoring MonoBehaviours.
- **Singleton Authoring Count**: Each gameplay subscene contains exactly one `PureDotsConfigAuthoring` and exactly one `SpatialPartitionAuthoring`.
- **ResourceTypeIndex Source**: `ResourceTypeIndex` data always originates from `PureDotsConfigBaker`. Bootstrap audits already enforce this; never invent ad-hoc sources.
- **Runtime Defaults**: `CoreSingletonBootstrapSystem` may create fallback singletons only when corresponding authoring was omitted, and the defaults must be explicitly defined (no implicit/null behavior).

Use this checklist during scene reviews and CI sweeps to ensure shared bootstrap guarantees stay intact.
