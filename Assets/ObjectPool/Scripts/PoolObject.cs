using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Pooling
{
    public class PoolObject : MonoBehaviour, IRecycle
    {

        GameObject prefab;
        public GameObject PrefabType
        {
            get
            {
                return prefab;
            }

            set
            {
                prefab = value;
            }
        }

    }
}
