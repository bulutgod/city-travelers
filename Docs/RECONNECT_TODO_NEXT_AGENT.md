## Reconnect & Host Migration – Dev Notları (Handoff)

Bu dosya, mevcut host migration + reconnect sisteminin **nerede çalıştığını** ve **nerede bozulduğunu** anlatmak için yazıldı. Diğer ajan / geliştirici buradan devam edebilir.

---

## Genel Mimari (Kısa Özet)

- **Transport**: Mirror + FizzyFacepunch (Facepunch.Steamworks).
- **Lobby**: `SteamLobbyManager` (Steam lobby, `HostSteamId` metadata ile Mirror host’u işaretliyoruz).
- **Network**: `GameNetworkManager : NetworkManager`
- **Oyun turu / state**: `GameTurnManager`, `PlayerObject`, `GameStateSnapshot`.
- **Host migration**:
  - `HostMigrationManager` client tarafında:
    - `GameNetworkManager.OnClientDisconnect()` oyun sahnesindeyken (`GameScene`) çağırıyor.
    - ~`migrationWaitSeconds` (şu an Inspector’dan ayarlanıyor, varsayılan 5) bekliyor.
    - Lobideki üyelerden **en düşük SteamId**’li kişiyi yeni host seçiyor.
    - Yeni host → `SteamLobbyManager.BecomeNewHostAndStartServer(snapshot)`.
    - Diğerleri → `SteamLobbyManager.WaitForNewHostAndReconnect()` + `OnLobbyDataChanged` ile yeni `HostSteamId`’e bağlanıyor.

- **Reconnect / Leave UI**:
  - `DisconnectPanelUI` SampleScene’de bir panel + “Reconnect / Leave” butonları.
  - Panel **iki durumda** gösteriliyor:
    - GameScene’de host migration beklerken (HostMigrationManager.IsWaitingForMigration).
    - SampleScene’de, network bağlı değilken ve `SteamLobbyManager.HasLastLobbyToReconnect()` true iken.

- **Son lobi bilgisi (Reconnect için)**:
  - `SteamLobbyManager` PlayerPrefs’te şunları tutuyor:
    - `LastLobbyId`
    - `LastHostSteamId`
  - `SaveLastLobbyPrefs()` → `OnLobbyCreated`, `OnLobbyEntered`, `BecomeNewHostAndStartServer` ve `OnLobbyDataChanged` içinde çağrılıyor.

- **Explicit leave (bu oyundan tamamen çık)**:
  - Oyun içinde: `GameNetworkManager.RequestVoluntaryLeaveAndReturnToMenu()` → local `PlayerObject.CmdVoluntaryLeave()` → server tarafında `_voluntaryLeaveSteamIds` set’i, `_disconnectedStates`’ten silme, 3 dk beklemeden state drop.
  - Oyun kapandıktan sonra (SampleScene’de “Leave”): `SteamLobbyManager.LeaveLastGamePermanently()` →
    - Kaydedilmiş `LastLobbyId`’ye `JoinLobby()`.
    - Lobby’ye ve host’a bağlanınca `LeaveAfterReconnectFlow()` coroutine’i içinde `RequestVoluntaryLeaveAndReturnToMenu()` çağrılıyor.
    - En sonunda `ClearLastLobbyPrefs()` çağrılıyor → bu oyuna dair reconnect kaydı siliniyor.

---

## Şu An Çalışan Davranışlar

- **Steam başlangıcı / build**:
  - `SteamAppIdBoot` + `Assets/StreamingAssets/steam_appid.txt` ile steam_appid otomatik yazılıyor.
  - Windows build’te Facepunch Win64 DLL’i doğru platformlara atanmış durumda; arkadaşın makinesinde Steam bağlantısı çalışıyor.

- **Host migration (oyun sahnesindeyken)**:
  - Host oyunu kapatınca diğer client’lar crash olmadan kalıyor.
  - ~`migrationWaitSeconds` sonra lobi üyeleri arasından **en düşük SteamId** yeni host seçiliyor.
  - Yeni host `BecomeNewHostAndStartServer(snapshot)` ile GameScene’i host’layıp state’i restore ediyor.
  - Diğer client’lar yeni host’a otomatik bağlanıyor (Steam lobby metadata `HostSteamId` değişimini takip ediyorlar).

- **Reconnect paneli**:
  - SampleScene’de, network bağlı değilken ve `LastLobbyId` kayıtlıysa **Reconnect / Leave** paneli görünüyor.
  - GameScene’de host migration beklerken de (disconnect sırasında) mesaj + Reconnect/Leave gösteriliyor.

- **Lobiye sonradan katılma / reconnect filtreleri**:
  - `GameNetworkManager.OnServerChangeScene(GameScene)`:
    - O an bağlı tüm oyuncuların SteamId’leri `_allowedPlayerSteamIds` içine yazılıyor → “bu oyunun asıl kadrosu”.
  - `OnServerAddPlayer`:
    - Eğer `isGameScene` ve `_allowedPlayerSteamIds.Count > 0` ise:
      - `wasInitial = _allowedPlayerSteamIds.Contains(steamId)`
      - `hasDisconnectedState = _disconnectedStates.TryGetValue(steamId, out restored)`
      - Eğer `!wasInitial && !hasDisconnectedState` → oyuncu bu oyunun parçası değil → bağlantı **reddediliyor**.
      - Diğer durumlarda (başlangıç kadrosu veya 3 dk içinde reconnect eden) kabul ediliyor, state restore ediliyor.

---

## Hâlâ Bozuk / Eksik Olanlar

### 1. Host reconnect sonrası GameScene’e geçmemesi

**Gözlenen davranış:**
- Host, GameScene’deyken oyunu kapatıyor.
- Diğer oyuncular migration ile devam ediyor (yeni host atanıyor).
- Host oyunu tekrar açıp SampleScene’de **Reconnect** butonuna basıyor:
  - Steam lobby’ye başarıyla yeniden katılıyor.
  - Lobby UI’da oyuncu listesinde görünüyor (yani Steam tarafı OK).
  - Mirror client spawn oluyor, `PlayerObject` geliyor ancak:
    - Host **GameScene’e geçmiyor**, hala SampleScene’de lobby paneli açık kalıyor.
    - Zar atamıyor, sıra ona gelince oyun kilitleniyor (çünkü o GameScene’de yok).
  - Console’da şu hata görüldü (en az bir testte):
    - `Spawn scene object not found for 9D77966E32B97E2F. Make sure that client and server use exactly the same project. This only happens if the hierarchy gets out of sync.`

**Önemli notlar:**
- Daha sonra tüm sahneler `Ctrl+S` ile kaydedilip arkadaşlara **aynı güncel build** atıldı; yine de
  - Reconnect sonrası host SampleScene’de kaldı,
  - `PlayerObject` geldi ama GameScene’e geçmedi,
  - Turn sistemi host’u hiç görmedi / zar atılamadı.

**Muhtemel ana sebep (inceleme önerisi):**
- `SteamLobbyManager.OnLobbyEntered(Lobby lobby)` mantığı:
  - `_weJustCreatedLobby` → yeni host olarak StartHost.
  - `if (NetworkServer.active)` → zaten host’sak sadece LobbyUI aç.
  - `if (lobby.IsOwnedBy(SteamClient.SteamId))` → yine sadece LobbyUI aç; **client StartClient çağrısı yok**.
  - **else** → `HostSteamId` metadata’sını okuyup `GameNetworkManager.StartClient()` çağrısı.
- Host migration sonrasında:
  - Steam lobby **sahibi** hala **ilk host** (Steam’in gerçek lobby owner’ı değişmiyor).
  - Ama bizim meta `HostSteamId` alanımız **yeni host’a** geçiyor.
- İlk host oyunu kapatıp açtıktan sonra:
  - Lobby’ye tekrar girdiğinde `lobby.IsOwnedBy(SteamClient.SteamId)` yine **true**.
  - Kod “ben lobby owner’ıyım, host’um” varsayımıyla sadece `LobbyUINew.NotifyLobbyJoined()` çağırıyor, **hiç StartClient/StartHost çağırmıyor.**
  - Bu yüzden:
    - Ya hiç network client açılmıyor (veya yanlış modda),
    - Ya da sahne sync’i eksik kalıyor → GameScene’e geçiş olmuyor, host SampleScene’de takılıyor.

**Sonraki ajan için önerilen düzeltme yönü:**
- `OnLobbyEntered` içindeki mantığı host migration’a uygun hale getir:
  - Eğer **Mirror tarafında şu anda host değilsek** (`!NetworkServer.active`) ve
    - `HostSteamId` metadata’sı **biz değilsek**:
      - Her durumda **StartClient** çalışmalı (owner olsak bile).
    - `HostSteamId` metadata’sı **biz isek**:
      - StartHost + GameScene’e geç.
- Kısaca: “Steam lobby owner == biziz” demek artık “Mirror host biziz” anlamına gelmiyor; sadece metadata’daki `HostSteamId` karar vermeli.

Bu bug şu anda **Reconnect sonrası GameScene’e geçememe + zar atamama** davranışına sebep oluyor.

### 2. Host atama süresi kullanıcıya uzun geliyor

- `HostMigrationManager.migrationWaitSeconds` varsayılan 5 saniye.
- Kullanıcı, host düştükten sonra 5 sn beklemenin uzun geldiğini söylüyor.

**Basit çözüm:**
- Inspector’da `HostMigrationManager.migrationWaitSeconds` değerini **2** yap (veya koda default 2 yaz).
- Ekstra mantık gerekmez; bu tamamen UX ayarı.

### 3. Editor hataları (düşük öncelik)

- Unity Editor’da şu hatalar görüldü (özellikle GameScene açılırken):
  - `NullReferenceException` / `SerializedObjectNotCreatableException` / `MissingReferenceException`  
    (tamamı `UnityEditor.GameObjectInspector`, `TMP_BaseEditorPanel` vb. içinde).
- DisconnectPanelUI artık `DontDestroyOnLoad` kullanmadığı için bu hatalar azaldı; kalanlar muhtemelen Unity 6000.2.10f1 + TMP editor side bug’ları.
- Build’i etkilemiyor; şu an için **düşük öncelikli**.

---

## Kodda Dokunulan Başlıca Noktalar

### SteamLobbyManager

- **Yeni alanlar:**
  - `PrefLastLobbyId`, `PrefLastHostSteamId` (PlayerPrefs key’leri).
  - `_reconnectToLastLobbyRequested`, `_leaveAfterReconnectRequested`.
- **Yeni API’ler:**
  - `bool HasLastLobbyToReconnect()`
  - `void TryReconnectToLastLobby()`
  - `void LeaveLastGamePermanently()`
  - `void SaveLastLobbyPrefs(ulong lobbyId, ulong hostSteamId)`
  - `void ClearLastLobbyPrefs()`
  - `IEnumerator LeaveAfterReconnectFlow()`
- **Önemli davranışlar:**
  - `LeaveLobby()` artık `ClearLastLobbyPrefs()` **çağırmıyor** → normal leave, reconnect hakkını silmiyor.
  - Sadece `LeaveAfterReconnectFlow()` sonunda `ClearLastLobbyPrefs()` çağrılıyor (yani explicit “Leave” ile).
  - `OnLobbyEntered`’de lobby ID + `HostSteamId` kaydediliyor.
  - `OnLobbyDataChanged`’de `HostSteamId` PlayerPrefs’e güncelleniyor.

### GameNetworkManager

- `Awake()` override → kendini root objeye alıp `base.Awake()` çağırıyor.
- `OnClientDisconnect()`:
  - GameScene’de + HostMigrationManager varsa migration akışına giriyor, aksi halde SampleScene/OfflineScene’e dönüyor.
- `OnServerAddPlayer()`:
  - `_allowedPlayerSteamIds` ve `_disconnectedStates` bazlı filtre yukarıda anlatıldığı gibi düzenlendi.
- `OnServerDisconnect()`:
  - `_voluntaryLeaveSteamIds` set’i eklenerek **explicit leave** ile gelen disconnect’lerde state tutulmuyor.
- `RequestVoluntaryLeaveAndReturnToMenu()`:
  - Local player’a `CmdVoluntaryLeave()` → kısa delay → `LeaveLobby()` + `StopClient()` + offline/lobi sahnesine dön.

### HostMigrationManager

- Single instance + `DontDestroyOnLoad` root düzeltildi (root’a alıyor).
- `TryReconnectToCurrentHost()` ve `LeaveAndReturnToMenu()` eklendi; DisconnectPanelUI bunları kullanıyor.

### DisconnectPanelUI

- Artık `DontDestroyOnLoad` kullanmıyor; sadece SampleScene’de (ve GameScene’de migration beklerken) gösterilmesi için yazıldı.
- Gösterme koşulları:
  - Migration bekliyor → her sahnede.
  - Veya:
    - `activeScene == lobbySceneName` (varsayılan "SampleScene")
    - `!NetworkClient.active && !NetworkServer.active`
    - `SteamLobbyManager.HasLastLobbyToReconnect() == true`
- Butonlar:
  - **Reconnect**:
    - Migration varsa: `HostMigrationManager.TryReconnectToCurrentHost()`.
    - Yoksa: `SteamLobbyManager.TryReconnectToLastLobby()`.
  - **Leave**:
    - Migration varsa: `HostMigrationManager.LeaveAndReturnToMenu()`.
    - Yoksa: `SteamLobbyManager.LeaveLastGamePermanently()`.

---

## Önerilen Sonraki Adımlar (Diğer Ajan İçin)

1. **OnLobbyEntered mantığını migration-aware hale getir**
   - Lobby owner (Steam) ile bizim `HostSteamId` metadata’sını ayrıştır.
   - Eğer Mirror tarafında host değilsek (`!NetworkServer.active`):
     - `lobby.GetData("HostSteamId") != SteamClient.SteamId.ToString()` ise → StartClient (eski host reconnect senaryosu).
     - Eşitse → StartHost + GameScene (yeni host biz isek).

2. **Host atama süresi ayarı**
   - Kullanıcı 5 saniyeyi uzun buluyor → `HostMigrationManager.migrationWaitSeconds` için daha düşük bir default (örn. 2) belirle ve doc’a not düş.

3. **Reconnect sonrası sahne senkronunu doğrula**
   - İki tarafta da (host ve reconnect eden eski host’ta) **aynı build** kullanıldığından emin ol.
   - Reconnect eden oyuncu için:
     - Lobby → StartClient → GameScene auto-load → PlayerObject spawn → turn sistemi.
   - Hâlâ `Spawn scene object not found` hatası alınıyorsa:
     - İlgili scene object’in (ID’si 9D77...) hangi prefab / obje olduğunu bulup GameScene’deki NetworkIdentity listesini karşılaştır.

4. **Gerekirse HOST_MIGRATION.md ile bu dosyayı birleştir**
   - Kullanıcı host migration dokümantasyonuna alışkın; final davranış netleşince tek bir doküman altında toparlamak faydalı olur.

