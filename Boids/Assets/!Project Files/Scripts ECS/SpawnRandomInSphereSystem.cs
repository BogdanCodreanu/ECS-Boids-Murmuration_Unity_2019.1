namespace BogdanCodreanu.ECS {
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class SpawnRandomInSphereSystem : ComponentSystem {
        struct SpawnRandomInSphereInstance {
            public int spawnerIndex;
            public Entity sourceEntity;
            public float3 position;
        }

        EntityQuery m_MainGroup;

        protected override void OnCreate() {
            m_MainGroup = GetEntityQuery(
                ComponentType.ReadOnly<SpawnRandomInSphere>(),
                ComponentType.ReadOnly<LocalToWorld>());
        }

        protected override void OnUpdate() {
            List<SpawnRandomInSphere> uniqueTypes = new List<SpawnRandomInSphere>(10);

            EntityManager.GetAllUniqueSharedComponentData(uniqueTypes);

            //Debug.Log(uniqueTypes.Count);

            int spawnInstanceCount = 0;
            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++) {
                var spawner = uniqueTypes[sharedIndex];
                m_MainGroup.SetFilter(spawner);
                var entityCount = m_MainGroup.CalculateLength();
                spawnInstanceCount += entityCount;
            }

            if (spawnInstanceCount == 0)
                return;

            var spawnInstances = new NativeArray<SpawnRandomInSphereInstance>(spawnInstanceCount, Allocator.Temp);
            {
                int spawnIndex = 0;
                for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++) {
                    var spawner = uniqueTypes[sharedIndex];
                    m_MainGroup.SetFilter(spawner);

                    if (m_MainGroup.CalculateLength() == 0)
                        continue;

                    var entities = m_MainGroup.ToEntityArray(Allocator.TempJob);
                    var localToWorld = m_MainGroup.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);

                    for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++) {
                        var spawnInstance = new SpawnRandomInSphereInstance();

                        spawnInstance.sourceEntity = entities[entityIndex];
                        spawnInstance.spawnerIndex = sharedIndex;
                        spawnInstance.position = localToWorld[entityIndex].Position;

                        spawnInstances[spawnIndex] = spawnInstance;
                        spawnIndex++;
                    }

                    entities.Dispose();
                    localToWorld.Dispose();
                }
            }

            var random = new Unity.Mathematics.Random();
            random.InitState();
            for (int spawnIndex = 0; spawnIndex < spawnInstances.Length; spawnIndex++) {
                int spawnerIndex = spawnInstances[spawnIndex].spawnerIndex;
                var spawner = uniqueTypes[spawnerIndex];
                int count = spawner.count;
                var entities = new NativeArray<Entity>(count, Allocator.Temp);
                GameObject prefab = spawner.prefab;
                float radius = spawner.radius;
                var spawnPositions = new NativeArray<float3>(count, Allocator.TempJob);
                float3 center = spawnInstances[spawnIndex].position;
                var sourceEntity = spawnInstances[spawnIndex].sourceEntity;

                GeneratePoints.RandomPointsInUnitSphere(spawnPositions);

                UIControl.Instance.NrOfBoidsInitial += count;

                EntityManager.Instantiate(prefab, entities);

                for (int i = 0; i < count; i++) {
                    float3 spawnPos = spawnPositions[i];
                    spawnPos.y *= 0.2f;
                    EntityManager.SetComponentData(entities[i], new LocalToWorld {
                        Value = float4x4.TRS(
                            center + (spawnPos * radius),
                            random.NextQuaternionRotation(),
                            //quaternion.AxisAngle(new float3(0, 1, 0), Random,
                            //quaternion.LookRotationSafe(spawnPositions[i], math.up()),
                            new float3(1.0f, 1.0f, 1.0f))
                    });
                }

                EntityManager.RemoveComponent<SpawnRandomInSphere>(sourceEntity);

                spawnPositions.Dispose();
                entities.Dispose();
            }
            spawnInstances.Dispose();
        }
    }
}
