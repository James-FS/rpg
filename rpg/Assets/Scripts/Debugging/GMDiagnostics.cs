#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using AIBot.Unity;
using AIBot.Core.Output;
using UnityEngine;
using UnityEngine.Events;

namespace FogHarbor.Debugging
{
    public sealed class GMDiagnostics : MonoBehaviour
    {
        public sealed class Entry
        {
            public int AgentId;
            public string Text;
        }
        private sealed class Subscription
        {
            public UnityAction<AgentToolExecutionEvent> Tool;
            public UnityAction<string> Error, Status, Fallback;
            public UnityAction Cancelled;
            public UnityAction<StructuredReply> Reply;
            public float StartedAt = -1;
        }
        private readonly Dictionary<NpcAgent, Subscription> subscriptions = new();
        private readonly Queue<Entry> entries = new();
        public IEnumerable<Entry> Entries => entries;
        public int SubscriptionCount => subscriptions.Count;
        private float nextRefresh;

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void Update()
        {
            foreach (var pair in subscriptions)
            {
                if (pair.Key == null) continue;
                if (pair.Key.IsBusy && pair.Value.StartedAt < 0) pair.Value.StartedAt = Time.realtimeSinceStartup;
                if (!pair.Key.IsBusy) pair.Value.StartedAt = -1;
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1;
            Refresh();
        }

        public void Refresh()
        {
            var removed = new List<NpcAgent>();
            foreach (var pair in subscriptions) if (pair.Key == null) removed.Add(pair.Key);
            foreach (var agent in removed) subscriptions.Remove(agent);
            foreach (var agent in FindObjectsOfType<NpcAgent>())
            {
                if (subscriptions.ContainsKey(agent)) continue;
                int id = agent.GetInstanceID();
                string name = agent.name;
                var sub = new Subscription();
                string Elapsed() => sub.StartedAt >= 0 ? "（本轮约 " + Mathf.RoundToInt((Time.realtimeSinceStartup - sub.StartedAt) * 1000) + "ms）" : "";
                sub.Tool = evt =>
                {
                    if (evt == null) return;
                    Add(id, name + " 工具 " + evt.toolName + " " + (evt.success ? "成功" : "拒绝") + Elapsed()
                        + "\n" + Clip(evt.argumentsJson, 160) + "\n" + Clip(evt.result, 240));
                };
                sub.Error = value => Add(id, name + " " + (value != null && (value.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 || value.Contains("超时")) ? "连接超时 " : "请求错误 ") + Elapsed() + Clip(value, 360));
                sub.Status = value => Add(id, name + " 连接 " + Clip(value, 240));
                sub.Fallback = value => Add(id, name + " 兜底 " + Clip(value, 240));
                sub.Cancelled = () => Add(id, name + " 请求取消 " + Elapsed());
                sub.Reply = reply => Add(id, name + " 对话完成 " + Elapsed());
                agent.onToolExecuted?.AddListener(sub.Tool);
                agent.onError?.AddListener(sub.Error);
                agent.onServerStatus?.AddListener(sub.Status);
                agent.onFallback?.AddListener(sub.Fallback);
                agent.onCancelled?.AddListener(sub.Cancelled);
                agent.onReply?.AddListener(sub.Reply);
                subscriptions.Add(agent, sub);
            }
        }

        private static string Clip(string value, int limit)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Avoid displaying credentials or raw SSE payloads in diagnostics.
            if (value.IndexOf("apiKey", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("data: {", StringComparison.OrdinalIgnoreCase) >= 0) return "响应详情已隐藏";
            return value.Length <= limit ? value : value.Substring(0, limit) + "...";
        }
        private void Add(int id, string text)
        {
            if (entries.Count >= 80) entries.Dequeue();
            entries.Enqueue(new Entry { AgentId = id, Text = DateTime.Now.ToString("HH:mm:ss") + " " + text });
        }
        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Add(0, "运行错误 " + Clip(message, 360));
        }
        public void Clear() => entries.Clear();
        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            foreach (var pair in subscriptions)
            {
                if (pair.Key == null) continue;
                pair.Key.onToolExecuted?.RemoveListener(pair.Value.Tool);
                pair.Key.onError?.RemoveListener(pair.Value.Error);
                pair.Key.onServerStatus?.RemoveListener(pair.Value.Status);
                pair.Key.onFallback?.RemoveListener(pair.Value.Fallback);
                pair.Key.onCancelled?.RemoveListener(pair.Value.Cancelled);
                pair.Key.onReply?.RemoveListener(pair.Value.Reply);
            }
            subscriptions.Clear();
        }
    }
}
#endif
