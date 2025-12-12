Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Features.Time.Space4XTimeOfDayAdapterSystem targeting PureDOTS.Systems.Time.TimeOfDaySystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.TimeSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:299)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.Rendering.FreezeStaticLODObjects targeting Unity.Rendering.LODRequirementsUpdateSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:471)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.Rendering.UpdateSceneBoundingVolumeFromRendererBounds targeting Unity.Rendering.RenderBoundsUpdateSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:471)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Registry.Space4XCaptainAlignmentBehaviorSystem targeting Space4X.Registry.Space4XCaptainReadinessSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XFocusMoraleIntegrationSystem targeting Space4X.Registry.Space4XMoraleSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XFocusDepartmentIntegrationSystem targeting Space4X.Registry.Space4XDepartmentSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XFocusRepairIntegrationSystem targeting Space4X.Registry.Space4XFieldRepairHullSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Registry.Space4XModuleRatingAggregationSystem targeting Space4X.Registry.Space4XModuleStatAggregationSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Registry.Space4XModuleTelemetryAggregationSystem targeting Space4X.Registry.Space4XModuleMaintenanceTelemetrySystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XGrudgeMoraleIntegrationSystem targeting Space4X.Registry.Space4XMoraleSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XTargetPrioritySystem targeting Space4X.Registry.Space4XCaptainOrderSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on PureDOTS.Systems.Navigation.NavKnowledgeGraphBuilderSystem targeting PureDOTS.Systems.Navigation.NavGraphStateUpdateSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Registry.Space4XAutomationCleanupSystem targeting Space4X.Registry.Space4XAutomationSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XFocusCombatIntegrationSystem targeting Space4X.Registry.Space4XWeaponSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XFocusTargetingIntegrationSystem targeting Space4X.Registry.Space4XTargetPrioritySystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XFocusShieldIntegrationSystem targeting Space4X.Registry.Space4XShieldRegenSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Space4X.Registry.Space4XResourceLevelUpdateSystem targeting Space4X.Registry.Space4XRecallSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Climate.Systems.BioDeckSystem targeting PureDOTS.Systems.Environment.ClimateControlSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.GameplaySystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Registry.Space4XMiningMovementBridgeSystem targeting Space4X.Registry.Space4XMinerMiningSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Space4X.Systems.AI.Space4XTransportAISystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Space4X.Systems.AI.VesselMovementSystem targeting Space4X.Systems.AI.Space4XTransportAISystemGroup.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemSorter.cs:471)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:300)
Unity.Entities.DefaultWorldInitialization:AddSystemsToRootLevelSystemGroups (Unity.Entities.World,System.Collections.Generic.IReadOnlyList`1<System.Type>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:191)
PureDOTS.Systems.PureDotsWorldBootstrap:Initialize (string) (at C:/Users/Moni/Documents/claudeprojects/unity/PureDOTS/Packages/com.moni.puredots/Runtime/Systems/PureDotsWorldBootstrap.cs:39)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:139)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

