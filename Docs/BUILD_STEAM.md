# Build'de Steam Bağlantısı

## Sorun
Editörde oyun Steam'e bağlanıyor ama build alıp arkadaşa attığında onun bilgisayarında Steam'e bağlanmıyor.

## Çözüm (projede yapıldı)
1. **steam_appid.txt**: Steam API, çalıştırılabilir (.exe) ile **aynı klasörde** `steam_appid.txt` arar. Build'de bu dosya olmazsa Steam init başarısız olur.
2. **SteamAppIdBoot.cs**: Oyun açılır açılmaz (Steam init'ten önce) `StreamingAssets/steam_appid.txt` içeriğini okuyup exe klasörüne yazar. Böylece build'i arkadaşına attığında dosya otomatik oluşur.
3. **StreamingAssets/steam_appid.txt**: İçinde `480` (Spacewar – test App ID) var. Kendi Steam App ID'nizi aldığınızda bu dosyayı güncelleyin ve GameNetworkManager objesindeki FizzyFacepunch bileşeninde **Steam App ID** alanını da aynı değer yapın.

## Arkadaşının yapması gerekenler
- **Steam istemcisi açık ve giriş yapılmış olmalı.** Oyunu çalıştırmadan önce Steam'i açıp hesaba giriş yapsın.
- Build'i **zip/klasör olarak** attıysanız, oyunu klasörden çıkarıp **içindeki .exe'yi** çalıştırsın (steam_appid.txt ilk çalıştırmada exe'nin yanına yazılır).

## Kendi App ID'niz
Steamworks partner hesabından kendi oyununuzun App ID'sini alın. Test için 480 (Spacewar) kullanılıyor; yayın için kendi App ID'nizi hem `StreamingAssets/steam_appid.txt` hem de FizzyFacepunch Inspector'da girin.
