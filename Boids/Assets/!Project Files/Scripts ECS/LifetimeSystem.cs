namespace BogdanCodreanu.ECS {
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Burst;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;


    public class LifetimeSystem : JobComponentSystem {
        private EntityCommandBufferSystem barrier;

        protected override void OnCreate() {
            barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public struct LifeTimeJob : IJobForEachWithEntity<Lifetime> {
            [ReadOnly] public float dt;
            [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

            public void Execute(Entity entity, int index, ref Lifetime lifetime) {
                lifetime.value -= dt;
                if (lifetime.value <= 0) {
                    commandBuffer.DestroyEntity(index, entity);
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            EntityCommandBuffer.Concurrent commandBuffer = barrier.CreateCommandBuffer().ToConcurrent();

            var lifeTimeJobHandle = new LifeTimeJob {
                commandBuffer = commandBuffer,
                dt = Time.deltaTime
            }.Schedule(this, inputDeps);

            barrier.AddJobHandleForProducer(lifeTimeJobHandle);

            return lifeTimeJobHandle;
        }
    }
}
