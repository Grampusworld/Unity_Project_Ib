using System;
using UnityEngine;

namespace Ib.UI
{
    /// <summary>右下角常驻的按键提示。资源：Resources/Texts/Controls.asset，和 T1–T16、Hints 同一套管理。</summary>
    [CreateAssetMenu(menuName = "Ib/Control Hint Table", fileName = "Controls")]
    public sealed class ControlHintTable : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public string key;
            public string action;
            [Tooltip("拿到手电（GameFlow.FlashlightObtained）之后才显示。")]
            public bool needsFlashlight;
        }

        public Line[] lines;
    }
}
