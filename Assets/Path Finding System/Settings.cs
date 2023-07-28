using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Assets.Path_Finding_System
{
    internal class Settings : MonoBehaviour
    {
        [SerializeField] int maxFps;
        int _prevFps;
        private void Start()
        {
            _prevFps = maxFps;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.SetQualityLevel(0);
            Application.targetFrameRate = maxFps;
        }
        private void Update()
        {
            if(maxFps != _prevFps)
            {
                Application.targetFrameRate = maxFps;
                _prevFps = maxFps;
            }
        }
    }
}
