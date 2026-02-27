using UnityEngine;

/// <summary>
/// 38 karelik oyun tahtasi. Pozisyonlar boardRoot'un 38 child'indan alinir.
/// boardRoot atanmazsa placeholder (dikdortgen yol) otomatik olusur.
/// Sonra 3D model geldiginde: model root'u boardRoot'a surukle birak yeterli (root 38 child icermeli, sirayla).
/// Uno Flip icin: Flipped true yapilinca kare sirasi tersine doner.
/// </summary>
public class BoardManager : MonoBehaviour
{
    public const int SpaceCount = 38;

    [Header("Tahta referansi")]
    [Tooltip("38 child'i olan root. Atanmazsa placeholder olusur. Model gelince buraya surukle birak.")]
    [SerializeField] private Transform boardRoot;

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
    [SerializeField] private int labelFontSize = 28;
    [Header("Placeholder Materyal")]
    [Tooltip("Opsiyonel. Atarsan tum placeholder kareler bu materyali kullanir.")]
    [SerializeField] private Material placeholderMaterial;

    [Header("Flip (ileride Uno Flip modu)")]
    [SerializeField] private bool flipped;

    private Transform[] _spaces;
    private Material _runtimePlaceholderMaterial;

    public bool Flipped
    {
        get => flipped;
        set => flipped = value;
    }

    private void Awake()
    {
        if (boardRoot != null && boardRoot.childCount >= SpaceCount)
        {
            _spaces = new Transform[SpaceCount];
            for (int i = 0; i < SpaceCount; i++)
                _spaces[i] = boardRoot.GetChild(i);
        }
        else
        {
            CreatePlaceholderBoard();
        }
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
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
        idx++;

        // Ust ara: 9 kare
        for (int i = 0; i < 9; i++)
        {
            x = -halfW + (i + 1) * step;
            z = halfH;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
            idx++;
        }

        // Sag ust kose
        x = halfW;
        z = halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
        idx++;

        // Sag ara: 8 kare
        for (int i = 0; i < 8; i++)
        {
            x = halfW;
            z = halfH - (i + 1) * step;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
            idx++;
        }

        // Sag alt kose
        x = halfW;
        z = -halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
        idx++;

        // Alt ara: 9 kare
        for (int i = 0; i < 9; i++)
        {
            x = halfW - (i + 1) * step;
            z = -halfH;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
            idx++;
        }

        // Sol alt kose
        x = -halfW;
        z = -halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
        idx++;

        // Sol ara: 8 kare
        for (int i = 0; i < 8; i++)
        {
            x = -halfW;
            z = -halfH + (i + 1) * step;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx);
            idx++;
        }
        boardRoot = root.transform;
    }

    private Transform CreateSpace(Transform parent, float x, float y, float z, int index)
    {
        var info = boardSpaceData != null ? boardSpaceData.GetSpace(index) : null;
        string name = info != null && !string.IsNullOrEmpty(info.displayName) ? $"Space_{index}_{info.displayName}" : $"Space_{index}";
        Color color = info != null ? info.color : Color.Lerp(Color.green, Color.white, index / (float)SpaceCount);

        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

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
            CreateLabel(go.transform, label);
        }

        return go.transform;
    }

    private Material GetOrCreatePlaceholderMaterial()
    {
        if (placeholderMaterial != null) return placeholderMaterial;
        if (_runtimePlaceholderMaterial != null) return _runtimePlaceholderMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;

        _runtimePlaceholderMaterial = new Material(shader);
        _runtimePlaceholderMaterial.name = "BoardRuntimePlaceholderMat";
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
        if (boardRoot != null && boardRoot.childCount != SpaceCount)
            Debug.LogWarning($"[BoardManager] boardRoot {boardRoot.name} child sayisi {boardRoot.childCount}, {SpaceCount} olmali. Placeholder kullanilacak.");
    }
#endif
}
