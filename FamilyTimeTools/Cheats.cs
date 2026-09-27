using System.Collections.Generic;
using GAT;
using HarmonyLib;

namespace FamilyTimeTools
{
    // 建造的材料校验只有这一个入口：锤子点击建造（MalletTool.TryConstructBuildPlan）、
    // 村民自动施工（ConstructionScheduler）以及 BuildPlanManager.TryConstructBuildPlan
    // 全部先调用它。开启免费建造后直接判定材料齐全，并把待消耗列表清空，
    // 于是点击蓝图即可立即建成，且不扣除任何材料。
    [HarmonyPatch(typeof(BuildPlanManager), nameof(BuildPlanManager.TryGetConstructionMaterials))]
    internal static class FreeBuildPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result, List<RigidTransform> __2)
        {
            if (!EspMod.Instance.FreeBuildEnabled) return true;
            __2.Clear();
            __result = true;
            return false;
        }
    }
}
