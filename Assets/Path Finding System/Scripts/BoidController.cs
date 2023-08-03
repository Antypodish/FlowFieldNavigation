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
        public float SeperationRange;
        [Range(0,100)] public float SeperationMultiplier;
        [Header("Alignment")]
        public float AlignmentRange;
        [Range(0,1)] public float AlignmentMultiplier;
        private void Start()
        {
            Instance = this;
        }
    }
}
