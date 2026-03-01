using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zar kozmetik se�im modal?n? y�netir.
/// `LobbyUINew` ve `DiceSlotUI` ile birlikte �al???r.
/// </summary>
public class DicePickerUI : MonoBehaviour
{
    [Header("Modal")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;   // Arka plana t?klan?nca kapat

    [Header("Zar Slot Prefab ve Container")]
    [SerializeField] private Transform diceSlotsContainer;
    [SerializeField] private GameObject diceSlotPrefab;  // Her zar i�in prefab

    // Callback: se�im yap?l?nca �a?r?l?r
    public Action<int> OnDiceSelected;

    private int _currentSelected = 0;
    private DiceSlotUI[] _slots;

    // -------------------------------------------------------
    // Zar Verisi
    // -------------------------------------------------------

    [Serializable]
    public class DiceSkinData
    {
        public string label;
        public Sprite diceSprite;
        public bool isUnlocked;
        public int coinPrice;
    }

    [Header("Zar Verileri")]
    public DiceSkinData[] diceSkins =
    {
        new DiceSkinData { label = "PEMBE",   isUnlocked = true,  coinPrice = 0   },
        new DiceSkinData { label = "MAVİ",    isUnlocked = true,  coinPrice = 0   },
        new DiceSkinData { label = "ALTIN",   isUnlocked = false, coinPrice = 500 },
        new DiceSkinData { label = "SİYAH",   isUnlocked = false, coinPrice = 750 },
        new DiceSkinData { label = "YEŞİL",   isUnlocked = false, coinPrice = 300 },
        new DiceSkinData { label = "KIRMIZI", isUnlocked = false, coinPrice = 400 },
    };

    // -------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------

    private void Awake()
    {
        closeButton?.onClick.AddListener(Hide);
        backdropButton?.onClick.AddListener(Hide);

        // Slotlar? olu?tur
        BuildSlots();

        // Ba?lang?�ta gizle
        if (modalPanel) modalPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // Modal A�ma/Kapama
    // -------------------------------------------------------

    public void Show(int currentIndex)
    {
        _currentSelected = Mathf.Clamp(currentIndex, 0, diceSkins.Length - 1);
        RefreshSlots();
        if (modalPanel) modalPanel.SetActive(true);
    }

    public void Hide()
    {
        if (modalPanel) modalPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // Slot Y�netimi
    // -------------------------------------------------------

    private void BuildSlots()
    {
        if (diceSlotsContainer == null || diceSlotPrefab == null || diceSkins == null)
            return;

        _slots = new DiceSlotUI[diceSkins.Length];

        for (int i = 0; i < diceSkins.Length; i++)
        {
            int idx = i; // closure i�in
            var go = Instantiate(diceSlotPrefab, diceSlotsContainer);
            var slot = go.GetComponent<DiceSlotUI>();
            if (slot == null) continue;

            slot.Setup(diceSkins[i], () => OnSlotClicked(idx));
            _slots[i] = slot;
        }
    }

    private void RefreshSlots()
    {
        if (_slots == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null)
                _slots[i].SetSelected(i == _currentSelected);
        }
    }

    private void OnSlotClicked(int index)
    {
        if (!diceSkins[index].isUnlocked)
        {
            // Kilitli: sat?n alma ak???n? tetikle (ileride eklenecek)
            Debug.Log($"[DicePicker] Kilitli zar t?kland?: {diceSkins[index].label}, Fiyat: {diceSkins[index].coinPrice}");
            return;
        }

        _currentSelected = index;
        RefreshSlots();
        OnDiceSelected?.Invoke(index);
        Hide();
    }

    /// <summary>
    /// Verilen indeks i�in zar skin bilgisini d�ner.
    /// </summary>
    public DiceSkinData GetSkin(int index)
    {
        if (diceSkins == null || diceSkins.Length == 0)
            return null;

        index = Mathf.Clamp(index, 0, diceSkins.Length - 1);
        return diceSkins[index];
    }
}

