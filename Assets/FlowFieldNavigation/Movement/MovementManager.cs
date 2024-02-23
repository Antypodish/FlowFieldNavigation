using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class MovementManager
    {
        AgentDataContainer _agentDataContainer;
        FlowFieldNavigationManager _navigationManager;

        internal NativeList<AgentMovementData> AgentMovementDataList;
        internal NativeList<float3> AgentPositionChangeBuffer;
        internal NativeList<RoutineResult> RoutineResults;
        internal NativeList<int> NormalToHashed;
        internal NativeList<int> HashedToNormal;
        internal NativeArray<UnsafeList<HashTile>> HashGridArray;
        internal NativeList<float> PathReachDistances;

        NativeArray<int2> _hashGridColAndRowAmounts;
        NativeList<UnsafeListReadOnly<byte>> _costFieldCosts;
        NativeList<float3> _agentPositions;
        JobHandle _routineHandle;
        internal MovementManager(AgentDataContainer agentDataContainer, FlowFieldNavigationManager navigationManager)
        {
            _agentDataContainer = agentDataContainer;
            _navigationManager = navigationManager;
            AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
            RoutineResults = new NativeList<RoutineResult>(Allocator.Persistent);
            AgentPositionChangeBuffer = new NativeList<float3>(Allocator.Persistent);
            _agentPositions = new NativeList<float3>(Allocator.Persistent);
            _costFieldCosts = new NativeList<UnsafeListReadOnly<byte>>(Allocator.Persistent);
            _routineHandle = new JobHandle();
            //SPATIAL HASH GRID INSTANTIATION
            int gridAmount = (int)math.ceil(FlowFieldUtilities.MaxAgentSize / FlowFieldUtilities.BaseAgentSpatialGridSize);
            HashGridArray = new NativeArray<UnsafeList<HashTile>>(gridAmount, Allocator.Persistent);
            _hashGridColAndRowAmounts = new NativeArray<int2>(HashGridArray.Length, Allocator.Persistent);
            for (int i = 0; i < HashGridArray.Length; i++)
            {
                float fieldHorizontalSize = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
                float fieldVerticalSize = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize;

                float gridTileSize = i * FlowFieldUtilities.BaseAgentSpatialGridSize + FlowFieldUtilities.BaseAgentSpatialGridSize;
                int gridColAmount = (int)math.ceil(fieldHorizontalSize / gridTileSize);
                int gridRowAmount = (int)math.ceil(fieldVerticalSize / gridTileSize);
                int gridSize = gridColAmount * gridRowAmount;
                UnsafeList<HashTile> grid = new UnsafeList<HashTile>(gridSize, Allocator.Persistent);
                grid.Length = gridSize;
                HashGridArray[i] = grid;
                _hashGridColAndRowAmounts[i] = new int2(gridColAmount, gridRowAmount);
            }
            NormalToHashed = new NativeList<int>(Allocator.Persistent);
            HashedToNormal = new NativeList<int>(Allocator.Persistent);
            PathReachDistances = new NativeList<float>(Allocator.Persistent);
        }
        public void DisposeAll()
        {
            AgentMovementDataList.Dispose();
            AgentPositionChangeBuffer.Dispose();
            RoutineResults.Dispose();
            NormalToHashed.Dispose();
            HashedToNormal.Dispose();
            for (int i = 0; i < HashGridArray.Length; i++)
            {
                UnsafeList<HashTile> hashGrid = HashGridArray[i];
                hashGrid.Dispose();
            }
            HashGridArray.Dispose();
            PathReachDistances.Dispose();
        }
        internal void ScheduleRoutine(float deltaTime, JobHandle dependency)
        {
            TransformAccessArray agentTransforms = _navigationManager.AgentDataContainer.AgentTransforms;
            NativeArray<AgentData> agentDataArray = _agentDataContainer.AgentDataList.AsArray();
            NativeArray<float> agentRadii = _agentDataContainer.AgentRadii.AsArray();
            NativeArray<int> agentCurPathIndexArray = _agentDataContainer.AgentCurPathIndicies.AsArray();
            NativeArray<PathLocationData> exposedPathLocationDataArray = _navigationManager.PathDataContainer.ExposedPathLocationData.AsArray();
            NativeArray<PathFlowData> exposedPathFlowDataArray = _navigationManager.PathDataContainer.ExposedPathFlowData.AsArray();
            NativeArray<float2> exposedPathDestinationArray = _navigationManager.PathDataContainer.ExposedPathDestinations.AsArray();
            NativeArray<int> agentFlockIndexArray = _navigationManager.AgentDataContainer.AgentFlockIndicies.AsArray();
            NativeArray<float> exposedPathReachDistanceCheckRanges = _navigationManager.PathDataContainer.ExposedPathReachDistanceCheckRanges.AsArray();
            NativeArray<int> exposedPathFlockIndicies = _navigationManager.PathDataContainer.ExposedPathFlockIndicies.AsArray();
            NativeArray<PathState> exposedPathStateList = _navigationManager.PathDataContainer.ExposedPathStateList.AsArray();
            NativeArray<bool> exposedPathAgentStopFlagList = _navigationManager.PathDataContainer.ExposedPathAgentStopFlagList.AsArray();

            //CLEAR
            AgentMovementDataList.Clear();
            AgentPositionChangeBuffer.Clear();
            RoutineResults.Clear();
            NormalToHashed.Clear();
            AgentMovementDataList.Length = agentDataArray.Length;
            RoutineResults.Length = agentDataArray.Length;
            AgentPositionChangeBuffer.Length = agentDataArray.Length;
            NormalToHashed.Length = agentDataArray.Length;
            HashedToNormal.Length = agentDataArray.Length;

            //Copying agent positions from transforms
            _agentPositions.Length = agentTransforms.length;
            AgentPositionGetJob agentMovementPositionGet = new AgentPositionGetJob()
            {
                MaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                MaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                MinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                MinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                PositionOutput = _agentPositions.AsArray(),
            };
            JobHandle agentMovementPositionGetHandle = agentMovementPositionGet.Schedule(agentTransforms);
            dependency = JobHandle.CombineDependencies(agentMovementPositionGetHandle, dependency);

            //Get cost field costs
            UnsafeListReadOnly<byte>[] costFielCosts = _navigationManager.GetAllCostFieldCostsAsUnsafeListReadonly();
            _costFieldCosts.Length = costFielCosts.Length;
            for (int i = 0; i < costFielCosts.Length; i++)
            {
                _costFieldCosts[i] = costFielCosts[i];
            }

            //Reset position changes
            AgentPositionChangeResetJob positionChangeReset = new AgentPositionChangeResetJob()
            {
                PositionChanges = AgentPositionChangeBuffer.AsArray(),
            };
            JobHandle positionChangeResetHandle = positionChangeReset.Schedule(dependency);

            //SPATIAL HASHING
            AgentDataSpatialHasherJob spatialHasher = new AgentDataSpatialHasherJob()
            {
                BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                TileSize = FlowFieldUtilities.TileSize,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                MaxAgentSize = FlowFieldUtilities.MaxAgentSize,
                MinAgentSize = FlowFieldUtilities.MinAgentSize,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                AgentDataArray = agentDataArray,
                AgentRadii = agentRadii,
                AgentPositions = _agentPositions.AsArray(),
                AgentFlockIndexArray = agentFlockIndexArray,
                AgentHashGridArray = HashGridArray,
                AgentMovementDataArray = AgentMovementDataList.AsArray(),
                NormalToHashed = NormalToHashed.AsArray(),
                HashedToNormal = HashedToNormal.AsArray(),
            };
            JobHandle spatialHasherHandle = spatialHasher.Schedule(positionChangeResetHandle);

            //FILL AGENT MOVEMENT DATA ARRAY
            AgentRoutineDataCalculationJob routineDataCalcJob = new AgentRoutineDataCalculationJob()
            {
                DeltaTime = deltaTime,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
                AgentCurPathIndicies = agentCurPathIndexArray,
                AgentDataArray = agentDataArray,
                ExposedPathDestinationArray = exposedPathDestinationArray,
                ExposedPathFlowDataArray = exposedPathFlowDataArray,
                ExposedPathLocationDataArray = exposedPathLocationDataArray,
                HashedToNormal = HashedToNormal.AsArray(),

                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                TileSize = FlowFieldUtilities.TileSize,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                AgentMovementData = AgentMovementDataList.AsArray(),
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
            };
            JobHandle movDataHandle = routineDataCalcJob.Schedule(routineDataCalcJob.AgentMovementData.Length, 64, spatialHasherHandle);

            //SCHEDULE LOCAL AVODANCE JOB
            LocalAvoidanceJob avoidanceJob = new LocalAvoidanceJob()
            {
                FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TileSize = FlowFieldUtilities.TileSize,
                SeperationMultiplier = FlowFieldBoidUtilities.SeperationMultiplier,
                SeperationRangeAddition = FlowFieldBoidUtilities.SeperationRangeAddition,
                SeekMultiplier = FlowFieldBoidUtilities.SeekMultiplier,
                AlignmentMultiplier = FlowFieldBoidUtilities.AlignmentMultiplier,
                AlignmentRangeAddition = FlowFieldBoidUtilities.AlignmentRangeAddition,
                MovingAvoidanceRangeAddition = FlowFieldBoidUtilities.MovingAvoidanceRangeAddition,
                BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                RoutineResultArray = RoutineResults.AsArray(),
                AgentSpatialHashGrid = new AgentSpatialHashGrid()
                {
                    BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                    FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                    FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                    HashGridColAndRowAmounts = _hashGridColAndRowAmounts,
                    AgentHashGridArray = HashGridArray,
                    RawAgentMovementDataArray = AgentMovementDataList.AsArray(),
                    FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
                },
                CostFieldEachOffset = _costFieldCosts.AsArray(),
            };
            JobHandle avoidanceHandle = avoidanceJob.Schedule(agentDataArray.Length, 64, movDataHandle);


            //SCHEDULE AGENT COLLISION JOB
            JobHandle lastIterationHandle = avoidanceHandle;
            for(int i = 0; i < 6; i++)
            {
                CollisionResolutionJob colResJob = new CollisionResolutionJob()
                {
                    AgentSpatialHashGrid = new AgentSpatialHashGrid()
                    {
                        BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                        FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                        FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                        HashGridColAndRowAmounts = _hashGridColAndRowAmounts,
                        AgentHashGridArray = HashGridArray,
                        RawAgentMovementDataArray = AgentMovementDataList.AsArray(),
                        FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
                    },
                    RoutineResultArray = RoutineResults.AsArray(),
                    AgentPositionChangeBuffer = AgentPositionChangeBuffer.AsArray(),
                };
                JobHandle colResHandle = colResJob.Schedule(agentDataArray.Length, 4, lastIterationHandle);

                //SCHEDULE WALL COLLISION JOB
                AgentWallCollisionJob wallCollision = new AgentWallCollisionJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    TileSize = FlowFieldUtilities.TileSize,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                    HalfTileSize = FlowFieldUtilities.TileSize / 2,
                    FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                    SectorMatrixTileAmont = FlowFieldUtilities.SectorMatrixTileAmount,
                    AgentMovementData = AgentMovementDataList.AsArray(),
                    AgentPositionChangeBuffer = AgentPositionChangeBuffer.AsArray(),
                    CostFieldEachOffset = _costFieldCosts.AsArray(),
                };
                JobHandle wallCollisionHandle = wallCollision.Schedule(wallCollision.AgentMovementData.Length, 64, colResHandle);

                AgentPosChangeToPosJob positionChangeToPos = new AgentPosChangeToPosJob()
                {
                    AgentMovementDataArrayRaw = AgentMovementDataList.AsArray(),
                    PositionChanges = AgentPositionChangeBuffer.AsArray(),
                };
                JobHandle positionChangeToPositionHandle = positionChangeToPos.Schedule(wallCollisionHandle);
                lastIterationHandle = positionChangeToPositionHandle;
            }
            CollisionFinalPositionChangeJob finalPosChangeJob = new CollisionFinalPositionChangeJob()
            {
                AgentInitialPositionsNormal = _agentPositions.AsArray(),
                FinalPositionChanges = AgentPositionChangeBuffer.AsArray(),
                AgentMovementDataArrayHashed = AgentMovementDataList.AsArray(),
                NormalToHashed = NormalToHashed.AsArray(),
            };
            JobHandle finalPosChangeHandle = finalPosChangeJob.Schedule(lastIterationHandle);

            //SCHEDULE TENSON RES JOB
            TensionResolver tensionResJob = new TensionResolver()
            {
                AgentSpatialHashGrid = new AgentSpatialHashGrid()
                {
                    BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                    FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                    FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                    HashGridColAndRowAmounts = _hashGridColAndRowAmounts,
                    AgentHashGridArray = HashGridArray,
                    RawAgentMovementDataArray = AgentMovementDataList.AsArray(),
                    FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
                },
                RoutineResultArray = RoutineResults.AsArray(),
                SeperationRangeAddition = FlowFieldBoidUtilities.SeperationRangeAddition,
            };
            JobHandle tensionHandle = tensionResJob.Schedule(finalPosChangeHandle);


            //Height Calculation
            AgentHeightCalculationJob heightCalculation = new AgentHeightCalculationJob()
            {
                TriangleSpatialHashGrid = _navigationManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
                AgentMovementDataArray = AgentMovementDataList.AsArray(),
                AgentPositionChangeArray = AgentPositionChangeBuffer.AsArray(),
                Verticies = _navigationManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
            };
            JobHandle heightCalculationHandle = heightCalculation.Schedule(agentDataArray.Length, 32, tensionHandle);

            //Avoidance wall detection
            AvoidanceWallDetectionJob wallDetection = new AvoidanceWallDetectionJob()
            {
                FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TileSize = FlowFieldUtilities.TileSize,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                AgentMovementDataArray = AgentMovementDataList.AsArray(),
                CostFieldPerOffset = _costFieldCosts.AsArray(),
                RoutineResultArray = RoutineResults.AsArray(),
            };
            JobHandle wallDetectionHandle = wallDetection.Schedule(agentDataArray.Length, 64, heightCalculationHandle);

            AgentDirectionHeightCalculationJob directionHeightJob = new AgentDirectionHeightCalculationJob()
            {
                TriangleSpatialHashGrid = _navigationManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
                Verticies = _navigationManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
                AgentMovementDataArray = AgentMovementDataList.AsArray(),
                RoutineResultArray = RoutineResults.AsArray(),
            };
            JobHandle directionHeightJobHandle = directionHeightJob.Schedule(agentDataArray.Length, 64, wallDetectionHandle);

            PathReachDistances.Length = exposedPathReachDistanceCheckRanges.Length;
            DestinationReachedAgentCountJob destinationReachedCounter = new DestinationReachedAgentCountJob()
            {
                AgentDestinationReachStatus = _agentDataContainer.AgentDestinationReachedArray.AsArray(),
                HashedToNormal = HashedToNormal.AsArray(),
                AgentSpatialHashGrid = new AgentSpatialHashGrid()
                {
                    BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                    FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                    FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                    HashGridColAndRowAmounts = _hashGridColAndRowAmounts,
                    AgentHashGridArray = HashGridArray,
                    RawAgentMovementDataArray = AgentMovementDataList.AsArray(),
                    FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
                },
                AgentFlockIndicies = agentFlockIndexArray,
                PathDestinationArray = exposedPathDestinationArray,
                PathReachDistances = PathReachDistances.AsArray(),
                PathReachDistanceCheckRanges = exposedPathReachDistanceCheckRanges,
                PathFlockIndexArray = exposedPathFlockIndicies,
                PathStateList = exposedPathStateList,
                PathAgentStopFlagList = exposedPathAgentStopFlagList,
            };
            JobHandle destinationReachedCounterHandle = destinationReachedCounter.Schedule(exposedPathFlockIndicies.Length, 64, directionHeightJobHandle);

            AgentStoppingJob stoppingJob = new AgentStoppingJob()
            {
                AgentDestinationReachStatus = _agentDataContainer.AgentDestinationReachedArray.AsArray(),
                AgentCurPathIndicies = agentCurPathIndexArray,
                PathDestinationReachRanges = PathReachDistances.AsArray(),
                AgentMovementDataArray = AgentMovementDataList.AsArray(),
                NormalToHashed = NormalToHashed.AsArray(),
                PathAgentStopFlags = exposedPathAgentStopFlagList,
            };
            JobHandle stoppingHandle = stoppingJob.Schedule(agentDataArray.Length, 64, destinationReachedCounterHandle);

            if (FlowFieldUtilities.DebugMode) { stoppingHandle.Complete(); }
            agentMovementPositionGetHandle.Complete();
            _routineHandle = stoppingHandle;
        }
        internal void ForceCompleteRoutine()
        {
            _routineHandle.Complete();
        }
        internal void SendRoutineResults(float deltaTime)
        {
            TransformAccessArray agentTransforms = _navigationManager.AgentDataContainer.AgentTransforms;
            NativeList<bool> agentDestinationReachArray = _navigationManager.AgentDataContainer.AgentDestinationReachedArray;
            NativeList<int> agentCurPathIndicies = _navigationManager.AgentDataContainer.AgentCurPathIndicies;
            NativeList<AgentData> agentDataArray = _navigationManager.AgentDataContainer.AgentDataList;
            NativeArray<bool> agentUseNavigationMovementFlags = _agentDataContainer.AgentUseNavigationMovementFlags.AsArray();

            AgentPositionChangeSendJob posSendJob = new AgentPositionChangeSendJob()
            {
                AgentUseNavigationMovementFlags = agentUseNavigationMovementFlags,
                AgentPositionChangeBuffer = AgentPositionChangeBuffer.AsArray(),
                NormalToHashed = NormalToHashed.AsArray(),
            };
            posSendJob.Schedule(agentTransforms).Complete();

            RoutineResultSendJob directionSetJob = new RoutineResultSendJob()
            {
                AgentDestinationReachedArray = agentDestinationReachArray.AsArray(),
                AgentCurPathIndicies = agentCurPathIndicies.AsArray(),
                MovementDataArray = AgentMovementDataList.AsArray(),
                AgentDataArray = agentDataArray.AsArray(),
                RoutineResultArray = RoutineResults.AsArray(),
                NormalToHashed = NormalToHashed.AsArray(),
            };
            directionSetJob.Schedule().Complete();

            //MOVE
            AgentMovementUpdateJob movJob = new AgentMovementUpdateJob()
            {
                DeltaTime = deltaTime,
                AgentUseNavigationMovementFlags = agentUseNavigationMovementFlags,
                AgentDataArray = agentDataArray.AsArray(),
            };
            movJob.Schedule(agentTransforms).Complete();

            //ROTATE
            AgentRotationUpdateJob rotateJob = new AgentRotationUpdateJob()
            {
                agentData = agentDataArray.AsArray(),
            };
            rotateJob.Schedule(agentTransforms).Complete();
        }
    }

}