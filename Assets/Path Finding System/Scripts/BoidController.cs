using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Path_Finding_System.Scripts
{
    internal class BoidController : MonoBehaviour
    {
        public static BoidController Instance;

        [Header("Seperation")]
        public float SeperationRangeAddition;
        [Range(0,100)] public float SeperationMultiplier;
        [Range(0, 10)] public float MovingForeignFlockSeperationRangeMultiplier;
        [Header("Alignment")]
        public float AlignmentRangeMultiplier;
        public float AlignmentDecreaseStartDistance;

        private void Start()
        {
            Instance = this;
        }
    }
}
