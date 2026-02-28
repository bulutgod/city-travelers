# Agent TODO – Sonraki Adımlar (macOS geçişi için)

Bu dosya, projeye yeni geçen agent'ın (özellikle macOS ortamında) hızlıca devam edebilmesi için hazırlanmıştır.

---

## Proje Özeti

- **Oyun:** Monopoly tarzı multiplayer (4 oyunculu)
- **Teknoloji:** Unity + Mirror + FizzyFacepunch (Steam P2P)
- **Sahneler:** `SampleScene` (lobi), `GameScene` (oyun)

---

## Tamamlanan Özellikler

- [x] Steam lobi, host/client, 4 oyunculu
- [x] Zar atma, tahta hareketi (38 kare)
- [x] Para, mülk satın alma, kira
- [x] Özel kareler: Start (geçişte +200), Tax, Chance, Community, Jail, FreeParking
- [x] İflas (para ≤ 0)
- [x] Bildirimler (kira, vergi, Start, vb.)
- [x] Reconnect + Host migration
- [x] **Bot:** Disconnect olunca bot spawn, yerine oynar; reconnect’te bot state’i geri yüklenir

---

## Sonraki Adımlar (Öncelik Sırasıyla)

### 1. Ev Sistemi ✅ (Tamamlandı)
- [x] Her mülkte 0–4 ev
- [x] 4. ev için Start’tan en az 1 kez geçmiş olma şartı
- [x] Ev sayısına göre kira artışı (1 ev x2, 2 ev x4, 3 ev x8, 4 ev x16) + UI

### 2. Oyun Sonu ✅ (Tamamlandı)
- [x] Tek oyuncu kalınca "X kazandı!" ekranı
- [x] İflas edenler hariç 1 kişi kaldığında oyun bitişi (bot da oyuncu sayılır)

### 3. UI İyileştirmeleri
- Oyuncu kartlarında para gösterimi
- Mülk listesi (hangi mülkler kimde)
- Daha belirgin bildirimler / animasyonlar

### 4. GoToJail
- Bu kareye gelince hapishaneye git
- Hapishaneden çıkma kuralları (basit: 1 tur bekleme)

### 5. FreeParking Toplama
- Vergi/ceza paralarının FreeParking’de birikmesi
- Oraya inen oyuncunun bu parayı alması

### 6. Para / Denge (Map tasarımı geldiğinde)
- Başlangıç: 2–3 milyon TL
- Matematiksel fiyat/kira formülü
- *Şimdilik atlandı – map gelişigüzel dolduruldu*

---

## Önemli Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `GameNetworkManager.cs` | OnServerAddPlayer, OnServerDisconnect, bot spawn, reconnect restore |
| `GameTurnManager.cs` | Tur, zar, özel kareler, bot sırası, ServerRollAndMove |
| `PropertyManager.cs` | Mülk sahipliği, satın alma, kira |
| `PlayerObject.cs` | money, isBot, currentSpaceIndex, CmdBuyProperty |
| `BoardSpaceData.cs` | SpaceInfo (purchasePrice, rent, taxAmount, startBonus) |
| `GameHudUI.cs` | HUD, para, satın alma paneli, bildirim toast |
| `SteamLobbyManager.cs` | Lobi, reconnect, host migration |
| `DisconnectPanelUI.cs` | Bağlanıyor overlay, Yeniden bağlan / Terket |
| `Docs/GAME_MECHANICS_TODO.md` | Gelecek mekanikler (ev, para/denge) |

---

## Teknik Notlar

- **Bot:** `isBot = true` olan PlayerObject; `OnStartServer` içinde `if (isBot) return;` ile host bilgisi yazılmıyor
- **Reconnect:** Bot varsa restore sırasında bot’un state’i (position, money) kullanılıyor
- **BoardSpaces:** `Assets/ScriptableObject/BoardSpaces.asset` – kare verileri
- **Transport:** FizzyFacepunch (Steam P2P), dedicated server için KCP düşünülebilir

---

## Test Senaryoları

1. **Bot:** 2 oyunculu başlat → Client disconnect → Bot spawn, oynar → Client reconnect → Eski slot + bot state
2. **Reconnect:** Oyuncu disconnect → "Bağlanıyor" → Reconnect → "Bağlandı!" → Devam
3. **Host migration:** Host disconnect → 2 sn bekle → Yeni host seçilir → State restore
