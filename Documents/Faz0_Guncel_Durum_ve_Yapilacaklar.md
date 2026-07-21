# Journey of a Baby Sea Turtle — Faz 0 Güncel Durum ve Yapılacaklar Listesi
*Güncelleme: 21 Temmuz 2026*

**GitHub:** https://github.com/krslnhsyn-ghub/BabyCaretta

---

## ⏱️ Zamanlama

- **Faz 0 planı:** 10 hafta, tam zamanlı, 3 iş kolu paralel
- **Başlangıç:** ~10 Temmuz 2026
- **Bugün:** 21 Temmuz 2026 → **~11. gün, planın ilk %15'i kadarı**
- **Değerlendirme:** Bu süreye göre tamamlanan iş hacmi (tüm temel state'ler, self-action mimarisi, interaction sistemi, UpperBody layer, ShellSlide, gövde tilt) beklenenin üzerinde — takvimde gecikme yok, tersine ilk hafta planın önünde ilerlemiş görünüyor. IK'daki CAT rig takılması normal bir teknik risk, takvimi tehdit eden bir gecikme değil (zaten gerçek model bekleniyordu).

---

## ✅ Yapılmış Olanlar

### 📄 Dokümantasyon
- PFD (Project Foundation Document) — 28 maddelik vizyon/DNA belgesi
- GDD v0.1
- Unity Mimari ve Klasör Yapısı
- Faz 0 Üretim Yol Haritası (10 haftalık, 3 kol)
- Faz 0 Checklist (Notion)
- Animasyon State Listesi v1
- PFD Madde 14 Revizyonu (güncel tuş şeması)

### 💻 Script / Gameplay (Unity)
- `TurtleController.cs` — switch tabanlı tek dosya state mimarisi
- Tank kontrolü (W/S, A/D)
- State'ler: Idle, Walk, Turn, Hop, ShellEnter, ShellIdle, ShellExit, Dig, Burrow — tümü çalışıyor
- Hop mekaniği: Y ekseni root motion'dan, X/Z koddan
- CharacterController tabanlı hareket (Rigidbody değil)
- Self-action mimarisi: Shell (Q) ve Sand (E), dış nesne aramadıkları için `TurtleController` içinde yönetiliyor
  - Q: toggle (ShellEnter↔ShellExit), yürürken de tetiklenebiliyor
  - E: Tap/Hold ayrımı → Dig / Burrow
- **Shell'de eğimde otomatik kayma (ShellSlide)** — tamamlandı
- **Gövde tilt sistemi** (zemin eğimine göre otomatik) — tamamlandı, IK ayak sistemine temel oluşturuyor
- Interaction sistemi (Mouth/Body, dış nesne arayan etkileşimler):
  - `IInteractable.cs`, `InteractionController.cs`, `PushableItem.cs`, `EdibleItem.cs`
  - OverlapSphere + açı filtresi, Tap/Hold eşiği, menzil dışına çıkınca otomatik HoldEnd
  - `PlayOrganAnimation()` ile sorumluluk ayrımı (nesne "ne olur", controller "kaplumbağa ne yapar")
- **UpperBody_Mouth layer** — Avatar Mask + tag tabanlı weight yönetimi (`IsTag("UpperBody")` + `Mathf.MoveTowards`) — **doğrulandı, düzgün çalışıyor**
- Burrow zemin kısıtlaması için karar: **Tag tabanlı** (`"Sand"` tag) — layer değil

### 🎨 Animasyon / Rig
- 3ds Max + CAT rig, dummy karakter ve klipler ile üretim
- CAT Hub/Root eksen sorunu → nötr Point Helper wrapper ile kalıcı çözüldü
- Animator Controller kuruldu: Speed, TurnDirection, Hop, ShellEnter, ShellExit, Dig, IsBurrowing, Headbutt, Bite, IsPushing, IsCarrying parametreleri
- Movement state'leri ayrı sub-state machine'e (`Movement`) taşındı
- Çözülen bug'lar: TurnDirection sıfırlanmaması, Hop'ta yerçekimi çakışması, Any State + Bool riski (Burrow'un sürekli yeniden tetiklenmesi)

### 🔧 Karar Kayıtları
- Malbers Animal Controller: NO-GO (şu an), Swim fazında tekrar değerlendirilecek
- Unity Visual Scripting / Playmaker: kullanılmayacak
- State machine: switch tabanlı tek dosya (~15-20 state sınırı ile sürdürülebilir kabul edildi)
- Self-action vs Interaction ayrımı netleşti ve korunuyor

---

## ⬜ Yapılacaklar

### 💻 Script / Gameplay
- [ ] **Burrow — zemin kısıtlaması (uygulama):** Kumsal zemin objelerine `"Sand"` tag'i verilecek, `UpdateSandInput`'taki `canStart` koşuluna `CompareTag("Sand")` kontrolü eklenecek
- [ ] Arkadaş kurtarma etkileşimi için animasyon/mekanik netleştirmesi (Grab+Carry yeterli mi, "çekme" hissi ayrı mı gerekiyor)
- [ ] CharacterController kapsül şeklinin kaplumbağaya oturmama sorunu — şimdilik kabul edilebilir, ciddi sorun çıkarsa Rigidbody'ye geçiş değerlendirilecek
- [ ] "Yürüyerek itme" (temas tabanlı push) — gerçek Push animasyonu geldiğinde değerlendirilecek

### 🦴 IK Ayak Sistemi — Beklemede
- [x] Animation Rigging paketi kuruldu
- [x] Rig Builder + Two Bone IK Constraint kurulumu yapıldı
- [x] Hedefleme script'i (raycast tabanlı) ve weight blend mantığı yazıldı
- [ ] ⏸️ **Çalışmıyor — CAT rig kaynaklı bir sorunla ilişkili görünüyor.** Gerçek karakter modeli/rig'i gelene kadar ertelendi. Mimari hazır; yeni rig geldiğinde yapılacak iş, Two Bone IK Constraint'lerdeki Root/Mid/Tip bone referanslarını yeniden bağlamaktan ibaret olmalı

### 🎨 Animasyon
- [ ] Kalan V1 animasyon kliplerinin tamamlanması (TurnLeft/Right'ın gerçek halleri, Struggle/Reaction grubu vb. — Animasyon State Listesi v1'e göre)
- [ ] Gerçek karakter modeli/rig'i geldiğinde: dummy karakterden geçiş, IK constraint referanslarının yeniden bağlanması, animasyon kalibrasyonu

### 🖼️ Grafik / Ortam
- Gerçek karakter modeli ve rig'i üretim aşamasında (Environment/Generalist artist tarafında) — henüz Faz 0 script tarafına entegre değil
- Beach/Coral biyomlarının vertical slice ortam varlıkları — Faz 1 kapsamına girecek, bu fazda henüz başlanmadı

### 🖱️ UI
- Bu fazda henüz ele alınmadı — GDD/roadmap'te ayrı bir madde olarak detaylandırılmamış, planlama gerektiği not edilsin

### 🔊 Ses
- Bu fazda henüz ele alınmadı — GDD/roadmap'te ayrı bir madde olarak detaylandırılmamış, planlama gerektiği not edilsin

### 📄 Dokümantasyon / Süreç
- [ ] Faz 0 Definition of Done — checklist dosyasındaki tüm maddelerin gözden geçirilip işaretlenmesi
- [ ] Faz 1 (Vertical Slice) planlaması — hangi biyomun girileceğinin netleştirilmesi
- [ ] Swim fazı öncesi Malbers Animal Controller GO/NO-GO kararının kesinleştirilmesi

---

## 🚀 Önerilen Sıradaki Öncelik

1. **Burrow zemin kısıtlaması** — tag ekleme + `canStart` kontrolü (küçük, hızlı kapanır)
2. Kalan V1 animasyon kliplerinin tamamlanması
3. Faz 0 Definition of Done tam kontrolü
4. Faz 1 planlaması
5. IK ayak sistemi — gerçek model/rig geldiğinde devreye alınacak (şu an bloklanmış değil, bekliyor)

---

## 📎 Referans Belgeler
1. `PFD_Journey_of_a_Baby_Sea_Turtle.docx`
2. `GDD_v0.1_Draft.docx`
3. `Unity_Mimari_ve_Klasor_Yapisi_v0.1.docx`
4. `Faz0_Uretim_Yol_Haritasi.docx`
5. `Faz0_Checklist_Notion.md`
6. `Animasyon_State_Listesi_v1.md`
7. `PFD_Madde14_Revizyon.docx`
