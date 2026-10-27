using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 灯泡闪烁。挂在带 Light 的物体上即可，不改动其他逻辑。
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

        private Light target;
        private float baseIntensity;
        private float steadyTimer;
        private float stepTimer;
        private int stepsLeft;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
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

            steadyTimer -= Time.deltaTime;
            if (steadyTimer <= 0f)
            {
                steadyTimer = Random.Range(steadyMin, steadyMax);
                stepsLeft = Random.Range(flickerStepsMin, flickerStepsMax + 1);
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
