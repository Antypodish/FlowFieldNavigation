using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.VisualScripting;
using System;

[BurstCompile]
public struct TensionResolver : IJob
{
    public float SeperationRangeAddition;
    public AgentSpatialHashGrid AgentSpatialHashGrid;
    public NativeArray<RoutineResult> RoutineResultArray;

    public void Execute()
    {
        UnsafeList<Tension> tensionlist = new UnsafeList<Tension>(0, Allocator.Temp);
        UnsafeList<int> tensionPowerList = new UnsafeList<int>(0, Allocator.Temp);
        for (int i = 0; i < AgentSpatialHashGrid.RawAgentMovementDataArray.Length; i++)
        {
            AgentMovementData agentData = AgentSpatialHashGrid.RawAgentMovementDataArray[i];
            float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
            float2 agentDir = RoutineResultArray[i].NewDirection;
            AvoidanceStatus agentAvoidance = RoutineResultArray[i].NewAvoidance;
            if (agentAvoidance == 0) { continue; }

            float checkRange = agentData.Radius + SeperationRangeAddition;
            for (int j = 0; j < AgentSpatialHashGrid.GetGridCount(); j++)
            {
                SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, j);
                while (iterator.HasNext())
                {
                    NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                    for (int k = 0; k < agentsToCheck.Length; k++)
                    {
                        AgentMovementData mateData = agentsToCheck[k];
                        RoutineResult mateRoutineResult = RoutineResultArray[sliceStart + k];
                        float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                        if (mateRoutineResult.NewAvoidance == 0) { continue; }
                        float dot = math.dot(agentDir, matePos - agentPos);
                        if (dot < 0) { continue; }
                        if (mateRoutineResult.NewAvoidance == agentAvoidance) { continue; }
                        if (math.distance(agentPos, matePos) > mateData.Radius + checkRange) { continue; }
                        Tension tension = new Tension()
                        {
                            agent1 = i,
                            agent2 = sliceStart + k,
                        };
                        tensionlist.Add(tension);
                    }
                }
            }
        }

        //RESOLVE TENSIONS
        for (int i = 0; i < tensionlist.Length; i++)
        {
            Tension tension = tensionlist[i];
            AgentMovementData agent1 = AgentSpatialHashGrid.RawAgentMovementDataArray[tension.agent1];
            AgentMovementData agent2 = AgentSpatialHashGrid.RawAgentMovementDataArray[tension.agent2];

            RoutineResult result1 = RoutineResultArray[tension.agent1];
            RoutineResult result2 = RoutineResultArray[tension.agent2];
            /*
            bool succesfull = agent1.PathId == agent2.PathId && ExamineSplitting(ref result1, ref result2);
            if (succesfull)
            {
                RoutineResultArray[tension.agent2] = result2;
                RoutineResultArray[tension.agent1] = result1;
                continue;
            }*/

            int agent1Power;
            int agent2Power;
            if (agent1.TensionPowerIndex == -1)
            {
                agent1Power = GetTensionPower(tension.agent1, agent1.Avoidance, ref tensionPowerList);
            }
            else
            {
                agent1Power = tensionPowerList[agent1.TensionPowerIndex];
            }
            if (agent2.TensionPowerIndex == -1)
            {
                agent2Power = GetTensionPower(tension.agent2, agent2.Avoidance, ref tensionPowerList);
            }
            else
            {
                agent2Power = tensionPowerList[agent2.TensionPowerIndex];
            }
            if (agent1Power > agent2Power)
            {
                result2.NewAvoidance = result1.NewAvoidance;
                result2.NewDirection = result1.NewDirection;
                RoutineResultArray[tension.agent2] = result2;
            }
            else
            {
                result1.NewAvoidance = result2.NewAvoidance;
                result1.NewDirection = result2.NewDirection;
                RoutineResultArray[tension.agent1] = result1;
            }
        }
        //DECREASE SPLIT INTERVALS AND INFO
        for (int index = 0; index < AgentSpatialHashGrid.RawAgentMovementDataArray.Length; index++)
        {
            RoutineResult result = RoutineResultArray[index];
            result.NewSplitInterval = (byte)math.select(result.NewSplitInterval - 1, 0, result.NewSplitInterval == 0);
            result.NewSplitInfo = (byte)math.select(result.NewSplitInfo - 1, 0, result.NewSplitInfo == 0);
            RoutineResultArray[index] = result;
        }
    }
    bool ExamineSplitting(ref RoutineResult result1, ref RoutineResult result2)
    {
        bool succesfull = false;
        if (result1.NewSplitInfo > 0 && result2.NewSplitInfo == 0)
        {
            result2.NewAvoidance = result1.NewAvoidance;
            result2.NewAvoidance = result1.NewAvoidance;
            result2.NewSplitInterval = 0;
            result2.NewSplitInfo = 50;
            succesfull = true;
        }
        else if (result1.NewSplitInfo == 0 && result2.NewSplitInfo > 0)
        {
            result1.NewAvoidance = result2.NewAvoidance;
            result1.NewAvoidance = result2.NewAvoidance;
            result1.NewSplitInterval = 0;
            result1.NewSplitInfo = 50;
            succesfull = true;
        }
        else if (result1.NewSplitInfo > 0 && result2.NewSplitInfo > 0)
        {
            result1.NewSplitInfo = 0;
            result2.NewSplitInfo = 0;
            succesfull = false;
        }
        else if (result1.NewSplitInterval > 0 && result2.NewSplitInterval > 0)
        {
            float2 nextDir1 = result1.NewDirection;
            float2 nextDir2 = result2.NewDirection;
            AvoidanceStatus avoidance1 = result1.NewAvoidance;
            AvoidanceStatus avoidance2 = result2.NewAvoidance;

            result1.NewDirection = nextDir2;
            result1.NewAvoidance = avoidance2;
            result1.NewSplitInterval = 0;
            result1.NewSplitInfo = 50;

            result2.NewDirection = nextDir1;
            result2.NewAvoidance = avoidance1;
            result2.NewSplitInterval = 0;
            result2.NewSplitInfo = 50;

            succesfull = true;
            /*
            float2 agent1Dir = agent1.NextDirection;
            float2 agent2Dir = agent2.NextDirection;
            float2 agent1Pos = new float2(agent1.Position.x, agent1.Position.z);
            float2 agent2Pos = new float2(agent2.Position.x, agent2.Position.z);
            float2 center = (agent1Pos + agent2Pos) / 2;
            float agent1Power = 0;
            float agent2Power = 0;

            for(int i = 0; i < AgentMovementDataArray.Length; i++)
            {
                AgentMovementData agent = AgentMovementDataArray[i];
                float2 agentPos = new float2(agent.Position.x, agent.Position.z);
                if(agent.PathId != agent1.PathId) { continue; }
                if(math.distance(center, agentPos) > 20f) { continue; }

                if(math.dot(agent1Dir, agentPos - center) <= 0f && math.dot(agent.Flow, agent1Dir) >= 0f)
                {
                    agent1Power++;
                }
                if (math.dot(agent2Dir, agentPos - center) <= 0f && math.dot(agent.Flow, agent2Dir) >= 0f)
                {
                    agent2Power++;
                }
            }
            if(agent1Power / agent2Power < 1.4f && agent1Power / agent2Power > 0.6f)
            {
                
            }
            else
            {
                agent1.SplitInterval = 0;
                agent2.SplitInterval = 0;
                succesfull = false;
            }*/
        }
        else
        {
            result1.NewSplitInterval = 0;
            result2.NewSplitInterval = 0;
            succesfull = false;
        }
        return succesfull;
    }
    int GetTensionPower(int agentIndex, AvoidanceStatus avoidance, ref UnsafeList<int> tensionPowerList)
    {
        int index = tensionPowerList.Length;
        int power = 0;
        NativeQueue<int> neighbours = new NativeQueue<int>(Allocator.Temp);

        neighbours.Enqueue(agentIndex);

        AgentMovementData agentData = AgentSpatialHashGrid.RawAgentMovementDataArray[agentIndex];
        agentData.RoutineStatus |= AgentRoutineStatus.Traversed;
        agentData.TensionPowerIndex = index;
        AgentSpatialHashGrid.RawAgentMovementDataArray[agentIndex] = agentData;

        while (!neighbours.IsEmpty())
        {
            power++;
            int pickedIndex = neighbours.Dequeue();
            agentData = AgentSpatialHashGrid.RawAgentMovementDataArray[pickedIndex];
            GetNeighbourAgents(pickedIndex, agentData.Radius, new float2(agentData.Position.x, agentData.Position.z), 0.3f, neighbours, index, avoidance);
        }
        tensionPowerList.Add(power);
        return power;
    }
    void GetNeighbourAgents(int agentIndex, float agentRadius, float2 agentPos, float maxDistance, NativeQueue<int> neighbours, int tensionPowerIndex, AvoidanceStatus avoidance)
    {
        float checkRange = agentRadius + SeperationRangeAddition + maxDistance;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mateData = agentsToCheck[j];
                    if (j + sliceStart == agentIndex) { continue; }
                    if (mateData.Avoidance != avoidance) { continue; }

                    float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                    float distance = math.distance(matePos, agentPos);

                    if (distance > mateData.Radius + checkRange) { continue; }
                    if ((mateData.RoutineStatus & AgentRoutineStatus.Traversed) != AgentRoutineStatus.Traversed)
                    {
                        mateData.RoutineStatus |= AgentRoutineStatus.Traversed;
                        mateData.TensionPowerIndex = tensionPowerIndex;
                        agentsToCheck[j] = mateData;
                        neighbours.Enqueue(j + sliceStart);
                    }
                }
            }
        }
    }
}
public struct Tension
{
    public int agent1;
    public int agent2;
}