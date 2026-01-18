using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XScenarioRuntimeBridgeSystem))]
    public partial struct Space4XScenarioQuestionPackBridgeSystem : ISystem
    {
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<ScenarioHeadlessQuestionPackTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<ScenarioHeadlessQuestionPackTag>(out var sourceEntity) ||
                !state.EntityManager.HasBuffer<ScenarioHeadlessQuestionPackItem>(sourceEntity))
            {
                _done = 1;
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XHeadlessQuestionPackTag>(out var targetEntity))
            {
                targetEntity = state.EntityManager.CreateEntity(typeof(Space4XHeadlessQuestionPackTag));
            }

            var source = state.EntityManager.GetBuffer<ScenarioHeadlessQuestionPackItem>(sourceEntity);
            var target = state.EntityManager.HasBuffer<Space4XHeadlessQuestionPackItem>(targetEntity)
                ? state.EntityManager.GetBuffer<Space4XHeadlessQuestionPackItem>(targetEntity)
                : state.EntityManager.AddBuffer<Space4XHeadlessQuestionPackItem>(targetEntity);

            target.Clear();
            for (var i = 0; i < source.Length; i++)
            {
                var item = source[i];
                if (item.Id.Length == 0)
                {
                    continue;
                }

                target.Add(new Space4XHeadlessQuestionPackItem
                {
                    Id = item.Id,
                    Required = item.Required
                });
            }

            _done = 1;
        }
    }
}
