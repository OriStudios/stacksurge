using UnityEngine;

namespace StackSurge.UI
{
    public class ScreenShake : MonoBehaviour
    {
        public Transform Target;
        public float Remaining;
        public float Intensity;

        void LateUpdate()
        {
            if (Target == null) return;
            if (Remaining <= 0f)
            {
                Target.localPosition = Vector3.zero;
                Intensity = 0f;
                return;
            }

            Remaining -= Time.deltaTime;
            float t = Intensity * (Remaining > 0 ? 1f : 0f);
            Target.localPosition = new Vector3(
                (Random.value - 0.5f) * 2f * t,
                (Random.value - 0.5f) * 2f * t,
                0f);
        }

        public void AddShake(float amount, float duration)
        {
            Intensity = Mathf.Max(Intensity, amount);
            Remaining = Mathf.Max(Remaining, duration);
        }
    }
}
