using System;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace RadarRangeExtend
{
    /// <summary>
    /// Mod 主类：提供设置 UI 并在设置变更 / 游戏加载时应用反隐半径，同时通过 Harmony 修复原版存档开关 Bug
    /// </summary>
    public class RadarRangeMod : Mod
    {
        public static RadarRangeSettings Settings { get; private set; }

        // 原版默认半径，用于 UI 显示参考
        private const float DefaultRadius = 64f;
        private const float MinRadius = 10f;
        private const float MaxRadius = 500f;

        // 目标 Def 和字段名
        private const string TargetDefName = "NCL_Overwatch_Nexus";
        private const string CompTypeName = "NCL.CompProperties_AntiInvisibilityField";
        private const string FieldName = "effectiveRadius";

        // 缓存反射结果
        private static FieldInfo _radiusField;
        private static bool _reflectionResolved;

        public RadarRangeMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RadarRangeSettings>();

            // 在所有 Def 加载完毕后应用设置
            LongEventHandler.ExecuteWhenFinished(ApplyRadarRange);

            // 动态初始化 Harmony 补丁修复开关不保存的 Bug
            InitializeHarmonyPatches();
        }

        /// <summary>
        /// 动态注册 Harmony 补丁，完全解耦编译依赖
        /// </summary>
        private void InitializeHarmonyPatches()
        {
            try
            {
                var harmony = new Harmony("fishundbug.RadarRangeExtend");

                var targetType = AccessTools.TypeByName("NCL.CompAntiInvisibilityField");
                if (targetType == null)
                {
                    Log.Warning("[RadarRangeExtend] 未找到类型 NCL.CompAntiInvisibilityField，跳过存档状态修复补丁。");
                    return;
                }

                var targetMethod = AccessTools.Method(targetType, "PostSpawnSetup");
                if (targetMethod == null)
                {
                    Log.Warning("[RadarRangeExtend] 未找到方法 NCL.CompAntiInvisibilityField.PostSpawnSetup，跳过存档状态修复补丁。");
                    return;
                }

                var prefixMethod = AccessTools.Method(typeof(FixRadarSaveLoadPatch), nameof(FixRadarSaveLoadPatch.Prefix));
                var postfixMethod = AccessTools.Method(typeof(FixRadarSaveLoadPatch), nameof(FixRadarSaveLoadPatch.Postfix));

                harmony.Patch(targetMethod,
                    prefix: new HarmonyMethod(prefixMethod),
                    postfix: new HarmonyMethod(postfixMethod)
                );

                Log.Message("[RadarRangeExtend] 成功应用雷达存档状态修复补丁（基于反射动态解耦）！");
            }
            catch (Exception ex)
            {
                Log.Error("[RadarRangeExtend] 应用 Harmony 补丁时发生异常: " + ex);
            }
        }

        /// <summary>
        /// 渲染模组设置 UI
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"反隐雷达有效半径（原版默认：{DefaultRadius}）");
            listing.Gap(4f);

            // 滑块
            Settings.radarRange = Mathf.Round(
                listing.Slider(Settings.radarRange, MinRadius, MaxRadius)
            );

            listing.Label($"当前值：{Settings.radarRange} 格");

            listing.End();
        }

        /// <summary>
        /// 设置页面标题
        /// </summary>
        public override string SettingsCategory()
        {
            return "[MTW] Anti-Stealth Radar";
        }

        /// <summary>
        /// 保存设置时同步应用到 Def（实时生效）
        /// </summary>
        public override void WriteSettings()
        {
            base.WriteSettings();
            ApplyRadarRange();
        }

        /// <summary>
        /// 通过反射修改 NCL_Overwatch_Nexus 的 CompProperties_AntiInvisibilityField.effectiveRadius
        /// </summary>
        private static void ApplyRadarRange()
        {
            if (Settings == null) return;

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(TargetDefName);
            if (def == null)
            {
                Log.Warning("[RadarRangeExtend] 未找到 ThingDef: " + TargetDefName);
                return;
            }

            // 在 def 的 comps 列表中查找目标 CompProperties
            foreach (var comp in def.comps)
            {
                if (comp.GetType().FullName != CompTypeName) continue;

                // 首次使用时解析并缓存 FieldInfo
                if (!_reflectionResolved)
                {
                    _radiusField = comp.GetType().GetField(
                        FieldName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    _reflectionResolved = true;

                    if (_radiusField == null)
                    {
                        Log.Error("[RadarRangeExtend] 反射失败：未找到字段 " + FieldName);
                        return;
                    }
                }

                if (_radiusField != null)
                {
                    _radiusField.SetValue(comp, Settings.radarRange);
                    Log.Message($"[RadarRangeExtend] 反隐雷达有效半径已设置为 {Settings.radarRange}");
                }
                return;
            }

            Log.Warning("[RadarRangeExtend] 未在 " + TargetDefName + " 中找到 " + CompTypeName);
        }
    }

    /// <summary>
    /// 修复雷达开关无法保存的 Harmony 补丁类
    /// 使用 __state 状态传递，既保证了数据安全隔离，又完全不显式依赖目标 DLL
    /// </summary>
    public static class FixRadarSaveLoadPatch
    {
        public static void Prefix(ThingComp __instance, bool respawningAfterLoad, out bool? __state)
        {
            __state = null;
            if (respawningAfterLoad && __instance != null)
            {
                var field = __instance.GetType().GetField("isActivated", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    __state = (bool)field.GetValue(__instance);
                }
            }
        }

        public static void Postfix(ThingComp __instance, bool respawningAfterLoad, bool? __state)
        {
            if (respawningAfterLoad && __state.HasValue && __instance != null)
            {
                var field = __instance.GetType().GetField("isActivated", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(__instance, __state.Value);
                }
            }
        }
    }
}
