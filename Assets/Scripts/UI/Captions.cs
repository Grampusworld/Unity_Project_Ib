using System.Collections.Generic;
using UnityEngine;

namespace Ib.UI
{
    /// <summary>
    /// 内心独白字幕的快捷入口（2026-11-02）：按 id 从 Resources/Texts/&lt;id&gt;.asset 取 TextDocument.body，
    /// 交给 Presentation.ShowCaption 在屏幕下方淡入淡出。
    /// 这些不是可收集文本：不进 TextCatalog，也不调用 Presentation.MarkRead，
    /// 所以暂停菜单的 Texts 页（只列 Presentation.ReadTexts）不会多出 C_ 开头的条目。
    /// </summary>
    public static class Captions
    {
        private static readonly HashSet<string> warned = new HashSet<string>();

        /// <summary>取正文；没有这份资源时返回 null，同一个 id 只警告一次。</summary>
        public static string Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            TextDocument doc = Resources.Load<TextDocument>("Texts/" + id);
            if (doc != null)
            {
                return doc.body;
            }

            if (warned.Add(id))
            {
                Debug.LogWarning("[Captions] 找不到 Resources/Texts/" + id + ".asset");
            }

            return null;
        }

        /// <summary>屏幕下方 1/4 处显示一句独白，seconds 秒后淡出。取不到文本就什么也不做。</summary>
        public static void Show(string id, float seconds = 3.5f)
        {
            string text = Get(id);
            if (!string.IsNullOrEmpty(text))
            {
                Presentation.ShowCaption(text, seconds);
            }
        }
    }
}
