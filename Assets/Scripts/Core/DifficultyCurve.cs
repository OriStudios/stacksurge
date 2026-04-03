namespace StackSurge.Core
{
    /// <summary>
    /// GDD: difficulty scales with survival time only. Row interval and wild rate by time bucket.
    /// </summary>
    public static class DifficultyCurve
    {
        public static float GetRowIntervalSeconds(float timeAlive)
        {
            if (timeAlive < 60f) return 8f;
            if (timeAlive < 180f) return 6f;
            if (timeAlive < 360f) return 3f;
            return 2f;
        }

        public static float GetWildChance(float timeAlive)
        {
            if (timeAlive < 60f) return 1f / 8f;
            if (timeAlive < 180f) return 1f / 12f;
            if (timeAlive < 360f) return 1f / 18f;
            return 1f / 25f;
        }
    }
}
