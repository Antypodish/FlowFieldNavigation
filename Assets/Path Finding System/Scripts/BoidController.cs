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

        public float SeperationMultiplier;
        public float SeperationRangeAddition;
        public float SeekMultiplier;
        public float AlignmentMultiplier;
        private void Start()
        {
            Instance = this;
        }
    }
}
