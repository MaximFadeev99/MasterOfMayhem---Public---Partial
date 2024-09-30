using MasterOfMayhem.Weapons.Ranged;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterOfMayhem.Base
{
    [Serializable]
    public class ArmoryManager
    {
        [SerializeField] private List<RangedWeapon> _rangedWeaponPrefabs;
        [SerializeField] private Transform _bulletContainer;

        public RangedWeapon GetRangedWeapon() 
        {
            RangedWeapon newRangedWeapon = GameObject.Instantiate(_rangedWeaponPrefabs[0]);
            newRangedWeapon.Initialize(_bulletContainer);

            return newRangedWeapon;
        }
    }
}
