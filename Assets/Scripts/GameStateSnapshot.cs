using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

    /// <summary>
    /// Client tarafinda SyncVar'lardan alinan oyun state'i.
    /// Host migration'da yeni host bu state'i server'a restore eder.
    /// Baglanti koptuktan hemen sonra cagirilir; bazen Mirror spawn'lari temizlemis olabilir (best-effort).
    /// </summary>
[Serializable]
public class GameStateSnapshot
{
    public int currentTurnPlayerIndex;
    public int turnNumber;
    public int lastRollValue;
    public int lastRollDice1;
    public int lastRollDice2;
    public int lastRollPlayerIndex;
    public bool isRolling;
    public int rollingPlayerIndex;
    public int winnerPlayerIndex = -1;
    public string winnerName = "";

    [Serializable]
    public class PlayerEntry
    {
        public int playerIndex;
        public ulong steamId;
        public string steamName;
        public int currentSpaceIndex;
        public int selectedCharacterIndex;
        public int selectedDiceIndex;
        public int money;
        public bool hasPassedStart;
    }

    [Serializable]
    public class PropertyEntry
    {
        public int spaceIndex;
        public int ownerPlayerIndex;
        public int houseCount;
    }

    public List<PlayerEntry> players = new List<PlayerEntry>();
    public List<PropertyEntry> properties = new List<PropertyEntry>();

    public bool isValid => players != null && players.Count > 0;

    /// <summary>
    /// Client'ta cagir: mevcut GameTurnManager ve PlayerObject'lardan snapshot al.
    /// propertyOwnership: spaceIndex -> ownerPlayerIndex (PropertyManager.Instance.spaceOwners)
    /// propertyHouseCounts: spaceIndex -> houseCount (PropertyManager.Instance.spaceHouseCounts)
    /// </summary>
    public static GameStateSnapshot Capture(
        IEnumerable<KeyValuePair<int, int>> propertyOwnership = null,
        IEnumerable<KeyValuePair<int, int>> propertyHouseCounts = null)
    {
        var snap = new GameStateSnapshot();
        if (GameTurnManager.Instance != null)
        {
            snap.currentTurnPlayerIndex = GameTurnManager.Instance.currentTurnPlayerIndex;
            snap.turnNumber = GameTurnManager.Instance.turnNumber;
            snap.lastRollValue = GameTurnManager.Instance.lastRollValue;
            snap.lastRollDice1 = GameTurnManager.Instance.lastRollDice1;
            snap.lastRollDice2 = GameTurnManager.Instance.lastRollDice2;
            snap.lastRollPlayerIndex = GameTurnManager.Instance.lastRollPlayerIndex;
            snap.isRolling = GameTurnManager.Instance.isRolling;
            snap.rollingPlayerIndex = GameTurnManager.Instance.rollingPlayerIndex;
            snap.winnerPlayerIndex = GameTurnManager.Instance.winnerPlayerIndex;
            snap.winnerName = GameTurnManager.Instance.winnerName ?? "";
        }

        snap.players.Clear();
        foreach (var kv in NetworkClient.spawned)
        {
            if (kv.Value == null) continue;
            var po = kv.Value.GetComponent<PlayerObject>();
            if (po == null) continue;
            snap.players.Add(new PlayerEntry
            {
                playerIndex = po.playerIndex,
                steamId = po.steamId,
                steamName = po.steamName ?? "",
                currentSpaceIndex = po.currentSpaceIndex,
                selectedCharacterIndex = po.selectedCharacterIndex,
                selectedDiceIndex = po.selectedDiceIndex,
                money = po.money,
                hasPassedStart = po.hasPassedStart
            });
        }
        snap.players.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));

        snap.properties.Clear();
        var houseCountLookup = new Dictionary<int, int>();
        if (propertyHouseCounts != null)
        {
            foreach (var kv in propertyHouseCounts)
                if (kv.Value > 0)
                    houseCountLookup[kv.Key] = kv.Value;
        }
        if (propertyOwnership != null)
        {
            foreach (var kv in propertyOwnership)
            {
                if (kv.Value >= 0)
                {
                    int hc = houseCountLookup.TryGetValue(kv.Key, out int c) ? c : 0;
                    snap.properties.Add(new PropertyEntry { spaceIndex = kv.Key, ownerPlayerIndex = kv.Value, houseCount = hc });
                }
            }
        }

        return snap;
    }
}
