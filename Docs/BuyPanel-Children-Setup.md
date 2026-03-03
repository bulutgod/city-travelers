# BuyPanel Alt Elemanları: BuildContent ve RentOrBuyContent

BuyPanel’in altına **BuildContent** ve **RentOrBuyContent** eklemek için aşağıdaki hiyerarşiyi kullan. İsimler ve sıralar kodla eşleşmeli.

---

## 1. RentOrBuyContent (Kira / Sahibinden al)

**BuyPanel** altında bir child oluştur, adı tam olarak **RentOrBuyContent** olsun.

- **RentOrBuyContent** (GameObject, RectTransform – stretch)
  - **Info** → UI **Text**. Metin runtime’da doldurulur (boş bırakabilirsin).
  - **1. Button** → "GEÇ" (kirayı ödeyip geç).
  - **2. Button** → "SATIN AL" (sahibinden satın al).

Buton sırası önemli: Kod ilk butonu “GEÇ”, ikincisini “SATIN AL” olarak kullanır.

---

## 2. BuildContent (Ev dik)

**BuyPanel** altında bir child oluştur, adı tam olarak **BuildContent** olsun.

- **BuildContent** (GameObject, RectTransform – stretch)
  - **Title** → UI **Text**. Örn: "Ev dik (1=Yer, 2-4=Ev)".
  - **HouseRow** → GameObject. **Horizontal Layout Group** ekle (spacing ~12, child alignment middle).
    - Bu objenin **5 tane** child’ı olacak; her biri bir “slot” (1=yer, 2–4=ev).
    - Her slot için:
      - Bir **slot** GameObject’i (adı örn. HouseSlot_1 … HouseSlot_5).
      - Slot’un **doğrudan** child’ı olarak, **Toggle** component’i olan bir GameObject (Toggle’ın parent’ı slot olmalı).
      - Slot’un içinde en az bir **Text** (örn. "1", "2", "3", "4", "5") – kod bunu etiket için kullanır.
      - İstersen her slotta bir **Box** (Image) ve içinde **RestrictLabel** (Text) ekle; kod 4. slot için "1 tur geçmeden alınamaz" mesajını göstermek üzere `Box/RestrictLabel` arar.
  - **BottomRow** → GameObject (alt satır).
    - **Price** → UI **Text** (örn. "0 TL"). Kod yolu: `BottomRow/Price`.
    - **1. Button** → "EV DİK".
    - **2. Button** → "GEÇ".

Özet hiyerarşi:

```
BuildContent
├── Title (Text)
├── HouseRow (HorizontalLayoutGroup)
│   ├── HouseSlot_1
│   │   ├── Box (Image) + Num (Text)  ← isteğe bağlı
│   │   └── Toggle (Toggle component burada)
│   ├── HouseSlot_2
│   │   └── Toggle
│   ├── ... (3, 4, 5 aynı)
│   └── HouseSlot_5
│       └── Toggle
└── BottomRow
    ├── Price (Text)
    ├── EV DİK (Button)
    └── GEÇ (Button)
```

**Toggle nerede olur:** Toggle component’i ya **HouseSlot objesinin kendisinde** olabilir (direkt slot’a eklenir) ya da slot’un **direkt child’ı** bir GameObject’te (örn. "Toggle" adlı child). İkisi de desteklenir.

---

## Kontrol listesi

- [ ] BuyPanel altında **BuyContent** (1 Text + 2 Button: GEÇ, SATIN AL).
- [ ] BuyPanel altında **RentOrBuyContent** (1 Text “Info” + 2 Button: GEÇ, SATIN AL).
- [ ] BuyPanel altında **BuildContent** (Title, HouseRow içinde 5 slot + her slotta Toggle ve en az 1 Text, BottomRow içinde Price + 2 Button).

Bu yapıyı kurduktan sonra satın alma, kira/sahibinden al ve ev dikme panelleri kodla uyumlu çalışır.
