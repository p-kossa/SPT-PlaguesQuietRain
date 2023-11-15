using System.Reflection;
using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using EFT.EnvironmentEffect;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;
#if DEBUG
    using HarmonyLib.Tools;
#endif

namespace QuietRain
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class QuietRainPlugin : BaseUnityPlugin
    {

        public static GameObject Hook;
        public static QuietRainClass QuietRainClassComponent;

        public static ConfigEntry<float> AmbienceVolume;
        public static ConfigEntry<float> RainVolume;
        public static ConfigEntry<float> ThunderVolume;

        private void Awake()
        {
            #if DEBUG
                HarmonyFileLog.Enabled = true;
                Debug.LogError("Quiet Rain Awake()");
            #endif
            Hook = new GameObject();
            QuietRainClassComponent = Hook.AddComponent<QuietRainClass>();
            DontDestroyOnLoad(Hook);
        }

        private void Start()
        {
            AmbienceVolume = Config.Bind("World", "Outdoors Ambience Volume", 1.0f, new ConfigDescription("Sets other outdoors ambience volume (bird chirping, wind, etc.) [0-1]", new AcceptableValueRange<float>(0.0f, 1.0f), new ConfigurationManagerAttributes { Order = 300, IsAdvanced = false }));
            RainVolume = Config.Bind("World", "Rain Volume", 1.0f, new ConfigDescription("Sets rain volume [0-1]", new AcceptableValueRange<float>(0.0f, 1.0f), new ConfigurationManagerAttributes { Order = 200, IsAdvanced = false }));
            ThunderVolume = Config.Bind("World", "Thunder Volume", 1.0f, new ConfigDescription("Sets lightning/thunder volume [0-1]", new AcceptableValueRange<float>(0.0f, 1.0f), new ConfigurationManagerAttributes { Order = 100, IsAdvanced = false }));

            new QuietRainPatch1().Enable();
            new QuietRainPatch2().Enable();
            new QuietLightningPatch().Enable();
            new QuietThunderPatch().Enable();
            new QuietAmbiencePatch().Enable();
        }

        private static IEnumerable<CodeInstruction> ThunderLightningPatch(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);

            int loadVolumeArgumentPosition = -1;
            for (int i = 0; i < newInstructions.Count; i++)
            {
                if (newInstructions[i].Calls(typeof(MonoBehaviourSingleton<BetterAudio>).GetMethod("get_Instance", BindingFlags.Static | BindingFlags.Public)))
                {
                    for (int j = i; j < newInstructions.Count; j++)
                    {
                        if (newInstructions[j].opcode == OpCodes.Ldc_R4 && (float)newInstructions[j].operand == 1f)
                        {
                            loadVolumeArgumentPosition = j;
                            break;
                        }
                    }
                    break;
                }
            }

            if (loadVolumeArgumentPosition != -1)
            {
                newInstructions.Insert(loadVolumeArgumentPosition, new CodeInstruction(OpCodes.Ldsfld, typeof(QuietRainPlugin).GetField("ThunderVolume", BindingFlags.Static | BindingFlags.Public)));
                newInstructions.Insert(loadVolumeArgumentPosition + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ConfigEntry<float>), "Value").GetGetMethod()));
                newInstructions.RemoveAt(loadVolumeArgumentPosition + 2);
            }

            #if DEBUG
                FileLog.Log("\nOld Instructions:\n");
                foreach (CodeInstruction ins in instructions)
                    FileLog.Log(ins.ToString());
                FileLog.Log("\nNew Instructions:\n");
                foreach (CodeInstruction ins in newInstructions)
                    FileLog.Log(ins.ToString());
            #endif

            return newInstructions.AsEnumerable();
        }

        public class QuietRainPatch1 : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(EnvironmentManager).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            [PatchPrefix]
            private static void PatchPreFix(ref EnvironmentManager __instance)
            {
                QuietRainClass.ambienceChanged = true;
                QuietRainClass.environmentManager = __instance;
                Traverse.Create(__instance).Field("OutdoorRainVolume").SetValue(RainVolume.Value);
                Traverse.Create(__instance).Field("RainVolume").SetValue(RainVolume.Value * 0.7f);
            }
        }

        public class QuietRainPatch2 : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(EnvironmentManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            [PatchPrefix]
            private static void PatchPreFix(ref EnvironmentManager __instance)
            {
                QuietRainClass.environmentManager = null;
            }
        }

        public class QuietLightningPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(LightningController).GetMethod("SummonLightning", BindingFlags.Instance | BindingFlags.Public);
            }

            #if DEBUG
            [HarmonyEmitIL("./dumps")]
            #endif
            [PatchTranspiler]
            private static IEnumerable<CodeInstruction> PatchTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                #if DEBUG
                    FileLog.Log("\nPatching SummonLightning\n");
                #endif
                return ThunderLightningPatch(instructions);
            }
        }

        public class QuietThunderPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(LightningController).GetMethod("SummonThunder", BindingFlags.Instance | BindingFlags.Public);
            }

            #if DEBUG
            [HarmonyEmitIL("./dumps")]
            #endif
            [PatchTranspiler]
            private static IEnumerable<CodeInstruction> PatchTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                #if DEBUG
                    FileLog.Log("\nPatching SummonThunder\n");
                #endif
                return ThunderLightningPatch(instructions);
            }
        }

        public class QuietAmbiencePatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(EnvironmentManager).GetMethod("method_7", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            #if DEBUG
            [HarmonyEmitIL("./dumps")]
            #endif
            [PatchTranspiler]
            private static IEnumerable<CodeInstruction> PatchTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                #if DEBUG
                    FileLog.Log("\nPatching method_7\n");
                #endif

                List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);

                Label label = generator.DefineLabel();

                for (int i = 0; i < newInstructions.Count; i++)
                {
                    if (newInstructions[i].opcode == OpCodes.Ble_Un)
                    {
                        newInstructions[i].operand = label;
                        for (int j = i + 1; j < newInstructions.Count; j++)
                        {
                            if (newInstructions[j].opcode == OpCodes.Bge_Un)
                            {
                                newInstructions[j].operand = label;
                                break;
                            }
                        }
                        break;
                    }
                }

                int loadVolumeArgumentPosition = -1;
                for (int i = 0; i < newInstructions.Count; i++)
                {
                    if (newInstructions[i].opcode == OpCodes.Ldloc_2)
                    {
                        loadVolumeArgumentPosition = i + 2;
                        break;
                    }
                }

                if (loadVolumeArgumentPosition != -1)
                {
                    newInstructions.Insert(loadVolumeArgumentPosition, new CodeInstruction(OpCodes.Ldsfld, typeof(QuietRainPlugin).GetField("AmbienceVolume", BindingFlags.Static | BindingFlags.Public)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ConfigEntry<float>), "Value").GetGetMethod()));
                    newInstructions.Insert(loadVolumeArgumentPosition + 2, new CodeInstruction(OpCodes.Mul));
                }

                int temp = loadVolumeArgumentPosition;
                loadVolumeArgumentPosition = -1;
                for (int i = temp; i < newInstructions.Count; i++)
                {
                    if (newInstructions[i].opcode == OpCodes.Ldloc_2)
                    {
                        loadVolumeArgumentPosition = i + 1;
                        break;
                    }
                }

                if (loadVolumeArgumentPosition != -1)
                {
                    newInstructions.Insert(loadVolumeArgumentPosition, new CodeInstruction(OpCodes.Ldsfld, typeof(QuietRainPlugin).GetField("AmbienceVolume", BindingFlags.Static | BindingFlags.Public)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ConfigEntry<float>), "Value").GetGetMethod()));
                    newInstructions.Insert(loadVolumeArgumentPosition + 2, new CodeInstruction(OpCodes.Mul));

                    newInstructions.Insert(loadVolumeArgumentPosition + 4, new CodeInstruction(OpCodes.Br, newInstructions[loadVolumeArgumentPosition + 4].labels[0]));

                    newInstructions.Insert(loadVolumeArgumentPosition + 5, new CodeInstruction(OpCodes.Ldsfld, typeof(QuietRainClass).GetField("ambienceChanged", BindingFlags.Static | BindingFlags.Public)));
                    newInstructions[loadVolumeArgumentPosition + 5].labels = new List<Label> { label };
                    newInstructions.Insert(loadVolumeArgumentPosition + 6, new CodeInstruction(OpCodes.Brfalse, newInstructions[loadVolumeArgumentPosition + 6].labels[0]));

                    newInstructions.Insert(loadVolumeArgumentPosition + 7, new CodeInstruction(OpCodes.Ldc_I4_0));
                    newInstructions.Insert(loadVolumeArgumentPosition + 8, new CodeInstruction(OpCodes.Stsfld, typeof(QuietRainClass).GetField("ambienceChanged", BindingFlags.Static | BindingFlags.Public)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 9, new CodeInstruction(OpCodes.Ldarg_0));
                    newInstructions.Insert(loadVolumeArgumentPosition + 10, new CodeInstruction(OpCodes.Ldfld, typeof(EnvironmentManager).GetField("OutdoorSource", BindingFlags.Instance | BindingFlags.NonPublic)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 11, new CodeInstruction(OpCodes.Ldsfld, typeof(QuietRainPlugin).GetField("AmbienceVolume", BindingFlags.Static | BindingFlags.Public)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 12, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ConfigEntry<float>), "Value").GetGetMethod()));
                    newInstructions.Insert(loadVolumeArgumentPosition + 13, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(AudioSource), "volume").GetSetMethod()));
                    newInstructions.Insert(loadVolumeArgumentPosition + 14, new CodeInstruction(OpCodes.Ldarg_0));
                    newInstructions.Insert(loadVolumeArgumentPosition + 15, new CodeInstruction(OpCodes.Ldfld, typeof(EnvironmentManager).GetField("OutdoorMixSource", BindingFlags.Instance | BindingFlags.NonPublic)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 16, new CodeInstruction(OpCodes.Ldsfld, typeof(QuietRainPlugin).GetField("AmbienceVolume", BindingFlags.Static | BindingFlags.Public)));
                    newInstructions.Insert(loadVolumeArgumentPosition + 17, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ConfigEntry<float>), "Value").GetGetMethod()));
                    newInstructions.Insert(loadVolumeArgumentPosition + 18, new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(AudioSource), "volume").GetSetMethod()));

                }
                
                #if DEBUG
                    FileLog.Log("\nOld Instructions:\n");
                    foreach (CodeInstruction ins in instructions)
                        FileLog.Log(ins.ToString());
                    FileLog.Log("\nNew Instructions:\n");
                    foreach (CodeInstruction ins in newInstructions)
                        FileLog.Log(ins.ToString());
                #endif

                return newInstructions.AsEnumerable();
            }
        }
    }
}
