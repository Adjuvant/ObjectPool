using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Pooling
{
    public interface IRecycle
    {
        GameObject PrefabType { get; set; }
    }
}
