// ============================================================
// IStunnable.cs
// ------------------------------------------------------------
// Kaplumbağanın kum serpme (E) gibi ALAN etkili aksiyonlarından
// etkilenebilecek her nesnenin uyması gereken basit sözleşme.
// Tepkiye (donma, kaçma animasyonu, ses vb.) nesne kendi karar verir —
// Turtle sadece "şu kadar süre sersemledin" bilgisini iletir.
// ============================================================
namespace Game.Interaction
{
    public interface IStunnable
    {
        void Stun(float duration);
    }
}