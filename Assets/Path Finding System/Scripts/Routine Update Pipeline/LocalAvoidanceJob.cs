using System;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Security.Authentication;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;

[BurstCompile]
public struct LocalAvoidanceJob : IJob
{
    public float SeekMultiplier;
    public float AlignmentMultiplier;
    public float SeperationMultiplier;
    public float SeperationRangeAddition;
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<float2> AgentVelocities;


    public void Execute()
    {

        //ALIGN AND SEPERATE
        for (int index = 0; index < AgentMovementDataArray.Length; index++)
        {
            AgentMovementData agentData = AgentMovementDataArray[index];
            float agentRadius = agentData.Radius;
            float2 agentFlow = agentData.Flow;
            float2 agentPos2 = new float2(agentData.Position.x, agentData.Position.z);
            float2 agentCurVelocity = agentData.CurrentVelocity;
            float2 agentCurDirection = math.normalizesafe(agentCurVelocity);
            float2 seperationForce = 0;
            float2 destination = agentData.Destination;
            int pathId = agentData.PathId;


            float2 finalDirection = agentFlow;
            //AVOID
            if ((agentData.Status & AgentStatus.Moving) == AgentStatus.Moving && agentData.Avoidance == 0)
            {
                agentData.Avoidance = GetAvoidanceStatus(agentPos2, agentRadius, index, finalDirection);
                if(agentData.Avoidance != 0)
                {
                    agentData.SplitInterval = 50;
                }
            }
            if(agentData.Avoidance != 0)
            {
                finalDirection = GetAvoidanceDirection(agentPos2, agentCurDirection, agentRadius, index, agentData.Avoidance);
                if (finalDirection.Equals(0))
                {
                    agentData.Avoidance = 0;
                    finalDirection = agentFlow;
                }
                //CHECK IF FREE
                if(math.dot(agentData.Flow, agentCurDirection) > 0)
                {
                    if (IsDirectionFree(agentPos2, agentData.Flow, index, 1.2f, agentData.Radius + 2f))
                    {
                        agentData.Avoidance = 0;
                        finalDirection = agentData.Flow;
                    }
                }
                
            }


            //ALIGN
            if ((agentData.Status & AgentStatus.Moving) == AgentStatus.Moving && agentData.Avoidance == 0)
            {
                bool b = GetAvoidanceAlignment(agentPos2, agentRadius, index, finalDirection, pathId, out finalDirection);
                if (!b)
                {
                    finalDirection = GetAlignedDirectionToDestination(agentPos2, agentRadius, index, finalDirection, destination, pathId);
                }
            }

            //SEPERATE
            if ((agentData.Status & AgentStatus.Moving) == AgentStatus.Moving)
            {
                seperationForce = GetSeperationForce(agentPos2, finalDirection, agentRadius, index, agentData.Avoidance);
            }
            if(agentData.Status == 0)
            {
                seperationForce = GetStoppedSeperationForce(agentPos2, agentRadius, index);
            }
            float2 resultingForce = finalDirection + seperationForce;
            float resultingForceMag = math.length(resultingForce);
            agentData.NextDirection = math.select(resultingForce, resultingForce / resultingForceMag, resultingForceMag > 1);
            AgentMovementDataArray[index] = agentData;
        }

        //DETECT TENSIONS
        UnsafeList<Tension> tensionlist = new UnsafeList<Tension>(0, Allocator.Temp);
        UnsafeList<int> tensionPowerList = new UnsafeList<int>(0, Allocator.Temp);
        for(int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData agentData = AgentMovementDataArray[i];
            float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
            if(agentData.Avoidance == 0) { continue; }
            for(int j = i + 1; j < AgentMovementDataArray.Length; j++)
            {
                AgentMovementData mateData = AgentMovementDataArray[j];
                float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                if(mateData.Avoidance == 0) { continue; }

                float dot = math.dot(agentData.NextDirection, matePos - agentPos);
                if(dot < 0) { continue; }

                dot = math.dot(agentData.NextDirection, mateData.NextDirection);
                //if(dot > 0) { continue; }

                if(mateData.Avoidance == agentData.Avoidance) { continue; }

                if(math.distance(agentPos, matePos) > agentData.Radius + mateData.Radius + SeperationRangeAddition) { continue; }
                Tension tension = new Tension()
                {
                    agent1 = i,
                    agent2 = j,
                };
                tensionlist.Add(tension);
            }
        }

        //RESOLVE TENSIONS
        for(int i = 0; i < tensionlist.Length; i++)
        {
            Tension tension = tensionlist[i];
            AgentMovementData agent1 = AgentMovementDataArray[tension.agent1];
            AgentMovementData agent2 = AgentMovementDataArray[tension.agent2];

            bool succesfull = ExamineSplitting(ref agent1, ref agent2);
            if (succesfull)
            {
                AgentMovementDataArray[tension.agent2] = agent2;
                AgentMovementDataArray[tension.agent1] = agent1;
                continue;
            }

            int agent1Power;
            int agent2Power;
            if(agent1.TensionPowerIndex == -1)
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
            if(agent1Power > agent2Power)
            {
                agent2.Avoidance = agent1.Avoidance;
                agent2.NextDirection = agent1.NextDirection;
                AgentMovementDataArray[tension.agent2] = agent2;
            }
            else
            {
                agent1.Avoidance = agent2.Avoidance;
                agent1.NextDirection = agent2.NextDirection;
                AgentMovementDataArray[tension.agent1] = agent1;
            }
        }

        //DECREASE SPLIT INTERVALS AND INFO
        for (int index = 0; index < AgentMovementDataArray.Length; index++)
        {
            AgentMovementData data = AgentMovementDataArray[index];
            data.SplitInterval = (byte) math.select(data.SplitInterval - 1,0,data.SplitInterval == 0);
            data.SplitInfo = (byte) math.select(data.SplitInfo - 1,0,data.SplitInfo == 0);
            AgentMovementDataArray[index] = data;
        }

        //SEND DIRECTIONS
        for (int index = 0; index < AgentVelocities.Length; index++)
        {
            AgentVelocities[index] = AgentMovementDataArray[index].NextDirection * AgentMovementDataArray[index].Speed;
        }
    }
    bool ExamineSplitting(ref AgentMovementData agent1, ref AgentMovementData agent2)
    {
        bool succesfull = false;
        if(agent1.PathId != agent2.PathId) { return succesfull; }
        else if(agent1.SplitInfo > 0 && agent2.SplitInfo == 0)
        {
            agent2.NextDirection = agent1.NextDirection;
            agent2.Avoidance = agent1.Avoidance;
            agent2.SplitInterval = 0;
            agent2.SplitInfo = 50;
            succesfull = true;
        }
        else if (agent1.SplitInfo == 0 && agent2.SplitInfo > 0)
        {
            agent1.NextDirection = agent2.NextDirection;
            agent1.Avoidance = agent2.Avoidance;
            agent1.SplitInterval = 0;
            agent1.SplitInfo = 50;
            succesfull = true;
        }
        else if(agent1.SplitInfo > 0 && agent2.SplitInfo > 0)
        {
            agent1.SplitInfo = 0;
            agent2.SplitInfo = 0;
            succesfull = false;
        }
        else if(agent1.SplitInterval > 0 && agent2.SplitInterval > 0)
        {
            float2 nextDir1 = agent1.NextDirection;
            float2 nextDir2 = agent2.NextDirection;
            AvoidanceStatus avoidance1 = agent1.Avoidance;
            AvoidanceStatus avoidance2 = agent2.Avoidance;

            agent1.NextDirection = nextDir2;
            agent1.Avoidance = avoidance2;
            agent1.SplitInterval = 0;
            agent1.SplitInfo = 50;

            agent2.NextDirection = nextDir1;
            agent2.Avoidance = avoidance1;
            agent2.SplitInterval = 0;
            agent2.SplitInfo = 50;

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
            agent1.SplitInterval = 0;
            agent2.SplitInterval = 0;
            succesfull = false;
        }
        return succesfull;
    }
    bool GetAvoidanceAlignment(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection, int pathId, out float2 finalDirection)
    {
        float2 totalHeading = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = math.normalizesafe(mateData.CurrentVelocity);

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (pathId != mateData.PathId) { continue; }
            if (mateData.Avoidance == 0) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float mateRelativeLocation = math.dot(desiredDirection, matePos - agentPos);
            if (mateRelativeLocation <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);
            float alignmentRadius = (agentRadius + mateData.Radius + SeperationRangeAddition) * 3f;
            if (distance > alignmentRadius) { continue; }

            totalHeading += mateDirection;
        }

        if (totalHeading.Equals(0)) { finalDirection = desiredDirection; return false; }
        float2 averageHeading = math.normalize(totalHeading);
        float2 steering = averageHeading - desiredDirection;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        finalDirection = newDirection;
        return true;
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
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if (mateData.Avoidance != avoidance) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            if (distance > agentRadius + mateData.Radius + SeperationRangeAddition + maxDistance) { continue; }
            if ((mateData.RoutineStatus & AgentRoutineStatus.Traversed) != AgentRoutineStatus.Traversed)
            {
                mateData.RoutineStatus |= AgentRoutineStatus.Traversed;
                mateData.TensionPowerIndex = tensionPowerIndex;
                AgentMovementDataArray[i] = mateData;
                neighbours.Enqueue(i);
            }
        }
    }
    bool IsDirectionFree(float2 agentPos, float2 direction, int agentIndex, float desiredGap, float checkRange)
    {
        UnsafeList<float2> left = new UnsafeList<float2>(0, Allocator.Temp);
        UnsafeList<float2> right = new UnsafeList<float2>(0, Allocator.Temp);
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            float3 matePos3d = AgentMovementDataArray[i].Position;
            float2 matepos2d = new float2(matePos3d.x, matePos3d.z);
            if(i == agentIndex) { continue; }
            if(math.distance(matepos2d, agentPos) > checkRange) { continue; }
            if(math.dot(direction, matepos2d - agentPos) < 0) { continue; }
            float2 matetDir2d = matepos2d - agentPos;
            float dotRotated = direction.x * -matetDir2d.y + direction.y * matetDir2d.x;
            if (dotRotated > 0)
            {
                left.Add(matepos2d);
            }
            else
            {
                right.Add(matepos2d);
            }
        }
        if (left.Length == 0 || right.Length == 0) { return true; }
        for (int i = 0; i < left.Length; i++)
        {
            for (int j = 0; j < right.Length; j++)
            {
                if (math.distance(left[i], right[j]) < desiredGap + 1.2f)
                {
                    return false;
                }
            }
        }
        return true;
    }
    float2 GetAlignedDirectionToDestination(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection, float2 destination, int pathId)
    {
        float2 totalHeading = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float mateRelativeLocation = math.dot(desiredDirection, matePos - agentPos);
            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeLocation <= 0) { continue; }
            if (mateRelativeDirection <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);
            float alignmentRadius = (agentRadius + mateData.Radius) + 2f;
            if (distance > alignmentRadius) { continue; }

            float overlap = alignmentRadius - distance;
            totalHeading += mateDirection * overlap * 0.3f;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float waypointDistance = math.distance(agentPos, destination);
        float multiplier = AlignmentMultiplier;//math.select(waypointDistance / 10f, 1, waypointDistance > 10f);
        float2 steering = (averageHeading - desiredDirection) * multiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetSeperationForce(float2 agentPos, float2 agentDirection, float agentRadius, int agentIndex, AvoidanceStatus avoidance)
    {
        bool isAvoiding = avoidance != 0;
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status) == 0) { continue; }
            if(isAvoiding && mateData.Avoidance == 0) { continue; }
            if((mateData.Status & AgentStatus.HoldGround) == AgentStatus.HoldGround) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (distance > seperationRadius) { continue; }

            float dot = math.dot(agentDirection, matePos - agentPos);
            if (dot <= 0) { continue; }

            float overlapping = seperationRadius - distance;
            float multiplier = overlapping * SeperationMultiplier;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * multiplier, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        return totalSeperation;
    }
    float2 GetStoppedSeperationForce(float2 agentPos, float agentRadius, int agentIndex)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (distance > seperationRadius) { continue; }

            float overlapping = seperationRadius - distance;
            float multiplier = overlapping * SeperationMultiplier;
            float2 seperation = agentPos - matePos;
            seperation = math.select(seperation, new float2(i, 1), agentPos.Equals(matePos) && agentIndex < i);
            seperation = math.normalizesafe(seperation) * multiplier;
            totalSeperation += seperation;
        }
        return totalSeperation;
    }
    AvoidanceStatus GetAvoidanceStatus(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection)
    {
        NativeArray<AgentMovementData> agentMovementDataArray = AgentMovementDataArray;
        for (int i = 0; i < agentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = agentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 0.1f;

            if (distance > obstacleDetectionRange) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0.5f) { continue; }

            return DetermineAvoidance();
        }
        return 0;

        AvoidanceStatus DetermineAvoidance()
        {
            float2 totalLeftAvoiance = 0;
            float2 totalRightAvoidance = 0;
            for (int i = 0; i < agentMovementDataArray.Length; i++)
            {
                AgentMovementData mateData = agentMovementDataArray[i];
                if (i == agentIndex) { continue; }
                if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(matePos, agentPos);
                float obstacleDetectionRange = (agentRadius + mateData.Radius) + 0.2f + 3f;
                if (distance > obstacleDetectionRange) { continue; }
                float2 mateDir = matePos - agentPos;
                float dot = math.dot(desiredDirection, math.normalizesafe(mateDir));
                if (dot <= 0) { continue; }
                float2 leftAvoidance = math.normalizesafe(new float2(-mateDir.y, mateDir.x));
                float2 rightAvoidance = math.normalizesafe(new float2(mateDir.y, -mateDir.x));
                totalLeftAvoiance += leftAvoidance;
                totalRightAvoidance += rightAvoidance;
            }
            totalLeftAvoiance = math.normalizesafe(totalLeftAvoiance);
            totalRightAvoidance = math.normalizesafe(totalRightAvoidance);
            float leftDot = math.dot(totalLeftAvoiance, desiredDirection);
            float rightDot = math.dot(totalRightAvoidance, desiredDirection);
            return leftDot > rightDot ? AvoidanceStatus.L : AvoidanceStatus.R;
        }
    }
    float2 GetAvoidanceDirection(float2 agentPos, float2 currentDirection, float agentRadius, int agentIndex, AvoidanceStatus agentAvoidance)
    {
        float2 closestObstaclePos = 0;
        float closestObstacleRadius = 0;
        float closesObstacleDotProduct = float.MinValue;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);
            float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 1f;
            if (distance > obstacleDetectionRange) { continue; }
            float2 mateDir = matePos - agentPos;
            float dot = math.dot(currentDirection, math.normalizesafe(mateDir));
            if (dot < -1f) { continue; }
            
            if(dot > closesObstacleDotProduct)
            {
                closestObstaclePos = matePos;
                closestObstacleRadius = mateData.Radius;
                closesObstacleDotProduct = dot;
            }
        }
        if(closesObstacleDotProduct == float.MinValue) { return 0; }
        float totalRadius = closestObstacleRadius + agentRadius + SeperationRangeAddition;
        float2 destinationPoint = agentAvoidance == AvoidanceStatus.L ? GetLeftDirection(agentPos, closestObstaclePos, currentDirection, totalRadius) : GetRightDirection(agentPos, closestObstaclePos, currentDirection, totalRadius);
        return math.normalizesafe(destinationPoint - agentPos);

        float2 GetLeftDirection(float2 point, float2 circleCenter, float2 agentDirection, float circleRadius)
        {
            float2 obstacleRelativePos = circleCenter - point;
            float obstacleDistance = math.length(obstacleRelativePos);
            float2 agentDirectionResized = agentDirection * obstacleDistance;
            float2 newDirection = agentDirectionResized;
            float dotRotated = obstacleRelativePos.x * -agentDirectionResized.y + obstacleRelativePos.y * agentDirectionResized.x;
            if(dotRotated > 0)
            {
                obstacleRelativePos = obstacleRelativePos / obstacleDistance;
                agentDirectionResized = agentDirectionResized / obstacleDistance;
                float cos = math.dot(obstacleRelativePos, agentDirectionResized);
                float sin = math.sqrt(1 - cos * cos);
                newDirection = new float2(cos * obstacleRelativePos.x - sin * obstacleRelativePos.y, sin * obstacleRelativePos.x + cos * obstacleRelativePos.y) * obstacleDistance;
            }
            float2 newWorldDirection = point + newDirection;
            float2 snap = newWorldDirection - circleCenter;
            snap = math.normalizesafe(snap) * circleRadius;
            return circleCenter + snap;
        }
        float2 GetRightDirection(float2 point, float2 circleCenter, float2 agentDirection, float circleRadius)
        {
            float2 obstacleRelativePos = circleCenter - point;
            float obstacleDistance = math.length(obstacleRelativePos);
            float2 agentDirectionResized = agentDirection * obstacleDistance;
            float2 newDirection = agentDirectionResized;
            float dotRotated = obstacleRelativePos.x * -agentDirectionResized.y + obstacleRelativePos.y * agentDirectionResized.x;
            if (dotRotated < 0)
            {
                obstacleRelativePos = math.normalizesafe(obstacleRelativePos);
                agentDirectionResized = math.normalizesafe(agentDirectionResized);
                float cos = -1 * math.dot(obstacleRelativePos, agentDirectionResized);
                float sin = math.sqrt(1 - cos * cos);
                obstacleRelativePos *= -1;
                newDirection = new float2(cos * obstacleRelativePos.x - sin * obstacleRelativePos.y, sin * obstacleRelativePos.x + cos * obstacleRelativePos.y) * obstacleDistance;
            }
            float2 newWorldDirection = point + newDirection;
            float2 snap = newWorldDirection - circleCenter;
            snap = math.normalizesafe(snap) * circleRadius;
            return circleCenter + snap;
        }
    }
}
public struct Tension
{
    public int agent1;
    public int agent2;
}