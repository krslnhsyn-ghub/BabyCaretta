# Journey of a Baby Sea Turtle — Faz 0 Güncel Durum ve Yapılacaklar Listesi
*Güncelleme: 23 Temmuz 2026*

**GitHub:** https://github.com/krslnhsyn-ghub/BabyCaretta

---

## ⏱️ Zamanlama

- **Faz 0 planı:** 10 hafta, tam zamanlı, 3 iş kolu paralel
- **Başlangıç:** ~10 Temmuz 2026
- **Bugün:** 23 Temmuz 2026 → **~13. gün, planın ilk %20'si kadarı**
- **Değerlendirme:** Bu süreye göre script/gameplay tarafındaki iş hacmi (tüm temel state'ler, self-action mimarisi, interaction sistemi, carry/attach sistemi, alan etkili stun mekaniği) beklenenin oldukça üzerinde — takvimde gecikme yok. IK'nın CAT rig'te takılıp doğru şekilde ertelenmesi, riskin önceden öngörülmüş ve yönetilmiş olduğunu gösteriyor.

---

## ✅ Yapılmış Olanlar

### 📄 Dokümantasyon
- PFD (Project Foundation Document) — 28 maddelik vizyon/DNA belgesi
- GDD v0.1
- Unity Mimari ve Klasör Yapısı
- Faz 0 Üretim Yol Haritası (10 haftalık, 3 kol)
- Faz 0 Checklist (Notion — harici, bu repoda tutulmuyor)
- Animasyon State Listesi v1
- PFD Madde 14 Revizyonu (güncel tuş şeması)

### 💻 Script / Gameplay (Unity)
- `TurtleController.cs` — switch tabanlı tek dosya state mimarisi
- Tank kontrolü (W/S, A/D)
- State'ler: Idle, Walk, Turn, Hop, ShellEnter, ShellIdle, ShellExit, Dig, Burrow — tümü çalışıyor
- Hop mekaniği: Y ekseni root motion'dan, X/Z koddan
- CharacterController tabanlı hareket (Rigidbody değil)
- Self-action mimarisi: Shell (Q) ve Sand (E), `TurtleController` içinde yönetiliyor
  - Q: toggle (ShellEnter↔ShellExit), yürürken de tetiklenebiliyor
  - E: Tap/Hold ayrımı → Dig / Burrow
- **Shell'de eğimde otomatik kayma (ShellSlide)** — tamamlandı
- **Gövde tilt sistemi** (zemin eğimine göre otomatik) — tamamlandı
- **Burrow zemin kısıtlaması** — Tag tabanlı (`"Sand"` tag), `canStart` koşuluna `CompareTag` kontrolü eklendi — tamamlandı
- **IsHidden bayrağı** — Burrow'a girip çıkarken set/reset ediliyor, ileride predator AI'ın kaplumbağayı görmezden gelmesi için hazır
- Interaction sistemi (Mouth/Body, dış nesne arayan etkileşimler):
  - `IInteractable.cs`, `InteractionController.cs`, `PushableItem.cs`, `EdibleItem.cs`
  - OverlapSphere + açı filtresi, Tap/Hold eşiği, menzil dışına çıkınca otomatik HoldEnd
  - `PlayOrganAnimation()` ile sorumluluk ayrımı (nesne "ne olur", controller "kaplumbağa ne yapar")
- **UpperBody_Mouth layer** — Avatar Mask + tag tabanlı weight yönetimi — **doğrulandı, düzgün çalışıyor**
- **Mouth-carry/attach sistemi** — `CarryableItem.cs`, `InteractionContext`'e eklenen `AttachPoint` alanı, `InteractionController`'daki `mouthAttachPoint` referansı — tamamlandı
  - Taşırken duvar/zemine gömülme sorunu, kinematic+parent + `IsObstructed` engel kontrolü ile çözüldü (karar mercii `InteractionController`'da kalıyor, sorumluluk ayrımı korunuyor)
- **Kum serpme alan etkisi (stun)** — `IStunnable.cs` arayüzü, `TurtleController.ApplySandStunEffect()`, `Crab.cs` ile test edildi ve **doğrulandı, çalışıyor**
- **Particle efektleri** — kum atma (E-Tap), gömülme (E-Hold), stun anı için üçü de eklendi, basit ama işlevsel
- **Arkadaş kurtarma tasarım kararı**: manuel taşıma yok — yaklaşınca tetikleyici gönderilir, gerisini sinematik/arkadaşın kendi animasyonu halleder. İleride yeterli görülürse manuel kurtarma tekrar değerlendirilecek

### 🎨 Animasyon / Rig
- 3ds Max + CAT rig, dummy karakter ve klipler ile üretim
- CAT Hub/Root eksen sorunu → nötr Point Helper wrapper ile kalıcı çözüldü
- Animator Controller kuruldu: Speed, TurnDirection, Hop, ShellEnter, ShellExit, Dig, IsBurrowing, Headbutt, Bite, IsPushing, IsCarrying parametreleri
- Movement state'leri ayrı sub-state machine'e (`Movement`) taşındı
- Çözülen bug'lar: TurnDirection sıfırlanmaması, Hop'ta yerçekimi çakışması, Any State + Bool riski

### 🔧 Karar Kayıtları
- Malbers Animal Controller: NO-GO (şu an), Swim fazında tekrar değerlendirilecek
- Unity Visual Scripting / Playmaker: kullanılmayacak
- State machine: switch tabanlı tek dosya (~15-20 state sınırı ile sürdürülebilir kabul edildi)
- Self-action vs Interaction ayrımı netleşti ve korunuyor
- Karakter rig import tipi: Generic (Humanoid değil)

---

## ⬜ Yapılacaklar

### 💻 Script / Gameplay
- [ ] CharacterController kapsül şeklinin kaplumbağaya oturmama sorunu — şimdilik kabul edilebilir
- [ ] "Yürüyerek itme" (temas tabanlı push) — gerçek Push animasyonu geldiğinde değerlendirilecek
- [ ] Arkadaş kurtarma — implementasyon (tetikleyici + sinematik) — gerçek arkadaş asset'i/sinematik gelince yapılacak

### 🦴 IK Ayak Sistemi — Beklemede
- [x] Animation Rigging paketi kuruldu, Rig Builder + Two Bone IK Constraint kurulumu yapıldı, hedefleme script'i yazıldı
- [ ] ⏸️ **Çalışmıyor** — CAT rig bone çözümleme hatası + eksik `legs[]` referansları doğrulandı. Component'ler sahnede devre dışı bırakıldı (silinmedi). Gerçek karakter modeli/rig'i gelene kadar ertelendi

### 🎨 Animasyon
- [ ] Kalan V1 animasyon kliplerinin tamamlanması — gerçek karakter modelini bekliyor
- [ ] Gerçek model/rig geldiğinde: dummy karakterden geçiş, IK constraint referanslarının yeniden bağlanması, animasyon kalibrasyonu

### 🖼️ Grafik / Ortam
- Gerçek karakter modeli ve rig'i üretim aşamasında — henüz Faz 0 script tarafına entegre değil
- Beach/Coral biyomlarının vertical slice ortam varlıkları — Faz 1 kapsamına girecek

### 🖱️ UI / 🔊 Ses
- Bu fazda henüz ele alınmadı, planlama gerektiği not edilsin

### 📄 Dokümantasyon / Süreç
- [ ] Faz 0 Definition of Done — Notion checklist'i harici olduğu için tam madde eşleşmesi yapılamadı, ama script/gameplay tarafı bu belgeye göre büyük ölçüde doymuş durumda
- [ ] Faz 1 (Vertical Slice) planlaması — hangi biyomun girileceğinin netleştirilmesi
- [ ] Swim fazı öncesi Malbers Animal Controller GO/NO-GO kararının kesinleştirilmesi

---

## 🚀 Önerilen Sıradaki Öncelik

1. Engel/tehlike davranışları (yengeç, martı vb.) — `IStunnable` altyapısı zaten hazır, temel AI eklenmesi mantıklı bir sonraki adım
2. Faz 1 planlaması — hangi biyomun vertical slice'a gireceği
3. Kalan V1 animasyon klipleri ve IK — model/rig geldiğinde devreye alınacak

---

## 📎 Referans Belgeler
1. `PFD_Journey_of_a_Baby_Sea_Turtle.docx`
2. `GDD_v0.1_Draft.docx`
3. `Unity_Mimari_ve_Klasor_Yapisi_v0.1.docx`
4. `Faz0_Uretim_Yol_Haritasi.docx`
5. `Faz0_Checklist_Notion.md` (harici, Notion)
6. `Animasyon_State_Listesi_v1.md`
7. `PFD_Madde14_Revizyon.docx`
