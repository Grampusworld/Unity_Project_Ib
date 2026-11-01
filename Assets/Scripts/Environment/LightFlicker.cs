using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 灯泡闪烁。挂在带 Light 的物体上即可，不改动其他逻辑。
    /// 断电后（MuseumLights.On == false）闪得更勤、每阵更长，间隔仍然随机（老大 2026-11-01）。
    /// 灯自己强制开阴影：这种灯挂在楼梯厅，不开阴影光会穿过楼板照到楼上。
    /// 纯氛围件，删掉不影响任何功能。
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float steadyMin = 2.5f;
        [SerializeField, Min(0f)] private float steadyMax = 9f;
        [SerializeField, Range(0f, 1f)] private float dimTo = 0.05f;
        [SerializeField, Min(0.01f)] private float flickerStep = 0.06f;
        [SerializeField, Min(1)] private int flickerStepsMin = 3;
        [SerializeField, Min(1)] private int flickerStepsMax = 8;

        [Header("断电后")]
        [SerializeField, Min(0f)] private float darkSteadyMin = 0.3f;
        [SerializeField, Min(0f)] private float darkSteadyMax = 2.2f;
        [SerializeField, Min(1)] private int darkStepsMin = 4;
        [SerializeField, Min(1)] private int darkStepsMax = 14;

        private Light target;
        private float baseIntensity;
        private float steadyTimer;
        private float stepTimer;
        private int stepsLeft;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            if (target.shadows == LightShadows.None)
            {
                target.shadows = LightShadows.Soft;
                target.shadowStrength = 0.9f;
                target.shadowBias = 0.05f;
                target.shadowNormalBias = 0.4f;
            }
            steadyTimer = Random.Range(steadyMin, steadyMax);
        }

        private void Update()
        {
            if (stepsLeft > 0)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    stepTimer = flickerStep;
                    stepsLeft--;
                    target.intensity = baseIntensity * Random.Range(dimTo, 1f);

                    if (stepsLeft == 0)
                    {
                        target.intensity = baseIntensity;
                    }
                }

                return;
            }

            // 刚断电：别等完亮灯时那段长间隔
            if (!MuseumLights.On && steadyTimer > darkSteadyMax)
            {
                steadyTimer = Random.Range(darkSteadyMin, darkSteadyMax);
            }

            steadyTimer -= Time.deltaTime;
            if (steadyTimer <= 0f)
            {
                bool dark = !MuseumLights.On;
                steadyTimer = dark ? Random.Range(darkSteadyMin, darkSteadyMax) : Random.Range(steadyMin, steadyMax);
                stepsLeft = dark ? Random.Range(darkStepsMin, darkStepsMax + 1) : Random.Range(flickerStepsMin, flickerStepsMax + 1);
                stepTimer = 0f;
            }
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.intensity = baseIntensity;
            }
        }
    }
}
