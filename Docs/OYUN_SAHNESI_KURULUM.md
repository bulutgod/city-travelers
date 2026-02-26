# Oyun sahnesi (GameScene) kurulumu

"Oyunu Baslat" butonu lobiden **GameScene** sahnesine gecis yapar. Bu sahnenin var olmasi ve Build Settings'te ekli olmasi gerekir.

## Adimlar

1. **Unity'de yeni sahne:** File → New Scene → Basic (Built-in) veya Empty.
2. **Kaydet:** File → Save As → `Assets/Scenes/GameScene.unity`
3. **Build Settings:** File → Build Settings → "Add Open Scenes" ile GameScene'i listeye ekle (veya sahneyi surukleyip birak).
4. **Opsiyonel:** GameNetworkManager objesinde Inspector'dan "Game Scene Name" alanini kontrol et; varsayilan `GameScene` dir. Sahneyi farkli isimle kaydettiysen buraya yaz.

Bu islemlerden sonra lobide host "Oyunu Baslat"a basinca tum oyuncular GameScene sahnesine gecer.
