using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Artemis.Example
{
    public class EventOnStart : MonoBehaviour
    {
        [SerializeField]
        private UnityEvent onFire;

        void Start()
        {
            onFire?.Invoke();
            Destroy(this);
        }
    }
}
