using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal class CostFieldEditManager
    {
        internal uint FieldState { get { return _fieldState; } }
        internal NativeArray<SectorBitArray>.ReadOnly EditedSectorBitArraysForEachField { get { return _editedSectorBitArray.AsArray().AsReadOnly(); } }

        FlowFieldNavigationManager _navigationManager;

        List<JobHandle> _costEditHandle;
        List<JobHandle> _islandReconfigHandle;

        uint _fieldState;
        NativeList<SectorBitArray> _editedSectorBitArray;
        NativeList<CostEdit> _newCostEditRequests;
        internal CostFieldEditManager(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _costEditHandle = new List<JobHandle>();
            _islandReconfigHandle = new List<JobHandle>();
            _editedSectorBitArray = new NativeList<SectorBitArray>(Allocator.Persistent);
            _newCostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
            _fieldState = 0;
        }
        internal void DisposeAll()
        {
            _costEditHandle = null;
            _islandReconfigHandle = null;
            _editedSectorBitArray.Dispose();
            _newCostEditRequests.Dispose();
        }
        internal JobHandle GetCurrentCostFieldEditHandle()
        {
            if (_costEditHandle.Count == 0) { return new JobHandle(); }
            return _costEditHandle[0];
        }
        internal JobHandle GetCurrentIslandFieldReconfigHandle()
        {
            if (_islandReconfigHandle.Count == 0) { return new JobHandle(); }
            return _islandReconfigHandle[0];
        }
        internal void Schedule(NativeArray<CostEdit>.ReadOnly costEditRequests)
        {
            _editedSectorBitArray.Clear();
            _newCostEditRequests.Clear();
            //COPY OBSTACLE REQUESTS
            ReadOnlyNativeArrayToNativeListCopyJob<CostEdit> obstacleRequestCopy = new ReadOnlyNativeArrayToNativeListCopyJob<CostEdit>()
            {
                Source = costEditRequests,
                Destination = _newCostEditRequests,
            };
            obstacleRequestCopy.Schedule().Complete();
            ApplyFieldEdits();
        }
        internal void TryComplete()
        {
            //ISLAND REC
            if (_islandReconfigHandle.Count != 0)
            {
                if (_islandReconfigHandle[0].IsCompleted)
                {
                    _islandReconfigHandle[0].Complete();
                    _islandReconfigHandle.RemoveAtSwapBack(0);
                }
                _fieldState++;
            }
        }
        internal void ForceComplete()
        {
            //COST EDIT
            if (_costEditHandle.Count != 0)
            {
                _costEditHandle[0].Complete();
                _costEditHandle.RemoveAtSwapBack(0);
            }
            //ISLAND RECONFIG
            if (_islandReconfigHandle.Count != 0)
            {
                _islandReconfigHandle[0].Complete();
                _islandReconfigHandle.RemoveAtSwapBack(0);
                _fieldState++;
            }
        }
        void ApplyFieldEdits()
        {
            if (_newCostEditRequests.Length == 0) { return; }
            FieldGraph[] fieldGraphs = _navigationManager.FieldDataContainer.GetAllFieldGraphs();
            CostField[] costFields = _navigationManager.FieldDataContainer.GetAllCostFields();
            JobHandle combinedCostStampApplyHandle = new JobHandle();
            JobHandle combinedCostEditHandle = new JobHandle();
            JobHandle combinedIslandFieldReconfigHandle = new JobHandle();
            for (int i = 0; i < fieldGraphs.Length; i++)
            {
                CostField costField = costFields[i];
                FieldGraph fieldGraph = fieldGraphs[i];

                //Obstacle copy
                NativeListCopyJob<CostEdit> newObstaclesTransfer = new NativeListCopyJob<CostEdit>()
                {
                    Source = _newCostEditRequests,
                    Destination = fieldGraph.NewCostEdits,
                };
                JobHandle newObstacleTransferHandle = newObstaclesTransfer.Schedule();

                //Cost stamp apply
                CostStampApplyJob costStampApply = new CostStampApplyJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                    Offset = costField.Offset,
                    Costs = costField.Costs,
                    CostStamps = costField.StampCounts,
                    BaseCosts = costField.BaseCosts,
                    NewCostEdits = fieldGraph.NewCostEdits,
                };
                JobHandle costStampApplyHandle = costStampApply.Schedule(newObstacleTransferHandle);
                if (FlowFieldUtilities.DebugMode) { costStampApplyHandle.Complete(); }
                combinedCostStampApplyHandle = JobHandle.CombineDependencies(costStampApplyHandle, combinedCostStampApplyHandle);

                //Field edit
                CostFieldEditJob costEditJob = new CostFieldEditJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    Offset = costField.Offset,
                    SectorNodes = fieldGraph.SectorNodes,
                    SecToWinPtrs = fieldGraph.SecToWinPtrs,
                    EditedSectorBits = fieldGraph.EditedSectorMarks,
                    EditedSectorIndicies = fieldGraph.EditedSectorList,
                    WinToSecPtrs = fieldGraph.WinToSecPtrs,
                    Costs = costField.Costs,
                    CostStamps = costField.StampCounts,
                    BaseCosts = costField.BaseCosts,
                    EditedWindowIndicies = fieldGraph.EditedWinodwList,
                    EditedWindowMarks = fieldGraph.EditedWindowMarks,
                    IntegratedCosts = fieldGraph.SectorIntegrationField,
                    IslandFields = fieldGraph.IslandFields,
                    Islands = fieldGraph.IslandDataList.AsArray(),
                    NewCostEdits = fieldGraph.NewCostEdits,
                    PorPtrs = fieldGraph.PorToPorPtrs,
                    PortalNodes = fieldGraph.PortalNodes,
                    PortalPerWindow = fieldGraph.PortalPerWindow,
                    WindowNodes = fieldGraph.WindowNodes,
                };
                JobHandle editHandle = costEditJob.Schedule(costStampApplyHandle);
                if (FlowFieldUtilities.DebugMode) { editHandle.Complete(); }
                combinedCostEditHandle = JobHandle.CombineDependencies(combinedCostEditHandle, editHandle);
                _editedSectorBitArray.Add(costEditJob.EditedSectorBits);

                //Island Field Reconfig
                IslandReconfigurationJob islandReconfig = new IslandReconfigurationJob()
                {
                    Offset = costField.Offset,
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    SectorNodes = fieldGraph.SectorNodes,
                    SecToWinPtrs = fieldGraph.SecToWinPtrs,
                    EditedSectorIndicies = fieldGraph.EditedSectorList,
                    PortalEdges = fieldGraph.PorToPorPtrs,
                    CostsL = costField.Costs,
                    IslandFields = fieldGraph.IslandFields,
                    Islands = fieldGraph.IslandDataList,
                    PortalNodes = fieldGraph.PortalNodes,
                    WindowNodes = fieldGraph.WindowNodes,
                };
                JobHandle islandFieldReconfigHandle = islandReconfig.Schedule(editHandle);
                if (FlowFieldUtilities.DebugMode) { islandFieldReconfigHandle.Complete(); }
                combinedIslandFieldReconfigHandle = JobHandle.CombineDependencies(combinedIslandFieldReconfigHandle, islandFieldReconfigHandle);
            }
            combinedCostStampApplyHandle.Complete();
            _costEditHandle.Add(combinedCostEditHandle);
            _islandReconfigHandle.Add(combinedIslandFieldReconfigHandle);
        }
        /*
        JobHandle ScheduleCostEditRequests()
        {
            if (_newCostEditRequests.Length == 0) { return new JobHandle(); }

            NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
            for (int i = 0; i <= FlowFieldUtilities.MaxCostFieldOffset; i++)
            {
                CostField costField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(i);
                FieldGraph fieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(i);

                NativeListCopyJob<CostEdit> newObstaclesTransfer = new NativeListCopyJob<CostEdit>()
                {
                    Source = _newCostEditRequests,
                    Destination = fieldGraph.NewCostEdits,
                };
                JobHandle newObstacleTransferHandle = newObstaclesTransfer.Schedule();

                CostFieldEditJob costEditJob = new CostFieldEditJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    Offset = i,
                    SectorNodes = fieldGraph.SectorNodes,
                    SecToWinPtrs = fieldGraph.SecToWinPtrs,
                    EditedSectorBits = fieldGraph.EditedSectorMarks,
                    EditedSectorIndicies = fieldGraph.EditedSectorList,
                    WinToSecPtrs = fieldGraph.WinToSecPtrs,
                    Costs = costField.Costs,
                    CostStamps = costField.StampCounts,
                    BaseCosts = costField.BaseCosts,
                    EditedWindowIndicies = fieldGraph.EditedWinodwList,
                    EditedWindowMarks = fieldGraph.EditedWindowMarks,
                    IntegratedCosts = fieldGraph.SectorIntegrationField,
                    IslandFields = fieldGraph.IslandFields,
                    Islands = fieldGraph.IslandDataList.AsArray(),
                    NewCostEdits = fieldGraph.NewCostEdits,
                    PorPtrs = fieldGraph.PorToPorPtrs,
                    PortalNodes = fieldGraph.PortalNodes,
                    PortalPerWindow = fieldGraph.PortalPerWindow,
                    WindowNodes = fieldGraph.WindowNodes,
                };
                JobHandle editHandle = costEditJob.Schedule(newObstacleTransferHandle);
                editHandles.Add(editHandle);

                _editedSectorBitArray.Add(costEditJob.EditedSectorBits);
            }
            JobHandle cominedHandle = JobHandle.CombineDependencies(editHandles.AsArray());
            editHandles.Dispose();

            if (FlowFieldUtilities.DebugMode) { cominedHandle.Complete(); }
            _costEditHandle.Add(cominedHandle);
            return cominedHandle;
        }
        JobHandle ScheduleIslandFieldReconfig(JobHandle dependency)
        {
            if (_newCostEditRequests.Length == 0) { return new JobHandle(); }

            NativeArray<JobHandle> handlesToCombine = new NativeArray<JobHandle>(FlowFieldUtilities.MaxCostFieldOffset + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i <= FlowFieldUtilities.MaxCostFieldOffset; i++)
            {
                FieldGraph fieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(i);
                CostField costField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(i);

                IslandReconfigurationJob islandReconfig = new IslandReconfigurationJob()
                {
                    Offset = i,
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    SectorNodes = fieldGraph.SectorNodes,
                    SecToWinPtrs = fieldGraph.SecToWinPtrs,
                    EditedSectorIndicies = fieldGraph.EditedSectorList,
                    PortalEdges = fieldGraph.PorToPorPtrs,
                    CostsL = costField.Costs,
                    IslandFields = fieldGraph.IslandFields,
                    Islands = fieldGraph.IslandDataList,
                    PortalNodes = fieldGraph.PortalNodes,
                    WindowNodes = fieldGraph.WindowNodes,
                };
                handlesToCombine[i] = islandReconfig.Schedule(dependency);
            }
            JobHandle combinedHandles = JobHandle.CombineDependencies(handlesToCombine);

            if (FlowFieldUtilities.DebugMode) { combinedHandles.Complete(); }
            _islandReconfigHandle.Add(combinedHandles);
            return combinedHandles;
        }*/
    }
}
