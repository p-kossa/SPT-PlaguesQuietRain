using System;
using System.Reflection;
using EFT.EnvironmentEffect;
using UnityEngine;

namespace QuietRain
{
    public class QuietRainClass : MonoBehaviour
    {

        public static bool ambienceChanged = true;
        public static EnvironmentManager environmentManager = null;

        public void Start()
        {
            QuietRainPlugin.RainVolume.SettingChanged += UpdateQuietRain;
            QuietRainPlugin.AmbienceVolume.SettingChanged += UpdateAmbience;
        }

        private void UpdateQuietRain(object sender, EventArgs e)
        {
            if(environmentManager != null)
            {
                MethodInfo Init = environmentManager.GetType().GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic);
                Init.Invoke(environmentManager, null);
                MethodInfo method_4 = environmentManager.GetType().GetMethod("method_4", BindingFlags.Instance | BindingFlags.NonPublic);
                method_4.Invoke(environmentManager, null);
            }
        }

        private void UpdateAmbience(object sender, EventArgs e)
        {
            ambienceChanged = true;
        }
    }
}