using System.Collections.Generic;
using Ib.Core;
using Ib.Flow;
using UnityEngine;

namespace Ib.Threats
{
    /// <summary>
    /// 名画人物（GDD §5.3）：现在只有出动开关，出动条件见 ThreatSchedule.CollectionOut。
    /// 出动后的行为和攻击以后再做；死亡重生时回到原位、按当前状态重设开关。
    /// 外形可选：stillVisual 在画里，outVisual 出画后显示；都不填就只切 IsActive。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintingFigure : MonoBehaviour
    {
        public static readonly List<PaintingFigure> All = new List<PaintingFigure>();

        [SerializeField] private GameObject stillVisual;
        [SerializeField] private GameObject outVisual;

        public bool IsActive { get; private set; }

        private Vector3 homePosition;
        private Quaternion homeRotation;

        private void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void OnEnable()
        {
            All.Add(this);
            GameFlow.StateChanged += OnStateChanged;
            GameFlow.FlashlightObtained += Refresh;
            GameOverScreen.Continued += ResetForRespawn;
            Refresh();
        }

        private void OnDisable()
        {
            All.Remove(this);
            GameFlow.StateChanged -= OnStateChanged;
            GameFlow.FlashlightObtained -= Refresh;
            GameOverScreen.Continued -= ResetForRespawn;
        }

        private void OnStateChanged(GameState from, GameState to) => Refresh();

        private void ResetForRespawn()
        {
            transform.SetPositionAndRotation(homePosition, homeRotation);
            Refresh();
        }

        private void Refresh()
        {
            IsActive = ThreatSchedule.CollectionOut;
            if (stillVisual != null)
            {
                stillVisual.SetActive(!IsActive);
            }

            if (outVisual != null)
            {
                outVisual.SetActive(IsActive);
            }
        }
    }
}
