using UnityEngine;
using TMPro;

/// <summary>
/// 38 karelik oyun tahtasi. Pozisyonlar boardRoot'un child'larindan alinir.
/// boardRoot atanmazsa placeholder (dikdortgen yol) otomatik olusur.
/// 3D model: 38 child (sadece kareler) veya 39 child (38 kare + 1 ortadaki board). centerChildIndex ile ortayi atla.
/// reverseSpaceOrder: Model saat yonunde ise true yap (baslangic ters kalirsa).
/// </summary>
public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }
    public const int SpaceCount = 38;

    [Header("Tahta referansi")]
    [Tooltip("38 veya 39 child'i olan root. 39 ise ortadaki board icin centerChildIndex atla.")]
    [SerializeField] private Transform boardRoot;
    [Tooltip("Sadece gorunum icin 3D model. 38 child icermeyen modeller icin: buraya surukle, Hierarchy'de Board altina da ekle. Placeholder kareler gizlenir.")]
    [SerializeField] private Transform boardVisual;
    [Tooltip("39 child varsa: ortadaki board objesinin index'i (0-38). Atlanir, 38 kare kullanilir. Genelde 0.")]
    [SerializeField] private int centerChildIndex = 0;
    [Tooltip("Model saat yonunde siralanmissa ac. Baslangic (GO) ters tarafta kaliyorsa true yap.")]
    [SerializeField] private bool reverseSpaceOrder;
    [Tooltip("Model kullanilirken de kare isimlerini goster (floating label).")]
    [SerializeField] private bool showLabelsWhenUsingModel = true;
    [Tooltip("Modeldeki tile rotasyonunu kullanarak etiketi ayni yone hizalar.")]
    [SerializeField] private bool useModelSpaceRotation = true;
    [Tooltip("Model rotasyonuna eklenecek yaw ofseti.")]
    [SerializeField] private float labelYawOffset = 0f;
    [Tooltip("Tek yon: tum 180. Kenar bazli: her kenar disa bakar (overlap olmaz).")]
    [SerializeField] private bool useSingleLabelRotation = false;
    [SerializeField] private float labelRotAll = 180f;
    [SerializeField] private float labelRotBottom = 180f, labelRotRight = 270f, labelRotTop = 0f, labelRotLeft = 90f;

    [Header("Kare verisi (ScriptableObject)")]
    [Tooltip("Opsiyonel. Atanirsa placeholder kareler bu renk/isim/tip ile cizilir. Create > RichContractor > Board Spaces Data ile olustur.")]
    [SerializeField] private BoardSpaceData boardSpaceData;

    [Header("Placeholder kare boyutu")]
    [Tooltip("Kare genisligi (kup scale). Kareler arasi mesafe = tileSize + tileGap.")]
    [SerializeField] private float tileSize = 1.8f;
    [SerializeField] private float tileHeight = 0.3f;
    [Tooltip("Kareler arasi bosluk. 0 = bitisik.")]
    [SerializeField] private float tileGap = 0.04f;
    [Tooltip("Placeholder karelerin ustunde isim yazisini goster.")]
    [SerializeField] private bool showSpaceLabels = true;
    [SerializeField] private float labelHeight = 0.22f;
    [SerializeField] private int labelFontSize = 52;
    [Tooltip("Label genel olcegi. Buyutunce yazi da buyur.")]
    [SerializeField] private float labelScale = 0.16f;
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private Color labelOutlineColor = Color.black;
    [SerializeField] private float labelOutlineWidth = 0f;
    [Header("Placeholder Materyal")]
    [Tooltip("Opsiyonel. Atarsan tum placeholder kareler bu materyali kullanir.")]
    [SerializeField] private Material placeholderMaterial;

    [Header("Flip (ileride Uno Flip modu)")]
    [SerializeField] private bool flipped;

    private Transform[] _spaces;
    private TMPro.TextMeshPro[] _rentLabels;
    private Material _runtimePlaceholderMaterial;

    public bool Flipped
    {
        get => flipped;
        set => flipped = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (boardRoot != null && TryBuildSpacesFromModel(boardRoot))
        {
            if (showLabelsWhenUsingModel)
                CreateLabelsForModelSpaces();
        }
        else
        {
            CreatePlaceholderBoard();
        }
        if (boardVisual != null)
            boardVisual.gameObject.SetActive(true);
    }

    /// <summary>
    /// Flip dahil efektif kare indeksi (0..37).
    /// </summary>
    public int GetEffectiveIndex(int index)
    {
        index = ((index % SpaceCount) + SpaceCount) % SpaceCount;
        return flipped ? (SpaceCount - 1 - index) : index;
    }

    public Vector3 GetSpacePosition(int index)
    {
        int i = GetEffectiveIndex(index);
        if (_spaces != null && i >= 0 && i < _spaces.Length && _spaces[i] != null)
            return _spaces[i].position;
        return Vector3.zero;
    }

    public Transform GetSpaceTransform(int index)
    {
        int i = GetEffectiveIndex(index);
        if (_spaces != null && i >= 0 && i < _spaces.Length)
            return _spaces[i];
        return null;
    }

    /// <summary>
    /// Tahtayi ters cevir (Uno Flip modu). Sira tersine doner.
    /// </summary>
    public void SetFlipped(bool value)
    {
        flipped = value;
    }

    /// <summary>
    /// Kare verisini dondurur (ScriptableObject atanmissa). Oyun kurallari icin kullanilir.
    /// </summary>
    public SpaceInfo GetSpaceInfo(int index)
    {
        int i = GetEffectiveIndex(index);
        return boardSpaceData != null ? boardSpaceData.GetSpace(i) : null;
    }

    /// <summary>
    /// Model'den 38 kare al. 38 child varsa direkt; 39 varsa centerChildIndex atlanir.
    /// reverseSpaceOrder: model sirasi tersse ac (baslangic karsi tarafta kaliyorsa).
    /// </summary>
    private bool TryBuildSpacesFromModel(Transform root)
    {
        int cnt = root.childCount;
        if (cnt == SpaceCount)
        {
            _spaces = new Transform[SpaceCount];
            for (int i = 0; i < SpaceCount; i++)
            {
                int modelIdx = reverseSpaceOrder ? (SpaceCount - 1 - i) : i;
                _spaces[i] = root.GetChild(modelIdx);
            }
            return true;
        }
        if (cnt == SpaceCount + 1)
        {
            int skip = Mathf.Clamp(centerChildIndex, 0, SpaceCount);
            _spaces = new Transform[SpaceCount];
            for (int i = 0; i < SpaceCount; i++)
            {
                int modelOrder = reverseSpaceOrder ? (SpaceCount - 1 - i) : i;
                int modelIdx = modelOrder < skip ? modelOrder : modelOrder + 1;
                _spaces[i] = root.GetChild(modelIdx);
            }
            return true;
        }
        return false;
    }

    private void CreatePlaceholderBoard()
    {
        var root = new GameObject("Board_Placeholder");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // 38 space: 4 kose + (ust 9, sag 8, alt 9, sol 8 ara kare).
        // Tum kareler ayni boyut; koseler dogrudan path'in parcasi (bosluk yok).
        float step = tileSize + tileGap;
        float halfW = 5f * step;
        float halfH = 4.5f * step;
        float x, z;

        _spaces = new Transform[SpaceCount];
        int idx = 0;

        // Sol ust kose
        x = -halfW;
        z = halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
        idx++;

        // Ust ara: 9 kare
        for (int i = 0; i < 9; i++)
        {
            x = -halfW + (i + 1) * step;
            z = halfH;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
            idx++;
        }

        // Sag ust kose
        x = halfW;
        z = halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
        idx++;

        // Sag ara: 8 kare
        for (int i = 0; i < 8; i++)
        {
            x = halfW;
            z = halfH - (i + 1) * step;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
            idx++;
        }

        // Sag alt kose
        x = halfW;
        z = -halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
        idx++;

        // Alt ara: 9 kare
        for (int i = 0; i < 9; i++)
        {
            x = halfW - (i + 1) * step;
            z = -halfH;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
            idx++;
        }

        // Sol alt kose
        x = -halfW;
        z = -halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
        idx++;

        // Sol ara: 8 kare
        for (int i = 0; i < 8; i++)
        {
            x = -halfW;
            z = -halfH + (i + 1) * step;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
        idx++;
        }
        boardRoot = root.transform;
    }

    private float GetLabelYRotationForSpace(int index)
    {
        if (useModelSpaceRotation && _spaces != null && index >= 0 && index < _spaces.Length && _spaces[index] != null)
        {
            Vector3 fwd = _spaces[index].forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                return yaw + labelYawOffset;
            }
        }
        if (useSingleLabelRotation) return labelRotAll;
        if (index >= 0 && index <= 9) return labelRotBottom;
        if (index >= 11 && index <= 18) return labelRotRight;
        if (index >= 20 && index <= 28) return labelRotTop;
        if (index >= 30 && index <= 37) return labelRotLeft;
        if (index == 10) return labelRotRight;
        if (index == 19) return labelRotTop;
        if (index == 29) return labelRotLeft;
        return labelRotBottom;
    }

    private Bounds GetSpaceBounds(Transform space)
    {
        var r = space.GetComponentInChildren<Renderer>();
        if (r != null && r.bounds.size.sqrMagnitude > 0.001f)
        {
            var b = r.bounds;
            if (b.size.x < 8f && b.size.z < 8f)
                return b;
        }
        return new Bounds(space.position, Vector3.one * 1.5f);
    }

    private void CreateLabelsForModelSpaces()
    {
        if (_spaces == null) return;
        _rentLabels = new TMPro.TextMeshPro[SpaceCount];
        var labelsRoot = new GameObject("Board_Labels");
        labelsRoot.transform.SetParent(transform);
        labelsRoot.transform.localPosition = Vector3.zero;
        labelsRoot.transform.localRotation = Quaternion.identity;
        labelsRoot.transform.localScale = Vector3.one;
        for (int i = 0; i < _spaces.Length; i++)
        {
            var space = _spaces[i];
            if (space == null) continue;
            var info = boardSpaceData != null ? boardSpaceData.GetSpace(i) : null;
            string label = info != null && !string.IsNullOrWhiteSpace(info.displayName) ? info.displayName : $"Space_{i}";
            if (info != null && info.IsPurchasable && info.rent > 0)
                label += $"\n{info.rent} TL";
            var labelGo = new GameObject($"Label_{i}");
            labelGo.transform.SetParent(labelsRoot.transform);
            var bounds = GetSpaceBounds(space);
            // Push label slightly toward outer edge like classic Monopoly boards.
            Vector3 dir = (space.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            float outward = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.2f;
            labelGo.transform.position = new Vector3(bounds.center.x, bounds.max.y + labelHeight, bounds.center.z) + dir * outward;
            float yRot = GetLabelYRotationForSpace(i);
            labelGo.transform.rotation = Quaternion.Euler(90f, yRot, 0f);
            labelGo.transform.localScale = Vector3.one * Mathf.Max(0.05f, labelScale);
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = Mathf.Max(24, labelFontSize);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = labelColor;
            tmp.outlineWidth = labelOutlineWidth;
            tmp.outlineColor = labelOutlineColor;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14;
            tmp.fontSizeMax = Mathf.Max(24, labelFontSize);
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            if (info != null && info.IsPurchasable)
                _rentLabels[i] = tmp;
            var rt = labelGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                float ex = Mathf.Clamp(bounds.size.x, 0.3f, 2.5f) * 0.78f;
                float ez = Mathf.Clamp(bounds.size.z, 0.3f, 2.5f) * 0.78f;
                float s = Mathf.Max(0.01f, labelScale);
                float w = Mathf.Max(ex, ez) / s;
                float h = Mathf.Min(ex, ez) / s * 0.5f;
                rt.sizeDelta = new Vector2(w, Mathf.Max(h, 2f));
            }
        }
    }

    private float _rentRefreshTimer;

    private void Update()
    {
        _rentRefreshTimer -= Time.deltaTime;
        if (_rentRefreshTimer > 0f) return;
        _rentRefreshTimer = 1f;
        RefreshRentLabels();
    }

    private void RefreshRentLabels()
    {
        if (_rentLabels == null || boardSpaceData == null || PropertyManager.Instance == null) return;
        for (int i = 0; i < _rentLabels.Length; i++)
        {
            if (_rentLabels[i] == null) continue;
            var info = boardSpaceData.GetSpace(i);
            if (info == null || !info.IsPurchasable) continue;
            string name = !string.IsNullOrWhiteSpace(info.displayName) ? info.displayName : $"Space_{i}";
            int rent = PropertyManager.Instance.GetRentWithHouses(i, info.rent);
            _rentLabels[i].text = $"{name}\n{rent} TL";
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Transform CreateSpace(Transform parent, float x, float y, float z, int index, bool showVisual = true)
    {
        var info = boardSpaceData != null ? boardSpaceData.GetSpace(index) : null;
        string name = info != null && !string.IsNullOrEmpty(info.displayName) ? $"Space_{index}_{info.displayName}" : $"Space_{index}";
        Color color = info != null ? info.color : Color.Lerp(Color.green, Color.white, index / (float)SpaceCount);

        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        if (!showVisual)
            return go.transform;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Visual";
        cube.transform.SetParent(go.transform);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = new Vector3(tileSize, tileHeight, tileSize);
        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = GetOrCreatePlaceholderMaterial();
            if (mat != null)
                renderer.sharedMaterial = mat;
            ApplyColor(renderer, color);
        }

        if (showSpaceLabels)
        {
            string label = info != null && !string.IsNullOrWhiteSpace(info.displayName)
                ? info.displayName
                : index.ToString();
            if (info != null && info.IsPurchasable && info.rent > 0)
                label += $"\n{info.rent} TL";
            CreateLabel(go.transform, label);
        }

        return go.transform;
    }

    private Material GetOrCreatePlaceholderMaterial()
    {
        if (placeholderMaterial != null) return placeholderMaterial;
        if (_runtimePlaceholderMaterial != null) return _runtimePlaceholderMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null) return null;

        _runtimePlaceholderMaterial = new Material(shader);
        _runtimePlaceholderMaterial.name = "BoardRuntimePlaceholderMat";
        _runtimePlaceholderMaterial.color = Color.white;
        return _runtimePlaceholderMaterial;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void ApplyColor(Renderer renderer, Color color)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color); // URP/HDRP
        block.SetColor(ColorId, color);     // Built-in
        renderer.SetPropertyBlock(block);
    }

    private void CreateLabel(Transform parent, string text)
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(parent);
        labelGo.transform.localPosition = new Vector3(0f, tileHeight * 0.5f + labelHeight, 0f);
        labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        labelGo.transform.localScale = Vector3.one * 0.08f;

        var mesh = labelGo.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = Mathf.Max(8, labelFontSize);
        mesh.characterSize = 0.2f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.black;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (boardRoot != null)
        {
            int c = boardRoot.childCount;
            if (c != SpaceCount && c != SpaceCount + 1)
                Debug.LogWarning($"[BoardManager] boardRoot {boardRoot.name} child sayisi {c}. 38 veya 39 (ortadaki board) olmali. Placeholder kullanilacak.");
        }
    }
#endif
}
