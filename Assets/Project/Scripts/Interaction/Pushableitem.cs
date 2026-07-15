// ============================================================
// PushableItem.cs
// ------------------------------------------------------------
// Ne işe yarar:
//   Body (RMB) ile itilebilen herhangi bir nesne için genel bileşen
//   (kaya, kütük, çöp konteyneri vb.). Faz 0 tuş kararına göre:
//   Tap (kısa basış) = Headbutt (küçük, tek seferlik itiş)
//   Hold (basılı tutma) = Push (basılı tuttukça süren, biriken itiş)
//
// İçerdiği fonksiyonlar:
//   - CanInteract(context) : sadece Body organıyla etkileşime izin verir
//   - OnInteract(context)  : context.Phase'e göre Headbutt ya da Push uygular
//   - ApplyPush()          : itme kuvvetini gerçekten uygulayan ortak yardımcı
// ============================================================
using UnityEngine;

namespace Game.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public class PushableItem : MonoBehaviour, IInteractable
    {
        [Header("Tap = Headbutt")]
        [SerializeField] private float headbuttForce = 2f;

        [Header("Hold = Push (her karede uygulanır)")]
        [SerializeField] private float holdPushForce = 6f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        // Şimdilik sadece Body (RMB) ile etkileşime giriyor.
        public bool CanInteract(InteractionContext context)
        {
            return context.Organ == InteractionOrgan.Body;
        }

        public void OnInteract(InteractionContext context)
        {
            switch (context.Phase)
            {
                case InteractionPhase.Tap:
                    ApplyPush(context, headbuttForce, ForceMode.Impulse);
                    break;

                case InteractionPhase.HoldTick:
                    // Her karede çağrılır, bu yüzden Time.deltaTime ile ölçekliyoruz
                    // (yoksa Push, Headbutt'tan onlarca kat güçlü olurdu).
                    ApplyPush(context, holdPushForce * Time.deltaTime, ForceMode.Impulse);
                    break;

                    // HoldStart ve HoldEnd için şimdilik özel bir tepki yok,
                    // ileride burada "hazırlanma" ya da "durma" animasyonu tetiklenebilir.
            }
        }

        private void ApplyPush(InteractionContext context, float force, ForceMode mode)
        {
            Vector3 pushDirection = transform.position - context.Initiator.transform.position;
            pushDirection.y = 0f;
            rb.AddForce(pushDirection.normalized * force, mode);
        }
    }
}