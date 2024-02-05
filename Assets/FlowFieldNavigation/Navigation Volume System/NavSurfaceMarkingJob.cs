using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct NavSurfaceMarkingJob : IJob
{
    internal float3 VolumeStartPos;
    internal float VoxHorSize;
    internal float VoxVerSize;
    internal int XVoxCount;
    internal int YVoxCount;
    internal int ZVoxCount;
    [ReadOnly] internal NativeArray<float3> Verts;
    [ReadOnly] internal NativeArray<int> Trigs;
    [WriteOnly] internal NativeBitArray VolumeMarks;
    public void Execute()
    {
        for (int i = 0; i < Trigs.Length; i += 3)
        {
            int v1Index = Trigs[i];
            int v2Index = Trigs[i + 1];
            int v3Index = Trigs[i + 2];
            float3 v1 = Verts[v1Index];
            float3 v2 = Verts[v2Index];
            float3 v3 = Verts[v3Index];

            float3 boundingBoxMin = math.min(math.min(v1, v2), v3);
            float3 boundingBoxMax = math.max(math.max(v1, v2), v3);
            float3 boundingBoxSize = boundingBoxMax - boundingBoxMin;

            int3 boxMinIndex = FlowFieldVolumeUtilities.PosToIndex(boundingBoxMin, VolumeStartPos, VoxHorSize, VoxVerSize);
            int3 boxMaxIndex = FlowFieldVolumeUtilities.PosToIndex(boundingBoxMax, VolumeStartPos, VoxHorSize, VoxVerSize);

            //Clamp
            boxMinIndex = FlowFieldVolumeUtilities.Clamp(boxMinIndex, XVoxCount, YVoxCount, ZVoxCount);
            boxMaxIndex = FlowFieldVolumeUtilities.Clamp(boxMaxIndex, XVoxCount, YVoxCount, ZVoxCount);
            for (int y = boxMinIndex.y; y <= boxMaxIndex.y; y++)
            {
                for (int z = boxMinIndex.z; z <= boxMaxIndex.z; z++)
                {
                    for (int x = boxMinIndex.x; x <= boxMaxIndex.x; x++)
                    {
                        int3 voxToCheck = new int3(x, y, z);
                        float3 voxelStartPos = FlowFieldVolumeUtilities.GetVoxelStartPos(voxToCheck, VolumeStartPos, VoxHorSize, VoxVerSize);
                        float3 voxelSize = new float3(VoxHorSize, VoxVerSize, VoxHorSize);
                        if (IsColliding(v1,v2,v3,voxelStartPos, voxelSize))
                        {
                            int vox1d = FlowFieldVolumeUtilities.To1D(voxToCheck, XVoxCount, YVoxCount, ZVoxCount);
                            VolumeMarks.Set(vox1d, true);
                        }
                    }
                }

            }
        }
        //+Get triangles
        //+Calculate bounding boxes
        //+Clamp the box to the volume
        //For each voxel within bounding box, check if it collides with the triangle
        //If yes, mark. Else, do not mark.
    }

    bool IsColliding(float3 trigv1, float3 trigv2, float3 trigv3, float3 voxelStartPos, float3 voxelSize)
    {
        float3 voxelMin = voxelStartPos;
        float3 voxelMax = voxelStartPos + voxelSize;
        bool3 moreThanMinV1 = trigv1 >= voxelMin;
        bool3 moreThanMinV2 = trigv2 >= voxelMin;
        bool3 moreThanMinV3 = trigv3 >= voxelMin;
        bool3 lessThanMaxV1 = trigv1 <= voxelMax;
        bool3 lessThanMaxV2 = trigv2 <= voxelMax;
        bool3 lessThanMaxV3 = trigv3 <= voxelMax;
        bool3 inside3 = moreThanMinV1 & moreThanMinV2 & moreThanMinV3 & lessThanMaxV1 & lessThanMaxV2 & lessThanMaxV3;
        bool inside = inside3.x & inside3.y & inside3.z;
        if (inside) { return true; }

        float3 leftFaceMin = voxelStartPos;
        float3 leftFaceMax = voxelStartPos + new float3(0f, voxelSize.y, voxelSize.z);
        if (XFaceCheck(leftFaceMin.x, leftFaceMin.y, leftFaceMax.y, leftFaceMin.z, leftFaceMax.z, trigv1, trigv2, trigv3)) { return true; }

        float3 rightFaceMin = voxelStartPos + new float3(voxelSize.x, 0f, 0f);
        float3 rightFaceMax = voxelStartPos + voxelSize;
        if (XFaceCheck(rightFaceMin.x, rightFaceMin.y, rightFaceMax.y, rightFaceMin.z, rightFaceMax.z, trigv1, trigv2, trigv3)) { return true; }

        float3 frontFaceMin = voxelStartPos;
        float3 frontFaceMax = voxelStartPos + new float3(voxelSize.x, voxelSize.y, 0);
        if (ZFaceCheck(frontFaceMin.z, frontFaceMin.x, frontFaceMax.x, frontFaceMin.y, frontFaceMax.y, trigv1, trigv2, trigv3)) { return true; }

        float3 backFaceMin = voxelStartPos + new float3(0, 0, voxelSize.z);
        float3 backFaceMax = voxelStartPos + voxelSize;
        if (ZFaceCheck(backFaceMin.z, backFaceMin.x, backFaceMax.x, backFaceMin.y, backFaceMax.y, trigv1, trigv2, trigv3)) { return true; }

        float3 topFaceMin = voxelStartPos + new float3(0, voxelSize.y, 0);
        float3 topFaceMax = voxelStartPos + voxelSize;
        if (YFaceCheck(topFaceMin.y, topFaceMin.x, topFaceMax.x, topFaceMin.z, topFaceMax.z, trigv1, trigv2, trigv3)) { return true; }

        float3 botFaceMin = voxelStartPos;
        float3 botFaceMax = voxelStartPos + new float3(voxelSize.x, 0, voxelSize.z);
        if (YFaceCheck(botFaceMin.y, botFaceMin.x, botFaceMax.x, botFaceMin.z, botFaceMax.z, trigv1, trigv2, trigv3)) { return true; }
        return false;
    }
    bool XFaceCheck(float faceX, float faceMinY, float faceMaxY, float faceMinZ, float faceMaxZ, float3 v1, float3 v2, float3 v3)
    {
        //If does not even reach
        float trigMaxX = math.max(math.max(v1.x, v2.x), v3.x);
        float trigMinX = math.min(math.min(v1.x, v2.x), v3.x);
        if (faceX > trigMaxX || faceX < trigMinX) { return false; }

        //Getting collision points to the plane of face
        float3x2 line1 = new float3x2(v1, v2);
        float3 line1Min = math.min(line1.c0, line1.c1);
        float3 line1Max = math.max(line1.c0, line1.c1);
        float3x2 line2 = new float3x2(v2, v3);
        float3 line2Min = math.min(line2.c0, line2.c1);
        float3 line2Max = math.max(line2.c0, line2.c1);
        float3x2 line3 = new float3x2(v3, v1);
        float3 line3Min = math.min(line3.c0, line3.c1);
        float3 line3Max = math.max(line3.c0, line3.c1);

        float3 line1CollisionPos = float.MaxValue;
        float3 line2CollisionPos = float.MaxValue;
        float3 line3CollisionPos = float.MaxValue;
        if (faceX > line1Min.x && faceX < line1Max.x)
        {
            float3 delta = line1.c1 - line1.c0;
            float3 initial = line1.c0;
            float t = (faceX - initial.x) / delta.x;
            float y = initial.y + delta.y * t;
            float z = initial.z + delta.z * t;
            line1CollisionPos = new float3(faceX, y, z);
        }
        if (faceX > line2Min.x && faceX < line2Max.x)
        {
            float3 delta = line2.c1 - line2.c0;
            float3 initial = line2.c0;
            float t = (faceX - initial.x) / delta.x;
            float y = initial.y + delta.y * t;
            float z = initial.z + delta.z * t;
            line2CollisionPos = new float3(faceX, y, z);
        }
        if (faceX > line3Min.x && faceX < line3Max.x)
        {
            float3 delta = line3.c1 - line3.c0;
            float3 initial = line3.c0;
            float t = (faceX - initial.x) / delta.x;
            float y = initial.y + delta.y * t;
            float z = initial.z + delta.z * t;
            line3CollisionPos = new float3(faceX, y, z);
        }
        float3 collisionPos1 = line1CollisionPos;
        float3 collisionPos2 = line2CollisionPos;
        collisionPos1 = math.select(collisionPos1, line3CollisionPos, collisionPos1 == float.MaxValue);
        collisionPos2 = math.select(collisionPos2, line3CollisionPos, collisionPos2 == float.MaxValue);
        float2x2 collisionLineSegment = new float2x2()
        {
            c0 = new float2(collisionPos1.y, collisionPos1.z),
            c1 = new float2(collisionPos2.y, collisionPos2.z),
        };
        float2x4 faceLineStrip = new float2x4()
        {
            c0 = new float2(faceMinY, faceMinZ),
            c1 = new float2(faceMinY, faceMaxZ),
            c2 = new float2(faceMaxY, faceMaxZ),
            c3 = new float2(faceMaxY, faceMinZ),
        };

        //Is line inside
        float2 faceMin = new float2(faceMinY, faceMinZ);
        float2 faceMax = new float2(faceMaxY, faceMaxZ);
        bool2 lessThanMax = collisionLineSegment.c0 < faceMax;
        bool2 moreThanMin = collisionLineSegment.c0 > faceMin;
        bool2 inside2 = lessThanMax & moreThanMin;
        bool inside = inside2.x & inside2.y;
        if (inside) { return true; }

        //Does line intersect
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c0, faceLineStrip.c1)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c1, faceLineStrip.c2)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c2, faceLineStrip.c3)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c3, faceLineStrip.c0)) { return true; }
        return false;
    }
    bool ZFaceCheck(float faceZ, float faceMinX, float faceMaxX, float faceMinY, float faceMaxY, float3 v1, float3 v2, float3 v3)
    {
        //If does not even reach
        float trigMaxZ = math.max(math.max(v1.z, v2.z), v3.z);
        float trigMinZ = math.min(math.min(v1.z, v2.z), v3.z);
        if (faceZ > trigMaxZ || faceZ < trigMinZ) { return false; }

        //Getting collision points to the plane of face
        float3x2 line1 = new float3x2(v1, v2);
        float3 line1Min = math.min(line1.c0, line1.c1);
        float3 line1Max = math.max(line1.c0, line1.c1);
        float3x2 line2 = new float3x2(v2, v3);
        float3 line2Min = math.min(line2.c0, line2.c1);
        float3 line2Max = math.max(line2.c0, line2.c1);
        float3x2 line3 = new float3x2(v3, v1);
        float3 line3Min = math.min(line3.c0, line3.c1);
        float3 line3Max = math.max(line3.c0, line3.c1);

        float3 line1CollisionPos = float.MaxValue;
        float3 line2CollisionPos = float.MaxValue;
        float3 line3CollisionPos = float.MaxValue;
        if (faceZ > line1Min.z && faceZ < line1Max.z)
        {
            float3 delta = line1.c1 - line1.c0;
            float3 initial = line1.c0;
            float t = (faceZ - initial.z) / delta.z;
            float y = initial.y + delta.y * t;
            float x = initial.x + delta.x * t;
            line1CollisionPos = new float3(x, y, faceZ);
        }
        if (faceZ > line2Min.z && faceZ < line2Max.z)
        {
            float3 delta = line2.c1 - line2.c0;
            float3 initial = line2.c0;
            float t = (faceZ - initial.z) / delta.z;
            float y = initial.y + delta.y * t;
            float x = initial.x + delta.x * t;
            line2CollisionPos = new float3(x, y, faceZ);
        }
        if (faceZ > line3Min.z && faceZ < line3Max.z)
        {
            float3 delta = line3.c1 - line3.c0;
            float3 initial = line3.c0;
            float t = (faceZ - initial.z) / delta.z;
            float y = initial.y + delta.y * t;
            float x = initial.x + delta.x * t;
            line3CollisionPos = new float3(x, y, faceZ);
        }
        float3 collisionPos1 = line1CollisionPos;
        float3 collisionPos2 = line2CollisionPos;
        collisionPos1 = math.select(collisionPos1, line3CollisionPos, collisionPos1 == float.MaxValue);
        collisionPos2 = math.select(collisionPos2, line3CollisionPos, collisionPos2 == float.MaxValue);
        float2x2 collisionLineSegment = new float2x2()
        {
            c0 = new float2(collisionPos1.x, collisionPos1.y),
            c1 = new float2(collisionPos2.x, collisionPos2.y),
        };
        float2x4 faceLineStrip = new float2x4()
        {
            c0 = new float2(faceMinX, faceMinY),
            c1 = new float2(faceMinX, faceMaxY),
            c2 = new float2(faceMaxX, faceMaxY),
            c3 = new float2(faceMaxX, faceMinY),
        };

        //Is line inside
        float2 faceMin = new float2(faceMinX, faceMinY);
        float2 faceMax = new float2(faceMaxX, faceMaxY);
        bool2 lessThanMax = collisionLineSegment.c0 < faceMax;
        bool2 moreThanMin = collisionLineSegment.c0 > faceMin;
        bool2 inside2 = lessThanMax & moreThanMin;
        bool inside = inside2.x & inside2.y;
        if (inside) { return true; }

        //Does line intersect
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c0, faceLineStrip.c1)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c1, faceLineStrip.c2)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c2, faceLineStrip.c3)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c3, faceLineStrip.c0)) { return true; }
        return false;
    }
    bool YFaceCheck(float faceY, float faceMinX, float faceMaxX, float faceMinZ, float faceMaxZ, float3 v1, float3 v2, float3 v3)
    {
        //If does not even reach
        float trigMaxY = math.max(math.max(v1.y, v2.y), v3.y);
        float trigMinY = math.min(math.min(v1.y, v2.y), v3.y);
        if (faceY > trigMaxY || faceY < trigMinY) { return false; }

        //Getting collision points to the plane of face
        float3x2 line1 = new float3x2(v1, v2);
        float3 line1Min = math.min(line1.c0, line1.c1);
        float3 line1Max = math.max(line1.c0, line1.c1);
        float3x2 line2 = new float3x2(v2, v3);
        float3 line2Min = math.min(line2.c0, line2.c1);
        float3 line2Max = math.max(line2.c0, line2.c1);
        float3x2 line3 = new float3x2(v3, v1);
        float3 line3Min = math.min(line3.c0, line3.c1);
        float3 line3Max = math.max(line3.c0, line3.c1);

        float3 line1CollisionPos = float.MaxValue;
        float3 line2CollisionPos = float.MaxValue;
        float3 line3CollisionPos = float.MaxValue;
        if (faceY > line1Min.y && faceY < line1Max.y)
        {
            float3 delta = line1.c1 - line1.c0;
            float3 initial = line1.c0;
            float t = (faceY - initial.y) / delta.y;
            float x = initial.x + delta.x * t;
            float z = initial.z + delta.z * t;
            line1CollisionPos = new float3(x, faceY, z);
        }
        if (faceY > line2Min.y && faceY < line2Max.y)
        {
            float3 delta = line2.c1 - line2.c0;
            float3 initial = line2.c0;
            float t = (faceY - initial.y) / delta.y;
            float x = initial.x + delta.x * t;
            float z = initial.z + delta.z * t;
            line2CollisionPos = new float3(x, faceY, z);
        }
        if (faceY > line3Min.y && faceY < line3Max.y)
        {
            float3 delta = line3.c1 - line3.c0;
            float3 initial = line3.c0;
            float t = (faceY - initial.y) / delta.y;
            float x = initial.x + delta.x * t;
            float z = initial.z + delta.z * t;
            line3CollisionPos = new float3(x, faceY, z);
        }
        float3 collisionPos1 = line1CollisionPos;
        float3 collisionPos2 = line2CollisionPos;
        collisionPos1 = math.select(collisionPos1, line3CollisionPos, collisionPos1 == float.MaxValue);
        collisionPos2 = math.select(collisionPos2, line3CollisionPos, collisionPos2 == float.MaxValue);
        float2x2 collisionLineSegment = new float2x2()
        {
            c0 = new float2(collisionPos1.x, collisionPos1.z),
            c1 = new float2(collisionPos2.x, collisionPos2.z),
        };
        float2x4 faceLineStrip = new float2x4()
        {
            c0 = new float2(faceMinX, faceMinZ),
            c1 = new float2(faceMinX, faceMaxZ),
            c2 = new float2(faceMaxX, faceMaxZ),
            c3 = new float2(faceMaxX, faceMinZ),
        };

        //Is line inside
        float2 faceMin = new float2(faceMinX, faceMinZ);
        float2 faceMax = new float2(faceMaxX, faceMaxZ);
        bool2 lessThanMax = collisionLineSegment.c0 < faceMax;
        bool2 moreThanMin = collisionLineSegment.c0 > faceMin;
        bool2 inside2 = lessThanMax & moreThanMin;
        bool inside = inside2.x & inside2.y;
        if (inside) { return true; }

        //Does line intersect
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c0, faceLineStrip.c1)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c1, faceLineStrip.c2)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c2, faceLineStrip.c3)) { return true; }
        if (LinesIntersect(collisionLineSegment.c0, collisionLineSegment.c1, faceLineStrip.c3, faceLineStrip.c0)) { return true; }
        return false;
    }
    bool LinesIntersect(float2 p0, float2 p1, float2 p2, float2 p3)
    {
        float2 v1 = p1 - p0;
        float2 v2 = p3 - p2;
        float alpha = (-v1.y * (p0.x - p2.x) + v1.x * (p0.y - p2.y)) / (-v2.x * v1.y + v1.x * v2.y);
        float beta = (v2.x * (p0.y - p2.y) - v2.y * (p0.x - p2.x)) / (-v2.x * v1.y + v1.x * v2.y);
        return alpha >= 0 && alpha <= 1 && beta >= 0 && beta <= 1;
    }
}
