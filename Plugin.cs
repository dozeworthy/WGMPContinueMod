using BepInEx;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

namespace MPContinueMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("WildGunsReloaded.exe")]
    public class Plugin : BaseUnityPlugin
    {
        static BepInEx.Logging.ManualLogSource logger;

        private void Awake()
        {
            logger = Logger;
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        /*[HarmonyPatch(typeof(wgObjBase), "Step1Set")]
        [HarmonyPrefix]
        public static void ObjBadeStepPre(System.Reflection.MethodBase __originalMethod, wgObjBase __instance, ref int __0)
        {
            if (__0 == 99 && __instance.GetType().Name == "wgObjGameOver")
            {
                __0 = 10;
            }
        }*/

        [HarmonyPatch(typeof(wgObjGameOver), "Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GameOverUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            CodeInstruction contPromptCode = null;
            int selectSceneLoadIdx = -1;

        /* 0x00025646 28BC000006   *//*
        IL_021E: call int32 GameMain::GetUserCount()
        *//* 0x0002564B 17           *//*
                                      IL_0223: ldc.i4.1
        *//* 0x0002564C 3E0D000000   *//*
                                      IL_0224: ble IL_0236*/

            for (var i = 0; i < codes.Count - 1; ++i)
            {
                if (contPromptCode == null && codes[i].opcode == OpCodes.Ldc_I4_S && codes[i].OperandIs(99) &&
                    codes[i+1].opcode == OpCodes.Call && (codes[i + 1].operand as MethodInfo)?.Name == "Step1Set")
                {
                    //prevent skiping continue prompt in multiplayer
                    contPromptCode = codes[i];
                }else if (selectSceneLoadIdx == -1 && codes[i].opcode == OpCodes.Ldstr && codes[i].OperandIs("PlayerSelectSingle") &&
                    codes[i+1].opcode == OpCodes.Call && (codes[i + 1].operand as MethodInfo)?.Name == "LoadScene")
                {
                    //load correct character select scene for single or multi player
                    selectSceneLoadIdx = i+1;
                }
            }

            if (contPromptCode != null && selectSceneLoadIdx > -1)
            {
                contPromptCode.operand = 10;

                var payload = new List<CodeInstruction>();
                payload.Add(new CodeInstruction(OpCodes.Call, typeof(GameMain).GetMethod("GetUserCount")));
                payload.Add(new CodeInstruction(OpCodes.Ldc_I4_1));
                var newLabel = generator.DefineLabel();
                codes[selectSceneLoadIdx] = codes[selectSceneLoadIdx].WithLabels(newLabel);
                payload.Add(new CodeInstruction(OpCodes.Ble, newLabel));
                payload.Add(new CodeInstruction(OpCodes.Pop));
                payload.Add(new CodeInstruction(OpCodes.Ldstr, "PlayerSelectMulti"));
                codes.InsertRange(selectSceneLoadIdx, payload);

                logger.LogInfo("Successfully applied MP continue patch");
            }
            else
            {
                logger.LogInfo("Failed to apply MP continue patch");
            }

            return codes.AsEnumerable();
        }
    }
}
