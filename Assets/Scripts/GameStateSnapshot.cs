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
    public int lastRollPlayerIndex;
    public bool isRolling;
    public int rollingPlayerIndex;

    [Serializable]
    public class PlayerEntry
    {
        public int playerIndex;
        public ulong steamId;
        public string steamName;
        public int currentSpaceIndex;
        public int selectedCharacterIndex;
        public int selectedDiceIndex;
    }

    public List<PlayerEntry> players = new List<PlayerEntry>();

    public bool isValid => players != null && players.Count > 0;

    /// <summary>
    /// Client'ta cagir: mevcut GameTurnManager ve PlayerObject'lardan snapshot al.
    /// </summary>
    public static GameStateSnapshot Capture()
    {
        var snap = new GameStateSnapshot();
        if (GameTurnManager.Instance != null)
        {
            snap.currentTurnPlayerIndex = GameTurnManager.Instance.currentTurnPlayerIndex;
            snap.turnNumber = GameTurnManager.Instance.turnNumber;
            snap.lastRollValue = GameTurnManager.Instance.lastRollValue;
            snap.lastRollPlayerIndex = GameTurnManager.Instance.lastRollPlayerIndex;
            snap.isRolling = GameTurnManager.Instance.isRolling;
            snap.rollingPlayerIndex = GameTurnManager.Instance.rollingPlayerIndex;
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
                selectedDiceIndex = po.selectedDiceIndex
            });
        }
        snap.players.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return snap;
    }
}
