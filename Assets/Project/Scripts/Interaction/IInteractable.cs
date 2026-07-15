// ============================================================
// IInteractable.cs
// ------------------------------------------------------------
// Ne işe yarar:
//   Kaplumbağanın etkileşime girebileceği her nesnenin (kaya, balık,
//   arkadaş...) uyması gereken ortak sözleşme. Her nesne kendi
//   tepkisine kendi karar verir — InteractionController sadece
//   "en yakın nesneyi bul, bu arayüz üzerinden konuş" der, nesnenin
//   ne yapacağına hiç karışmaz.
//
// İçerdiği tipler/fonksiyonlar:
//   - InteractionOrgan          : hangi "organ" ile etkileşime girildiği (Mouth/Body)
//   - InteractionPhase          : Tap (kısa basış) / HoldStart / HoldTick / HoldEnd
//   - InteractionContext        : etkileşimi başlatan obje + organ + faz bilgisi
//   - CanInteract(context)      : bu an, bu nesneyle etkileşim mümkün mü
//   - OnInteract(context)       : etkileşim tetiklendiğinde çalışır — nesne, context.Phase'e
//                                 göre kendi içinde switch yapıp farklı tepki verebilir
// ============================================================
using UnityEngine;

namespace Game.Interaction
{
    // LMB = Mouth, RMB = Body (bkz. tuş tablosu kararı)
    public enum InteractionOrgan
    {
        Mouth,
        Body,
    }

    // Tap: kısa basış, tek seferlik (ör. Isırma, Kafa Atma)
    // HoldStart: basılı tutma eşiği aşıldığında bir kere
    // HoldTick: basılı tutma devam ederken her karede
    // HoldEnd: bırakılınca bir kere
    public enum InteractionPhase
    {
        Tap,
        HoldStart,
        HoldTick,
        HoldEnd,
    }

    public struct InteractionContext
    {
        public GameObject Initiator;
        public InteractionOrgan Organ;
        public InteractionPhase Phase;
    }

    public interface IInteractable
    {
        bool CanInteract(InteractionContext context);
        void OnInteract(InteractionContext context);
    }
}