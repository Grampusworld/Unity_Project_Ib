namespace Ib.Core
{
    /// <summary>
    /// 分类音量（0~1），由设置页写入（GameSettings），乘在各音源自己的音量上。
    /// 总音量仍走 AudioListener.volume；这里只管"音乐"和"音效"两类的相对大小。
    /// </summary>
    public static class AudioMix
    {
        public static float Music = 1f;
        public static float Effects = 1f;
    }
}
