# Journey of a Baby Sea Turtle — Proje Durum Raporu ve Yol Haritası
*Bu belge, projeyi başka bir AI asistanına veya ekip üyesine anlatmak için kullanılabilir. Taşınabilir metin (markdown) formatındadır.*

**GitHub:** https://github.com/krslnhsyn-ghub/BabyCaretta
*(Kod artık burada — yeni bir AI ile konuşmaya başlarken kod dosyalarını tekrar yapıştırmak yerine bu linki paylaşıp ilgili dosyayı incelemesini isteyebilirsiniz, bu ciddi bir bağlam/kota tasarrufu sağlar.)*

---

## 📋 Proje Özeti

- **Oyun:** Journey of a Baby Sea Turtle (çalışma adı) — atmosferik keşif oyunu, Journey/Abzu/A Short Hike tarzı
- **Motor:** Unity 6 (6.5 sürümü)
- **Ekip:** 1 Gameplay/Unity Developer, 1 Environment Artist, 1 Generalist Artist, AI destekli geliştirme
- **Şu an:** Faz 0 (Öğrenme + Çekirdek Prototip, ~10 hafta, tam zamanlı)
- **Karakter:** Yeni doğmuş Caretta Caretta, 3ds Max + CAT rig ile üretiliyor

---

## ✅ Şu Ana Kadar Tamamlananlar

### Belgeler
- **PFD** (Project Foundation Document) — 28 maddelik vizyon/DNA belgesi
- **GDD v0.1** — PFD'den türetilmiş tasarım belgesi
- **Unity Mimari ve Klasör Yapısı** — klasör iskeleti, isimlendirme kuralları
- **Faz 0 Üretim Yol Haritası** — 10 haftalık, 3 iş kolu paralel plan
- **Faz 0 Checklist** (Notion-uyumlu, işaretlenebilir)
- **Animasyon State Listesi v1**
- **PFD Madde 14 — Revizyon** — güncel tuş şeması

### Karakter Hareketi
- `TurtleController.cs` — tek dosya, switch tabanlı state mantığı (bilinçli mimari karar: anlaşılırlık > mimari saflık)
- Tank kontrolü (W/S ileri-geri, A/D giderken kavisli / dururken yerinde dönüş)
- **State'ler:** Idle, Walk, Turn, Hop, ShellEnter, ShellIdle, ShellExit, Dig, Burrow — hepsi çalışıyor
- Hop: Y ekseni animasyondan (root motion), X/Z koddan (`hopMoveSpeed`, `hopForwardDelay`), Hop sırasında kod tarafı yerçekimi uygulanmıyor
- `CharacterController` tabanlı (Rigidbody değil)

### Kabuk (Q) ve Kum (E) — Self-Action Mimarisi
- Q ve E, **dış nesne aramayan** aksiyonlar oldukları için `InteractionController`'da değil, doğrudan `TurtleController` içinde, Hop'la aynı desende yönetiliyor
- **Q:** Basit toggle — Idle/Walk/Turn'den `ShellEnter`→`ShellIdle`, tekrar basınca `ShellExit`→`Idle`. Yürürken de tetiklenebiliyor (refleks olması için bilinçli karar)
- **E:** Tap/Hold ayrımı (`sandHoldThreshold`) — kısa basış `Dig`, uzun basılı tutma `Burrow` (bırakılana kadar), bırakınca `Idle`
- **Henüz yok:** ShellIdle'da eğime göre otomatik kayma, Burrow'un sadece kumsalda çalışması (zemin kontrolü) — bilinçli olarak ertelendi, "önce mekanizma çalışsın, kısıtlamayı sonra ekleriz" kararı

### Animasyon Sistemi
- 3ds Max + CAT rig, dummy karakter ve klipler; CAT Hub/Root eksen sorunları **nötr Point Helper wrapper** ile kalıcı çözüldü
- Animator Controller: `Speed`, `TurnDirection`, `Hop`, `ShellEnter`, `ShellExit`, `Dig`, `IsBurrowing`, `Headbutt`, `Bite`, `IsPushing`, `IsCarrying` parametreleri kurulu
- Movement state'leri bir sub-state machine'e (`Movement`) taşındı, karışıklığı azaltmak için
- **Çözülen bug'lar:** TurnDirection sıfırlanmıyordu, Hop sırasında yerçekimi çakışması, Burrow'un "Any State"ten sürekli yeniden tetiklenmesi (bool parametreyle Any State kullanmanın riski öğrenildi — Trigger'larda sorun yok, Bool'larda spesifik state'ten bağlamak gerekiyor)

### Interaction Sistemi (Mouth/Body — dış nesne arayan etkileşimler)
- `IInteractable.cs`, `InteractionController.cs`, `PushableItem.cs` (Body: Tap=Headbutt, Hold=Push), `EdibleItem.cs` (Mouth: Tap=Ye, Hold=Tut/Bırak)
- `OverlapSphere` + açı filtresi (koni yerine, daha basit), Tap/Hold süre eşiği (`holdThreshold`), Hold sırasında menzil dışına çıkınca otomatik `HoldEnd`
- `PlayOrganAnimation()` — kaplumbağanın kendi animasyonunu (Headbutt/Bite/IsPushing/IsCarrying) tetikliyor, hedef nesneden bağımsız (doğru sorumluluk ayrımı: nesne "ne olur"a, InteractionController "kaplumbağa ne yapar"a karar veriyor)

### UpperBody_Mouth Layer (Bite/Carry için)
- Avatar Mask ile sadece kafa/boyun/gövde maskeleniyor, bacaklar Base Layer'da serbest kalıyor (yürürken ısırabiliyor)
- **Layer weight, akıllı bir yöntemle yönetiliyor:** `animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag("UpperBody")` — şu an oynayan state bu tag'e sahipse weight 1'e, değilse 0'a yumuşakça (`Mathf.MoveTowards`) geçiyor. Bu, elle süre tahmini tutmaktan (Hop'ta yaptığımız gibi) daha sağlam bir çözüm — **önemli önkoşul: `Loco_Bite` ve `Loco_Carry` state'lerinin Animator'da "UpperBody" tag'ine sahip olması gerekiyor, bu doğrulanmalı**

### Karar Kayıtları
- **Malbers Animal Controller:** NO-GO (şu an) — kendi sistemle devam. **Swim fazında tekrar değerlendirilecek.**
- **Unity Visual Scripting / Playmaker:** Kullanılmayacak — AI destekli geliştirme felsefesiyle çelişiyor
- **State machine mimarisi:** Switch tabanlı tek dosya (bilinçli karar, ~15-20 state'e kadar sürdürülebilir sınır kondu)
- **Self-action vs Interaction ayrımı:** Dış nesne arayan (Mouth/Body) → `InteractionController`; kendi üzerinde olan (Hop/Shell/Sand) → `TurtleController`'ın kendi içinde. Bu ayrım bir ara karışmıştı (bir AI, Shell/Dig'i event sistemiyle InteractionController'a taşımıştı), **geri düzeltildi** — tek sorumluluk prensibi korunuyor
- **Tuş şeması (PFD Madde 14 Revizyon):**

| Tuş | Organ | Tap | Hold |
|---|---|---|---|
| WASD | Hareket | Yürü/Dön | — |
| Space | Hareket | Hop | — |
| Fare hareketi | Kamera | — | Serbest bakış (sürekli) |
| Q | Kabuk (Shell) | Kabuğa çekil/çık | Shell Idle'dayken eğim varsa otomatik kayma *(henüz kodlanmadı)* |
| E | Kum (Sand) | Dig | Burrow Enter→Idle→Exit *(zemin kontrolü henüz yok)* |
| LMB | Ağız (Mouth) | Isır/Yakala/Ye | Tutar (Carry), bırakınca Drop |
| RMB | Gövde (Body) | Headbutt | Push (sürekli) |

---

## 🗂️ Kod Dosyaları (GitHub'da güncel — yol: `Assets/Project/Scripts/`)

```
Character/Controllers/Turtle Controller.cs   → Hareket + state mantığı + Shell/Sand self-action + Animator/Interaction köprüsü
Interaction/IInteractable.cs                 → Etkileşim arayüzü, InteractionOrgan/Phase enum'ları
Interaction/InteractionController.cs         → En yakın etkileşilebilir nesneyi bulma, Tap/Hold kararı, UpperBody layer weight
Interaction/Pushableitem.cs                  → Test nesnesi (Body/RMB) — itilebilir herhangi bir obje
Interaction/Edibleitem.cs                    → Test nesnesi (Mouth/LMB) — yenebilir/tutulabilir herhangi bir obje
```

*Not: Proje ayrıca Unity'nin ücretsiz Starter Assets (ThirdPerson) paketini referans/inceleme amaçlı içeriyor (`Assets/StarterAssets/`) — bizim kendi sistemimizin bir parçası değil, öğrenme kaynağı olarak duruyor.*

---

## ⚠️ Bilinen Açık Sorular / Sonraya Bırakılanlar

- **ShellIdle'da otomatik kayma** — eğim algılama (raycast) henüz eklenmedi
- **Burrow'un sadece kumsalda çalışması** — zemin/biyom kontrolü henüz eklenmedi
- **Arkadaş kurtarma için ayrı animasyon** gerekebilir (Grab+Carry yeterli mi, "çekme/zorlama" hissi mi lazım) — netleşmedi
- **UpperBody_Mouth layer'ın "UpperBody" state tag'leri gerçekten set edildi mi** — doğrulanmalı, edilmediyse layer hiç açılmaz
- **CharacterController kapsül şekli** kaplumbağaya tam oturmuyor (Unity'de "hemisphere" yok) — kabul edilebilir, ciddi sorun çıkarsa büyük bir mimari karar (Rigidbody'ye geçiş) gerekir
- **"Yürüyerek itme" (fiziksel temas tabanlı push)** — RMB-Hold ile uzaktan kuvvet yerine gerçek temas — gerçek Push animasyonu yapılırken değerlendirilecek

---

## 🚀 Sırada Ne Var — Önerilen Sıra

### 1. UpperBody layer doğrulaması
`Loco_Bite`/`Loco_Carry` state'lerinde "UpperBody" tag'i gerçekten var mı kontrol et, yoksa ekle. Test: LMB ile ısırırken/taşırken layer weight'in 0→1→0 yumuşak geçiş yaptığını gözle doğrula.

### 2. Shell — eğimde otomatik kayma
`ShellIdle` case'ine raycast tabanlı zemin eğimi kontrolü ekle; eğim eşiği aşılırsa `ShellSlide` state'ine geç (yeni bir state + animasyon gerekecek).

### 3. Burrow — zemin kısıtlaması
Kumsal biyomunu temsil eden bir trigger/tag sistemi kur; `UpdateSandInput`'taki `canStart` koşuluna "zeminde mi" kontrolünü ekle.

### 4. Kalan V1 animasyonlarını tamamlama
Animasyon Listesi'ndeki kalan klipleri (varsa TurnLeft/Right'ın gerçek halleri, Struggle/Reaction grubu vb.) üretip bağlama.

### 5. Faz 0 Definition of Done — tam kontrol
Checklist dosyasındaki tüm maddeleri gözden geçirip işaretleme.

### 6. Faz 1 (Vertical Slice) planlaması
Hangi biyomun vertical slice'a gireceğini netleştirme.

### 7. Swim fazı öncesi hatırlatma
Malbers Animal Controller'ı gerçekten deneyip GO/NO-GO kararını netleştirme.

---

## 📎 Diğer Referans Belgeler

1. `PFD_Journey_of_a_Baby_Sea_Turtle.docx`
2. `GDD_v0.1_Draft.docx`
3. `Unity_Mimari_ve_Klasor_Yapisi_v0.1.docx`
4. `Faz0_Uretim_Yol_Haritasi.docx`
5. `Faz0_Checklist_Notion.md`
6. `Animasyon_State_Listesi_v1.md`
7. `PFD_Madde14_Revizyon.docx`

---

## 💡 Yeni Sohbete Geçerken Öneri

Yeni AI'ya bu belgeyi yapıştırdıktan sonra, kod hakkında soru sorması gerekirse **GitHub linkini verip ilgili dosyayı incelemesini isteyin** — kodu tekrar tekrar mesaj içine yapıştırmak (özellikle uzun dosyalarda) hem sizin hem AI'nın bağlam/kota kullanımını gereksiz yere şişiriyor. Repoyu güncel tutmak (her önemli değişiklikten sonra commit/push) bu yüzden değerli bir alışkanlık.
