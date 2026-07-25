# Baby Caretta — Düşman & Engel Tasarım Raporu

*Bu rapor, oyunun düşman/engel sistemine dair yapılan tasarım tartışmalarının özetidir. Amaç, ileride bu sisteme dönüldüğünde "neye, neden karar verdik" sorusuna hızlıca cevap bulabilmek.*

---

## 1. Temel Tanım: Düşman vs. Engel

İki kategori net olarak ayrıldı:

| Kategori | Kapsam | Sistem etkisi |
|---|---|---|
| **Aktif Yırtıcı (Predator)** | Martı, küçük kuşlar, küçük yengeçler, büyük yengeç, (ileride) kayalık yaratığı | Yeni state'ler gerektirir |
| **Pasif Engel (Obstacle)** | İnsan atıkları | Mevcut CarryableItem / interaction sistemine oturur, **yeni state gerektirmez** |

**Karar:** İnsan atıkları V1 kapsamında sadece taşınabilir/itilebilir puzzle objeleri olarak kalacak. Yeni mekanik veya state eklenmeyecek — bu iş, oyun tamamlandıktan sonra (post-launch) değerlendirilebilir.

---

## 2. Senaryo Taslağı (Predator Davranışları)

1. **Martı (büyük kuş)** — Belirli bir % ihtimalle ayağı veya gagasıyla kaplumbağayı yakalayıp checkpoint'e döndürür. Ayaktan yakalanırsa **ısırarak kurtulma** şansı var.
2. **Küçük kuşlar** — Vuruş yaparak kaplumbağayı ters çevirmeye çalışır. Kalkamazsa 2. saldırıda yakalanır, yine ısırma ile kurtulma şansı var.
3. **Küçük yengeçler** — Bacağı yakalayıp yavaşlatır, birden fazla yengeç yakalarsa yavaşlama katlanır. 3 yengeç yakalar ve 3–5 saniyede kurtulunamazsa tam durma + checkpoint.
4. **Büyük yengeç** — Tek başına yakalar/yavaşlatır, birkaç saniyede kurtulunamazsa yem olunur, checkpoint. (Gizli bir delikten çıkan bir tür olabilir.)
5. **Kayalık yaratığı (gelecek fikir)** — Yengeç ile birebir aynı kod/mantık, sadece farklı model/prefab.

---

## 3. Mimari Kararı: Tek Sistem, Çoklu Görünüm

Ayrı ayrı script yazmak yerine:

- **`PredatorController`** — tüm predator tiplerinde ortak davranışı yöneten tek script
- **`PredatorData` (ScriptableObject)** — her tip için farklı veri: yakalama ihtimali, escape yöntemi, escape süresi, stackable olup olmadığı, yakalanma sonucu, görsel prefab

**Sonuç:** Kayalık yaratığı gibi "aynı mantık, farklı görünüm" istekleri yeni kod değil, yeni bir `PredatorData` asset'i + yeni prefab ile çözülür.

---

## 4. Kaplumbağa Tarafı — Gereken State'ler

| State | Ne zaman | Amaç |
|---|---|---|
| **Grabbed** | Martı yakaladığında | Kontrol kilitli, ısırma-escape aktif |
| **Restrained** | Yengeç(ler) bacağı tuttuğunda | Hareket kısıtlı, stackable sayaç (kaç yengeç tutuyor) |
| **Flipped** | Küçük kuş vuruşu sonrası | Kalkış penceresi; başarısızsa Grabbed/Restrained'e geçiş |
| **EscapeStruggle** | Yukarıdaki üçünün ortak alt-katmanı | Input dinleme + escape başarı/başarısızlık mantığı |
| **Captured** | Kurtulamama sonucu | Ekran kararması + checkpoint tetikleyici |

**Not:** IsHidden flag'i (Burrow state'inden) predator'ların "bu hedefi görmezden gel" kontrolüne bağlanabilir — ekstra state gerekmiyor.

---

## 5. Escape Mekanikleri (Cozy Tona Uygun Hale Getirildi)

**Genel prensip:** Mash (hızlı buton çakma) mekaniği **kullanılmayacak** — panik/refleks testi, oyunun Journey/ABZU/A Short Hike ruhuna aykırı. Bunun yerine zamanlama/ritim tabanlı, cömert pencereli input tercih edildi.

### 5.1 Martı — Isırma
- Tek atımlık, **zamanlama penceresine** bağlı (Animation Event ile tetiklenen bir "pencere açık/kapalı" bool'u)
- Pencere, animasyon klibi üzerine konan `OnBiteWindowOpen()` / `OnBiteWindowClose()` event'leriyle tanımlanır
- Pencere süresi cömert tutulacak (frame-perfect değil, ~250–400ms)
- **İlk karşılaşmada** martının ayağı/gagasının yanında küçük, diegetic bir ipucu (ok/parlama) gösterilecek; sonraki karşılaşmalarda gösterilmeyecek

### 5.2 Ters Dönme — Düzelme
- İki aşamalı: 1. klip (toparlanma denemesi) → kendi penceresi → doğru anda zıplarsa 2. klibe (düzelme hamlesi) geçer → onun da kendi penceresi var → geçemezse tekrar düşer
- Saf %30 şans yerine zamanlamaya bağlı başarı tercih edildi (net oran/yöntem implementasyon aşamasında netleşecek)

### 5.3 Yengeç — Titretme (çift basış, aralık kontrollü)
- Mash değil; **iki basış arasındaki zaman aralığı** kontrol edilir (çok hızlı da çok yavaş da başarısız sayılır)
- Örnek eşikler: min ~0.15sn, max ~0.6sn
- Yengecin debelenme animasyon temposuyla görsel olarak eşleştirilecek

### 5.4 Görsel geri bildirim
- Yengeç bir bacağı tuttuğunda o bacağın yürüme animasyonu **durmayacak**, bunun yerine tüm locomotion **"aksak yürüyüş" (limp walk) blend'ine** geçecek — tam IK bazlı tekil bacak dondurma yerine (mevcut CAT rig/IK sorunları nedeniyle daha zahmetli olurdu), hazır bir aksak yürüyüş klibine crossfade edilecek. Bu hem daha kolay hem de "neden yavaşladım" sorusuna anında görsel cevap verir.

---

## 6. Avcı Karşılaşmaları Dışında Oyunu Canlı Tutma

Amaç: sürekli tehditle uğraşmadan da dünyanın "boş gezinme" gibi hissettirmemesi.

- **Reaktif çevre yaşamı** — balık sürüleri dağılması, tehdit olmayan küçük yengeçlerin deliğe kaçması, martıların tedirginliği (bunlar tehdit değil, sadece "dünyanın seni fark etmesi")
- **Hareketin kendisinin keyifli olması** — akıntıya girme, dalıp süzülme gibi saf hareket anları
- **Keşif izleri** — opsiyonel toplanabilir/dokunulabilir objeler (mevcut CarryableItem sistemine oturur)
- **Yemek yeme mekaniği** (zaten mevcut) bu boşluğu kısmen dolduruyor
- **Kısa, tetiklemeli ambiyans anları** (uzaktan geçen yunus sürüsü, gün batımı ışık değişimi) — etkileşim gerektirmeden "bir şey oluyor" hissi

**Scope notu:** Bunların hepsi aynı anda kurulmayacak; vertical slice için 1–2 tanesi seçilip biyom biyom genişletilecek.

---

## 7. Scope Kontrolü — Üretim Sırası Önerisi

1. **Vertical slice:** Sadece **1 predator tipi** uçtan uca tamamlanacak (önerilen: küçük yengeç — en basit, stackable escape mantığını test eder)
2. Mimari (PredatorController + PredatorData) bu ilk tip üzerinden doğrulanacak
3. Diğer tipler, sadece yeni `PredatorData` asset + yeni model olarak eklenecek ("ucuz" genişleme)
4. Uçan/vuran tipler (martı, küçük kuş) daha fazla animasyon/rig işi gerektirdiği için ikinci sıraya alınacak
5. İnsan atıkları hiçbir zaman bu sisteme dahil edilmeyecek

---

## 8. Cezalandırma Tonu

- Klasik "GAME OVER" ekranı yok; yumuşak fade + checkpoint
- Checkpoint mesafesi minimal — amaç "yeniden dene" değil "hikayeye devam et" hissi
- Gerilim anları **seyrek/özel set-piece** olarak kalacak, sık tekrar eden bir core-loop olmayacak (biyom başına 1–2 anlamlı an, kontrast amaçlı)

---

*Bu rapor, ileride enemy sistemine dönüldüğünde referans noktası olarak kullanılabilir. Yeni kararlar alındıkça güncellenmesi önerilir.*
