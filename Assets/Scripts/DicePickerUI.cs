using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Zar kozmetik seçim modalýný yönetir.
/// Kilitli zarlar coin fiyatýyla gösterilir, açýk olanlar seçilebilir.
/// </summary>
public class DicePickerUI : MonoBehaviour
{
    [Header("Modal")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;   // Arka plana týklanýnca kapat

    [Header("Zar Slot Prefab ve Container")]
    [SerializeField] private Transform diceSlotsContainer;
    [SerializeField] private GameObject diceSlotPrefab;  // Her zar için prefab

    // Callback: seçim yapýlýnca çaðrýlýr
    public Action<int> OnDiceSelected;

    private int _currentSelected = 0;
    private DiceSlotUI[] _slots;

    // -------------------------------------------------------
    // Zar Verisi
    // -------------------------------------------------------

    [System.Serializable]
    public class DiceSkinData
    {
        public string label;
        public Color diceColor;
        public Color dotColor;
        public bool isUnlocked;
        public int coinPrice;        // Kilitliyse fiyat
    }

    [Header("Zar Verileri")]
    public DiceSkinData[] diceSkins = new DiceSkinData[]
    {
        new DiceSkinData { label = "PEMBE",  diceColor = new Color(1f,    0.24f, 0.67f), dotColor = Color.white, isUnlocked = true,  coinPrice = 0    },
        new DiceSkinData { label = "MAVÝ",   diceColor = new Color(0.17f, 0.53f, 0.77f), dotColor = Color.white, isUnlocked = true,  coinPrice = 0    },
        new DiceSkinData { label = "ALTIN",  diceColor = new Color(1f,    0.84f, 0f),    dotColor = new Color(0.2f,0.2f,0.2f), isUnlocked = false, coinPrice = 500  },
        new DiceSkinData { label = "SÝYAH",  diceColor = new Color(0.1f,  0.1f,  0.1f), dotColor = Color.white, isUnlocked = false, coinPrice = 750  },
        new DiceSkinData { label = "YEÞÝL",  diceColor = new Color(0.18f, 0.80f, 0.44f), dotColor = Color.white, isUnlocked = false, coinPrice = 300  },
        new DiceSkinData { label = "KIRMIZI",diceColor = new Color(0.9f,  0.2f,  0.2f), dotColor = Color.white, isUnlocked = false, coinPrice = 400  },
    };

    // -------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------

    private void Awake()
    {
        closeButton?.onClick.AddListener(Hide);
        backdropButton?.onClick.AddListener(Hide);

        // Slotlarý oluþtur
        BuildSlots();

        // Baþlangýçta gizle
        if (modalPanel) modalPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // Modal Açma/Kapama
    // -------------------------------------------------------

    public void Show(int currentIndex)
    {
        _currentSelected = currentIndex;
        RefreshSlots();
        if (modalPanel) modalPanel.SetActive(true);
    }

    public void Hide()
    {
        if (modalPanel) modalPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // Slot Yönetimi
    // -------------------------------------------------------

    private void BuildSlots()
    {
        if (diceSlotsContainer == null || diceSlotPrefab == null) return;

        _slots = new DiceSlotUI[diceSkins.Length];

        for (int i = 0; i < diceSkins.Length; i++)
        {
            int idx = i; // closure için
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
            // Kilitli: satýn alma akýþýný tetikle (ileride eklenecek)
            Debug.Log($"[DicePicker] Kilitli zar týklandý: {diceSkins[index].label}, Fiyat: {diceSkins[index].coinPrice}");
            return;
        }

        _currentSelected = index;
        RefreshSlots();
        OnDiceSelected?.Invoke(index);
        Hide();
    }

    public DiceSkinData GetSkin(int index) => diceSkins[index];
}