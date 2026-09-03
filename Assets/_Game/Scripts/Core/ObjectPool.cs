using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    public sealed class ObjectPool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Stack<T> free = new Stack<T>();
        public readonly List<T> Active = new List<T>();

        public ObjectPool(T prefab, Transform parent, int prewarm = 0)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (int i = 0; i < prewarm; i++) free.Push(Create());
        }

        T Create()
        {
            var item = Object.Instantiate(prefab, parent);
            item.gameObject.SetActive(false);
            return item;
        }

        public T Get()
        {
            var item = free.Count > 0 ? free.Pop() : Create();
            item.gameObject.SetActive(true);
            Active.Add(item);
            if (item is IPoolable p) p.OnSpawn();
            return item;
        }

        public void Release(T item)
        {
            if (!Active.Remove(item)) return;
            if (item is IPoolable p) p.OnDespawn();
            item.gameObject.SetActive(false);
            free.Push(item);
        }

        public void ReleaseAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--) Release(Active[i]);
        }
    }
}
