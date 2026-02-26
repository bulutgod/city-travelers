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

    [Header("Flip (ileride Uno Flip modu)")]
    [SerializeField] private bool flipped;

    private Transform[] _spaces;

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

    private void CreatePlaceholderBoard()
    {
        var root = new GameObject("Board_Placeholder");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // Kareler bitisik olsun: merkez araligi = kup boyutu (tileSize)
        const float tileSize = 2f;
        const float w = 9f * tileSize;  // ust/alt: 10 nokta, 9 aralik
        const float h = 8f * tileSize;  // sag/sol: 9 nokta, 8 aralik
        float x, z;

        _spaces = new Transform[SpaceCount];
        int idx = 0;

        // Ust kenar: 10 kare, sol ust (-w/2, h/2) -> sag ust (w/2, h/2) - kose noktalari dahil
        for (int i = 0; i < 10; i++)
        {
            x = -w / 2f + i * (w / 9f);
            z = h / 2f;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx++);
        }
        // Sag kenar: 9 kare, sag ust (w/2, h/2) -> sag alt (w/2, -h/2) - ust kose zaten cizildi
        for (int i = 1; i <= 9; i++)
        {
            x = w / 2f;
            z = h / 2f - i * (h / 9f);
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx++);
        }
        // Alt kenar: 10 kare, sag alt -> sol alt
        for (int i = 0; i < 10; i++)
        {
            x = w / 2f - i * (w / 9f);
            z = -h / 2f;
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx++);
        }
        // Sol kenar: 9 kare, sol alt -> sol ust - alt kose zaten cizildi
        for (int i = 1; i <= 9; i++)
        {
            x = -w / 2f;
            z = -h / 2f + i * (h / 9f);
            _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx++);
        }

        boardRoot = root.transform;
    }

    private static Transform CreateSpace(Transform parent, float x, float y, float z, int index)
    {
        var go = new GameObject($"Space_{index}");
        go.transform.SetParent(parent);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Visual";
        cube.transform.SetParent(go.transform);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = new Vector3(1.8f, 0.3f, 1.8f);
        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
            renderer.material.color = Color.Lerp(Color.green, Color.white, index / (float)SpaceCount);

        return go.transform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (boardRoot != null && boardRoot.childCount != SpaceCount)
            Debug.LogWarning($"[BoardManager] boardRoot {boardRoot.name} child sayisi {boardRoot.childCount}, {SpaceCount} olmali. Placeholder kullanilacak.");
    }
#endif
}
