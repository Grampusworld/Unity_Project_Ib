using UnityEngine;

namespace Ib.UI
{
    /// <summary>一份可读文本（GDD §6.11）。资源放在 Resources/Texts/&lt;textId&gt;.asset，文件名就是 textId。</summary>
    [CreateAssetMenu(menuName = "Ib/Text Document", fileName = "T0")]
    public sealed class TextDocument : ScriptableObject
    {
        /// <summary>GDD §6.11.5 R3-4：1 种印刷体 + 3 种手写体。字体没进工程前，手写体先用斜体占位。</summary>
        public enum Hand { Print, Grampus, Lily, Plain }

        public string title;
        [TextArea(4, 20)] public string body;
        public Hand hand;
    }
}
