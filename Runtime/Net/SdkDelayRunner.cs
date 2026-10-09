using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnjoylixSDK.Net
{
    internal sealed class SdkDelayRunner : MonoBehaviour
    {
        private static SdkDelayRunner _instance;

        public static SdkDelayRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("[Enjoylix] SdkDelayRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SdkDelayRunner>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        public Task DelayAsync(int delayMs, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(DelayRoutine(delayMs / 1000f, tcs, ct));
            return tcs.Task;
        }

        public CancellationTokenSource CreateTimeoutSource(TimeSpan timeout, CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            StartCoroutine(CancelRoutine((float)timeout.TotalSeconds, cts));
            return cts;
        }

        private static IEnumerator CancelRoutine(float seconds, CancellationTokenSource cts)
        {
            float endTime = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < endTime)
            {
                if (IsSettled(cts))
                    yield break;

                yield return null;
            }

            TryCancel(cts);
        }

        private static bool IsSettled(CancellationTokenSource cts)
        {
            try
            {
                return cts.IsCancellationRequested;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        private static void TryCancel(CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private IEnumerator DelayRoutine(float seconds, TaskCompletionSource<bool> tcs, CancellationToken ct)
        {
            float endTime = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < endTime)
            {
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(ct);
                    yield break;
                }

                yield return null;
            }

            tcs.TrySetResult(true);
        }
    }
}
