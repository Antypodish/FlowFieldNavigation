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

        [SerializeField] public float SeperationRange;
        [SerializeField] [Range(0,1)] public float SeperationMultiplier;

        private void Start()
        {
            Instance = this;
        }
    }
}
