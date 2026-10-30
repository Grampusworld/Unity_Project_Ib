using UnityEngine;

namespace Ib.UI
{
    /// <summary>六条任务提示（GDD §3.7 / §7.2）。lines[0] 对应 HintId.Explore。资源：Resources/Texts/Hints.asset。</summary>
    [CreateAssetMenu(menuName = "Ib/Hint Table", fileName = "Hints")]
    public sealed class HintTable : ScriptableObject
    {
        public string[] lines;

        public string Get(HintId id)
        {
            int i = (int)id - 1;
            return lines != null && i >= 0 && i < lines.Length ? lines[i] : null;
        }
    }
}
