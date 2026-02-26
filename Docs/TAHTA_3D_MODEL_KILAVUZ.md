# 3D tahta modeli – sürükle bırak kilavuzu

Tahta modeli hazir oldugunda asagidaki yapida olursa GameScene'de **tek referans** ile degistirmek yeterli.

## Gereksinim

- **Tek root** GameObject (orn. "Board_Model").
- Bu root'un **tam 38 child** GameObject'i olmali.
- Child'lar **tahta sirasina gore** dizilmeli:
  - 0: baslangic (genelde sol ust)
  - 1..9: ust kenar
  - 10..18: sag kenar
  - 19..28: alt kenar
  - 29..37: sol kenar (baslangica donus)

Her child'in **transform pozisyonu** piyonun o karede duracagi noktadir. Scale/rotation serbest.

## Unity'de yapilacaklar

1. Modeli sahneye surukle (prefab veya model).
2. 38 kareyi tek bir parent altinda topla; parent'in adi onemli degil.
3. GameScene'de **BoardManager** bilesenini bul.
4. **Board Root** alanina bu 38 child'li parent'i surukle birak.
5. (Opsiyonel) Board_Placeholder objesini sil veya devre disi birak.

Boylece kod ayni kalir; sadece tahta gorseli degismis olur. Flip (tersine donme) sonra ayni BoardManager uzerinden eklenecek.
