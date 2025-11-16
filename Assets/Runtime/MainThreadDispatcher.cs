using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Netcode.Transports.NearbyConnections
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static SynchronizationContext _unityContext;
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var go = new GameObject("MainThreadDispatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<MainThreadDispatcher>();

            _unityContext = SynchronizationContext.Current;
        }

        public static void Run(Action action)
        {
            if (SynchronizationContext.Current == _unityContext)
                action();
            else
                _queue.Enqueue(action);
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
                action();
        }
    }

    // -------------------------
    // Extensions
    // -------------------------
    public static class MainThreadExtensions
    {
        public static void InvokeOnMainThread(this Action action)
        {
            MainThreadDispatcher.Run(action);
        }
        
        public static void InvokeOnMainThread<T1>(this Action<T1> action, T1 a)
        {
            MainThreadDispatcher.Run(() => action(a));
        }

        public static void InvokeOnMainThread<T1, T2>(this Action<T1, T2> action, T1 a, T2 b)
        {
            MainThreadDispatcher.Run(() => action(a, b));
        }

        public static void InvokeOnMainThread<T1, T2, T3>(this Action<T1, T2, T3> action, T1 a, T2 b, T3 c)
        {
            MainThreadDispatcher.Run(() => action(a, b, c));
        }
    }
}