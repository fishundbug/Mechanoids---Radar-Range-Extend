using Verse;

namespace RadarRangeExtend
{
    /// <summary>
    /// 模组设置：存储玩家配置的反隐雷达有效半径
    /// </summary>
    public class RadarRangeSettings : ModSettings
    {
        /// <summary>反隐雷达有效半径，默认 128 格</summary>
        public float radarRange = 128f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref radarRange, "radarRange", 128f);
            base.ExposeData();
        }
    }
}
