using System.Collections;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 38 karelik oyun tahtasi. Pozisyonlar boardRoot'un child'larindan alinir.
/// boardRoot atanmazsa placeholder (dikdortgen yol) otomatik olusur.
/// 3D model: 38 child (sadece kareler) veya 39 child (38 kare + 1 ortadaki board). centerChildIndex ile ortayi atla.
/// reverseSpaceOrder: Model saat yonunde ise true yap (baslangic ters kalirsa).
/// </summary>
[ExecuteInEditMode]
public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    [Header("Tahta referansi")]
    [Tooltip("Kare sayisi. Yeni board 36, klasik 38. Model child sayisi bu veya bu+1 (ortadaki board) olmali.")]
    [SerializeField] private int spaceCount = 36;
    [Tooltip("Manuel atama: Board child'larini sirasiyla surukle (0=Start, 1=sonraki...). Doluysa boardRoot yok sayilir.")]
    [SerializeField] private Transform[] manualSpaceTransforms;
    [Tooltip("36 veya 38 child'i olan root. manualSpaceTransforms bos ise kullanilir.")]
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

    [Header("Tahta etiket fontu")]
    [Tooltip("Tahta kare etiketleri icin Font Asset. Bos birakilirsa LiberationSans SDF denenir.\nYeni font: Project'te .ttf dosyasina TIKLA > Sag tik > Create > TextMeshPro > Font Asset (SDF).")]
    [SerializeField] private TMP_FontAsset labelFontAsset;
    [Tooltip("Font kalinligi: Normal = ince, Bold = kalin.")]
    [SerializeField] private TMPro.FontStyles labelFontStyle = TMPro.FontStyles.Normal;
    [Tooltip("Isim ve kira ayri TextMesh olarak olusturulur. Isim ustte, kira altta.")]
    [SerializeField] private float labelNameRentSpacing = 0.08f;
    [Tooltip("Etiketler her zaman kameraya baksin (billboard). Aciksa useModelSpaceRotation vb. yok sayilir.")]
    [SerializeField] private bool labelBillboard = false;
    [Tooltip("Billboard: Sadece Y ekseni etrafinda don (yatay, tahta duzleminde). Kapaliysa tam billboard (kameraya dogru egilir).")]
    [SerializeField] private bool labelBillboardYOnly = true;

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

    [Header("Piyon pozisyonu (FBX pivot merkezdeyse)")]
    [Tooltip("FBX modelinde space pivot'lari merkezdeyse: Mesh merkezini kullan. Kapaliysa transform.position kullanilir.")]
    [SerializeField] private bool useMeshCenterForPawnPosition = true;
    [Tooltip("Tum space pozisyonlarina eklenen offset. Board yanlis yerdeyse ayarlayin.")]
    [SerializeField] private Vector3 spacePositionOffset = Vector3.zero;

    [Header("Kare basma animasyonu")]
    [Tooltip("Oyuncu indiginde karenin asagi inme miktari (Y).")]
    [SerializeField] private float spacePressDepth = 0.08f;
    [Tooltip("Asagi inme suresi (saniye).")]
    [SerializeField] private float spacePressDownDuration = 0.08f;
    [Tooltip("Yukari cikma suresi (saniye).")]
    [SerializeField] private float spacePressUpDuration = 0.12f;

    private TMP_FontAsset _cachedLabelFont;

    private TMP_FontAsset GetLabelFontAsset()
    {
        if (labelFontAsset != null) return labelFontAsset;
        if (_cachedLabelFont != null) return _cachedLabelFont;
        _cachedLabelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_cachedLabelFont != null) return _cachedLabelFont;
        return TMP_Settings.defaultFontAsset;
    }

    private Transform[] _spaces;
    private Transform[] _labelTransforms;
    private TMPro.TextMeshPro[] _nameLabels;
    private TMPro.TextMeshPro[] _rentLabels;
    private Material _runtimePlaceholderMaterial;

    /// <summary>Tahta kare sayisi (36 veya 38). Diger scriptler bu degeri kullanir.</summary>
    public int SpaceCount => spaceCount;

    public bool Flipped
    {
        get => flipped;
        set => flipped = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (TryBuildSpacesFromManualTransforms())
        {
            if (showLabelsWhenUsingModel)
            {
                var existingLabels = transform.Find("Board_Labels");
                if (existingLabels != null && existingLabels.childCount >= SpaceCount)
                {
                    TrySoftRefreshLabels();
                    return;
                }
                CreateLabelsForModelSpaces();
            }
        }
        else if (boardRoot != null && TryBuildSpacesFromModel(boardRoot))
        {
            if (showLabelsWhenUsingModel)
            {
                var existingLabels = transform.Find("Board_Labels");
                if (existingLabels != null && existingLabels.childCount >= SpaceCount)
                {
                    TrySoftRefreshLabels();
                    return;
                }
                CreateLabelsForModelSpaces();
            }
        }
        else
        {
            CreatePlaceholderBoard();
        }
        if (boardVisual != null)
            boardVisual.gameObject.SetActive(true);
    }

    private bool TryBuildSpacesFromManualTransforms()
    {
        if (manualSpaceTransforms == null || manualSpaceTransforms.Length < SpaceCount) return false;
        int valid = 0;
        for (int i = 0; i < SpaceCount; i++)
        {
            if (manualSpaceTransforms[i] != null) valid++;
        }
        if (valid < SpaceCount) return false;
        _spaces = new Transform[SpaceCount];
        for (int i = 0; i < SpaceCount; i++)
            _spaces[i] = manualSpaceTransforms[i];
        return true;
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
        if (_spaces == null || i < 0 || i >= _spaces.Length || _spaces[i] == null)
            return Vector3.zero;
        var space = _spaces[i];
        Vector3 pos;
        if (useMeshCenterForPawnPosition)
        {
            var mf = space.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                pos = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
            }
            else
            {
                var r = space.GetComponentInChildren<Renderer>();
                if (r != null && r.bounds.size.sqrMagnitude > 0.001f)
                {
                    var b = r.bounds;
                    if (b.size.x < 3f && b.size.z < 3f)
                        pos = b.center;
                    else
                        pos = space.position;
                }
                else
                {
                    pos = space.position;
                }
            }
        }
        else
        {
            pos = space.position;
        }
        return pos + spacePositionOffset;
    }

    public Transform GetSpaceTransform(int index)
    {
        int i = GetEffectiveIndex(index);
        if (_spaces != null && i >= 0 && i < _spaces.Length)
            return _spaces[i];
        return null;
    }

    /// <summary>
    /// Oyuncu indiginde karenin asagi inip yukari cikma animasyonu.
    /// </summary>
    public void PlaySpaceLandingAnimation(int spaceIndex)
    {
        if (_spaces == null) return;
        int i = GetEffectiveIndex(spaceIndex);
        if (i < 0 || i >= _spaces.Length || _spaces[i] == null) return;
        StartCoroutine(AnimateSpacePress(i));
    }

    private IEnumerator AnimateSpacePress(int spaceIndex)
    {
        var space = _spaces[spaceIndex];
        if (space == null) yield break;

        Vector3 basePos = space.localPosition;
        Vector3 downPos = basePos + Vector3.down * spacePressDepth;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, spacePressDownDuration);
            space.localPosition = Vector3.Lerp(basePos, downPos, Mathf.Clamp01(t));
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, spacePressUpDuration);
            space.localPosition = Vector3.Lerp(downPos, basePos, Mathf.Clamp01(t));
            yield return null;
        }

        space.localPosition = basePos;
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

        // 36 space: 4 kose + (8,8,8,8). 38 space: 4 kose + (9,8,9,8).
        int top = 8, right = 8, bottom = 8, left = 8;
        if (spaceCount == 38) { top = 9; bottom = 9; }

        float step = tileSize + tileGap;
        float halfW = (top + 1) * 0.5f * step;
        float halfH = (right + 1) * 0.5f * step;
        float x, z;

        _spaces = new Transform[SpaceCount];
        int idx = 0;

        // Sol ust kose
        x = -halfW;
        z = halfH;
        _spaces[idx] = CreateSpace(root.transform, x, 0f, z, idx, boardVisual == null);
        idx++;

        // Ust ara
        for (int i = 0; i < top; i++)
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

        // Sag ara
        for (int i = 0; i < right; i++)
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

        // Alt ara
        for (int i = 0; i < bottom; i++)
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

        // Sol ara
        for (int i = 0; i < left; i++)
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
        int bottomEnd = (spaceCount == 38) ? 9 : 8;
        int rightEnd = bottomEnd + 1 + 8;
        int topEnd = rightEnd + 1 + (spaceCount == 38 ? 9 : 8);
        if (index >= 0 && index <= bottomEnd) return labelRotBottom;
        if (index > bottomEnd && index <= rightEnd) return labelRotRight;
        if (index > rightEnd && index <= topEnd) return labelRotTop;
        if (index > topEnd && index < spaceCount) return labelRotLeft;
        return labelRotBottom;
    }

    /// <summary>Mevcut etiketler varsa sadece metni gunceller, pozisyon/font/scale dokunmaz. Manuel ayarlar korunur.</summary>
    private bool TrySoftRefreshLabels()
    {
        var root = transform.Find("Board_Labels");
        if (root == null || root.childCount < SpaceCount) return false;
        _nameLabels = new TMPro.TextMeshPro[SpaceCount];
        _rentLabels = new TMPro.TextMeshPro[SpaceCount];
        _labelTransforms = new Transform[SpaceCount];
        for (int i = 0; i < SpaceCount; i++)
        {
            var labelT = root.Find($"Label_{i}");
            if (labelT == null && i < root.childCount) labelT = root.GetChild(i);
            if (labelT == null) return false;
            var nameT = labelT.Find("Name");
            var rentT = labelT.Find("Rent");
            if (nameT == null) return false;
            var nameTmp = nameT.GetComponent<TextMeshPro>();
            var rentTmp = rentT != null ? rentT.GetComponent<TextMeshPro>() : null;
            if (nameTmp == null) return false;
            var info = boardSpaceData != null ? boardSpaceData.GetSpace(i) : null;
            // Isim metnine dokunma - kullanicinin enter/satir sonu ayarlari korunsun
            if (rentTmp != null && info != null && info.IsPurchasable)
            {
                int baseRent = GameEconomy.ScalePrice(info.rent);
                int rent = PropertyManager.Instance != null ? PropertyManager.Instance.GetRentWithHouses(i, baseRent) : baseRent;
                rentTmp.text = GameEconomy.FormatMoney(rent);
                _rentLabels[i] = rentTmp;
            }
            _nameLabels[i] = nameTmp;
            _labelTransforms[i] = labelT;
        }
        return true;
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

    private void CreateLabelsForModelSpaces(bool forceRebuild = false)
    {
        if (_spaces == null) return;
        if (!forceRebuild && TrySoftRefreshLabels()) return;
        var existing = transform.Find("Board_Labels");
        if (existing != null) DestroyImmediate(existing);
        _nameLabels = new TMPro.TextMeshPro[SpaceCount];
        _rentLabels = new TMPro.TextMeshPro[SpaceCount];
        _labelTransforms = new Transform[SpaceCount];
        var labelsRoot = new GameObject("Board_Labels");
        labelsRoot.transform.SetParent(transform);
        labelsRoot.transform.localPosition = Vector3.zero;
        labelsRoot.transform.localRotation = Quaternion.identity;
        labelsRoot.transform.localScale = Vector3.one;
        int fontSize = Mathf.Max(24, labelFontSize);
        float ex = 1.5f, ez = 1.5f, s = Mathf.Max(0.01f, labelScale);
        float w = Mathf.Max(ex, ez) / s;
        float h = Mathf.Min(ex, ez) / s * 0.5f;
        Vector2 sizeDelta = new Vector2(Mathf.Max(w, 50f), Mathf.Max(h, 20f));

        for (int i = 0; i < _spaces.Length; i++)
        {
            var space = _spaces[i];
            if (space == null) continue;
            var info = boardSpaceData != null ? boardSpaceData.GetSpace(i) : null;
            string name = info != null && !string.IsNullOrWhiteSpace(info.displayName) ? info.displayName : $"Space_{i}";
            bool isPurchasable = info != null && info.IsPurchasable && info.rent > 0;

            Vector3 spaceCenter = GetSpacePosition(i);
            var bounds = GetSpaceBounds(space);
            Vector3 dir = (spaceCenter - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            float outward = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.2f;
            Vector3 basePos = new Vector3(spaceCenter.x, bounds.max.y + labelHeight, spaceCenter.z) + dir * outward;
            float yRot = labelBillboard ? 0f : GetLabelYRotationForSpace(i);
            Quaternion rot = Quaternion.Euler(90f, yRot, 0f);
            float scale = Mathf.Max(0.05f, labelScale);

            var labelGo = new GameObject($"Label_{i}");
            labelGo.transform.SetParent(labelsRoot.transform);
            labelGo.transform.position = basePos;
            labelGo.transform.rotation = rot;
            labelGo.transform.localScale = Vector3.one * scale;
            _labelTransforms[i] = labelGo.transform;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(labelGo.transform, false);
            nameGo.transform.localPosition = new Vector3(0f, labelNameRentSpacing, 0f);
            nameGo.transform.localRotation = Quaternion.identity;
            nameGo.transform.localScale = Vector3.one;
            var nameTmp = nameGo.AddComponent<TextMeshPro>();
            nameTmp.text = name;
            SetupLabelTmp(nameTmp, sizeDelta, fontSize);
            _nameLabels[i] = nameTmp;

            if (isPurchasable)
            {
                var rentGo = new GameObject("Rent");
                rentGo.transform.SetParent(labelGo.transform, false);
                rentGo.transform.localPosition = new Vector3(0f, -labelNameRentSpacing, 0f);
                rentGo.transform.localRotation = Quaternion.identity;
                rentGo.transform.localScale = Vector3.one;
                var rentTmp = rentGo.AddComponent<TextMeshPro>();
                rentTmp.text = GameEconomy.FormatMoney(GameEconomy.ScalePrice(info.rent));
                SetupLabelTmp(rentTmp, sizeDelta, fontSize);
                _rentLabels[i] = rentTmp;
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

    private void LateUpdate()
    {
        if (!labelBillboard || _labelTransforms == null) return;
        var cam = Camera.main;
#if UNITY_EDITOR
        if (cam == null && !Application.isPlaying)
            cam = SceneView.lastActiveSceneView?.camera;
#endif
        if (cam == null) return;
        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < _labelTransforms.Length; i++)
        {
            var t = _labelTransforms[i];
            if (t == null) continue;
            Vector3 pos = t.position;
            if (labelBillboardYOnly)
            {
                float yRot = Mathf.Atan2(camPos.x - pos.x, camPos.z - pos.z) * Mathf.Rad2Deg + 180f;
                t.rotation = Quaternion.Euler(90f, yRot, 0f);
            }
            else
            {
                t.LookAt(2f * pos - camPos);
                t.Rotate(90f, 0f, 0f);
            }
        }
    }

    private void RefreshRentLabels()
    {
        if (_rentLabels == null || boardSpaceData == null || PropertyManager.Instance == null) return;
        for (int i = 0; i < _rentLabels.Length; i++)
        {
            if (_rentLabels[i] == null) continue;
            var info = boardSpaceData.GetSpace(i);
            if (info == null || !info.IsPurchasable) continue;
            int rent = PropertyManager.Instance.GetRentWithHouses(i, GameEconomy.ScalePrice(info.rent));
            _rentLabels[i].text = GameEconomy.FormatMoney(rent);
        }
    }

    private void SetupLabelTmp(TextMeshPro tmp, Vector2 sizeDelta, int fontSize)
    {
        var rt = tmp.rectTransform;
        rt.sizeDelta = sizeDelta;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = labelColor;
        tmp.outlineWidth = labelOutlineWidth;
        tmp.outlineColor = labelOutlineColor;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableWordWrapping = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Mathf.Max(12, fontSize / 2);
        tmp.fontSizeMax = fontSize;
        tmp.font = GetLabelFontAsset();
        tmp.fontStyle = labelFontStyle;
        tmp.ForceMeshUpdate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (boardRoot != null && (_spaces == null || _spaces.Length == 0))
            TryBuildSpacesFromModel(boardRoot);
        if (_spaces == null || _spaces.Length == 0) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < _spaces.Length; i++)
        {
            if (_spaces[i] == null) continue;
            var pos = GetSpacePosition(i);
            Gizmos.DrawWireSphere(pos, 0.15f);
        }
    }
#endif

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
                label += $"\n{GameEconomy.FormatMoney(GameEconomy.ScalePrice(info.rent))}";
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
    /// <summary>Etiketleri silip yeniden olusturur. BoardSpaceData'dan isim/kira alir. Duzenledikten sonra sahneyi kaydet (Ctrl+S).</summary>
    [ContextMenu("Önizleme: Etiketleri Yenile")]
    public void EditorRefreshLabels()
    {
        if (Application.isPlaying) return;
        Undo.RegisterFullObjectHierarchyUndo(gameObject, "Etiketleri Yenile");
        var existing = transform.Find("Board_Labels");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }
        bool ok = TryBuildSpacesFromManualTransforms();
        if (!ok && boardRoot != null) ok = TryBuildSpacesFromModel(boardRoot);
        if (ok)
        {
            CreateLabelsForModelSpaces(forceRebuild: true);
            var labelsRoot = transform.Find("Board_Labels");
            if (labelsRoot != null)
            {
                Undo.RegisterCreatedObjectUndo(labelsRoot.gameObject, "Etiketleri Yenile");
            }
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            }
        }
    }

    private void OnValidate()
    {
        if (boardSpaceData != null && boardSpaceData.spaces != null && boardSpaceData.spaces.Length > 0)
            spaceCount = boardSpaceData.Count;
        if (manualSpaceTransforms == null)
            manualSpaceTransforms = new Transform[spaceCount];
        else if (manualSpaceTransforms.Length < spaceCount)
            System.Array.Resize(ref manualSpaceTransforms, spaceCount);
        if (boardRoot != null && (manualSpaceTransforms == null || manualSpaceTransforms.Length == 0))
        {
            int c = boardRoot.childCount;
            if (c != spaceCount && c != spaceCount + 1)
                Debug.LogWarning($"[BoardManager] boardRoot {boardRoot.name} child sayisi {c}. {spaceCount} veya {spaceCount + 1} (ortadaki board) olmali. Placeholder kullanilacak.");
        }
    }
#endif
}
