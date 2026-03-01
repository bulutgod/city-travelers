using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Zar picker modalındaki tek bir zar slotunu temsil eder.
/// Kilitli zarlar coin fiyatı gösterir.
/// </summary>
public class DiceSlotUI : MonoBehaviour
{
    [Header("UI Referansları")]
    [SerializeField] private Image dicePreviewImage;    // Zar sprite görseli
    [SerializeField] private TextMeshProUGUI labelText; // "ALTIN", "MAVİ" vs.
    [SerializeField] private TextMeshProUGUI priceText; // "500 🪙" - kilitliyse görünür
    [SerializeField] private Image lockIcon;            // 🔒 ikonu
    [SerializeField] private Image selectedOutline;     // Seçili olunca parlayan kenar
    [SerializeField] private Image dimOverlay;          // Kilitliyse karartma
    [SerializeField] private Button button;

    private DicePickerUI.DiceSkinData _data;
    private Action _onClicked;

    // -------------------------------------------------------
    // Setup
    // -------------------------------------------------------

    public void Setup(DicePickerUI.DiceSkinData data, Action onClicked)
    {
        _data = data;
        _onClicked = onClicked;

        if (dicePreviewImage)
        {
            dicePreviewImage.sprite = data.diceSprite;
            dicePreviewImage.color = Color.white;
            dicePreviewImage.preserveAspect = true;
        }

        // Label
        if (labelText) labelText.text = data.label;

        // Kilitli mi?
        bool locked = !data.isUnlocked;

        if (lockIcon) lockIcon.gameObject.SetActive(locked);
        if (dimOverlay) dimOverlay.gameObject.SetActive(locked);
        if (priceText)
        {
            priceText.gameObject.SetActive(locked);
            if (locked) priceText.text = $"{data.coinPrice} 🪙";
        }

        // Buton
        button?.onClick.RemoveAllListeners();
        button?.onClick.AddListener(() => _onClicked?.Invoke());

        // Seçili değil başlangıçta
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedOutline)
        {
            selectedOutline.gameObject.SetActive(selected);
            // Hafif büyüme efekti için scale
            transform.localScale = selected
                ? Vector3.one * 1.08f
                : Vector3.one;
        }
    }
}