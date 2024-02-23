using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEngine.UIElements;
using System;
public class testjob : MonoBehaviour
{
    private void Update()
    {/*
        int i = -0b_10000000_00000000_00000000_00000000;
        UnityEngine.Debug.Log(Convert.ToString(i, 2));*/
    }
    /*
    GameObject towardsObject;
    [SerializeField] float2 Current;
    [SerializeField] float CircularSpeed;
    float2 Towards;
    private void Start()
    {
    }
    private void OnDrawGizmos()
    {
        if(towardsObject == null)
        {
            towardsObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            towardsObject.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        }
        Towards = new float2(towardsObject.transform.position.x, towardsObject.transform.position.z);
        Current = math.normalizesafe(Current);
        Towards = math.normalizesafe(Towards);
        float3 current3 = new float3(Current.x, 0f, Current.y);
        float3 towards3 = new float3(Towards.x, 0f, Towards.y);

        RotateTowards(Current, Towards, CircularSpeed, Time.deltaTime, out float2 circularVelocity, out float2 newVelocity);
        float3 circularVelocity3 = new float3(circularVelocity.x, 0f, circularVelocity.y);
        float3 newVelocity3 = new float3(newVelocity.x, 0f, newVelocity.y);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(float3.zero, current3);
        Handles.Label(current3, "current");
        Gizmos.color = Color.white;
        Gizmos.DrawLine(float3.zero, towards3);
        Handles.Label(towards3, "towards");
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(current3, current3 + circularVelocity3);
        Handles.Label(current3 + circularVelocity3, "circularVelocity");
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(float3.zero, newVelocity3);
        Current = newVelocity;
    }

    void RotateTowards(float2 current, float2 towards, float circulatSpeed, float deltaTime, out float2 circularVelocity, out float2 newVelocity)
    {
        circularVelocity = GetCircularVelocityNormalized(current, towards) * circulatSpeed * deltaTime;
        float2 newDirection = current + circularVelocity;

        bool isOldLeft = IsLeft(current, towards);
        bool isNewLeft = IsLeft(newDirection, towards);
        if(isOldLeft ^ isNewLeft)
        {
            newVelocity = math.normalizesafe(towards);
        }
        else
        {
            newVelocity = math.normalizesafe(newDirection);
        }
    }
    
    float2 GetCircularVelocityNormalized(float2 current, float2 towards)
    {
        if(IsLeft(towards, current))
        {
            return math.normalizesafe(new float2(-current.y, current.x));
        }
        return math.normalizesafe(new float2(current.y, -current.x));
    }
    bool IsLeft(float2 operand, float2 referance)
    {
        return math.dot(referance, new float2(-operand.y, operand.x)) < 0;
    }*/
}