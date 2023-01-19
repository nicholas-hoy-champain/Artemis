using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Artemis
{
    public class FlagState : ScriptableObject
    { 
        [SerializeField]
        public SortedStrictDictionary<FlagID, Flag> flagsUsed;

        public void Add(Flag _flag)
        {
            if(_flag != null)
            {
                flagsUsed.Add(_flag.GetFlagId(), _flag);
            }
        }

        public void Remove(Flag _flag)
        {
            if(flagsUsed.HasValue(_flag))
            {
                flagsUsed.Remove(_flag.GetFlagId());
            }
        }

    }
}