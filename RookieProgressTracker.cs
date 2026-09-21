using System;
using System.Collections.Generic;
using KSerialization;

namespace Rookie100
{
    /// <summary>Per-save progress owner. Legacy JSON is an import source only.</summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class RookieProgressTracker : KMonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;

        // Zero must remain the default: an old save has no serialized tracker.
        [Serialize] private int schemaVersion;
        [Serialize] private List<string> claimed = new List<string>();
        [Serialize] private List<string> accepted = new List<string>();

        public int SchemaVersion { get { return schemaVersion; } }
        public bool IsReady { get; private set; }
        public int ClaimedCount { get { return IsReady ? claimed.Count : 0; } }

        /// <summary>
        /// Called at Game.OnSpawn, after save restoration. Never import into v1,
        /// even when its lists are empty (reset/earlier save snapshots are valid).
        /// </summary>
        internal void Initialize(IEnumerable<string> legacyClaimed, IEnumerable<string> legacyAccepted)
        {
            if (IsReady)
                return;
            if (schemaVersion < 0 || schemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException("Unsupported Rookie100 progress schema: " + schemaVersion);

            // Build both lists before committing the migration. Preserve unknown IDs.
            var nextClaimed = Normalize(schemaVersion == 0 ? legacyClaimed : claimed);
            var nextAccepted = Normalize(schemaVersion == 0 ? legacyAccepted : accepted);
            switch (schemaVersion)
            {
                case 0: // legacy JSON (or a new colony) -> native save v1
                    claimed = nextClaimed;
                    accepted = nextAccepted;
                    schemaVersion = 1;
                    break;
                case 1:
                    claimed = nextClaimed;
                    accepted = nextAccepted;
                    break;
            }
            IsReady = true;
        }

        private static List<string> Normalize(IEnumerable<string> source)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
                foreach (var id in source)
                    if (!string.IsNullOrEmpty(id) && seen.Add(id))
                        result.Add(id);
            return result;
        }

        public bool IsClaimed(string id)
        {
            return IsReady && id != null && claimed.Contains(id);
        }

        public bool IsAccepted(string id)
        {
            return IsReady && id != null && accepted.Contains(id);
        }

        /// <summary>A detached read-only snapshot; a missing ID never creates progress.</summary>
        public ProgressSnapshot GetProgress(string id)
        {
            bool isClaimed = IsClaimed(id);
            bool isAccepted = IsAccepted(id);
            return isClaimed || isAccepted ? new ProgressSnapshot(isAccepted, isClaimed) : null;
        }

        public sealed class ProgressSnapshot
        {
            public readonly bool IsAccepted;
            public readonly bool IsClaimed;
            internal ProgressSnapshot(bool isAccepted, bool isClaimed)
            {
                IsAccepted = isAccepted;
                IsClaimed = isClaimed;
            }
        }

        internal bool AcceptQuest(string id)
        {
            if (!IsReady || string.IsNullOrEmpty(id) || claimed.Contains(id) || accepted.Contains(id))
                return false;
            accepted.Add(id);
            return true;
        }

        internal void MarkClaimed(string id)
        {
            if (IsReady && !string.IsNullOrEmpty(id) && !claimed.Contains(id))
                claimed.Add(id);
        }

        internal void ResetAll()
        {
            if (!IsReady)
                return;
            claimed.Clear();
            accepted.Clear();
        }

        protected override void OnCleanUp()
        {
            Content.QuestStore.DeactivateColony(this);
            IsReady = false;
            base.OnCleanUp();
        }
    }
}
