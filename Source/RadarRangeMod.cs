using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RadarRangeExtend
{
    /// <summary>
    /// Mod 主类：提供设置 UI 并在设置变更 / 游戏加载时应用反隐半径
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
}
