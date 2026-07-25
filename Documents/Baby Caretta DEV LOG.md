**İlk Hafta (10 Temmuz 2026\)** 

**Mekanik eklemeler**

* WASD (tank kontrolü) ile hareket sistemi eklendi — W/S ileri-geri, A/D dönüş  
* Idle / Walk / Turn animatör stateleri ve geçişleri hazırlandı  
* Zıplama (Hop) özelliği eklendi — Space tuşu, animasyon süresine bağlı ileri hareket  
* Kabuk (Shell) sistemi eklendi — Q ile kabuğa girme/çıkma (toggle)  
* Kum kazma sistemi eklendi — E tuşu: kısa basış Dig, basılı tutma Burrow  
* Mouth/Body etkileşim sistemi (fare sol/sağ tık, tap & hold) InteractionController üzerinden bağlandı  
* Yerçekimi ve zemine yapışma sistemi eklendi  
* Yokuş/eğim sistemi eklendi: eğime göre yürüme hızının yavaşlaması ve belirli açı üstünde otomatik kayma (Slide) başlaması  
* Kabuk kayması (ShellSlide) eklendi: Q ile kabuktayken dik zeminde kayma, yan yönde (A/D) ağırlık etkisi  
* Yokuş yukarı algılama hatası düzeltildi — yavaşlama/kayma tetikleyicisi artık sadece gerçekten tırmanırken çalışıyor, aşağı inerken yanlışlıkla tetiklenmiyor  
* Q toggle mantığı düzeltildi — kayma artık basılı tutma değil tekrar basmayla başlıyor/duruyor; kabuktan çıkış hem dururken hem kayarken çalışıyor  
* Yürüme kayması ve kabuk kayması ayarları birbirinden bağımsız parametrelere ayrıldı  
* Kabuk kaymasına başlarken küçük bir "itiliş" hızı eklendi  
* Kayma yönü için zemin normali yumuşatma sistemi eklendi — titreşimi azaltmak için

**18 Temmuz 2026** 

**Hareket iyileştirmeler**

* Yokuş yukarı kayarken karakterin kayma yönüne dönmesi kaldırıldı — artık sadece pozisyon kayıyor, bakış yönü sabit kalıyor  
* Gövdenin zemin eğimine göre otomatik tilt (pitch/roll) alması eklendi — yaw'a dokunmadan, ileride eklenecek IK ayak sistemine temel oluşturacak şekilde  
* Zemin normali için ayrı bir yumuşatma katmanı eklendi — köşeli/engebeli objeler üzerinden geçişte ani sıçramaları önlemek için

19 - 22 Temmuz

* MLB basılı tutarak obje taşıma eklendi.
* E ye basınca kum atma hareketine etkileşim alanı eklendi ve IStunnable scripti ile etkileşim eklendi. inspectordan kum atma çapı ve açısı stun süresi eklendi
* Ayakların yere temasını sağlamak için Two bone IK Contraint ve scripti eklendi ama kemikler uygun olmadığı için doğru çalışmıyor şu an
* Smooth hızlanma ve yavaşlama eklendi
* E'ye kısa basış (Dig) kum particle'ı: Hayır, henüz eklemedik. TurtleController'da şu an sadece ApplySandStunEffect() çağrısı var, particle tetikleme kodu yok.
* E'ye uzun basış (Burrow) — hem IsHidden hem gömülme particle'ı: IsHidden bayrağı zaten eklendi (bir önceki mesajda), ama gömülme particle'ı henüz yok.