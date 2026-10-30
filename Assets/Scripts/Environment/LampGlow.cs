using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 灯具的发光件（吊灯罩、射灯镜片）跟着全馆灯光开关：断电后换成不发光的材质，不然灯灭了灯罩还亮着。
    /// 出口指示灯、雕像手里那盏灯不挂这个（应急灯 / 剧情上就该一直亮）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LampGlow : MonoBehaviour
    {
        [SerializeField] private Renderer[] glows = new Renderer[0];
        [SerializeField] private Material onMaterial;
        [SerializeField] private Material offMaterial;

        private void OnEnable()
        {
            MuseumLights.Changed += Apply;
            Apply(MuseumLights.On);
        }

        private void OnDisable()
        {
            MuseumLights.Changed -= Apply;
        }

        private void Apply(bool on)
        {
            foreach (Renderer r in glows)
            {
                if (r != null)
                {
                    r.sharedMaterial = on ? onMaterial : offMaterial;
                }
            }
        }
    }
}
