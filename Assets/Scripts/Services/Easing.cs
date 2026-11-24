using UnityEngine;

namespace Services
{
    public static class Easing
    {
        // smoothstep: 3x² − 2x³
        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // 1 − (1 − x)²
        public static float InQuad(float x) => x * x;

        public static float OutQuad(float x) => 1f - (1f - x) * (1f - x);

        // 1 − (1 − x)³
        public static float OutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        // 1 + (c + 1)(x − 1)³ + c(x − 1)²
        public static float OutBack(float x, float overshoot)
        {
            if (x <= 0f || x >= 1f)
            {
                return Mathf.Clamp01(x);
            }

            float p = x - 1f;
            return 1f + (overshoot + 1f) * p * p * p + overshoot * p * p;
        }

        // 4u(1 − u): 0 at the ends, 1 in the middle
        public static float Parabola(float u) => 4f * u * (1f - u);

        // 1 − e^(−k·dt): frame-rate independent lerp factor
        public static float Damp(float sharpness, float deltaTime) => 1f - Mathf.Exp(-sharpness * deltaTime);
    }
}
