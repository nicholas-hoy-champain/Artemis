using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Artemis.Example.Rituals
{
    public class DamagingBox : MonoBehaviour
    {
        [SerializeField]
        HealthEffectSource damageSource;

        [SerializeField]
        int damageAmount;

        public static event UnityAction<int, HealthEffectSource> DamageDelt;

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.tag == "Player")
            {
                DamageDelt?.Invoke(-damageAmount, damageSource);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.tag == "Player")
            {
                DamageDelt?.Invoke(-damageAmount, damageSource);
            }
        }
    }
}
