using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


internal class BoidController : MonoBehaviour
{
    public static BoidController Instance;

    public float SeperationMultiplier;
    public float SeperationRangeAddition;
    public float SeekMultiplier;
    public float AlignmentMultiplier;
    public float AlignmentRangeAddition;
    public float MovingAvoidanceRangeAddition;
    private void Start()
    {
        Instance = this;
    }
}