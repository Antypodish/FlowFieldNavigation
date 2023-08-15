using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling.Memory.Experimental;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

[BurstCompile]
public struct LocalAvoidanceJob : IJob
{
    public float MovingForeignFlockSeperationRangeMultiplier;
    public float SeperationRangeAddition;
    public float SeperationMultiplier;
    public float AlignmentRadiusMultiplier;
    public float AlignmentDecreaseStartDistance;
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<float2> AgentDirections;
    

    public void Execute()
    {
        for(int index = 0; index < AgentMovementDataArray.Length; index++)
        {
            AgentMovementData agentData = AgentMovementDataArray[index];
            float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
            float2 finalDirection = agentData.Flow;
            if ((agentData.Status & AgentStatus.Moving) == AgentStatus.Moving)
            {
                if(!GetAvoidance(agentData, index, finalDirection, out finalDirection))
                {
                    agentData.Avoidance = 0;
                    AgentMovementDataArray[index] = agentData;
                }
                if (agentData.Waypoint.position.Equals(agentData.Destination))
                {
                    finalDirection = GetAlignmentDirectionToDestination(agentPos, agentData.Radius, index, finalDirection, agentData.PathId, agentData.Destination);
                }
                else
                {
                    finalDirection = GetAlignedDirectionDecreasing(agentPos, agentData.Radius, index, finalDirection, agentData.Waypoint, agentData.PathId);
                }
                finalDirection = GetSeperationNew(agentPos, agentData.Radius, agentData.PathId, index, finalDirection);
                AgentDirections[index] = finalDirection;
            }
            else if ((agentData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround)
            {
                AgentDirections[index] = GetStoppedSeperationNew(agentPos, agentData.Radius, index, agentData.Flow);
            }
        }

    }
    bool GetAvoidance(AgentMovementData agentData, int agentIndex, float2 desiredDirection, out float2 finalDirection)
    {
        finalDirection = desiredDirection;
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float agentRadius = agentData.Radius;
        NativeArray<AgentMovementData> agentMovementDataArray = AgentMovementDataArray;
        if (agentData.Avoidance == AvoidanceStatus.None)
        {
            for (int i = 0; i < agentMovementDataArray.Length; i++)
            {
                AgentMovementData mateData = agentMovementDataArray[i];
                if (i == agentIndex) { continue; }
                if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }

                float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(matePos, agentPos);

                float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 1f;

                if (distance > obstacleDetectionRange) { continue; }

                float dot = math.dot(desiredDirection, matePos - agentPos);
                if (dot <= 0) { continue; }

                float2 obstacleCenter = DetectObstacle(i, 2.5f);
                agentData.Avoidance = GetAvoidanceStatus(agentPos, agentData.Waypoint.position, obstacleCenter, matePos);
                AgentMovementDataArray[agentIndex] = agentData;
                return true;
            }
            return false; ;
        }
        else if(agentData.Avoidance == AvoidanceStatus.CW)
        {
            float2 totalAvoidance = 0;
            bool holdGroundDetected = false;
            for (int i = 0; i < agentMovementDataArray.Length; i++)
            {
                AgentMovementData mateData = agentMovementDataArray[i];
                if (i == agentIndex) { continue; }
                if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(matePos, agentPos);
                float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 3f;
                if (distance > obstacleDetectionRange) { continue; }
                float2 mateDir = matePos - agentPos;
                float dot = math.dot(desiredDirection, mateDir);
                if (dot <= 0) { continue; }
                float overlapping = obstacleDetectionRange - distance;
                float2 avoidance = math.normalizesafe(new float2(mateDir.y, -mateDir.x));
                totalAvoidance += avoidance;
                holdGroundDetected = true;
            }
            totalAvoidance = math.normalizesafe(totalAvoidance);
            finalDirection = totalAvoidance;
            return holdGroundDetected;
        }
        else if(agentData.Avoidance == AvoidanceStatus.CCW)
        {
            float2 totalAvoidance = 0;
            bool holdGroundDetected = false;
            for (int i = 0; i < agentMovementDataArray.Length; i++)
            {
                AgentMovementData mateData = agentMovementDataArray[i];
                if (i == agentIndex) { continue; }
                if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(matePos, agentPos);
                float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 3f;
                if (distance > obstacleDetectionRange) { continue; }
                float2 mateDir = matePos - agentPos;
                float dot = math.dot(desiredDirection, mateDir);
                if (dot <= 0) { continue; }
                float overlapping = obstacleDetectionRange - distance;
                float2 avoidance = math.normalizesafe(new float2(-mateDir.y, mateDir.x));
                totalAvoidance += avoidance;
                holdGroundDetected = true;
            }
            totalAvoidance = math.normalizesafe(totalAvoidance);
            finalDirection = totalAvoidance;
            return holdGroundDetected;
        }
        return false;


        AvoidanceStatus GetAvoidanceStatus(float2 agentPos, float2 waypointPos, float2 obstacleCenter, float2 obstacleAgentPos)
        {
            float2 obstacleDirection = obstacleCenter - agentPos;
            float2 waypointDirection = waypointPos - agentPos;
            float dotRotated = obstacleDirection.x * -waypointDirection.y + obstacleDirection.y * waypointDirection.x;
            return dotRotated > 0 ? AvoidanceStatus.CW : AvoidanceStatus.CCW;
        }
        float2 DetectObstacle(int startAgentIndex, float maxDistance)
        {
            float2 obstaclePos = 0;
            int detectedAgentCount = 0;
            NativeQueue<int> neighbours = new NativeQueue<int>(Allocator.Temp);

            neighbours.Enqueue(startAgentIndex);

            AgentMovementData agentData = agentMovementDataArray[startAgentIndex];
            agentData.RoutineStatus |= AgentRoutineStatus.Traversed;
            agentMovementDataArray[startAgentIndex] = agentData;

            while (!neighbours.IsEmpty())
            {
                detectedAgentCount++;
                int agentIndex = neighbours.Dequeue();
                agentData = agentMovementDataArray[agentIndex];
                obstaclePos += new float2(agentData.Position.x, agentData.Position.z);
                GetNeighbourAgents(agentIndex, new float2(agentData.Position.x, agentData.Position.z), maxDistance, neighbours);
            }
            return obstaclePos / detectedAgentCount;
        }
        void GetNeighbourAgents(int agentIndex, float2 agentPos, float maxDistance, NativeQueue<int> neighbours)
        {
            for (int i = 0; i < agentMovementDataArray.Length; i++)
            {
                AgentMovementData mateData = agentMovementDataArray[i];
                if (i == agentIndex) { continue; }
                if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }

                float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(matePos, agentPos);

                if (distance > maxDistance) { continue; }
                if ((mateData.RoutineStatus & AgentRoutineStatus.Traversed) != AgentRoutineStatus.Traversed)
                {
                    mateData.RoutineStatus |= AgentRoutineStatus.Traversed;
                    agentMovementDataArray[i] = mateData;
                    neighbours.Enqueue(i);
                }
            }
        }
    }

    float2 GetSeperationNew(float2 agentPos, float agentRadius, int pathId, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        bool nonFlowMate = false;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);
            
            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (mateData.PathId != pathId)
            {
                nonFlowMate = true;
                seperationRadius *= MovingForeignFlockSeperationRangeMultiplier;
            }
            if (distance > seperationRadius) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0) { continue; }

            float overlapping = seperationRadius - distance;
            float multiplier = overlapping * SeperationMultiplier;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * multiplier, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 newVelocity = desiredDirection + totalSeperation;
        if (!nonFlowMate && math.dot(newVelocity, desiredDirection) < 0)
        {

            float2 newDir = math.select(math.normalize(newVelocity), 0f, newVelocity.Equals(0));
            float2 perp1 = new float2(1, (-desiredDirection.x) / math.select(desiredDirection.y, 0.000001f , desiredDirection.y == 0));;
            float2 perp2 = new float2(-1, desiredDirection.x / math.select(desiredDirection.y, 0.000001f, desiredDirection.y == 0));
            perp1 = math.normalize(perp1);
            perp2 = math.normalize(perp2);
            float perp1Distance = math.distance(perp1, newDir);
            float perp2Distance = math.distance(perp2, newDir);
            newVelocity = math.select(perp2, perp1, perp1Distance < perp2Distance);
        }
        else
        {
            newVelocity = math.select(newVelocity, math.normalize(newVelocity), math.length(newVelocity) > 1);
        }
        return newVelocity;
    }
    float2 GetStoppedSeperationNew(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);
            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (distance >= seperationRadius) { continue; }
            float overlapping = seperationRadius - distance;
            float multiplier = overlapping * SeperationMultiplier;
            float2 push = (agentPos - matePos) * multiplier;
            push = math.select(push, new float2(i, 1), agentPos.Equals(matePos) && agentIndex < i);
            push = math.normalizesafe(push);
            totalSeperation += push;
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 newVelocity = desiredDirection + totalSeperation;
        newVelocity = math.select(newVelocity, math.normalize(newVelocity), math.length(newVelocity) > 1);
        return newVelocity;
    }

    float2 GetAlignedDirectionDecreasing(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection, Waypoint agentWaypoint, int pathId)
    {
        float2 totalHeading = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (!mateData.Waypoint.position.Equals(agentWaypoint.position)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float mateRelativeLocation = math.dot(desiredDirection, matePos - agentPos);
            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeLocation <= 0) { continue; }
            if (mateRelativeDirection <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);
            float alignmentRadius = (agentRadius + mateData.Radius) * AlignmentRadiusMultiplier;
            if (distance > alignmentRadius) { continue; }

            totalHeading += mateDirection;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float waypointDistance = math.distance(agentPos, agentWaypoint.position);
        float multiplier = math.select(waypointDistance / AlignmentDecreaseStartDistance, 1, waypointDistance > AlignmentDecreaseStartDistance);
        float2 steering = (averageHeading - desiredDirection) * multiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetAlignmentDirectionToDestination(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection, int pathId, float2 destination)
    {
        float2 totalHeading = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (!mateData.Waypoint.position.Equals(destination)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeDirection <= 0) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);
            float alignmentRadius = (agentRadius + mateData.Radius) * AlignmentRadiusMultiplier;
            if (distance > alignmentRadius) { continue; }
            totalHeading += mateDirection;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float2 newDirection = averageHeading;
        return newDirection;
    }
    float2 GetSeperationOld(float2 agentPos, float agentRadius, int pathId, int agentIndex, float2 desiredDirection)
    {
        float _flockmateSeperationRange = 1.2f;
        float _foreignSeperationRange = 1.6f;
        float _foreignSeperationMultiplier = 0.48f;
        float _flockmateSeperationMultiplier = 0.7f;

        float2 totalSeperation = 0;
        bool foreign = false;
        bool push = false;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];

            if (agentIndex == i) { continue; }

            if (pathId == mateData.PathId)
            {
                float2 foreignAgentPosition = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(foreignAgentPosition, agentPos);

                if (distance > _flockmateSeperationRange) { continue; }

                float dot = math.dot(desiredDirection, foreignAgentPosition - agentPos);
                if (dot <= 0) { continue; }

                float overlapping = _flockmateSeperationRange - distance;
                totalSeperation += math.select(math.normalize(agentPos - foreignAgentPosition) * overlapping, 0f, agentPos.Equals(foreignAgentPosition));
            }
            else
            {
                float2 foreignAgentPosition = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(foreignAgentPosition, agentPos);

                if (distance > _foreignSeperationRange) { continue; }

                float dot = math.dot(desiredDirection, foreignAgentPosition - agentPos);
                if (dot <= 0) { continue; }

                float overlapping = _foreignSeperationRange - distance;
                if (overlapping > 0.6) { push = true; }
                totalSeperation += math.select(math.normalize(agentPos - foreignAgentPosition) * overlapping, 0f, agentPos.Equals(foreignAgentPosition));
                foreign = true;
            }
        }
        if (totalSeperation.Equals(0f)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        _foreignSeperationMultiplier = math.select(_foreignSeperationMultiplier, 0.9f, push);
        float2 steering = (seperationDirection - desiredDirection) * math.select(_flockmateSeperationMultiplier, _foreignSeperationMultiplier, foreign);
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0f, desiredDirection.Equals(-steering));
        return newDirection;
    }
    float2 GetPush(float2 agentPos, float agentRadius, int pathId, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (mateData.PathId != pathId) { continue; }
            if (distance > seperationRadius) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot >= 0) { continue; }
            float mateRelativeDirection = math.dot(desiredDirection, mateData.Flow);
            if (mateRelativeDirection <= 0.3f) { continue; }

            float overlapping = seperationRadius - distance;
            if (overlapping < 0.5f) { continue; }
            float multiplier = overlapping * SeperationMultiplier;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * multiplier, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        totalSeperation = math.normalize(totalSeperation);
        float2 newVelocity = desiredDirection + totalSeperation;
        newVelocity = math.select(newVelocity, math.normalize(newVelocity), math.length(newVelocity) > 1);
        return newVelocity;
    }
    /*
    //LEGACY ALGORITHMS
    float2 GetSeperatedDirection(float2 agentPos, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            if (distance > SeperationRadius * 2 * 1f) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0) { continue; }

            float overlapping = SeperationRadius * 2 - distance;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * overlapping, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        float2 steering = (seperationDirection - desiredDirection) * SeperationMultiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetPushingForce(float2 agentPos, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];

            if (i == agentIndex) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            if (distance > SeperationRadius * 2) { continue; }

            float overlapping = SeperationRadius * 2 - distance;

            float2 push = math.select(math.normalize(agentPos - matePos) * overlapping, 0, overlapping == 0);
            push = math.select(push, math.normalize(new float2(i, 1)), agentPos.Equals(matePos));
            totalSeperation += push;
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        float2 steering = (seperationDirection - desiredDirection) * 0.5f;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetAlignedDirection(float2 agentPos, int agentIndex, float2 desiredDirection, Waypoint agentWaypoint, int pathId)
    {
        float2 totalHeading = 0;
        int count = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (!mateData.waypoint.position.Equals(agentWaypoint.position)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float mateRelativeLocation = math.dot(desiredDirection, matePos - agentPos);
            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeLocation <= 0) { continue; }
            if (mateRelativeDirection <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);

            if (distance > AlignmentRadius * 2) { continue; }

            float overlapping = AlignmentRadius * 2 - distance;
            totalHeading += mateDirection;
            count++;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float2 steering = (averageHeading - desiredDirection) * AlignmentMultiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }*/
}
