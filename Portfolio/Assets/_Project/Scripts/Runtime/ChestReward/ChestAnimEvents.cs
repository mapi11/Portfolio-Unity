using System;
using UnityEngine;

namespace Portfolio.ChestReward
{
    /// Принимает события из клипа Chest_Open и передаёт их контроллеру экрана.
    public class ChestAnimEvents : MonoBehaviour
    {
        public event Action LidBurst;
        public event Action Landed;

        // вызываются из AnimationEvent
        void OnLidBurst() => LidBurst?.Invoke();
        void OnLanded() => Landed?.Invoke();
    }
}
