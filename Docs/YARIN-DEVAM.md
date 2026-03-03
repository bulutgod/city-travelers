# Yarın macOS’tan Devam – Monopoly Clone

Bu dosya yarın projeye devam etmek için kısa bağlam ve BuyPanel yapısını özetler.

---

## Proje / Sahne

- **Unity** projesi: Monopoly clone.
- **GameHudUI** (`Assets/Scripts/GameHudUI.cs`): Ana HUD ve satın alma / ev dikme / kira panelleri burada çözülüyor.
- **Override Canvas**: Inspector’da atanırsa hazır UI kullanılır; atanmazsa kod UI üretir.

---

## BuyPanel Hiyerarşisi (Kodla Eşleşmeli)

### BuyPanel altında 3 ana child

| Child adı (tam) | İçerik |
|-----------------|--------|
| **BuyContent** | Mülk satın al ekranı: 1 Text (soru) + 2 Button (1. GEÇ, 2. SATIN AL). |
| **RentOrBuyContent** | Kira / sahibinden al: 1 Text (Info) + 2 Button (1. GEÇ, 2. SATIN AL). |
| **BuildContent** | Ev dik ekranı: Title, HouseRow (5 slot), BottomRow (Price + 2 Button). |

Buton sırası önemli: Kod `GetComponentsInChildren<Button>` sırasına göre 1. ve 2. butonu kullanır.

---

## BuildContent Detayı

```
BuildContent
├── Title (Text)
├── HouseRow (HorizontalLayoutGroup; 5 child = 5 slot)
│   ├── HouseSlot_1
│   │   ├── Box (Image, isteğe bağlı; içinde RestrictLabel olabilir)
│   │   └── [Toggle burada: ya slot üzerinde ya slot’un child’ı]
│   ├── HouseSlot_2 … HouseSlot_5 (aynı yapı)
│   └── HouseSlot_5
└── BottomRow
    ├── Price (Text)
    ├── 1. Button (EV DİK)
    └── 2. Button (GEÇ)
```

### Toggle nerede olabilir?

- **Seçenek A:** Toggle component’i **HouseSlot objesinin kendisinde** (HouseSlot_1 … HouseSlot_5’e direkt Toggle ekle). Kod bunu destekliyor.
- **Seçenek B:** Her slot’un **içinde ayrı bir child** (örn. "Toggle") aç, Toggle component’ini o child’a ekle.

Her slot için **tek** Toggle olmalı; toplam **5 Toggle** (HouseRow’da değil, her slot’ta).

### BottomRow

- **Price**: Obje adı tam olarak `Price` olmalı (veya en azından BottomRow içindeki ilk Text fiyat olarak kullanılıyor).
- Buton sırası: 1. = EV DİK, 2. = GEÇ.

---

## Kod Tarafında Yapılanlar (GameHudUI.cs)

- BuildContent / HouseRow / BottomRow **inactive** olsa bile bulunuyor: `GetComponentsInChildren(..., true)` ve `FindRecursive` kullanılıyor.
- Build panel açılınca **HouseRow** da açılıyor (`_buildHouseRow.SetActive(showBuild)`).
- **Price** bulunamazsa BottomRow içindeki ilk Text kullanılıyor.
- Toggle **HouseSlot üzerindeyse** doğru slot root’u dönüyor: `GetSlotRootFromToggle` içinde `parent.name == "HouseRow"` ise Toggle’ın `gameObject`’i (yani slot) kullanılıyor.
- Toggle yoksa slot’a runtime’da Toggle ekleniyor (`EnsureToggleForSlot`).
- Override kullanıldığında **OnHouseToggleChanged** ve **OnDeclineClicked** (BuildContent’teki GEÇ) bağlanıyor; fiyat güncelleniyor.

---

## Kontrol Listesi (Unity’de)

- [ ] BuyPanel → BuyContent (1 Text, 2 Button: GEÇ, SATIN AL).
- [ ] BuyPanel → RentOrBuyContent (1 Text Info, 2 Button: GEÇ, SATIN AL).
- [ ] BuyPanel → BuildContent → Title, HouseRow, BottomRow.
- [ ] HouseRow → tam 5 child (HouseSlot_1 … HouseSlot_5).
- [ ] Her HouseSlot’ta **bir** Toggle (ya slot objesinde ya slot’un child’ında).
- [ ] BottomRow → Price (Text), 1. Button (EV DİK), 2. Button (GEÇ).

---

## İlgili Dosyalar

- **GameHudUI.cs** – HUD, BuyPanel, BuildContent, RentOrBuyContent çözümlemesi.
- **Docs/BuyPanel-Children-Setup.md** – BuyPanel child’ları için detaylı kurulum notları.

Yarın macOS’ta bu .md’ye bakarak kaldığın yerden devam edebilirsin.
