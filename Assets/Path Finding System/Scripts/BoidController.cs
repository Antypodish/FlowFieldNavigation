using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


internal class BoidController : MonoBehaviour
{
    public static BoidController Instance;

    public float SeperationMultiplier;//15
    public float SeperationRangeAddition;//0.2
    public float SeekMultiplier;//0.1
    public float AlignmentMultiplier;//0.95
    public float AlignmentRangeAddition;//3
    public float MovingAvoidanceRangeAddition;//0.05
    private void Start()
    {
        Instance = this;
    }
}