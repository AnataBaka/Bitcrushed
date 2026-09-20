using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

/// Displayed HP lags table HP until the existing hit callback fires.
/// Mana is not stored here; views keep reading raw mana.
public static class CombatHpPresenter
{
    public const float WatchdogSeconds = 12f;

    public struct Displayed
    {
        public int Hp;
        public bool Alive;
        public bool Visible;
        public bool Targetable;
        public int PendingHits;
        public float PendingSince;
    }

    static readonly Dictionary<ulong, Displayed> State = new Dictionary<ulong, Displayed>();
    static readonly Dictionary<ulong, Entity> Ghosts = new Dictionary<ulong, Entity>();

    public static event Action Changed;

    public static bool HoldsTeardown
    {
        get
        {
            if (HasPending)
            {
                return true;
            }

            foreach (var pair in State)
            {
                if (!pair.Value.Visible || pair.Value.Hp <= 0)
                {
                    continue;
                }

                var live = GameManager.FindEntity(pair.Key);
                if (live == null || !live.Alive)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static bool HasPending
    {
        get
        {
            foreach (var pair in State)
            {
                if (pair.Value.PendingHits > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static void OnSubscribed()
    {
        State.Clear();
        Ghosts.Clear();
        if (GameManager.Conn == null)
        {
            return;
        }

        foreach (var entity in GameManager.Conn.Db.Entity.Iter())
        {
            Snap(entity);
        }
    }

    public static void OnInserted(Entity entity)
    {
        if (entity == null)
        {
            return;
        }

        Ghosts[entity.EntityId] = entity;
        Snap(entity);
        Changed?.Invoke();
    }

    public static void OnUpdated(Entity previous, Entity next)
    {
        if (next == null)
        {
            return;
        }

        Ghosts[next.EntityId] = next;
        if (GameManager.Instance == null || !GameManager.Instance.SubscriptionReady)
        {
            Snap(next);
            return;
        }

        var shown = ViewOf(next);
        if (next.Hp > (previous != null ? previous.Hp : shown.Hp))
        {
            // Healing is immediate. Displayed HP is the new authoritative value,
            // so it never exceeds table HP and never shows a value that was not real.
            shown.Hp = next.Hp;
        }
        else if (next.Hp < shown.Hp)
        {
            if (shown.PendingSince <= 0f)
            {
                shown.PendingSince = Time.unscaledTime;
            }
        }

        if (shown.PendingHits <= 0 && next.Hp >= shown.Hp)
        {
            shown.Hp = next.Hp;
            shown.PendingSince = 0f;
        }

        ApplyFlags(ref shown, next);
        State[next.EntityId] = shown;
        Changed?.Invoke();
    }

    public static void OnDeleted(Entity entity)
    {
        if (entity == null)
        {
            return;
        }

        var shown = ViewOf(entity);
        if (shown.Visible && shown.Hp > 0)
        {
            Ghosts[entity.EntityId] = entity;
            if (shown.PendingSince <= 0f)
            {
                shown.PendingSince = Time.unscaledTime;
            }

            ApplyFlags(ref shown, null);
            State[entity.EntityId] = shown;
            Changed?.Invoke();
            return;
        }

        Ghosts.Remove(entity.EntityId);
        State.Remove(entity.EntityId);
        Changed?.Invoke();
    }

    public static void RegisterHit(ulong entityId)
    {
        var shown = Shown(entityId);
        shown.PendingHits += 1;
        if (shown.PendingSince <= 0f)
        {
            shown.PendingSince = Time.unscaledTime;
        }

        State[entityId] = shown;
    }

    public static void ApplyImpact(ulong entityId, int damage)
    {
        var shown = Shown(entityId);
        if (shown.Hp <= 0 && !shown.Visible)
        {
            return;
        }

        if (shown.Hp <= 0)
        {
            if (shown.PendingHits > 0)
            {
                shown.PendingHits -= 1;
            }

            ApplyFlags(ref shown, GameManager.FindEntity(entityId));
            State[entityId] = shown;
            Changed?.Invoke();
            return;
        }

        var nextHp = Math.Max(0, shown.Hp - Math.Max(0, damage));
        var live = GameManager.FindEntity(entityId);
        if (live != null)
        {
            // Overlap: never drop below current table HP (heal already applied)
            // and never invent a value above what this impact stepped to.
            nextHp = Math.Max(live.Hp, nextHp);
        }

        shown.Hp = nextHp;
        if (shown.PendingHits > 0)
        {
            shown.PendingHits -= 1;
        }

        if (shown.PendingHits <= 0)
        {
            shown.PendingSince = 0f;
            if (live != null)
            {
                shown.Hp = live.Hp;
            }
        }

        ApplyFlags(ref shown, live);
        State[entityId] = shown;
        Changed?.Invoke();
    }

    public static void RevealNow(ulong entityId)
    {
        var live = GameManager.FindEntity(entityId);
        if (live != null)
        {
            Snap(live);
        }
        else if (State.TryGetValue(entityId, out var shown))
        {
            shown.Hp = 0;
            shown.PendingHits = 0;
            shown.PendingSince = 0f;
            ApplyFlags(ref shown, null);
            State[entityId] = shown;
        }

        Changed?.Invoke();
    }

    public static void Tick()
    {
        var now = Time.unscaledTime;
        var snapped = false;
        var ids = new List<ulong>(State.Keys);
        foreach (var id in ids)
        {
            var shown = State[id];
            var live = GameManager.FindEntity(id);
            var mismatch =
                (live != null && shown.Hp != live.Hp) || (live == null && shown.Hp > 0);
            if (!mismatch)
            {
                if (shown.PendingHits <= 0)
                {
                    shown.PendingSince = 0f;
                    State[id] = shown;
                }

                continue;
            }

            if (shown.PendingSince <= 0f)
            {
                shown.PendingSince = now;
                State[id] = shown;
                continue;
            }

            if (now - shown.PendingSince < WatchdogSeconds)
            {
                continue;
            }

            if (live != null)
            {
                Snap(live);
            }
            else
            {
                shown.Hp = 0;
                shown.PendingHits = 0;
                shown.PendingSince = 0f;
                ApplyFlags(ref shown, null);
                State[id] = shown;
            }

            snapped = true;
        }

        if (snapped)
        {
            Changed?.Invoke();
        }
    }

    public static Displayed ViewOf(Entity entity)
    {
        if (entity == null)
        {
            return default;
        }

        if (State.TryGetValue(entity.EntityId, out var shown))
        {
            return shown;
        }

        return FromEntity(entity);
    }

    public static int DisplayedHp(Entity entity) => ViewOf(entity).Hp;

    public static int DisplayedHp(ulong entityId)
    {
        if (State.TryGetValue(entityId, out var shown))
        {
            return shown.Hp;
        }

        var entity = GameManager.FindEntity(entityId) ?? Ghost(entityId);
        return entity != null ? entity.Hp : 0;
    }

    public static bool IsVisible(Entity entity) => entity != null && ViewOf(entity).Visible;

    public static bool IsVisible(ulong entityId)
    {
        if (State.TryGetValue(entityId, out var shown))
        {
            return shown.Visible;
        }

        var entity = GameManager.FindEntity(entityId);
        return entity != null && entity.Alive;
    }

    public static bool IsTargetable(Entity entity)
    {
        if (entity == null)
        {
            return false;
        }

        var shown = ViewOf(entity);
        var live = GameManager.FindEntity(entity.EntityId);
        return shown.Hp > 0 && live != null && live.Alive;
    }

    public static bool DisplayedAlive(Entity entity) => entity != null && ViewOf(entity).Alive;

    public static Entity Ghost(ulong entityId)
    {
        return Ghosts.TryGetValue(entityId, out var ghost) ? ghost : null;
    }

    public static List<Entity> VisibleTeam(Team faction)
    {
        var seen = new HashSet<ulong>();
        var list = new List<Entity>();
        if (GameManager.Conn != null)
        {
            foreach (var entity in GameManager.Conn.Db.Entity.Iter())
            {
                if (entity.Faction != faction)
                {
                    continue;
                }

                if (!IsVisible(entity) && !entity.Alive)
                {
                    continue;
                }

                seen.Add(entity.EntityId);
                list.Add(entity);
            }
        }

        foreach (var pair in Ghosts)
        {
            if (seen.Contains(pair.Key) || pair.Value == null || pair.Value.Faction != faction)
            {
                continue;
            }

            if (!IsVisible(pair.Key))
            {
                continue;
            }

            list.Add(pair.Value);
        }

        list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return list;
    }

    public static void Hide(ulong entityId)
    {
        if (!State.TryGetValue(entityId, out var shown))
        {
            Ghosts.Remove(entityId);
            return;
        }

        shown.Visible = false;
        shown.Targetable = false;
        shown.Alive = false;
        shown.Hp = 0;
        shown.PendingHits = 0;
        State[entityId] = shown;
        Ghosts.Remove(entityId);
        Changed?.Invoke();
    }

    static Displayed Shown(ulong entityId)
    {
        if (State.TryGetValue(entityId, out var shown))
        {
            return shown;
        }

        var entity = GameManager.FindEntity(entityId) ?? Ghost(entityId);
        if (entity != null)
        {
            shown = FromEntity(entity);
            State[entityId] = shown;
            return shown;
        }

        return default;
    }

    static void Snap(Entity entity)
    {
        State[entity.EntityId] = FromEntity(entity);
        Ghosts[entity.EntityId] = entity;
    }

    static Displayed FromEntity(Entity entity)
    {
        var live = GameManager.FindEntity(entity.EntityId);
        return new Displayed
        {
            Hp = entity.Hp,
            Alive = entity.Hp > 0,
            Visible = entity.Alive || entity.Hp > 0,
            Targetable = entity.Hp > 0 && live != null && live.Alive,
            PendingHits = 0,
            PendingSince = 0f,
        };
    }

    static void ApplyFlags(ref Displayed shown, Entity live)
    {
        shown.Alive = shown.Hp > 0;
        shown.Targetable = shown.Hp > 0 && live != null && live.Alive;
        if (shown.Hp > 0)
        {
            shown.Visible = true;
        }
    }
}
