namespace BogdanCodreanu.ECS {
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Burst;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    [UpdateBefore(typeof(TransformSystemGroup))]
    public class ProjectileSystem : JobComponentSystem {
        private EntityQuery MainGroup;
        private EntityCommandBufferSystem barrier;

        [BurstCompile]
        public struct MoveProjectilesJob : IJobForEachWithEntity<LocalToWorld, SpawnProjectile> {
            [ReadOnly] public float dt;
            [ReadOnly] public float terrainY;
            [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

            public void Execute(Entity entity, int index, ref LocalToWorld localToWorld,
                [ReadOnly] ref SpawnProjectile projectile) {

                float3 forward = localToWorld.Forward;
                if (localToWorld.Position.y <= terrainY) {
                    commandBuffer.DestroyEntity(index, entity);
                }

                localToWorld = new LocalToWorld {
                    Value = float4x4.TRS(
                        localToWorld.Position + forward * projectile.speed * dt,
                        quaternion.LookRotationSafe(forward, localToWorld.Up),
                        new float3(1, 1, 1)
                        )
                };
            }
        }
        
        protected override void OnCreate() {
            MainGroup = GetEntityQuery(
                ComponentType.ReadOnly<SpawnProjectile>(),
                ComponentType.ReadWrite<LocalToWorld>());
            barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            int projectilesCount = MainGroup.CalculateLength();
            EntityCommandBuffer.Concurrent commandBuffer = barrier.CreateCommandBuffer().ToConcurrent();

            var moveProjectilesJob = new MoveProjectilesJob {
                dt = Time.deltaTime,
                commandBuffer = commandBuffer,
                terrainY = ECSController.TerrainY
            };
            var moveProjectilesJobHandle = moveProjectilesJob.Schedule(MainGroup, inputDeps);
            barrier.AddJobHandleForProducer(moveProjectilesJobHandle);
            return moveProjectilesJobHandle;
        }
    }
}
