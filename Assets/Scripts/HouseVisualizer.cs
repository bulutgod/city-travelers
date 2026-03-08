using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PropertyManager.spaceOwners ve spaceHouseCounts degistikce board uzerinde
/// sahiplik/yer, ev ve otel gosterir. FBX prefab atanirsa onlar, atanmazsa kure/kup/silindir kullanilir.
/// 0 ev = yer, 1-3 ev = ev seviyesi 1/2/3, 4 ev = 3 ile aynı model, 5 = otel. Toplam 5 bina modeli.
/// </summary>
public class HouseVisualizer : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [Header("Primitive boyutlari (prefab atanmazsa kullanilir)")]
    [SerializeField] private float cubeSize = 0.28f;
    [SerializeField] private float sphereSize = 0.22f;
    [SerializeField] private float spacing = 0.32f;
    [Tooltip("Kare merkezinden dis kenara dogru uzaklik.")]
    [SerializeField] private float edgeOffset = 0.55f;

    [Header("Bina modelleri (FBX) - 1 yer, 3 ev seviyesi, 1 otel (toplam 5 model)")]
    [Tooltip("0 ev: yer / sahiplik (en kucuk). Atanmazsa kure kullanilir.")]
    [SerializeField] private GameObject ownerOnlyPrefab;
    [Tooltip("1 ev. Atanmazsa kup.")]
    [SerializeField] private GameObject houseLevel1Prefab;
    [Tooltip("2 ev. Atanmazsa 2 kup.")]
    [SerializeField] private GameObject houseLevel2Prefab;
    [Tooltip("3 ev ve 4 ev (ayni model). Atanmazsa 3/4 kup.")]
    [SerializeField] private GameObject houseLevel3Prefab;
    [Tooltip("Otel (5. seviye). Atanmazsa silindir.")]
    [SerializeField] private GameObject hotelPrefab;
    [Tooltip("Prefab modellerinin ortak olcegi. Tahta uzerine sigacak sekilde ayarla.")]
    [SerializeField] private float buildingModelScale = 0.4f;
    [Tooltip("Prefab kullanilirken sahip rengini modele uygula (tek renk).")]
    [SerializeField] private bool tintPrefabWithOwnerColor = false;

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

        // Havalimanlarında bina dikilmez - sadece sahiplik göstergesi
        var info = boardManager != null ? boardManager.GetSpaceInfo(spaceIndex) : null;
        bool isAirport = info != null && info.spaceType == SpaceInfo.SpaceType.Havalimani;
        if (isAirport) houseCount = 0;

        Color ownerColor = ColorByPlayerIndex(ownerIndex);
        var list = new List<GameObject>();

        Vector3 center = spaceTf.position;
        Vector3 outward = (center - boardManager.transform.position);
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.001f) outward = Vector3.forward;
        outward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, outward).normalized;
        float topY = GetSpaceTopY(spaceTf);

        Vector3 basePos = center - outward * edgeOffset;
        Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);

        if (houseCount <= 0)
        {
            if (ownerOnlyPrefab != null)
            {
                var go = Instantiate(ownerOnlyPrefab, basePos, rot);
                go.name = $"Owner_S{spaceIndex}";
                go.transform.position = new Vector3(basePos.x, topY, basePos.z);
                go.transform.rotation = rot;
                go.transform.localScale = Vector3.one * buildingModelScale;
                DisableColliders(go);
                if (tintPrefabWithOwnerColor) ApplyColorToAllRenderers(go, ownerColor);
                list.Add(go);
            }
            else
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Owner_S{spaceIndex}";
                Vector3 pos = basePos;
                pos.y = topY + sphereSize * 0.5f;
                sphere.transform.position = pos;
                sphere.transform.localScale = Vector3.one * sphereSize;
                var col = sphere.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                ApplyColorToRenderer(sphere.GetComponent<Renderer>(), ownerColor);
                list.Add(sphere);
            }
        }
        else if (houseCount >= 5)
        {
            if (hotelPrefab != null)
            {
                var go = Instantiate(hotelPrefab, basePos, rot);
                go.name = $"Hotel_S{spaceIndex}";
                go.transform.position = new Vector3(basePos.x, topY, basePos.z);
                go.transform.rotation = rot;
                go.transform.localScale = Vector3.one * buildingModelScale;
                DisableColliders(go);
                if (tintPrefabWithOwnerColor) ApplyColorToAllRenderers(go, ownerColor);
                list.Add(go);
            }
            else
            {
                var hotel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hotel.name = $"Hotel_S{spaceIndex}";
                float hotelH = cubeSize * 1.5f;
                Vector3 pos = basePos;
                pos.y = topY + hotelH * 0.5f;
                hotel.transform.position = pos;
                hotel.transform.localScale = new Vector3(cubeSize * 1.2f, hotelH * 0.5f, cubeSize * 1.2f);
                var col = hotel.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                ApplyColorToRenderer(hotel.GetComponent<Renderer>(), ownerColor);
                list.Add(hotel);
            }
        }
        else
        {
            // 1-3 ev = level 1/2/3, 4 ev = level 3 (aynı model)
            GameObject prefab = houseCount == 1 ? houseLevel1Prefab : houseCount == 2 ? houseLevel2Prefab : houseLevel3Prefab;
            if (prefab != null)
            {
                var go = Instantiate(prefab, basePos, rot);
                go.name = $"House_S{spaceIndex}";
                go.transform.position = new Vector3(basePos.x, topY, basePos.z);
                go.transform.rotation = rot;
                go.transform.localScale = Vector3.one * buildingModelScale;
                DisableColliders(go);
                if (tintPrefabWithOwnerColor) ApplyColorToAllRenderers(go, ownerColor);
                list.Add(go);
            }
            else
            {
                float totalWidth = (houseCount - 1) * spacing;
                float startX = -totalWidth * 0.5f;
                basePos.y = topY + cubeSize * 0.5f;
                for (int i = 0; i < houseCount; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"House_S{spaceIndex}_{i}";
                    cube.transform.position = basePos + right * (startX + i * spacing);
                    cube.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);
                    var col = cube.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                    ApplyColorToRenderer(cube.GetComponent<Renderer>(), ownerColor);
                    list.Add(cube);
                }
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

    private static void DisableColliders(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private static void ApplyColorToAllRenderers(GameObject go, Color color)
    {
        foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            ApplyColorToRenderer(rend, color);
    }

    /// <summary>
    /// Tüm prefab rengini degistirir: hem _Color (Built-in) hem _BaseColor (URP) uygulanir.
    /// Böylece zemin degil komple bina rengi degisir.
    /// </summary>
    private static void ApplyColorToRenderer(Renderer rend, Color color)
    {
        if (rend == null) return;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        try
        {
            var copy = new Material(rend.sharedMaterial);
            if (copy.HasProperty("_Color"))
                copy.SetColor("_Color", color);
            if (copy.HasProperty("_BaseColor"))
                copy.SetColor("_BaseColor", color);
            rend.sharedMaterial = copy;
        }
        catch
        {
            if (rend.material.HasProperty("_Color")) rend.material.SetColor("_Color", color);
            if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", color);
        }
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
