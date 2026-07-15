// ============================================================
// EdibleItem.cs
// ------------------------------------------------------------
// Ne işe yarar:
//   Mouth (LMB) ile etkileşime giren Faz 0 test nesnesi.
//   Tap (kısa basış) = Isır/Ye (nesne sahneden kaybolur)
//   Hold (basılı tutma) = Tut (HoldStart) ... Bırak (HoldEnd)
//   Gerçek taşıma/carry animasyonu ve elde tutma pozisyonu henüz yok —
//   amaç Hold sinyalinin uçtan uca doğru çalıştığını doğrulamak.
//
// İçerdiği fonksiyonlar:
//   - CanInteract(context) : sadece Mouth organıyla etkileşime izin verir
//   - OnInteract(context)  : context.Phase'e göre Ye / Tut / Bırak tepkisi verir
// ============================================================
using UnityEngine;

namespace Game.Interaction
{
    public class EdibleItem : MonoBehaviour, IInteractable
    {
        // Şimdilik sadece Mouth (LMB) ile etkileşime giriyor.
        public bool CanInteract(InteractionContext context)
        {
            return context.Organ == InteractionOrgan.Mouth;
        }

        public void OnInteract(InteractionContext context)
        {
            switch (context.Phase)
            {
                case InteractionPhase.Tap:
                    Debug.Log($"{gameObject.name}: Tap -> yendi.");
                    gameObject.SetActive(false); // Faz 0 test amaçlı basit tepki
                    break;

                case InteractionPhase.HoldStart:
                    Debug.Log($"{gameObject.name}: ağza alındı (tutmaya başlandı).");
                    break;

                case InteractionPhase.HoldEnd:
                    Debug.Log($"{gameObject.name}: bırakıldı.");
                    break;

                    // HoldTick şimdilik sessiz — gerçek Carry mantığı (elde taşıma
                    // pozisyonu, Upper Body Layer) ileride burada tetiklenecek.
            }
        }
    }
}