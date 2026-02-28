# Oyun Mekanikleri - Gelecek Güncellemeler

## Para ve Denge (Map tasarımı geldiğinde)

- **Başlangıç parası:** 1500 TL yerine 2–3 milyon TL
- **Fiyat/kira:** Matematiksel formül ile adil dağılım
  - Örnek: `purchasePrice = basePrice * (1 + index * 0.05)`
  - `rent = purchasePrice * 0.1` veya benzeri oran
  - *Şimdilik atlandı – map gelişigüzel dolduruldu, tasarım gelince yapılacak*

## Ev Sistemi ✅ (Uygulandı)

- **4 ev:** Her mülkte 0–4 ev dikilebilir (ucuzdan pahalıya)
- **4. ev kuralı:** 4. evi dikmek için Start’tan en az 1 kez geçmiş olmak gerekir
- **Kira artışı:** Ev sayısına göre kira katlanır (örn. 1 ev x2, 2 ev x4, 3 ev x8, 4 ev x16)

## Bot / Disconnect

- **Uygulandı:** Oyuncu disconnect olunca bot spawn edilir, yerine oynar (zar atar, satın alır/geçer)
- Bot: %70 satın alır (parası varsa), 1.5 sn gecikmeyle hareket eder
  - Sunucuda “ghost” PlayerObject veya ayrı GameState yapısı gerekir
