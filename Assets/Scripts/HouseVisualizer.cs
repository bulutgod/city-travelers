using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PropertyManager.spaceOwners ve spaceHouseCounts degistikce board uzerinde
/// sahiplik sphere'i ve ev kupleri olusturur.
/// 0 ev = 1 sphere (sahiplik isareti), 1-4 ev = 1-4 kup.
/// Sonradan sprite/model ile degistirilecek placeholder.
/// </summary>
public class HouseVisualizer : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private float cubeSize = 0.28f;
    [SerializeField] private float sphereSize = 0.22f;
    [SerializeField] private float spacing = 0.32f;
    [Tooltip("Kare merkezinden dis kenara dogru uzaklik.")]
    [SerializeField] private float edgeOffset = 0.55f;

    private static readonly Color[] Palette =
    {
        new Color(0.95f, 0.25f, 0.25f),
        new Color(0.25f, 0.55f, 1.00f),
        new Color(0.25f, 0.85f, 0.45f),
        new Color(0.95f, 0.80f, 0.20f)
    };

    private readonly Dictionary<int, List<GameObject>> _visuals = new Dictionary<int, List<GameObject>>();

    private struct SpaceState
    {
        public int owner;
        public int houses;
    }
    private readonly Dictionary<int, SpaceState> _lastState = new Dictionary<int, SpaceState>();

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
    }

    private void Update()
    {
        if (boardManager == null || PropertyManager.Instance == null) return;
        Refresh();
    }

    private void Refresh()
    {
        var pm = PropertyManager.Instance;
        var trackedSpaces = new HashSet<int>();

        foreach (var kv in pm.spaceOwners)
        {
            int spaceIndex = kv.Key;
            int owner = kv.Value;
            if (owner < 0) continue;

            trackedSpaces.Add(spaceIndex);
            int houses = pm.GetHouseCount(spaceIndex);

            _lastState.TryGetValue(spaceIndex, out SpaceState prev);
            if (prev.owner == owner && prev.houses == houses && _visuals.ContainsKey(spaceIndex))
                continue;

            _lastState[spaceIndex] = new SpaceState { owner = owner, houses = houses };
            RebuildVisualsForSpace(spaceIndex, houses, owner);
        }

        var toRemove = new List<int>();
        foreach (var kv in _lastState)
        {
            if (!trackedSpaces.Contains(kv.Key))
            {
                ClearVisuals(kv.Key);
                toRemove.Add(kv.Key);
            }
        }
        foreach (int idx in toRemove)
            _lastState.Remove(idx);
    }

    private void RebuildVisualsForSpace(int spaceIndex, int houseCount, int ownerIndex)
    {
        ClearVisuals(spaceIndex);

        Transform spaceTf = boardManager.GetSpaceTransform(spaceIndex);
        if (spaceTf == null) return;

        Color ownerColor = ColorByPlayerIndex(ownerIndex);
        var list = new List<GameObject>();

        Vector3 center = spaceTf.position;
        Vector3 outward = (center - boardManager.transform.position);
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.001f) outward = Vector3.forward;
        outward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, outward).normalized;
        float topY = GetSpaceTopY(spaceTf);

        if (houseCount <= 0)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Owner_S{spaceIndex}";
            Vector3 pos = center - outward * edgeOffset;
            pos.y = topY + sphereSize * 0.5f;
            sphere.transform.position = pos;
            sphere.transform.localScale = Vector3.one * sphereSize;

            var col = sphere.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var rend = sphere.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
                rend.material.color = ownerColor;

            list.Add(sphere);
        }
        else if (houseCount >= 5)
        {
            var hotel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hotel.name = $"Hotel_S{spaceIndex}";
            Vector3 pos = center - outward * edgeOffset;
            float hotelH = cubeSize * 1.5f;
            pos.y = topY + hotelH * 0.5f;
            hotel.transform.position = pos;
            hotel.transform.localScale = new Vector3(cubeSize * 1.2f, hotelH * 0.5f, cubeSize * 1.2f);

            var col = hotel.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var rend = hotel.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
                rend.material.color = ownerColor;

            list.Add(hotel);
        }
        else
        {
            float totalWidth = (houseCount - 1) * spacing;
            float startX = -totalWidth * 0.5f;
            Vector3 basePos = center - outward * edgeOffset;
            basePos.y = topY + cubeSize * 0.5f;

            for (int i = 0; i < houseCount; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"House_S{spaceIndex}_{i}";
                cube.transform.position = basePos + right * (startX + i * spacing);
                cube.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);

                var col = cube.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                var rend = cube.GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                    rend.material.color = ownerColor;

                list.Add(cube);
            }
        }

        _visuals[spaceIndex] = list;
    }

    private void ClearVisuals(int spaceIndex)
    {
        if (!_visuals.TryGetValue(spaceIndex, out var list)) return;
        foreach (var go in list)
        {
            if (go != null) Destroy(go);
        }
        list.Clear();
        _visuals.Remove(spaceIndex);
    }

    private float GetSpaceTopY(Transform spaceTf)
    {
        var rend = spaceTf.GetComponentInChildren<Renderer>();
        if (rend != null)
            return rend.bounds.max.y;
        return spaceTf.position.y + 0.15f;
    }

    private static Color ColorByPlayerIndex(int playerIndex)
    {
        if (playerIndex < 0) return Color.gray;
        int i = Mathf.Abs(playerIndex) % Palette.Length;
        return Palette[i];
    }

    private void OnDestroy()
    {
        foreach (var kv in _visuals)
        {
            foreach (var go in kv.Value)
            {
                if (go != null) Destroy(go);
            }
        }
        _visuals.Clear();
    }
}
