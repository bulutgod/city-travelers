using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Iki zar atma animasyonu: zarlar yukaridan firlatilir, havada doner, yere dusunce son yuzler gosterilir.
/// GameTurnManager.OnDiceRollStarted / OnDiceRollLanded(dice1, dice2) ile tetiklenir.
/// </summary>
public class DiceRollAnimator : MonoBehaviour
{
    [Header("Kurulum")]
    [Tooltip("Animasyonda kullanilacak zar prefabi. Assets/Dice_6/Prefabs/ icinden birini surukle.")]
    [SerializeField] private GameObject dicePrefab;
    [Tooltip("Zarlarin spawn edilecegi parent. Bos birakilirsa bu script'in transform'i kullanilir.")]
    [SerializeField] private Transform spawnParent;

    [Header("Firlatma")]
    [Tooltip("Zarlarin baslangic yuksekligi (parent pozisyonunun ustunde, world unit).")]
    [SerializeField] private float throwHeight = 4f;
    [Tooltip("Yere dusme suresi (saniye).")]
    [SerializeField] private float fallDuration = 1f;
    [Tooltip("Iki zar arasindaki yatay mesafe (world unit).")]
    [SerializeField] private float diceSpacing = 0.8f;
    [Tooltip("Havada donus hizi (derece/saniye).")]
    [SerializeField] private float spinSpeed = 720f;

    [Header("Sonuc")]
    [Tooltip("Yere dustukten sonra sonucun gosterildigi sure (saniye).")]
    [SerializeField] private float showResultDuration = 1.5f;

    [Header("Zar yuzu rotasyonlari (Euler, 1-6)")]
    [Tooltip("Mesh child objedeyse buraya child adini yaz (ornegin 'Model'); bos birakilirsa root. Prefab'ta hangi objenin Rotation'unu ayarladiysan o kullanilir.")]
    [SerializeField] private string applyRotationToChild;
    [Tooltip("Prefab'ta 1-6 yuzleri icin ayarladigin Transform Rotation (X,Y,Z). Inspector: Element 0 = Yuz 1, Element 1 = Yuz 2, ... Element 5 = Yuz 6.")]
    [SerializeField]
    private Vector3[] faceRotations = new Vector3[]
    {
        new Vector3(-90, 0, 0),   // Yuz 1 (Element 0)
        new Vector3(0, 0, 0),      // Yuz 2 (Element 1)
        new Vector3(0, 0, -90),    // Yuz 3 (Element 2)
        new Vector3(0, 0, 90),    // Yuz 4 (Element 3)
        new Vector3(180, 0, 0),    // Yuz 5 (Element 4)
        new Vector3(90, 0, 0),    // Yuz 6 (Element 5)
    };

    private struct DiceState
    {
        public GameObject go;
        public float startY;
        public float groundY;
        public float fallSpeed;
        public float landedAt;
        public int faceValue;
        public Vector3 spinAxis;
    }

    private DiceState _dice1;
    private DiceState _dice2;
    private Coroutine _rollCoroutine;
    private Transform _parent;

    private void OnEnable()
    {
        GameTurnManager.OnDiceRollStarted += OnRollStarted;
        GameTurnManager.OnDiceRollLanded += OnRollLanded;
    }

    private void OnDisable()
    {
        GameTurnManager.OnDiceRollStarted -= OnRollStarted;
        GameTurnManager.OnDiceRollLanded -= OnRollLanded;
        if (_rollCoroutine != null)
            StopCoroutine(_rollCoroutine);
        DestroyDice(ref _dice1);
        DestroyDice(ref _dice2);
    }

    private static void DestroyDice(ref DiceState state)
    {
        if (state.go != null)
        {
#if UNITY_EDITOR
            if (Selection.activeGameObject == state.go)
                Selection.activeGameObject = null;
#endif
            Destroy(state.go);
            state.go = null;
        }
    }

    private void OnRollStarted()
    {
        if (_rollCoroutine != null)
            StopCoroutine(_rollCoroutine);
        DestroyDice(ref _dice1);
        DestroyDice(ref _dice2);

        if (dicePrefab == null) return;

        _parent = spawnParent != null ? spawnParent : transform;
        Vector3 basePos = _parent.position;
        float groundY = basePos.y;
        float startY = basePos.y + throwHeight;
        float fallSpeed = throwHeight / Mathf.Max(0.01f, fallDuration);

        _dice1.go = Instantiate(dicePrefab, new Vector3(basePos.x - diceSpacing * 0.5f, startY, basePos.z), Quaternion.identity, _parent);
        _dice1.go.name = "DiceRollInstance1";
        _dice1.startY = startY;
        _dice1.groundY = groundY;
        _dice1.fallSpeed = fallSpeed;
        _dice1.landedAt = -1f;
        _dice1.faceValue = 0;
        _dice1.spinAxis = new Vector3(1f, 0.7f, 0.5f).normalized;

        _dice2.go = Instantiate(dicePrefab, new Vector3(basePos.x + diceSpacing * 0.5f, startY, basePos.z), Quaternion.identity, _parent);
        _dice2.go.name = "DiceRollInstance2";
        _dice2.startY = startY;
        _dice2.groundY = groundY;
        _dice2.fallSpeed = fallSpeed;
        _dice2.landedAt = -1f;
        _dice2.faceValue = 0;
        _dice2.spinAxis = new Vector3(-0.5f, 1f, 0.6f).normalized;

        _rollCoroutine = StartCoroutine(ThrowAndSpinUntilLanded());
    }

    private void OnRollLanded(int dice1Value, int dice2Value)
    {
        _dice1.faceValue = Mathf.Clamp(dice1Value, 1, 6);
        _dice2.faceValue = Mathf.Clamp(dice2Value, 1, 6);
    }

    private IEnumerator ThrowAndSpinUntilLanded()
    {
        float firstLandedTime = -1f;
        while (_dice1.go != null || _dice2.go != null)
        {
            float dt = Time.deltaTime;
            float spin = spinSpeed * dt;

            if (_dice1.go != null)
            {
                var t = _dice1.go.transform;
                if (_dice1.landedAt < 0f)
                {
                    float newY = t.position.y - _dice1.fallSpeed * dt;
                    if (newY <= _dice1.groundY)
                    {
                        newY = _dice1.groundY;
                        _dice1.landedAt = Time.time;
                        if (firstLandedTime < 0f) firstLandedTime = Time.time;
                    }
                    t.position = new Vector3(t.position.x, newY, t.position.z);
                }
                t.Rotate(_dice1.spinAxis.x * spin, _dice1.spinAxis.y * spin, _dice1.spinAxis.z * spin, Space.World);
            }

            if (_dice2.go != null)
            {
                var t = _dice2.go.transform;
                if (_dice2.landedAt < 0f)
                {
                    float newY = t.position.y - _dice2.fallSpeed * dt;
                    if (newY <= _dice2.groundY)
                    {
                        newY = _dice2.groundY;
                        _dice2.landedAt = Time.time;
                        if (firstLandedTime < 0f) firstLandedTime = Time.time;
                    }
                    t.position = new Vector3(t.position.x, newY, t.position.z);
                }
                t.Rotate(_dice2.spinAxis.x * spin, _dice2.spinAxis.y * spin, _dice2.spinAxis.z * spin, Space.World);
            }

            bool bothLanded = (_dice1.go == null || _dice1.landedAt >= 0f) && (_dice2.go == null || _dice2.landedAt >= 0f);
            bool bothValuesSet = _dice1.faceValue > 0 && _dice2.faceValue > 0;
            bool timeout = firstLandedTime >= 0f && (Time.time - firstLandedTime) > 1.5f;
            if (bothLanded && (bothValuesSet || timeout))
                break;

            yield return null;
        }

        if (_dice1.faceValue <= 0) _dice1.faceValue = 1;
        if (_dice2.faceValue <= 0) _dice2.faceValue = 1;

        ApplyFaceRotation(_dice1.go, _dice1.faceValue);
        ApplyFaceRotation(_dice2.go, _dice2.faceValue);

        yield return new WaitForSeconds(showResultDuration);

        DestroyDice(ref _dice1);
        DestroyDice(ref _dice2);
        _rollCoroutine = null;
    }

    private void ApplyFaceRotation(GameObject diceGo, int faceValue)
    {
        if (diceGo == null || faceRotations == null || faceValue < 1 || faceValue > 6)
            return;
        int index = faceValue - 1;
        if (index >= faceRotations.Length)
            return;

        var rb = diceGo.GetComponent<Rigidbody>();
        if (rb == null) rb = diceGo.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Transform target = diceGo.transform;
        if (applyRotationToChild != null && applyRotationToChild.Length > 0)
        {
            var child = diceGo.transform.Find(applyRotationToChild);
            if (child != null) target = child;
        }

        target.localRotation = Quaternion.Euler(faceRotations[index]);
    }
}
