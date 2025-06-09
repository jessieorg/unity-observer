using System;
using System.Collections.Generic;

using UnityEngine;

namespace DlzUnityGameLib.Core.Observer {

    public sealed class SARWatcherEventArgs {

        public object revObject;


        public List<object> Args = new List<object>();


        public static SARWatcherEventArgs CreateByArgs(object rev, params object[] args) {
            var arg = Create();
            arg.Args.Clear();
            arg.revObject = rev;
            if (args != null)
                arg.Args.AddRange(args);
            return arg;
        }

        public static SARWatcherEventArgs CreateByArgsNoRev(params object[] args) {
            var arg = Create();
            arg.Args.Clear();
            if (args != null)
                arg.Args.AddRange(args);
            return arg;
        }

        public T Arg<T>(int index) {
            if (index < 0 && index >= Args.Count) {
                Debug.LogError("获取参数越界");
                return default(T);
            }
            return (T)Args[index];
        }

        public object this[int index] {
            get {
                if (index < 0 && index >= Args.Count) {
                    Debug.LogError("获取参数越界");
                    return null;
                }
                return Args[index];
            }
        }

        private static Queue<SARWatcherEventArgs> queues = new Queue<SARWatcherEventArgs>();

        public static SARWatcherEventArgs Create() {
            return queues.Count == 0 ? new SARWatcherEventArgs() : queues.Dequeue();
        }

        public static void Destory(SARWatcherEventArgs args) {
            args.Args.Clear();
            args.revObject = null;
            queues.Enqueue(args);
        }

    }
    public static class SARSARWatcherBehaviourHelper {

        public static void RegSARWatcherEvent(this MonoBehaviour mono, string message, SARWatcherBehaviour.OnEventFuncDelegate func) {
            SARWatcherBehaviour.RegObserverEvent(mono, message, func);
        }

        public static void UnRegSARWatcherEvent(this MonoBehaviour mono, string message,
            SARWatcherBehaviour.OnEventFuncDelegate func) {
            SARWatcherBehaviour.UnRegObserverEvent(message, func);
        }

        public static void UnRegSARWatcherEventByObj(this MonoBehaviour mono) {
            SARWatcherBehaviour.UnRegObserverEvent(mono);
        }



        public static void SARFireEvent(this MonoBehaviour sender, string message, SARWatcherEventArgs arg) {
            SARWatcherBehaviour.Fire(sender, message, arg);
        }

        public static void SARFireEvent(this MonoBehaviour sender, string message, params object[] args) {
            SARWatcherBehaviour.Fire(sender, message, SARWatcherEventArgs.CreateByArgsNoRev(args));
        }


    }


    public class SARWatcherBehaviour : MonoBehaviour {


        private static SARWatcherBehaviour instance = null;


        private static SARWatcherBehaviour Instance {
            get {
                if (instance == null) {
                    instance = new GameObject("SARWatcherBehaviour").AddComponent<SARWatcherBehaviour>();
                }
                return instance;
            }
        }

        private sealed class WatcherTaskNode {
            public SARWatcherEventArgs arg;


            public MonoBehaviour sender;

            public string func;
            

            private static Queue<WatcherTaskNode> queues = new Queue<WatcherTaskNode>();

            public static WatcherTaskNode Create() {
                return queues.Count == 0 ? new WatcherTaskNode() : queues.Dequeue();
            }

            public static void Destory(WatcherTaskNode node) {
                if (node.arg != null)
                    SARWatcherEventArgs.Destory(node.arg);
                node.func = null;
                node.sender = null;
                queues.Enqueue(node);
            }
        }

        private sealed class WatcherFuncNode {

            public MonoBehaviour regObj = null;

            public OnEventFuncDelegate func;

        }

        public delegate void OnEventFuncDelegate(MonoBehaviour sender, SARWatcherEventArgs arg);


        private Dictionary<string, LinkedList<WatcherFuncNode>> observers = new Dictionary<string, LinkedList<WatcherFuncNode>>();


        private Queue<WatcherTaskNode> tasks = new Queue<WatcherTaskNode>();


        private Queue<WatcherTaskNode> runTasks = new Queue<WatcherTaskNode>();

        /// <summary>
        /// 注册一个观察者
        /// </summary>
        /// <param name="eventMessage"></param>
        /// <param name="func"></param>
        public static void RegObserverEvent(MonoBehaviour mono, string eventMessage, OnEventFuncDelegate func) {
            LinkedList<WatcherFuncNode> funcs = null;
            if (!Instance.observers.TryGetValue(eventMessage, out funcs)) {
                funcs = new LinkedList<WatcherFuncNode>();
                Instance.observers.Add(eventMessage, funcs);
            }
            funcs.AddLast(new WatcherFuncNode() { func = func, regObj = mono });
        }

        private static void removeFromtoLinkList<T>(LinkedList<T> list, Func<T, bool> compare) {
            var itr = list.First;
            while (itr != null) {
                if (compare(itr.Value)) {
                    var tmp = itr;
                    itr = itr.Next;
                    list.Remove(tmp);
                }
                else
                    itr = itr.Next;
            }
        }
        /// <summary>
        /// 反注册一个观察者
        /// </summary>
        /// <param name="eventMessage"></param>
        /// <param name="func"></param>
        public static void UnRegObserverEvent(string eventMessage, OnEventFuncDelegate func) {
            if (instance == null || instance.gameObject == null)
                return;
            LinkedList<WatcherFuncNode> funcs = null;
            if (instance.observers.TryGetValue(eventMessage, out funcs)) {
                removeFromtoLinkList(funcs, (o) => o == null || o.func == func);
            }
        }

        public static void UnRegObserverEvent(object regObj) {
            if (instance == null || instance.gameObject == null)
                return;
            LinkedList<WatcherFuncNode> funcs = null;
            foreach (var instanceObserver in instance.observers) {
                removeFromtoLinkList(instanceObserver.Value, (o) => o == null || o.regObj == regObj);
            }

        }


        public static void Fire(MonoBehaviour sender, string eventMessage, SARWatcherEventArgs args) {
            PushTask(sender, eventMessage, args);
        }

        /// <summary>
        /// 压入一个任务,让任务进入执行队列
        /// </summary>
        /// <param name="eventMessage">要执行的任务</param>
        /// <param name="args">参数</param>
        private static void PushTask(MonoBehaviour sender, string eventMessage, SARWatcherEventArgs args = null) {
            LinkedList<WatcherFuncNode> funcs = null;
            if (Instance.observers.TryGetValue(eventMessage, out funcs)) {
                lock (Instance.tasks) {
                    var node = WatcherTaskNode.Create();
                    node.func = eventMessage;
                    node.arg = args;
                    node.sender = sender;
                    Instance.tasks.Enqueue(node);
                }
            }
        }

        /// <summary>
        /// 清除所有信息
        /// </summary>
        public static void ClearAll() {
            ClearReg();
            lock (Instance.tasks) {
                Instance.tasks.Clear();
            }
            Instance.runTasks.Clear();


        }

        /// <summary>
        /// 清除所有注册的委托
        /// </summary>
        public static void ClearReg() {
            if (instance != null) {
                foreach (var instanceObserver in instance.observers) {
                    instanceObserver.Value.Clear();
                }
                instance.observers.Clear();
            }
        }

        public void OnDestroy() {
            ClearReg();
            instance = null;
        }

        private void FixedUpdate() {

            lock (tasks) {
                while (tasks.Count > 0)
                    runTasks.Enqueue(tasks.Dequeue());
            }

            while (runTasks.Count > 0) {
                var node = runTasks.Dequeue();
                LinkedList<WatcherFuncNode> funcs = null;
                if (observers.TryGetValue(node.func, out funcs)) {
                    var itr = funcs.First;
                    while (itr != null) {
                        if (itr.Value.regObj == null || itr.Value.regObj.gameObject == null) {
                            var tmp = itr;
                            itr = itr.Next;
                            funcs.Remove(tmp);

                        }
                        else {
                            itr.Value.func(node.sender, node.arg);
                            itr = itr.Next;
                        }
                    }
                    WatcherTaskNode.Destory(node);
                }
            }
        }
    }
}