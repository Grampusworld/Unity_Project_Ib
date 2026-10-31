using Ib.Flow;

namespace Ib.Threats
{
    /// <summary>
    /// 怪物什么时候出动（GDD §5.3 + §3.6 第 11 步），唯一来源，各怪物只读这里。
    /// 只读 GameFlow 的状态，不改状态。
    /// </summary>
    public static class ThreatSchedule
    {
        /// <summary>天使外形随全局状态：闭馆后、停电 = Peek（不动），天使出动 = Attack，其余 = Still。</summary>
        public static WeepingAngel.Phase AngelPhase(GameState state)
        {
            switch (state)
            {
                case GameState.AfterHours:
                case GameState.PowerOut:
                    return WeepingAngel.Phase.Peek;
                case GameState.AngelsActive:
                    return WeepingAngel.Phase.Attack;
                default:
                    return WeepingAngel.Phase.Still;
            }
        }

        /// <summary>强制过场（监控屏）期间为 true：会动的馆藏原地停住，不会抓人。</summary>
        public static bool Paused { get; set; }

        // 过场中途退回主菜单（重新加载场景）也要复位
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Paused = false;

        /// <summary>
        /// 人台、名画人物：拿到手电那一刻出动（此时状态仍是 PowerOut），一直到天使出动；结局和更早的状态都不动。
        /// </summary>
        public static bool CollectionOut =>
            GameFlow.HasFlashlight
            && (GameFlow.State == GameState.PowerOut || GameFlow.State == GameState.AngelsActive);
    }
}
