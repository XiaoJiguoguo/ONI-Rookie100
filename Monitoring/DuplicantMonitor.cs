using System;
using System.Collections;
using System.Collections.Generic;
using Rookie100.Content;
using Rookie100.UI;
using UnityEngine;

namespace Rookie100.Monitoring
{
    // Native roster events maintain references; sample only those duplicants once per second.
    // No FindObjects, Grid or Building scans, and no Update-based monitoring loop.
    public sealed class DuplicantMonitor : KMonoBehaviour
    {
        public static DuplicantMonitor Instance { get; private set; }
        public MonitorState State { get; } = new MonitorState();
        public event System.Action Changed;
        public bool Available { get; private set; }
        private readonly Dictionary<int, MinionIdentity> roster = new Dictionary<int, MinionIdentity>();
        private Coroutine sampling;
        private bool subscribed, warned;
        private GameObject hud;
        protected override void OnSpawn()
        {
            base.OnSpawn(); Instance = this;
            Components.LiveMinionIdentities.OnAdd += Add;
            Components.LiveMinionIdentities.OnRemove += Remove;
            subscribed = true;
            foreach (var dupe in Components.LiveMinionIdentities.Items) Add(dupe);
            sampling = StartCoroutine(SampleLoop());
        }
        protected override void OnCleanUp()
        {
            if (subscribed)
            {
                Components.LiveMinionIdentities.OnAdd -= Add;
                Components.LiveMinionIdentities.OnRemove -= Remove;
                subscribed = false;
            }
            Debugging.DebugExporter.EndSession();
            if (sampling != null) StopCoroutine(sampling);
            if (hud != null) UnityEngine.Object.Destroy(hud);
            roster.Clear(); Changed = null;
            if (Instance == this) Instance = null;
            base.OnCleanUp();
        }
        private void Add(MinionIdentity dupe) { if (dupe != null) roster[dupe.GetInstanceID()] = dupe; }
        private void Remove(MinionIdentity dupe) { if (dupe != null) roster.Remove(dupe.GetInstanceID()); }
        public MinionIdentity Resolve(int id) => roster.TryGetValue(id, out var dupe) ? dupe : null;
        public void Move(int direction) { if (State.Move(direction)) Publish(); }
        private IEnumerator SampleLoop()
        {
            // Wait until the game overlay exists, without assuming Game.OnSpawn UI order.
            while (true)
            {
                try
                {
                    Sample();
                    if (hud == null && GameScreenManager.Instance?.ssOverlayCanvas != null)
                        hud = DuplicantMonitorView.CreateHud(GameScreenManager.Instance.ssOverlayCanvas.transform, this);
                }
                catch (Exception e)
                {
                    Available = false;
                    State.Apply(QuestStore.ActiveColonyKey, ClusterManager.Instance?.activeWorld?.id ?? -1, null);
                    Publish();
                    if (!warned) { warned = true; ModLogger.Warn("复制人监测暂不可用: " + e.Message); }
                }
                Debugging.DebugExporter.Write();
                yield return new WaitForSecondsRealtime(1f);
            }
        }
        private static float? ReadPercent(Klei.AI.Amount amount, GameObject dupe)
        {
            try { var value = amount?.Lookup(dupe); return value == null ? null : MonitorState.Percent(value.value, value.GetMax()); }
            catch { return null; }
        }
        private void Sample()
        {
            int world = ClusterManager.Instance?.activeWorld?.id ?? -1;
            var readings = new List<DuplicantReading>();
            var amounts = Db.Get()?.Amounts;
            foreach (var pair in roster)
            {
                var dupe = pair.Value;
                if (dupe == null || dupe.GetMyWorldId() != world) continue;
                float? calories = null;
                try { var value = amounts?.Calories.Lookup(dupe.gameObject); if (value != null && !float.IsNaN(value.value) && !float.IsInfinity(value.value)) calories = value.value / 1000f; } catch { }
                readings.Add(new DuplicantReading { Id = pair.Key, World = world,
                    Name = dupe.GetProperName(), Health = ReadPercent(amounts?.HitPoints, dupe.gameObject),
                    Stress = ReadPercent(amounts?.Stress, dupe.gameObject), Breath = ReadPercent(amounts?.Breath, dupe.gameObject), Calories = calories });
            }
            State.Apply(QuestStore.ActiveColonyKey, world, readings); Available = true; Publish();
        }
        private void Publish()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (System.Action callback in handlers.GetInvocationList())
                try { callback(); } catch (Exception e) { ModLogger.Warn("复制人监测界面刷新失败: " + e.Message); }
        }
    }
}
