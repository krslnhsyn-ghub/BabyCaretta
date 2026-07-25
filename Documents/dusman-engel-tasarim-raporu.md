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

## 3. Mimari Kararı: Unity Behavior (Behavior Tree)

Predator davranışları için **Unity Behavior** paketi (Unity'nin resmi, ücretsiz behavior tree aracı) kullanılacak — üçüncü parti Behavior Designer değil.

**Neden:** Behavior Designer'ın yeni nesil sürümü (Pro), eski sürümle geriye dönük uyumlu değil — üçüncü parti asset'lerde büyük sürüm geçişlerinde bu tür kırılma riski var. Unity Behavior ise resmi paket, ücretsiz, Unity 6 ile entegre, no-code ayarlamaya (olasılık/eşik/süre gibi parametreleri kod açmadan grafikten değiştirme) izin veriyor — beginner seviyeye ve UE5 Blueprint alışkanlığına daha uygun.

**Yapı:**
- Her predator tipi kendi **Behavior Tree** asset'ine sahip (Sequence/Condition/Action node'lardan oluşan görsel graph)
- Ortak veri **Blackboard** üzerinden taşınır (hedef/turtle referansı, mesafe, escape durumu vs.)
- Yakalama, tespit gibi özel mantıklar **custom Action/Condition node'ları** (C# script) olarak bir kere yazılır; sonrasında olasılık/süre/mesafe gibi ince ayarlar kod açmadan, doğrudan Unity Behavior editöründen değiştirilebilir
- Kaplumbağa ↔ predator iletişimi yine **event tabanlı** kalır (predator bir Action node içinde event fırlatır, kaplumbağa dinler; kaplumbağa struggle sonucunu event olarak geri yollar)

**Sonuç:** Kayalık yaratığı gibi "aynı mantık, farklı görünüm" istekleri yeni kod değil, yengecin tree'sinin kopyalanıp farklı model/parametrelerle kullanılmasıyla çözülür.

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

---

## 9. Uygulama Planı

Bu plan, yukarıdaki tasarım kararlarını üretim sırasına çevirmek için hazırlanmıştır. Amaç, sistemi küçük ve doğrulanabilir bir vertical slice ile başlatmak, ardından aynı mimari üzerinden diğer predator tiplerini genişletmektir.

### 9.1 Kapsam Kararı

#### V1 Kapsamında

- Aktif yırtıcı sistemi kurulacak.
- İlk vertical slice yalnızca **küçük yengeç** üzerinden yapılacak.
- Kaplumbağa tarafında predator etkilerine cevap verecek state/event altyapısı kurulacak.
- Predator davranışları Unity Behavior ile yönetilecek.
- Escape mekanikleri mash yerine ritim/zamanlama tabanlı olacak.
- Yakalanma sonucu klasik game over değil, yumuşak fade + checkpoint dönüşü olacak.

#### V1 Dışında

- İnsan atıkları predator sistemine dahil edilmeyecek.
- İnsan atıkları yalnızca mevcut CarryableItem / interaction sistemine oturan pasif puzzle objeleri olarak kalacak.
- Martı, küçük kuş, büyük yengeç ve kayalık yaratığı küçük yengeç vertical slice tamamlanmadan uygulanmayacak.
- İnsan atıkları için yeni state veya yeni core mekanik eklenmeyecek.

### 9.2 Üretim Sırası

1. Mevcut kaplumbağa controller/state yapısı incelenecek.
2. Predator sisteminin ortak veri ve event sözleşmesi belirlenecek.
3. Küçük yengeç vertical slice geliştirilecek.
4. Kaplumbağa tarafında `Restrained`, `EscapeStruggle` ve `Captured` akışı doğrulanacak.
5. Checkpoint dönüşü ve fade akışı predator yakalanma sonucuna bağlanacak.
6. Küçük yengeç için Behavior Tree asset'i kurulacak.
7. Parametreler `PredatorData` üzerinden ayarlanabilir hale getirilecek.
8. Denge ve his ayarı yapılacak.
9. Küçük yengeç sistemi tamamlandıktan sonra diğer predator tipleri planlanacak.

### 9.3 Mimari Hedef

#### Ortak Sistemler

##### `PredatorController`

Her aktif yırtıcı prefabında bulunacak ana controller.

Sorumlulukları:

- Hedef kaplumbağayı tanımak.
- Behavior Tree ile oyun kodu arasında köprü olmak.
- Yakalama, bırakma, saldırı ve escape sonuç event'lerini yönetmek.
- `PredatorData` içindeki parametreleri kullanmak.

##### `PredatorData`

ScriptableObject tabanlı ayar datası.

İçermesi beklenen alanlar:

- Predator tipi
- Tespit mesafesi
- Saldırı mesafesi
- Yakalama süresi
- Escape penceresi / ritim eşikleri
- Yavaşlatma oranı
- Stack davranışı
- Checkpoint'e gönderme süresi
- İlk karşılaşma ipucu gösterilsin mi

##### `TurtlePredatorTarget`

Kaplumbağa üzerinde bulunacak predator hedef bileşeni.

Sorumlulukları:

- Predator'ların kaplumbağaya ortak bir arayüzden erişmesini sağlamak.
- `IsHidden` bilgisini predator sistemine açmak.
- Grabbed / Restrained / Flipped / Captured isteklerini ilgili state sistemine iletmek.
- Escape sonucunu predator'a event olarak geri bildirmek.

### 9.4 Kaplumbağa State Planı

#### `Restrained`

İlk vertical slice için öncelikli state.

Ne zaman girilir:

- Küçük yengeç kaplumbağanın bacağını tuttuğunda.

Davranış:

- Kaplumbağanın hareket hızı düşer.
- Birden fazla yengeç tutarsa yavaşlama katlanır.
- Locomotion, normal yürüyüşten aksak yürüyüş blend'ine geçer.
- EscapeStruggle input'u aktif hale gelir.

Çıkış:

- Oyuncu ritim/zamanlama input'unu başarıyla yaparsa yengeç bırakır.
- 3 yengeç tutar ve 3-5 saniye içinde kurtulamazsa `Captured` tetiklenir.

#### `EscapeStruggle`

Ortak escape mantığı olarak ele alınacak.

Not:

- Ayrı bir ana state olmak zorunda değildir.
- `Restrained`, `Grabbed` ve `Flipped` içinden kullanılan ortak bir modül/component olarak uygulanabilir.

İlk vertical slice davranışı:

- Yengeç için iki input arasındaki zaman aralığı ölçülür.
- Çok hızlı input başarısız sayılır.
- Çok yavaş input başarısız sayılır.
- Başarılı aralık örneği: 0.15-0.6 saniye.

#### `Captured`

Ne zaman girilir:

- Predator etkisinden zamanında kurtulunamazsa.

Davranış:

- Oyuncu kontrolü kapanır.
- Yumuşak fade başlar.
- Kaplumbağa en yakın/checkpoint sisteminin belirlediği noktaya döner.
- Fade açılır ve kontrol geri verilir.

### 9.5 Vertical Slice: Küçük Yengeç

#### Hedef

Küçük yengecin kaplumbağayı algılaması, yaklaşması, bacağını tutması, yavaşlatması, oyuncunun ritim input'u ile kurtulması ve başarısızlık halinde checkpoint'e dönmesi uçtan uca çalışmalıdır.

#### Minimum Davranış Akışı

1. Küçük yengeç idle/patrol durumunda bekler.
2. Kaplumbağa algılama mesafesine girerse hedeflenir.
3. Kaplumbağa `IsHidden == true` ise hedeflenmez.
4. Kaplumbağa saldırı mesafesine girerse yengeç tutunma aksiyonuna geçer.
5. Kaplumbağa `Restrained` etkisi alır.
6. Hareket hızı düşer.
7. Aksak yürüyüş animasyon blend'i aktif olur.
8. Oyuncu doğru ritimde input verirse yengeç bırakır.
9. Oyuncu başaramazsa yengeç tutmaya devam eder.
10. 3 yengeç stack olur ve süre dolarsa `Captured` tetiklenir.
11. Fade + checkpoint dönüşü gerçekleşir.

#### Acceptance Criteria

- Küçük yengeç kaplumbağayı yalnızca belirlenen mesafe içinde algılar.
- Kaplumbağa saklanmışsa küçük yengeç saldırmaz.
- Tek yengeç tutunca hareket belirgin şekilde yavaşlar ama tamamen durmaz.
- Birden fazla yengeç tutunca yavaşlama katlanır.
- Escape input'u mash gibi çalışmaz; doğru aralıkla çift basış ister.
- Başarılı escape sonrası yengeç bırakır ve kaplumbağa normal yürüyüşe döner.
- Başarısızlık süresi dolunca oyuncu game over görmeden checkpoint'e döner.
- Console'da hata oluşmaz.

### 9.6 Unity Behavior Planı

#### Küçük Yengeç Behavior Tree

Önerilen ilk graph:

1. `IsTargetHidden?`
   - Evetse idle/patrol.
   - Hayırsa devam.
2. `IsTargetInDetectionRange?`
   - Hayırsa idle/patrol.
   - Evetse hedefe yönel.
3. `MoveToTarget`
4. `IsTargetInGrabRange?`
   - Hayırsa takip etmeye devam.
   - Evetse grab aksiyonu.
5. `ApplyRestrained`
6. `WaitForEscapeOrTimeout`
7. Escape başarılıysa `ReleaseTarget`
8. Escape başarısız ve threshold dolduysa `TriggerCaptured`

#### Custom Node İhtiyaçları

İlk vertical slice için beklenen custom node'lar:

- Hedef saklanmış mı kontrolü
- Hedef algılama mesafesinde mi kontrolü
- Hedef tutma mesafesinde mi kontrolü
- Hedefe yaklaşma aksiyonu
- Restrained uygulama aksiyonu
- Escape sonucunu bekleme aksiyonu
- Hedefi bırakma aksiyonu
- Captured tetikleme aksiyonu

### 9.7 Event Sözleşmesi

Predator ve kaplumbağa doğrudan birbirinin iç state detaylarını yönetmemelidir. İletişim event tabanlı kalmalıdır.

#### Predator'dan Kaplumbağaya

- `OnPredatorGrabStarted`
- `OnPredatorRestrainedStarted`
- `OnPredatorRestrainedStackChanged`
- `OnPredatorCaptureRequested`
- `OnPredatorReleased`

#### Kaplumbağadan Predator'a

- `OnEscapeStarted`
- `OnEscapeSucceeded`
- `OnEscapeFailed`
- `OnTurtleHiddenChanged`
- `OnCheckpointRespawnCompleted`

İsimler implementasyon sırasında mevcut kod stiline göre değiştirilebilir; önemli olan yön ve sorumluluk ayrımıdır.

### 9.8 Animasyon ve Görsel Geri Bildirim

#### Küçük Yengeç

Gerekli minimum animasyonlar:

- Idle
- Walk/Scuttle
- Grab/Clamp
- Struggle reaction
- Release

#### Kaplumbağa

Gerekli minimum animasyonlar:

- Normal locomotion
- Aksak yürüyüş / limp walk blend
- Kısa struggle feedback
- Captured/fade öncesi kontrol kilidi pozu

#### Görsel Geri Bildirim

- Yengeç tuttuğunda bacak tek tek dondurulmayacak.
- Bunun yerine tüm locomotion aksak yürüyüş blend'ine geçecek.
- Bu tercih mevcut rig/IK riskini azaltır.

### 9.9 Sonraki Predator Tipleri

Küçük yengeç tamamlandıktan sonra sırayla:

1. Büyük yengeç
2. Martı
3. Küçük kuşlar
4. Kayalık yaratığı

#### Büyük Yengeç

- Küçük yengeç mantığının daha güçlü tekil versiyonu.
- Stack yerine tek başına yüksek tehdit.
- Gizli delikten çıkma set-piece'i eklenebilir.

#### Martı

- Uçma, yaklaşma, yakalama ve ısırma penceresi gerektirir.
- Animation Event ile `OnBiteWindowOpen()` / `OnBiteWindowClose()` kullanılır.
- İlk karşılaşmada diegetic ipucu gösterilir.

#### Küçük Kuşlar

- Kaplumbağayı ters çevirme davranışı gerektirir.
- `Flipped` state'i bu aşamada uygulanır.
- İki aşamalı kalkış animasyonu ve input penceresi gerekir.

#### Kayalık Yaratığı

- Yengeç mantığının farklı model/prefab ile varyasyonu.
- Yeni core kod yazılmadan `PredatorData` ve Behavior Tree kopyasıyla çözülmesi hedeflenir.

### 9.10 Test Planı

#### Editör Testleri

- PredatorData değerleri doğru okunuyor mu?
- Kaplumbağa saklanmışken predator hedeflemeyi bırakıyor mu?
- Stack sayısı doğru artıp azalıyor mu?
- Escape input aralığı doğru ölçülüyor mu?
- Captured event'i yalnızca şartlar sağlanınca tetikleniyor mu?

#### Sahne Testleri

- Tek yengeç tutma ve bırakma.
- İki yengeç ile kademeli yavaşlama.
- Üç yengeç ile süre dolunca checkpoint dönüşü.
- Saklanma durumunda saldırı iptali.
- Respawn sonrası yengeçlerin eski tutunma durumunda kalmaması.

#### Hata Kontrolü

- Script compile hatası yok.
- Console error/warning yok.
- Respawn sonrası null reference yok.
- Behavior Tree hedef referansını kaybettiğinde oyun akışı bozulmuyor.

### 9.11 Milestone'lar

#### Milestone 1 — Teknik Temel

- `PredatorData`
- `PredatorController`
- `TurtlePredatorTarget`
- İlk event sözleşmesi
- Kaplumbağa tarafında restrained isteğini alma

#### Milestone 2 — Küçük Yengeç Prototype

- Küçük yengeç prefabı
- Basit tespit ve yaklaşma
- Tutunma
- Hareket yavaşlatma
- Manual/test input ile bırakma

#### Milestone 3 — Escape ve Stack

- Çift basış aralığına dayalı escape
- Stackable yengeç sayacı
- 3 yengeç + süre dolunca captured

#### Milestone 4 — Checkpoint ve Polish

- Fade + checkpoint dönüşü
- Aksak yürüyüş blend'i
- Basic SFX/VFX feedback
- Console/test kontrolü

#### Milestone 5 — Genişleme Hazırlığı

- Küçük yengeç mimarisinin büyük yengeç için kopyalanabilir hale gelmesi
- Parametrelerin kod açmadan ayarlanabilir olması
- Behavior Tree ve PredatorData kullanımının belgelenmesi

### 9.12 Açık Kararlar

Bu kararlar implementasyon sırasında netleştirilecek:

- `EscapeStruggle` ayrı state mi olacak, yoksa ortak component/modül mü?
- Yengeç stack yavaşlatma oranı lineer mi, eğrisel mi olacak?
- 3-5 saniyelik başarısızlık süresi tam olarak kaç saniye olacak?
- Checkpoint dönüşü mevcut checkpoint sistemiyle doğrudan mı, yoksa predator-specific wrapper ile mi tetiklenecek?
- İlk küçük yengeç sahne karşılaşması tutorial gibi mi, doğal encounter gibi mi sunulacak?

### 9.13 İlk Uygulama Görevi

İlk geliştirme görevi:

> Küçük yengeç vertical slice için proje mimarisini incele, mevcut turtle state/checkpoint/input sistemine en az müdahaleyle `PredatorData`, `PredatorController` ve `TurtlePredatorTarget` temelini kur.

Bu görev tamamlandığında sistem henüz final histe olmak zorunda değildir; amaç predator ve kaplumbağa arasında doğru event/state akışını kanıtlamaktır.
