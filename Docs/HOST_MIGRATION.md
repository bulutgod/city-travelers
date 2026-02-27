# Host Migration (Host Düşünce Oyun Devam Eder)

Host bağlantısı koptuğunda oyun çökmüyor; bir süre sonra yeni host seçilip oyun kaldığı yerden devam ediyor.

## Kurulum

1. **HostMigrationManager**: Lobi/ana menü sahnesinde (ör. SampleScene) bir GameObject'e ekleyin (örn. SteamLobbyManager'ın olduğu objeye). DontDestroyOnLoad ile kalır, tüm sahnelerde geçerli olur.
2. Build Settings'te GameScene ekli olmalı (zaten varsa dokunmayın).
3. **Kopma paneli (Yeniden bağlan / Terket)**: **SampleScene**'de bir Canvas altına panel + mesaj metni + "Yeniden bağlan" ve "Terket" butonları ekleyin, `DisconnectPanelUI` script'ini panele (veya bir objeye) verin; Panel, Message Text, Reconnect Button, Leave Button referanslarını atayın. Script paneli DontDestroyOnLoad ile kalıcı yapar; hem GameScene'de bağlantı koptuğunda hem de SampleScene'de lobideyken oyuna bağlı değilken gösterilir. Alt+F4 atıp tekrar açınca ana menüde (SampleScene) başladığın için bu butonlar SampleScene'dedir.
4. **Oyundan çıkış (isteğe bağlı)**: Oyun içinde "Terket" / "Oyundan Çık" butonu için `GameNetworkManager.Instance.RequestVoluntaryLeaveAndReturnToMenu()` çağrılabilir; böylece sunucu 3 dk state tutmaz.

## Akış

- Host düşer → Client'lar bağlantı kopar, **crash olmaz**.
- ~5 saniye beklenir (host anlık geri gelebilir diye).
- Lobide kalan oyunculardan **en düşük SteamId**'li kişi yeni host seçilir.
- Yeni host: `StartHost()` + oyun state'i (tur, pozisyonlar vb.) restore edilir, lobi metadata'sında `HostSteamId` güncellenir.
- Diğer client'lar: Steam `OnLobbyDataChanged` ile yeni `HostSteamId`'i görüp otomatik yeni host'a bağlanır.
- Reconnect eden oyuncular (host dahil) mevcut slot/state restore ile aynı yerden devam eder.

## Ayarlar

- **HostMigrationManager.migrationWaitSeconds**: Host koptuktan sonra kaç saniye beklenecek (varsayılan 5).
- **HostMigrationManager.messageHostDisconnected**: Migration sırasında gösterilecek mesaj (opsiyonel UI için).
- **Opsiyonel UI**: GameScene'de migration beklerken mesaj göstermek için `HostMigrationManager.Instance.IsWaitingForMigration` ve `HostMigrationManager.Instance.MessageHostDisconnected` kullanılabilir.

## Not

- Migration sadece **oyun sahnesindeyken** (GameScene) devreye girer. Lobi sahnesindeyken host düşerse eskisi gibi menüye dönülür.
- State snapshot client tarafında SyncVar'lardan alınır; yeni host sunucuyu bu state ile açar.
- **DontDestroyOnLoad**: SteamLobbyManager, HostMigrationManager ve GameNetworkManager sahnede **root** objede olmalı (hiçbir parent altında olmamalı). Kodda Awake'te `SetParent(null)` ile root yapılıyor; yine de Hierarchy'de bu objeleri başka bir objenin child'ı yapmayın.
- **Oyunu kapatıp açan oyuncu**: Alt+F4 veya oyunu kapatıp tekrar açan oyuncu Steam lobisinden çıkar. Tekrar bağlanması için host'un onu **yeniden davet etmesi** veya oyuncunun lobi ID ile tekrar katılması gerekir. **GameScene'deyken davet edersen**, sadece **lobi başlangıcında oyunda olan** ve **3 dk içinde yeniden bağlanan** oyuncu kaldığı yerden devam eder; lobi başlangıcında olmayan veya 3 dk sonra davet edilen **oyuna alınmaz** (giriş reddedilir). "Yeniden bağlan" butonu ile davet etmeden mevcut host'a tekrar bağlanabilir (3 dk içinde). "Terket" ile çıkarsa sunucu state'i tutmaz (3 dk beklemez).
