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

    /// <summary>
    /// 全部可读文本的编号（收集用）：按 GDD 的 T 编号排，第 n 个就是 No. n。
    /// 加了新文本要在这里补上，否则它不显示编号、排在暂停菜单列表最后。
    /// </summary>
    public static class TextCatalog
    {
        public static readonly string[] Order =
        {
            "T1", "T2", "T3", "T4", "T5", "T6_1", "T6_2", "T6_3", "T6_4", "T7",
            "T8", "T9", "T10", "T11", "T12", "T13", "T14", "T15", "T16",
        };

        public static int Count => Order.Length;

        /// <summary>1 起的编号；不在表里返回 0。</summary>
        public static int Number(string textId) => System.Array.IndexOf(Order, textId) + 1;

        /// <summary>"No. 6 / 19"；不在表里返回空串。</summary>
        public static string Label(string textId)
        {
            int n = Number(textId);
            return n > 0 ? $"No. {n} / {Count}" : "";
        }
    }
}
