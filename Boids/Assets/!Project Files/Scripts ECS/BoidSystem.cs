namespace BogdanCodreanu.ECS {

    using System.Collections.Generic;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class BoidSystem : JobComponentSystem {

        /// <summary>
        /// The group of boid entities.
        /// </summary>
        private EntityQuery BoidGroup;

        /// <summary>
        /// The group of boid targets entities.
        /// </summary>
        private EntityQuery BoidTargetsGroup;

        /// <summary>
        /// The group of boid obstacles entities.
        /// </summary>
        private EntityQuery BoidObstaclesGroup;

        private EntityCommandBufferSystem barrierCommand;

        private List<Boid> UniqueTypes = new List<Boid>(10);

        /// <summary>
        /// List of all cells data.
        /// </summary>
        private List<CellsData> _CellsData = new List<CellsData>();

        private struct CellsData {

            /// <summary>
            /// Key is the hash of the "grid block" of a boid's position.
            /// Value is boid index that is inside that position-cell.
            /// All boids that are in the same "cube" share the same key.
            /// </summary>
            public NativeMultiHashMap<int, int> hashMapBlockIndexWithBoidsIndex;

            /// <summary>
            /// v[i] = index with the data that contains information about all boids in the same cube
            /// </summary>
            public NativeArray<int> indicesOfCells;

            /// <summary>
            /// sum of directions on the specific "block cell".
            /// this must be accesed via indicesOfCells[boidIndex] to get
            /// the data which saves information about all boids in the same cell.
            /// </summary>
            public NativeArray<float3> sumOfDirectionsOnCells;

            /// <summary>
            /// sum of positions on the specific "block cell".
            /// this must be accesed via indicesOfCells[boidIndex] to get
            /// the data which saves information about all boids in the same cell.
            /// </summary>
            public NativeArray<float3> sumOfPositionsOnCells;

            /// <summary>
            /// nr of boids in this cell.
            /// this must be accesed via indicesOfCells[boidIndex] to get
            /// the data which saves information about all boids in the same cell.
            /// </summary>
            public NativeArray<int> nrOfBoidsOnCells;

            /// <summary>
            /// Positions of targets.
            /// </summary>
            public NativeArray<float3> targetsPositions;

            /// <summary>
            /// Indices of closest boid target via cells.
            /// </summary>
            public NativeArray<int> closestTargetIndices;

            /// <summary>
            /// Positions of obstacles.
            /// </summary>
            public NativeArray<float3> obstaclesPositions;

            /// <summary>
            /// Indices of closest obstacle via cells.
            /// </summary>
            public NativeArray<int> closestObstacleIndices;

            /// <summary>
            /// Distance to the closest obstacle via cells.
            /// </summary>
            public NativeArray<float> closestObstacleSqDistances;
        }

        /// <summary>
        /// Save all the entity positions in a buffer
        /// </summary>
        [BurstCompile]
        private struct CopyPositionsInBuffer : IJobForEachWithEntity<LocalToWorld> {
            public NativeArray<float3> positionsResult;

            public void Execute(Entity entity, int index, [ReadOnly]ref LocalToWorld localToWorld) {
                positionsResult[index] = localToWorld.Position;
            }
        }

        /// <summary>
        /// Sum all the boids position in a float
        /// </summary>
        [BurstCompile]
        private struct SumPositions : IJobForEach<LocalToWorld> {
            public float3 positionsSum;

            public void Execute([ReadOnly]ref LocalToWorld localToWorld) {
                positionsSum += localToWorld.Position;
            }
        }

        /// <summary>
        /// Save all the entity headings in a buffer
        /// </summary>
        [BurstCompile]
        private struct CopyHeadingsInBuffer : IJobForEachWithEntity<LocalToWorld> {
            public NativeArray<float3> headingsResult;

            public void Execute(Entity entity, int index, [ReadOnly]ref LocalToWorld localToWorld) {
                headingsResult[index] = localToWorld.Forward;
            }
        }

        [BurstCompile]
        [RequireComponentTag(typeof(Boid))]
        private struct HashPositionsToHashMap : IJobForEachWithEntity<LocalToWorld> {
            public NativeMultiHashMap<int, int>.Concurrent hashMap;
            [ReadOnly] public float3 positionOffsetVary;
            [ReadOnly] public float cellRadius;

            public void Execute(Entity entity, int boidEntityIndex, [ReadOnly]ref LocalToWorld localToWorld) {
                // get the hash of the current position block-based.
                var hash = (int)math.hash(new int3(math.floor((localToWorld.Position + positionOffsetVary) / cellRadius)));
                // tell the hashmap that the current index is inside that specific block.
                hashMap.Add(hash, boidEntityIndex);
            }
        }

        /// <summary>
        /// Job to select and calculate data for this cell in space
        /// </summary>
        [BurstCompile]
        private struct MergeCellsJob : IJobNativeMultiHashMapMergedSharedKeyIndices {
            public NativeArray<int> indicesOfCells;
            public NativeArray<float3> cellAlignment;
            public NativeArray<float3> cellPositions;
            public NativeArray<int> cellCount;
            [ReadOnly] public NativeArray<float3> targetsPositions;
            public NativeArray<int> closestTargetIndexToCells;

            [ReadOnly] public NativeArray<float3> obstaclesPositions;
            public NativeArray<int> closestObstacleIndexToCells;
            public NativeArray<float> closestObstacleSqDistanceToCells;

            public struct IntFloat {

                public IntFloat(int i1, float f1) {
                    i = i1;
                    f = f1;
                }

                public int i;
                public float f;
            }

            public IntFloat CalculateIndexOfClosestPosition(NativeArray<float3> searchedPositions, float3 position) {
                int nearestPositionIndex = 0;
                if (searchedPositions.Length == 0) {
                    return new IntFloat(-1, 0);
                }
                float nearestDistanceSq = math.lengthsq(position - searchedPositions[0]);

                for (int i = 0; i < searchedPositions.Length; i++) {
                    float3 targetPosition = searchedPositions[i];
                    float distanceToThisPos = math.lengthsq(position - searchedPositions[i]);
                    bool isThisNearer = distanceToThisPos < nearestDistanceSq;

                    nearestDistanceSq = math.select(nearestDistanceSq, distanceToThisPos, isThisNearer);
                    nearestPositionIndex = math.select(nearestPositionIndex, i, isThisNearer);
                }

                return new IntFloat(nearestPositionIndex, nearestDistanceSq);
            }

            // index is the first value encountered at a hash.
            // the key hash cannot be accessed.
            // this value is the entity index.
            public void ExecuteFirst(int firstBoidIndexEncountered) {
                indicesOfCells[firstBoidIndexEncountered] = firstBoidIndexEncountered;

                float3 positionInThisCell = cellPositions[firstBoidIndexEncountered] / cellCount[firstBoidIndexEncountered];

                // calculate index of the closest target
                var targetsResult = CalculateIndexOfClosestPosition(targetsPositions, positionInThisCell);
                closestTargetIndexToCells[firstBoidIndexEncountered] = targetsResult.i;

                var obstaclesResult = CalculateIndexOfClosestPosition(obstaclesPositions, positionInThisCell);
                closestObstacleIndexToCells[firstBoidIndexEncountered] = obstaclesResult.i;
                closestObstacleSqDistanceToCells[firstBoidIndexEncountered] = obstaclesResult.f;
            }

            // first is the value that was first encountered
            // at with the same hash as index.
            // we store all the data using the first's index.
            // these values are entity indices.
            public void ExecuteNext(int firstBoidIndexWithTheSameKey, int boidIndexEncountered) {
                // cell first contains informations about all boids in this hash.
                cellCount[firstBoidIndexWithTheSameKey] += 1;
                cellAlignment[firstBoidIndexWithTheSameKey] += cellAlignment[boidIndexEncountered];
                cellPositions[firstBoidIndexWithTheSameKey] += cellPositions[boidIndexEncountered];

                // indices of cells is used to know in wich cell is all the data stored.
                indicesOfCells[boidIndexEncountered] = firstBoidIndexWithTheSameKey;
            }
        }

        [BurstCompile]
        [RequireComponentTag(typeof(Boid))]
        private struct MoveBoids : IJobForEachWithEntity<LocalToWorld> {
            [ReadOnly] public NativeArray<int> cellIndices;
            [ReadOnly] public float separationWeight;
            [ReadOnly] public float alignmentWeight;
            [ReadOnly] public float cohesionWeight;
            [ReadOnly] public float walkToFlockCenterWeight;
            [ReadOnly] public float maintainYWeight;
            [ReadOnly] public float moveSpeed;
            [ReadOnly] public float cellSize;
            [ReadOnly] public float sphereBoundarySize;
            [ReadOnly] public float sphereBoundaryWeight;
            [ReadOnly] public float nrOfTotalBoids;
            [ReadOnly] public NativeArray<float3> cellAlignment;
            [ReadOnly] public NativeArray<float3> cellPositions;
            [ReadOnly] public NativeArray<int> cellCount;
            [ReadOnly] public float dt;

            [ReadOnly] public float3 sumOfAllPositions;
            [ReadOnly] public float yLength;
            [ReadOnly] public float perlinNoiseScale;

            [ReadOnly] public float goToTargetsWeight;
            [ReadOnly] public NativeArray<float3> targetsPositions;
            [ReadOnly] public NativeArray<int> cellClosestTargetsIndices;

            [ReadOnly] public float startAvoidingObstacleAtDistance;
            [ReadOnly] public float avoidObstaclesWeight;
            [ReadOnly] public NativeArray<float3> obstaclesPositions;
            [ReadOnly] public NativeArray<int> cellClosestObstaclesIndices;
            [ReadOnly] public NativeArray<float> cellClosestObstaclesSqDistances;
            [ReadOnly] public float avoidXZwhileHeightBiggerThan;
            [ReadOnly] public float avoidXZwhileHeightBiggerFade;
            [ReadOnly] public float obstacleKillRadius;

            [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;
            [WriteOnly] public NativeQueue<float3>.Concurrent diedPositions;

            [ReadOnly] public float terrainY;
            [ReadOnly] public float distanceToAvoidTerrain;
            [ReadOnly] public float avoidTerrainWeight;

            public void Execute(Entity entity, int index, ref LocalToWorld localToWorld) {
                var forward = localToWorld.Forward;
                var currentPosition = localToWorld.Position;

                var cellIndex = cellIndices[index]; // index of the cell that this entity is a part of
                var boidsCountInSameCell = cellCount[cellIndex];
                var sumOfAlignments = cellAlignment[cellIndex];
                var sumOfPositionsInCell = cellPositions[cellIndex];

                // -------------------- alignment
                var alignmentResult = math.normalizesafe((sumOfAlignments - forward) / (boidsCountInSameCell - 1)) * alignmentWeight;

                var cohesionResult = float3.zero;

                // -------------------- separation and cohesion
                var separationResult = float3.zero;
                if (boidsCountInSameCell > 1) {
                    var neighbourMiddle = (sumOfPositionsInCell - currentPosition) / (boidsCountInSameCell - 1);

                    float distanceToMiddleSq = math.lengthsq(neighbourMiddle - currentPosition);
                    float maxDistanceToMiddleSq = cellSize * cellSize;

                    float distanceClamped = distanceToMiddleSq / maxDistanceToMiddleSq;
                    float needToLeave = 1 - distanceClamped;
                    //Debug.Log($"{index}:  {distanceClamped}");

                    separationResult = math.normalizesafe(currentPosition - neighbourMiddle) * separationWeight *
                        needToLeave;

                    cohesionResult = math.normalizesafe(neighbourMiddle - currentPosition) * cohesionWeight;
                }

                // -------------------- distance from center limit
                var limitCenter = float3.zero;
                var centerOffset = limitCenter - currentPosition;
                var limitBoundaryFactor = math.length(centerOffset) / sphereBoundarySize;

                var boundaryResult = math.select((limitCenter - currentPosition) * math.pow(limitBoundaryFactor - .5f, 4),
                    float3.zero, limitBoundaryFactor < .5f) * sphereBoundaryWeight;

                // -------------------- walk to center of flock
                var walkToFlockCenterResult = (sumOfAllPositions / nrOfTotalBoids - currentPosition) *
                    walkToFlockCenterWeight;

                // -------------------- y limitations
                float flockYAvg = (sumOfAllPositions / nrOfTotalBoids).y;
                float yDifferenceFromAvg = (flockYAvg - currentPosition.y);
                float distanceFromY = math.abs(flockYAvg - currentPosition.y);

                float noiseResultFromPos =
                    noise.cnoise(new float2(currentPosition.x, currentPosition.z) * perlinNoiseScale) + .2f;

                var maintainYResult = (new float3(0, yDifferenceFromAvg, 0))
                    * maintainYWeight * ((distanceFromY - noiseResultFromPos * yLength) * 0.1f);

                maintainYResult = math.select(maintainYResult, float3.zero,
                    distanceFromY < noiseResultFromPos * yLength);

                // -------------------- walk to targets positions
                int indexOfClosestTarget = cellClosestTargetsIndices[cellIndex];
                float3 walkToTargetsResult = float3.zero;
                if (indexOfClosestTarget >= 0) {
                    walkToTargetsResult = math.normalizesafe(targetsPositions[indexOfClosestTarget] - currentPosition) *
                        goToTargetsWeight;
                }

                // -------------------- avoid obstacles
                int indexOfClosestObstacle = cellClosestObstaclesIndices[cellIndex];
                float3 avoidObstacleResult = float3.zero;
                if (indexOfClosestObstacle >= 0) {
                    float sqDistanceNeeded = startAvoidingObstacleAtDistance * startAvoidingObstacleAtDistance;
                    float sqDistance = cellClosestObstaclesSqDistances[cellIndex];
                    if (sqDistance <= sqDistanceNeeded) {
                        float needToEvade = 1 - (sqDistance / sqDistanceNeeded);
                        needToEvade *= needToEvade;
                        float3 closestObstaclePosition = obstaclesPositions[indexOfClosestObstacle];
                        float heightDifference = math.abs(closestObstaclePosition.y - currentPosition.y);
                        float sqDistanceReal = math.lengthsq(currentPosition - closestObstaclePosition);

                        float3 positionWithSameHeightAsI =
                            new float3(obstaclesPositions[indexOfClosestObstacle].x,
                            currentPosition.y, closestObstaclePosition.z);

                        // this is the basic avoidance result.
                        avoidObstacleResult =
                            math.normalizesafe(currentPosition - closestObstaclePosition) *
                            avoidObstaclesWeight * needToEvade;

                        if (sqDistanceReal <= obstacleKillRadius * obstacleKillRadius) {
                            diedPositions.Enqueue(currentPosition);
                            commandBuffer.DestroyEntity(index, entity);
                        }

                        // here is the logic of adding xz avoidance while the obstacle is on a big Y diff
                        if (heightDifference > avoidXZwhileHeightBiggerThan) {
                            float needToUseXZ =
                                math.clamp((heightDifference - avoidXZwhileHeightBiggerThan) /
                                avoidXZwhileHeightBiggerFade, 0, 1);

                            avoidObstacleResult +=
                                math.normalizesafe(currentPosition - positionWithSameHeightAsI) * avoidObstaclesWeight
                                * needToEvade * needToUseXZ;
                        }
                    }
                }

                // -------------------- avoid terrain
                float distanceToTerrain = math.abs(currentPosition.y - terrainY);
                float3 avoidTerrainResult = float3.zero;
                if (distanceToTerrain <= distanceToAvoidTerrain) {
                    float goUpResult = (1 - distanceToTerrain / distanceToAvoidTerrain);
                    goUpResult = math.pow(goUpResult, 3);
                    avoidTerrainResult = new float3(0, goUpResult * avoidTerrainWeight, 0);
                }

                // -------------------- final result -------------
                var headingResult = math.normalizesafe(forward +
                    dt * (alignmentResult + separationResult + cohesionResult + boundaryResult +
                    walkToFlockCenterResult + maintainYResult + walkToTargetsResult + avoidObstacleResult +
                    avoidTerrainResult));

                // if avoiding obstacle then change heading result
                //if (isAvoiding) {
                //    headingResult = math.normalizesafe(forward +
                //        dt * (separationResult +
                //        avoidObstacleResult));
                //}

                localToWorld = new LocalToWorld {
                    Value = float4x4.TRS(
                        new float3(localToWorld.Position + (headingResult * moveSpeed * dt)),
                        quaternion.LookRotationSafe(headingResult, math.up()),
                        new float3(2.0f, 2.0f, 2.0f))
                };
            }
        }

        private void DisposeCellData(CellsData cell) {
            cell.hashMapBlockIndexWithBoidsIndex.Dispose();
            cell.indicesOfCells.Dispose();
            cell.sumOfDirectionsOnCells.Dispose();
            cell.sumOfPositionsOnCells.Dispose();
            cell.nrOfBoidsOnCells.Dispose();
            cell.targetsPositions.Dispose();
            cell.closestTargetIndices.Dispose();

            cell.obstaclesPositions.Dispose();
            cell.closestObstacleIndices.Dispose();
            cell.closestObstacleSqDistances.Dispose();
        }

        protected override void OnStopRunning() {
            for (var i = 0; i < _CellsData.Count; i++) {
                DisposeCellData(_CellsData[i]);
            }
            _CellsData.Clear();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            var settings = ECSController.FlockParams;
            var gameSettings = GlobalSettings.Instance;

            EntityManager.GetAllUniqueSharedComponentData(UniqueTypes);

            int targetsCount = BoidTargetsGroup.CalculateLength();
            int obstaclesCount = BoidObstaclesGroup.CalculateLength();
            UIControl.Instance.NrOfObstacles = obstaclesCount;

            // Ignore typeIndex 0, can't use the default for anything meaningful.
            for (int typeIndex = 1; typeIndex < UniqueTypes.Count; typeIndex++) {
                Boid boid = UniqueTypes[typeIndex];
                BoidGroup.SetFilter(boid);

                var boidCount = BoidGroup.CalculateLength();
                UIControl.Instance.NrOfBoidsAlive = boidCount;

                var cacheIndex = typeIndex - 1;
                // containers that store all the data.
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var hashMap = new NativeMultiHashMap<int, int>(boidCount, Allocator.TempJob);
                var cellCount = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var cellAlignment = new NativeArray<float3>(boidCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                var cellPositions = new NativeArray<float3>(boidCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                var targetsPositions = new NativeArray<float3>(targetsCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                var closestTargetIndices = new NativeArray<int>(boidCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                var obstaclesPositions = new NativeArray<float3>(obstaclesCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                var closestObstacleIndices = new NativeArray<int>(boidCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                var closestObstacleSqDistances = new NativeArray<float>(boidCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                float3 sumOfAllBoidsPositions = float3.zero;

                // copy values to buffers.
                var initialCellAlignmentJob = new CopyHeadingsInBuffer {
                    headingsResult = cellAlignment
                };
                var initialCellAlignmentJobHandle = initialCellAlignmentJob.Schedule(BoidGroup, inputDeps);

                var initialCopyPositionJob = new CopyPositionsInBuffer {
                    positionsResult = cellPositions
                };
                var initialCopyPositionJobHandle = initialCopyPositionJob.Schedule(BoidGroup, inputDeps);

                var sumPositionsJob = new SumPositions {
                    positionsSum = sumOfAllBoidsPositions
                };
                var sumPositionsJobHandle = sumPositionsJob.Schedule(BoidGroup, inputDeps);

                // copy targets positions
                var copyPositionsOfTargetsJob = new CopyPositionsInBuffer {
                    positionsResult = targetsPositions
                };
                var copyPositionsOfTargetsJobHandle = copyPositionsOfTargetsJob.Schedule(BoidTargetsGroup, inputDeps);

                // copy obstacles positions
                var copyPositionsOfObstaclesJob = new CopyPositionsInBuffer {
                    positionsResult = obstaclesPositions
                };
                var copyPositionsOfObstaclesJobHandle = copyPositionsOfObstaclesJob.Schedule(BoidObstaclesGroup, inputDeps);

                var newCellData = new CellsData {
                    indicesOfCells = cellIndices,
                    hashMapBlockIndexWithBoidsIndex = hashMap,
                    sumOfDirectionsOnCells = cellAlignment,
                    sumOfPositionsOnCells = cellPositions,
                    nrOfBoidsOnCells = cellCount,
                    targetsPositions = targetsPositions,
                    closestTargetIndices = closestTargetIndices,
                    closestObstacleIndices = closestObstacleIndices,
                    closestObstacleSqDistances = closestObstacleSqDistances,
                    obstaclesPositions = obstaclesPositions,
                };

                if (cacheIndex > (_CellsData.Count - 1)) {
                    _CellsData.Add(newCellData);
                } else {
                    DisposeCellData(_CellsData[cacheIndex]);
                }
                _CellsData[cacheIndex] = newCellData;

                // hash the entity position
                var hashPositionsJob = new HashPositionsToHashMap {
                    hashMap = hashMap.ToConcurrent(),
                    cellRadius = ECSController.Instance.CellSizeVaried,
                    positionOffsetVary = ECSController.Instance.PositionNeighbourCubeOffset
                };
                var hashPositionsJobHandle = hashPositionsJob.Schedule(BoidGroup, inputDeps);

                // set all cell count to 1.
                var initialCellCountJob = new MemsetNativeArray<int> {
                    Source = cellCount,
                    Value = 1
                };
                var initialCellCountJobHandle = initialCellCountJob.Schedule(boidCount, 64, inputDeps);

                // bariers. from now on we need to use the created buffers.
                // and we need to know that they are finished.
                var initialCellBarrierJobHandle = JobHandle.CombineDependencies(
                    initialCellAlignmentJobHandle, initialCopyPositionJobHandle, initialCellCountJobHandle);
                var mergeCellsBarrierJobHandle = JobHandle.CombineDependencies(
                    hashPositionsJobHandle, initialCellBarrierJobHandle, sumPositionsJobHandle);
                var targetsJobHandle = JobHandle.CombineDependencies(mergeCellsBarrierJobHandle,
                    copyPositionsOfTargetsJobHandle, copyPositionsOfObstaclesJobHandle);

                var mergeCellsJob = new MergeCellsJob {
                    indicesOfCells = cellIndices,
                    cellAlignment = cellAlignment,
                    cellPositions = cellPositions,
                    cellCount = cellCount,
                    targetsPositions = targetsPositions,
                    closestTargetIndexToCells = closestTargetIndices,
                    closestObstacleSqDistanceToCells = closestObstacleSqDistances,
                    closestObstacleIndexToCells = closestObstacleIndices,
                    obstaclesPositions = obstaclesPositions
                };
                // job now depends on last barrier.
                var mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap, 64, targetsJobHandle);
                EntityCommandBuffer.Concurrent commandBuffer = barrierCommand.CreateCommandBuffer().ToConcurrent();
                NativeQueue<float3> killedPositionsQueue = new NativeQueue<float3>(Allocator.TempJob);

                var steerJob = new MoveBoids {
                    cellIndices = newCellData.indicesOfCells,
                    alignmentWeight = gameSettings.AlignmentWeight,
                    separationWeight = gameSettings.SeparationWeight,
                    cohesionWeight = gameSettings.CohesionWeight,
                    cellSize = ECSController.Instance.CellSizeVaried,
                    sphereBoundarySize = gameSettings.SphereBoundarySize,
                    sphereBoundaryWeight = gameSettings.BoundaryWeight,
                    moveSpeed = gameSettings.MoveSpeed,
                    cellAlignment = cellAlignment,
                    cellPositions = cellPositions,
                    cellCount = cellCount,
                    dt = Time.deltaTime,
                    walkToFlockCenterWeight = gameSettings.WalkToFlockCenterWeight,
                    sumOfAllPositions = sumOfAllBoidsPositions,
                    nrOfTotalBoids = boidCount,
                    maintainYWeight = gameSettings.maintainYWeight,
                    yLength = gameSettings.yLength,
                    perlinNoiseScale = settings.perlinNoiseScale,
                    targetsPositions = targetsPositions,
                    cellClosestTargetsIndices = closestTargetIndices,
                    goToTargetsWeight = gameSettings.goToTargetsWeight,
                    obstaclesPositions = obstaclesPositions,
                    cellClosestObstaclesIndices = closestObstacleIndices,
                    cellClosestObstaclesSqDistances = closestObstacleSqDistances,
                    startAvoidingObstacleAtDistance = gameSettings.avoidDistanceObstacles,
                    avoidObstaclesWeight = gameSettings.avoidObstaclesWeight,
                    terrainY = ECSController.TerrainY,
                    distanceToAvoidTerrain = settings.distanceToAvoidTerrain,
                    avoidTerrainWeight = gameSettings.avoidTerrainWeight,
                    avoidXZwhileHeightBiggerThan = settings.avoidXZwhileHeightBiggerThan,
                    avoidXZwhileHeightBiggerFade = settings.avoidXZwhileHeightBiggerFade,
                    obstacleKillRadius = settings.obstacleKillRadius,
                    commandBuffer = commandBuffer,
                    diedPositions = killedPositionsQueue.ToConcurrent(),
                };
                // job depends on merge cells job
                var steerJobHandle = steerJob.Schedule(BoidGroup, mergeCellsJobHandle);

                barrierCommand.AddJobHandleForProducer(steerJobHandle);
                steerJobHandle.Complete();

                if (killedPositionsQueue.TryDequeue(out float3 pos)) {
                    GameController.Instance.KilledBoidAt(pos);
                }

                killedPositionsQueue.Dispose();
                inputDeps = steerJobHandle;
                BoidGroup.AddDependency(inputDeps);
            }
            UniqueTypes.Clear();

            return inputDeps;
        }

        protected override void OnCreate() {
            BoidGroup = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Boid>(), ComponentType.ReadWrite<LocalToWorld>() },
                Options = EntityQueryOptions.FilterWriteGroup
            });

            BoidTargetsGroup = GetEntityQuery(
                ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<BoidTarget>());

            BoidObstaclesGroup = GetEntityQuery(
                ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<BoidObstacle>());

            barrierCommand = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

            //BoidGroup = GetEntityQuery(ComponentType.ReadWrite<LocalToWorld>(), ComponentType.ReadOnly<Boid>());
        }
    }
}