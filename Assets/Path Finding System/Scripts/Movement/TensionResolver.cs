using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.VisualScripting;

[BurstCompile]
public struct TensionResolver : IJob
{
    public float SeperationRangeAddition;
    public AgentSpatialGridUtils HashGridUtils;
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<UnsafeList<HashTile>> HashGridArray;
    public NativeArray<RoutineResult> RoutineResultArray;

    public void Execute()
    {
        UnsafeList<Tension> tensionlist = new UnsafeList<Tension>(0, Allocator.Temp);
        UnsafeList<int> tensionPowerList = new UnsafeList<int>(0, Allocator.Temp);
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData agentData = AgentMovementDataArray[i];
            float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
            float2 agentDir = RoutineResultArray[i].NewDirection;
            AvoidanceStatus agentAvoidance = RoutineResultArray[i].NewAvoidance;
            if (agentAvoidance == 0) { continue; }

            for(int j = 0; j < HashGridArray.Length; j++)
            {
                UnsafeList<HashTile> pickedGrid = HashGridArray[j];
                GridTravesalData travData = HashGridUtils.GetGridTraversalData(agentPos, agentData.Radius + SeperationRangeAddition, j);
                for(int k = travData.botLeft; k <= travData.topLeft; k += travData.gridColAmount)
                {
                    for(int l = k; l < k + travData.horizontalSize; l++)
                    {
                        HashTile tile = pickedGrid[l];
                        for(int m = tile.Start; m < tile.Start + tile.Length; m++)
                        {
                            AgentMovementData mateData = AgentMovementDataArray[m];
                            RoutineResult mateRoutineResult = RoutineResultArray[m];
                            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                            if (mateRoutineResult.NewAvoidance == 0) { continue; }

                            float dot = math.dot(agentDir, matePos - agentPos);
                            if (dot < 0) { continue; }

                            if (mateRoutineResult.NewAvoidance == agentAvoidance) { continue; }
                            if (math.distance(agentPos, matePos) > agentData.Radius + mateData.Radius + SeperationRangeAddition) { continue; }
                            Tension tension = new Tension()
                            {
                                agent1 = i,
                                agent2 = m,
                            };
                            tensionlist.Add(tension);
                        }
                    }
                }
            }
        }

        //RESOLVE TENSIONS
        for (int i = 0; i < tensionlist.Length; i++)
        {
            Tension tension = tensionlist[i];
            AgentMovementData agent1 = AgentMovementDataArray[tension.agent1];
            AgentMovementData agent2 = AgentMovementDataArray[tension.agent2];

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
        for (int index = 0; index < AgentMovementDataArray.Length; index++)
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

        AgentMovementData agentData = AgentMovementDataArray[agentIndex];
        agentData.RoutineStatus |= AgentRoutineStatus.Traversed;
        agentData.TensionPowerIndex = index;
        AgentMovementDataArray[agentIndex] = agentData;

        while (!neighbours.IsEmpty())
        {
            power++;
            int pickedIndex = neighbours.Dequeue();
            agentData = AgentMovementDataArray[pickedIndex];
            GetNeighbourAgents(pickedIndex, agentData.Radius, new float2(agentData.Position.x, agentData.Position.z), 0.3f, neighbours, index, avoidance);
        }
        tensionPowerList.Add(power);
        return power;
    }
    void GetNeighbourAgents(int agentIndex, float agentRadius, float2 agentPos, float maxDistance, NativeQueue<int> neighbours, int tensionPowerIndex, AvoidanceStatus avoidance)
    {
        for (int i = 0; i < HashGridArray.Length; i++)
        {
            UnsafeList<HashTile> pickedGrid = HashGridArray[i];
            GridTravesalData travData = HashGridUtils.GetGridTraversalData(agentPos, agentRadius + SeperationRangeAddition, i);
            for (int j = travData.botLeft; j <= travData.topLeft; j += travData.gridColAmount)
            {
                for (int k = j; k < j + travData.horizontalSize; k++)
                {
                    HashTile tile = pickedGrid[k];
                    for (int l = tile.Start; l < tile.Start + tile.Length; l++)
                    {
                        AgentMovementData mateData = AgentMovementDataArray[l];
                        if (l == agentIndex) { continue; }
                        if (mateData.Avoidance != avoidance) { continue; }

                        float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                        float distance = math.distance(matePos, agentPos);

                        if (distance > agentRadius + mateData.Radius + SeperationRangeAddition + maxDistance) { continue; }
                        if ((mateData.RoutineStatus & AgentRoutineStatus.Traversed) != AgentRoutineStatus.Traversed)
                        {
                            mateData.RoutineStatus |= AgentRoutineStatus.Traversed;
                            mateData.TensionPowerIndex = tensionPowerIndex;
                            AgentMovementDataArray[l] = mateData;
                            neighbours.Enqueue(l);
                        }
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