using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Oyunculari tahta uzerinde basit capsule piyonlarla gosterir.
/// PlayerObject.currentSpaceIndex degeri degistikce piyonlar hareket eder.
/// </summary>
public class SimplePawnVisualizer : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [Tooltip("Piyonun kare uzerindeki yukseklik. Model kullanirken 0.4-0.7 arasi deneyin.")]
    [SerializeField] private float pawnHeight = 0.5f;
    [Tooltip("Ince ayar: ek yukseklik. Cok yukarida kalirsa negatif yap.")]
    [SerializeField] private float pawnHeightOffset = 0f;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float localYOffset = 0.02f;
    [SerializeField] private float bounceHeight = 0.25f;
    [SerializeField] private float bounceDuration = 0.2f;

    private readonly Dictionary<uint, PawnView> _pawns = new Dictionary<uint, PawnView>();

    /// <summary>Build'de pembe gorunmemesi icin URP/Built-in shader ile material; palet rengine gore cache.</summary>
    private static Material[] _cachedPawnMaterials;

    private static Material GetPawnMaterial(int playerIndex)
    {
        if (_cachedPawnMaterials == null)
            _cachedPawnMaterials = new Material[Palette.Length];
        int i = Mathf.Abs(playerIndex) % Palette.Length;
        if (_cachedPawnMaterials[i] != null)
            return _cachedPawnMaterials[i];
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null) return null;
        var mat = new Material(shader);
        mat.color = Palette[i];
        _cachedPawnMaterials[i] = mat;
        return mat;
    }

    private static readonly Color[] Palette =
    {
        new Color(0.95f, 0.25f, 0.25f),
        new Color(0.25f, 0.55f, 1.00f),
        new Color(0.25f, 0.85f, 0.45f),
        new Color(0.95f, 0.80f, 0.20f)
    };

    private class PawnView
    {
        public PlayerObject Player;
        public Transform Root;
        public Renderer Renderer;
        public TextMesh Label;
        public int LastSpaceIndex = -1;
        public float BounceTimer;
    }

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
    }

    private void Update()
    {
        if (boardManager == null) return;
        SyncPawns();
        UpdatePawnPositions();
    }

    private void SyncPawns()
    {
        var players = GetPlayers();
        var aliveNetIds = new HashSet<uint>();

        foreach (var player in players)
        {
            if (player == null) continue;
            uint id = player.netId;
            aliveNetIds.Add(id);

            if (_pawns.ContainsKey(id)) continue;

            var pawnGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pawnGo.name = $"Pawn_P{player.playerIndex}_{id}";
            pawnGo.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);

            var renderer = pawnGo.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = GetPawnMaterial(player.playerIndex);
                if (mat != null)
                    renderer.sharedMaterial = mat;
                else if (renderer.material != null)
                    renderer.material.color = ColorByPlayerIndex(player.playerIndex);
            }

            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(pawnGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.08f;

            var label = labelGo.AddComponent<TextMesh>();
            label.fontSize = 56;
            label.characterSize = 0.14f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;

            _pawns[id] = new PawnView
            {
                Player = player,
                Root = pawnGo.transform,
                Renderer = renderer,
                Label = label,
                LastSpaceIndex = player.currentSpaceIndex,
                BounceTimer = 0f
            };
        }

        var toRemove = new List<uint>();
        foreach (var kv in _pawns)
        {
            if (!aliveNetIds.Contains(kv.Key))
            {
                if (kv.Value?.Root != null) Destroy(kv.Value.Root.gameObject);
                toRemove.Add(kv.Key);
            }
        }

        foreach (var id in toRemove) _pawns.Remove(id);
    }

    private void UpdatePawnPositions()
    {
        foreach (var kv in _pawns)
        {
            var view = kv.Value;
            if (view == null || view.Player == null || view.Root == null) continue;

            int idx = view.Player.currentSpaceIndex;
            if (idx != view.LastSpaceIndex)
            {
                view.LastSpaceIndex = idx;
                view.BounceTimer = bounceDuration;
            }

            Vector3 basePos = boardManager.GetSpacePosition(idx);
            Vector3 offset = PerPlayerOffset(view.Player.playerIndex);
            float y = pawnHeight + pawnHeightOffset + (view.Player.isLocalPlayer ? localYOffset : 0f);
            if (view.BounceTimer > 0f)
            {
                float t = 1f - (view.BounceTimer / Mathf.Max(0.01f, bounceDuration));
                y += Mathf.Sin(t * Mathf.PI) * bounceHeight;
                view.BounceTimer -= Time.deltaTime;
            }
            Vector3 target = basePos + offset + Vector3.up * y;

            view.Root.position = Vector3.Lerp(view.Root.position, target, Time.deltaTime * moveSpeed);
            int activeTurnIndex = GameTurnManager.Instance != null ? GameTurnManager.Instance.currentTurnPlayerIndex : -1;
            bool isActive = view.Player.playerIndex == activeTurnIndex;
            float targetScale = isActive ? 0.78f : 0.65f;
            view.Root.localScale = Vector3.Lerp(view.Root.localScale, new Vector3(targetScale, targetScale, targetScale), Time.deltaTime * 8f);
            if (view.Label != null)
            {
                string n = string.IsNullOrWhiteSpace(view.Player.steamName)
                    ? $"P{view.Player.playerIndex}"
                    : view.Player.steamName;
                view.Label.text = n;
                view.Label.color = isActive ? Color.yellow : Color.white;
            }
        }
    }

    private List<PlayerObject> GetPlayers()
    {
        var list = new List<PlayerObject>();
        foreach (var kv in NetworkClient.spawned)
        {
            if (kv.Value == null) continue;
            var p = kv.Value.GetComponent<PlayerObject>();
            if (p != null && !list.Contains(p)) list.Add(p);
        }

        list.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return list;
    }

    private static Vector3 PerPlayerOffset(int playerIndex)
    {
        // Ayni karede birden fazla piyon oldugunda biraz ayrisinlar.
        float r = 0.25f;
        int i = Mathf.Abs(playerIndex) % 4;
        switch (i)
        {
            case 0: return new Vector3(-r, 0f, -r);
            case 1: return new Vector3(r, 0f, -r);
            case 2: return new Vector3(-r, 0f, r);
            default: return new Vector3(r, 0f, r);
        }
    }

    private static Color ColorByPlayerIndex(int playerIndex)
    {
        int i = Mathf.Abs(playerIndex) % Palette.Length;
        return Palette[i];
    }
}
