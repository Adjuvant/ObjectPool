using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Pooling
{
    public sealed class ObjectPool : MonoBehaviour
    {
        #region Variables
        public enum StartupPoolMode { Awake, Start, CallManually };

        [System.Serializable]
        public class StartupPool
        {
            public int size;
            public GameObject prefab;
        }

        static ObjectPool _instance;
        static Stack<GameObject> tempList = new Stack<GameObject>();

        Dictionary<GameObject, Stack<GameObject>> pooledObjects = new Dictionary<GameObject, Stack<GameObject>>();
        Dictionary<GameObject, Stack<GameObject>> relayPools = new Dictionary<GameObject, Stack<GameObject>>();

        public StartupPoolMode startupPoolMode;
        public StartupPool[] startupPools;
        static int DEFAULT_POOL_SIZE = 5;
        bool startupPoolsCreated;


        #endregion

        #region Setup
        void Awake()
        {
            _instance = this;
            if (startupPoolMode == StartupPoolMode.Awake)
                CreateStartupPools();
        }

        void Start()
        {
            if (startupPoolMode == StartupPoolMode.Start)
                CreateStartupPools();
        }

        public static void CreateStartupPools()
        {
            if (!instance.startupPoolsCreated)
            {
                instance.startupPoolsCreated = true;
                var pools = instance.startupPools;
                if (pools != null && pools.Length > 0)
                    for (int i = 0; i < pools.Length; ++i)
                        CreatePool(pools[i].prefab, pools[i].size);
            }
        }

        public static void CreatePool<T>(T prefab, int initialPoolSize) where T : Component
        {
            CreatePool(prefab.gameObject, initialPoolSize);
        }
        public static void CreatePool(GameObject prefab, int initialPoolSize)
        {
            if (prefab != null && !instance.pooledObjects.ContainsKey(prefab))
            {
                var poolList = new Stack<GameObject>(initialPoolSize);
                var relayList = new Stack<GameObject>();
                instance.pooledObjects.Add(prefab, poolList);
                instance.relayPools.Add(prefab, relayList);

                if (initialPoolSize > 0)
                {
                    bool active = prefab.activeSelf;
                    prefab.SetActive(false);
                    while (poolList.Count < initialPoolSize)
                    {
                        var obj = Instantiate(prefab);
                        obj.transform.SetParent(instance.transform, false); // worldPositionStays=false to keep UI objects spawning consistently
                        obj.AddComponent<PoolObject>().PrefabType = prefab;
                        poolList.Push(obj);
                    }
                    prefab.SetActive(active);
                }
            }
        }
        #endregion

        #region Spawn Components
        public static T Spawn<T>(T prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component
        {
            return Spawn(prefab.gameObject, parent, position, rotation).GetComponent<T>();
        }
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            return Spawn(prefab.gameObject, null, position, rotation).GetComponent<T>();
        }
        public static T Spawn<T>(T prefab, Transform parent, Vector3 position) where T : Component
        {
            return Spawn(prefab.gameObject, parent, position, Quaternion.identity).GetComponent<T>();
        }
        public static T Spawn<T>(T prefab, Vector3 position) where T : Component
        {
            return Spawn(prefab.gameObject, null, position, Quaternion.identity).GetComponent<T>();
        }
        public static T Spawn<T>(T prefab, Transform parent) where T : Component
        {
            return Spawn(prefab.gameObject, parent, Vector3.zero, Quaternion.identity).GetComponent<T>();
        }
        public static T Spawn<T>(T prefab) where T : Component
        {
            return Spawn(prefab.gameObject, null, Vector3.zero, Quaternion.identity).GetComponent<T>();
        }
        #endregion

        #region Spawn GameObjects
        public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var list = new Stack<GameObject>();
            Transform trans;
            GameObject obj = null;
            CreatePool(prefab.gameObject, DEFAULT_POOL_SIZE); // will create pool if none exists
            if (instance.pooledObjects.TryGetValue(prefab, out list))
            {
                obj = null;
                if (list.Count > 0)
                {
                    obj = list.Pop();

                    if (obj != null)
                    {
                        trans = obj.transform;
                        if (parent != null)
                        {
                            trans.SetParent(parent, false); // worldPositionStays=false to keep UI objects spawning consistently
                        }
                        trans.localPosition = position;
                        trans.localRotation = rotation;
                        obj.SetActive(true);
                        return obj;
                    }
                }
                obj = Instantiate(prefab);
                trans = obj.transform;
                if (parent != null)
                {
                    trans.SetParent(parent, false); // worldPositionStays=false to keep UI objects spawning consistently
                }
                trans.localPosition = position;
                trans.localRotation = rotation;
                obj.SetActive(true);
            }
            return obj;
        }

        public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position)
        {
            return Spawn(prefab, parent, position, Quaternion.identity);
        }
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab, null, position, rotation);
        }
        public static GameObject Spawn(GameObject prefab, Transform parent)
        {
            return Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        }
        public static GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return Spawn(prefab, null, position, Quaternion.identity);
        }
        public static GameObject Spawn(GameObject prefab)
        {
            return Spawn(prefab, null, Vector3.zero, Quaternion.identity);
        }
        #endregion

        #region Recycle
        public static void Recycle<T>(T obj) where T : Component
        {
            Recycle(obj.gameObject);
        }
        public static void Recycle(GameObject obj)
        {
            if (obj.GetComponent<IRecycle>() != null)
            {
                GameObject prefab;
                prefab = obj.GetComponent<IRecycle>().PrefabType;

                if (prefab != null && instance.pooledObjects.ContainsKey(prefab))
                {
                    Recycle(obj, prefab);
                }
                else
                {
                    Debug.LogWarning(obj.name + "cannot be recycled because it was never pooled. It will be destroyed instead.");
                    Destroy(obj);
                }
            }
            else
            {
                Debug.LogWarning(obj.name + "cannot be recycled because no pool object component could be found. It will be destroyed instead.");
                Destroy(obj);
            }
        }
        static void Recycle(GameObject obj, GameObject prefab)
        {
            obj.transform.SetParent(instance.transform, false); // worldPositionStays=false to keep UI objects spawning consistently
            obj.SetActive(false);
            instance.relayPools[prefab].Push(obj);
        }

        public static void RecycleAll<T>(T prefab) where T : Component
        {
            RecycleAll(prefab.gameObject);
        }
        public static void RecycleAll(GameObject prefab)
        {
            if (instance.pooledObjects.TryGetValue(prefab, out tempList))
            {
                foreach (var item in tempList)
                    if (item.activeInHierarchy)
                        Recycle(item, prefab);
                tempList.Clear();
            }
            else
            {
                Debug.LogWarning("Could not recycle all prefabs of type " + prefab.name);
            }
        }
        public static void RecycleAll()
        {
            foreach (var kvp in instance.pooledObjects)
            {
                var prefab = kvp.Key;
                var objects = kvp.Value;
                foreach (var go in objects)
                {
                    if (go.activeInHierarchy)
                    {
                        Recycle(go, prefab);
                    }
                }
            }
        }
        #endregion

        #region Utilities
        //public static bool IsSpawned(GameObject obj)
        //{
        //    return instance.spawnedObjects.ContainsKey(obj);
        //}

        public static int CountPooled<T>(T prefab) where T : Component
        {
            return CountPooled(prefab.gameObject);
        }

        public static int CountPooled(GameObject prefab)
        {
            Stack<GameObject> list;
            if (instance.pooledObjects.TryGetValue(prefab, out list))
                return list.Count;
            return 0;
        }

        //public static int CountSpawned<T>(T prefab) where T : Component
        //{
        //    return CountSpawned(prefab.gameObject);
        //}

        //public static int CountSpawned(GameObject prefab)
        //{
        //    int count = 0;
        //    foreach (var instancePrefab in instance.spawnedObjects.Values)
        //        if (prefab == instancePrefab)
        //            ++count;
        //    return count;
        //}

        public static int CountAllPooled()
        {
            int count = 0;
            foreach (var list in instance.pooledObjects.Values)
                count += list.Count;
            return count;
        }

        public static List<GameObject> GetPooled(GameObject prefab, List<GameObject> list, bool appendList)
        {
            if (list == null)
                list = new List<GameObject>();
            if (!appendList)
                list.Clear();
            Stack<GameObject> pooled;
            if (instance.pooledObjects.TryGetValue(prefab, out pooled))
                list.AddRange(pooled);
            return list;
        }
        //public static List<T> GetPooled<T>(T prefab, List<T> list, bool appendList) where T : Component
        //{
        //    if (list == null)
        //        list = new List<T>();
        //    if (!appendList)
        //        list.Clear();
        //    Stack<GameObject> pooled;
        //    if (instance.pooledObjects.TryGetValue(prefab.gameObject, out pooled))
        //        for (int i = 0; i < pooled.Count; ++i)
        //            list.Add(pooled.Peek().GetComponent<T>());
        //    return list;
        //}

        //public static List<GameObject> GetSpawned(GameObject prefab, List<GameObject> list, bool appendList)
        //{
        //    if (list == null)
        //        list = new List<GameObject>();
        //    if (!appendList)
        //        list.Clear();
        //    foreach (var item in instance.spawnedObjects)
        //        if (item.Value == prefab)
        //            list.Add(item.Key);
        //    return list;
        //}

        //public static List<T> GetSpawned<T>(T prefab, List<T> list, bool appendList) where T : Component
        //{
        //    if (list == null)
        //        list = new List<T>();
        //    if (!appendList)
        //        list.Clear();
        //    var prefabObj = prefab.gameObject;
        //    foreach (var item in instance.spawnedObjects)
        //        if (item.Value == prefabObj)
        //            list.Add(item.Key.GetComponent<T>());
        //    return list;
        //}
        #endregion

        #region Destroy Pools
        public static void DestroyPooled(GameObject prefab)
        {
            Stack<GameObject> pooled;
            if (instance.pooledObjects.TryGetValue(prefab, out pooled))
            {
                for (int i = 0; i < pooled.Count; ++i)
                    Destroy(pooled.Pop());
                pooled.Clear();
            }
        }
        public static void DestroyPooled<T>(T prefab) where T : Component
        {
            DestroyPooled(prefab.gameObject);
        }

        public static void DestroyAll(GameObject prefab)
        {
            RecycleAll(prefab);
            DestroyPooled(prefab);
        }
        public static void DestroyAll<T>(T prefab) where T : Component
        {
            DestroyAll(prefab.gameObject);
        }
		#endregion

		#region Relay
        void LateUpdate()
		{
            StartCoroutine(RelayPools());
		}

        IEnumerator RelayPools()
        {
            yield return new WaitForEndOfFrame();
            foreach (var kvp in relayPools.Where(kvp => kvp.Value.Count > 0))
            {
                var prefab = kvp.Key;
                var stack = kvp.Value;
                while (stack.Count > 0)
                {
                    Debug.Log("Moved from relay to pool");
                    instance.pooledObjects[prefab].Push(stack.Pop());
                }
            }
        }
		#endregion

		#region Singleton
		public static ObjectPool instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<ObjectPool>();
                if (_instance != null)
                    return _instance;

                var obj = new GameObject("ObjectPool");
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                _instance = obj.AddComponent<ObjectPool>();
                return _instance;
            }
        }
        #endregion
    }

    public static class ObjectPoolExtensions
    {
        public static void CreatePool<T>(this T prefab) where T : Component
        {
            ObjectPool.CreatePool(prefab, 0);
        }
        public static void CreatePool<T>(this T prefab, int initialPoolSize) where T : Component
        {
            ObjectPool.CreatePool(prefab, initialPoolSize);
        }
        public static void CreatePool(this GameObject prefab)
        {
            ObjectPool.CreatePool(prefab, 0);
        }
        public static void CreatePool(this GameObject prefab, int initialPoolSize)
        {
            ObjectPool.CreatePool(prefab, initialPoolSize);
        }

        public static T Spawn<T>(this T prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component
        {
            return ObjectPool.Spawn(prefab, parent, position, rotation);
        }
        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            return ObjectPool.Spawn(prefab, null, position, rotation);
        }
        public static T Spawn<T>(this T prefab, Transform parent, Vector3 position) where T : Component
        {
            return ObjectPool.Spawn(prefab, parent, position, Quaternion.identity);
        }
        public static T Spawn<T>(this T prefab, Vector3 position) where T : Component
        {
            return ObjectPool.Spawn(prefab, null, position, Quaternion.identity);
        }
        public static T Spawn<T>(this T prefab, Transform parent) where T : Component
        {
            return ObjectPool.Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        }
        public static T Spawn<T>(this T prefab) where T : Component
        {
            return ObjectPool.Spawn(prefab, null, Vector3.zero, Quaternion.identity);
        }
        public static GameObject Spawn(this GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            return ObjectPool.Spawn(prefab, parent, position, rotation);
        }
        public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return ObjectPool.Spawn(prefab, null, position, rotation);
        }
        public static GameObject Spawn(this GameObject prefab, Transform parent, Vector3 position)
        {
            return ObjectPool.Spawn(prefab, parent, position, Quaternion.identity);
        }
        public static GameObject Spawn(this GameObject prefab, Vector3 position)
        {
            return ObjectPool.Spawn(prefab, null, position, Quaternion.identity);
        }
        public static GameObject Spawn(this GameObject prefab, Transform parent)
        {
            return ObjectPool.Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        }
        public static GameObject Spawn(this GameObject prefab)
        {
            return ObjectPool.Spawn(prefab, null, Vector3.zero, Quaternion.identity);
        }

        public static void Recycle<T>(this T obj) where T : Component
        {
            ObjectPool.Recycle(obj);
        }
        public static void Recycle(this GameObject obj)
        {
            ObjectPool.Recycle(obj);
        }

        public static void RecycleAll<T>(this T prefab) where T : Component
        {
            ObjectPool.RecycleAll(prefab);
        }
        public static void RecycleAll(this GameObject prefab)
        {
            ObjectPool.RecycleAll(prefab);
        }

        public static int CountPooled<T>(this T prefab) where T : Component
        {
            return ObjectPool.CountPooled(prefab);
        }
        public static int CountPooled(this GameObject prefab)
        {
            return ObjectPool.CountPooled(prefab);
        }

        //public static int CountSpawned<T>(this T prefab) where T : Component
        //{
        //	return ObjectPool.CountSpawned(prefab);
        //}
        //public static int CountSpawned(this GameObject prefab)
        //{
        //	return ObjectPool.CountSpawned(prefab);
        //}

        //public static List<GameObject> GetSpawned(this GameObject prefab, List<GameObject> list, bool appendList)
        //{
        //	return ObjectPool.GetSpawned(prefab, list, appendList);
        //}
        //public static List<GameObject> GetSpawned(this GameObject prefab, List<GameObject> list)
        //{
        //	return ObjectPool.GetSpawned(prefab, list, false);
        //}
        //public static List<GameObject> GetSpawned(this GameObject prefab)
        //{
        //	return ObjectPool.GetSpawned(prefab, null, false);
        //}
        //public static List<T> GetSpawned<T>(this T prefab, List<T> list, bool appendList) where T : Component
        //{
        //	return ObjectPool.GetSpawned(prefab, list, appendList);
        //}
        //public static List<T> GetSpawned<T>(this T prefab, List<T> list) where T : Component
        //{
        //	return ObjectPool.GetSpawned(prefab, list, false);
        //}
        //public static List<T> GetSpawned<T>(this T prefab) where T : Component
        //{
        //	return ObjectPool.GetSpawned(prefab, null, false);
        //}

        public static List<GameObject> GetPooled(this GameObject prefab, List<GameObject> list, bool appendList)
        {
            return ObjectPool.GetPooled(prefab, list, appendList);
        }
        public static List<GameObject> GetPooled(this GameObject prefab, List<GameObject> list)
        {
            return ObjectPool.GetPooled(prefab, list, false);
        }
        public static List<GameObject> GetPooled(this GameObject prefab)
        {
            return ObjectPool.GetPooled(prefab, null, false);
        }
        //public static List<T> GetPooled<T>(this T prefab, List<T> list, bool appendList) where T : Component
        //{
        //    return ObjectPool.GetPooled(prefab, list, appendList);
        //}
        //public static List<T> GetPooled<T>(this T prefab, List<T> list) where T : Component
        //{
        //    return ObjectPool.GetPooled(prefab, list, false);
        //}
        //public static List<T> GetPooled<T>(this T prefab) where T : Component
        //{
        //    return ObjectPool.GetPooled(prefab, null, false);
        //}

        public static void DestroyPooled(this GameObject prefab)
        {
            ObjectPool.DestroyPooled(prefab);
        }
        public static void DestroyPooled<T>(this T prefab) where T : Component
        {
            ObjectPool.DestroyPooled(prefab.gameObject);
        }

        public static void DestroyAll(this GameObject prefab)
        {
            ObjectPool.DestroyAll(prefab);
        }
        public static void DestroyAll<T>(this T prefab) where T : Component
        {
            ObjectPool.DestroyAll(prefab.gameObject);
        }
    }
}
