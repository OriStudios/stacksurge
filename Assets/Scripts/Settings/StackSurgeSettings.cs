using UnityEngine;

namespace StackSurge.Settings
{
    [CreateAssetMenu(fileName = "StackSurgeSettings", menuName = "Stack Surge/Settings")]
    public class StackSurgeSettings : ScriptableObject
    {
        [Header("Grid")]
        public int Columns = 7;
        public int Rows = 9;

        [Header("Special spawn (mid-band; scaled by difficulty)")]
        [Range(0f, 1f)] public float BombChance = 1f / 20f;

        [Header("Combo / timing")]
        public float ComboWindowSeconds = 3f;
        public float SlowFillBuffSeconds = 12f;

        [Tooltip("No automatic row rise for this many seconds at run start (time to read controls).")]
        public float InitialRiseGraceSeconds = 6f;

        [Header("Juice")]
        public float ShakeBase = 4f;
        public float ShakePerExtraTile = 1.5f;
    }
}
