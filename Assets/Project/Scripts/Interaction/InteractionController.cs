// ============================================================
// InteractionController.cs
// ------------------------------------------------------------
// Ne işe yarar:
//   Kaplumbağanın "el/ağız/gövde" olarak DIŞ dünyayla (IInteractable
//   nesnelerle) konuştuğu katman. Belirli bir yarıçap VE görüş açısı
//   içinde en yakın IInteractable'ı bulur, hedefe döner, context'i
//   ona iletir. Ayrıca kaplumbağanın KENDİ animasyonunu (Headbutt/
//   Push/Bite/Carry) tetikler — bu animasyonlar hedef nesneden
//   bağımsızdır, bu yüzden PushableItem/EdibleItem gibi nesnelerde
//   değil, burada yaşar.
//   Basış süresine göre Tap (kısa basış) ile Hold (basılı tutma)
//   arasında ayrım yapar. KARAR VERMEZ — nesneye ne olacağına
//   ilgili IInteractable (ör. PushableItem) karar verir.
//
//   Not: Kabuk (Q) ve Kum (E) mekanikleri BURADA DEĞİL — onlar dış
//   nesne aramayan, kaplumbağanın kendi üzerinde olan aksiyonlar
//   (self-action), TurtleController'ın kendi içinde yaşıyorlar.
//
//   Akış: Input -> InteractionController -> Nearest Interactable -> Object decides
//
// İçerdiği fonksiyonlar:
//   - UpdateOrganInput(organ, isPressed, wasPressed, wasReleased)
//       : TurtleController'ın her karede çağırdığı ana giriş noktası.
//         Tap/HoldStart/HoldTick/HoldEnd fazlarına karar verir.
//   - PlayOrganAnimation(organ, phase) : organ+phase'e göre kaplumbağanın kendi Animator'ını tetikler
//   - TryTap() / BeginHold() / SendPhase() : Tap/Hold akışının parçaları
//   - FindNearestInteractable() : yarıçap + açı içindeki en yakın uygun IInteractable'ı bulur
//   - IsStillInRange()          : Hold sırasında nesne hâlâ menzilde mi kontrol eder
//   - FaceTarget(position)      : karakteri hedefe anında döndürür
//
// Not: Box/Cone collider yerine "OverlapSphere + açı filtresi" kullanıyoruz —
// aynı "koni" etkisini verir ama yeni bir geometri türü öğrenmeye gerek kalmaz.
//
// Not (Carry/Attach): mouthAttachPoint, sadece Mouth organı için context'e
// dolduruluyor (context.AttachPoint). Body organının şu an bir attach
// ihtiyacı yok, bu yüzden Body için AttachPoint her zaman null geçiyor.
//
// Not (Engel Kontrolü): HoldTick sırasında, tutulan nesne bir CarryableItem
// ise onun IsObstructed durumu da kontrol edilir — menzil dışına çıkma ile
// aynı yoldan (HoldEnd) sonlanır. Karar burada verilir, CarryableItem sadece
// durumunu bildirir (sorumluluk ayrımı korunur).
// ============================================================
using UnityEngine;

namespace Game.Interaction
{
    [RequireComponent(typeof(Animator))]
    public class InteractionController : MonoBehaviour
    {
        [Header("Etkileşim Ayarları")]
        [SerializeField] private float interactionRadius = 1.5f;
        [SerializeField] private float interactionAngle = 120f; // derece, görüş açısı (koni yerine)
        [SerializeField] private LayerMask interactableMask = ~0; // varsayılan: tüm layer'lar

        [Header("Tap / Hold Ayarı")]
        [SerializeField] private float holdThreshold = 0.2f; // bu süreden kısa basışlar Tap, uzun basışlar Hold sayılır

        [Header("Attach Noktaları")]
        [SerializeField] private Transform mouthAttachPoint;

        private Animator animator;
        // UpperBody Layer
        [SerializeField] private string upperBodyLayerName = "UpperBody_Mouth";
        [SerializeField] private float upperBodyBlendSpeed = 8f;

        private int upperBodyLayerIndex;
        private float upperBodyTargetWeight;

        // Mouth ve Body için ayrı ayrı takip edilmesi gereken küçük bir durum grubu.
        // İkisi de aynı şekle sahip olduğu için tek bir iç sınıfta topladık (kod tekrarını önlemek için).
        private class OrganState
        {
            public float pressStartTime;
            public bool awaitingDecision; // basıldı ama henüz Tap mı Hold mu belli değil
            public bool isHolding;
            public IInteractable heldTarget;
            public Transform heldTargetTransform; // mesafe/açı kontrolü için
        }

        private readonly OrganState mouthState = new OrganState();
        private readonly OrganState bodyState = new OrganState();

        private void Awake()
        {
            animator = GetComponent<Animator>();
            upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName);

            if (upperBodyLayerIndex == -1)
            {
                Debug.LogError($"Animator Layer bulunamadı: {upperBodyLayerName}");
            }
        }

        private void Update()
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex);

            upperBodyTargetWeight = state.IsTag("UpperBody") ? 1f : 0f;

            float currentWeight = animator.GetLayerWeight(upperBodyLayerIndex);

            animator.SetLayerWeight(
                upperBodyLayerIndex,
                Mathf.MoveTowards(
                    currentWeight,
                    upperBodyTargetWeight,
                    upperBodyBlendSpeed * Time.deltaTime));
        }

        // TurtleController her karede, her organ için bu fonksiyonu çağırır.
        public void UpdateOrganInput(InteractionOrgan organ, bool isPressed, bool wasPressedThisFrame, bool wasReleasedThisFrame)
        {
            OrganState state = organ == InteractionOrgan.Mouth ? mouthState : bodyState;

            if (wasPressedThisFrame)
            {
                state.pressStartTime = Time.time;
                state.awaitingDecision = true;
            }

            // Basılı tutma eşiği yeni aşıldıysa: HoldStart bir kere tetiklenir.
            if (state.awaitingDecision && isPressed && Time.time - state.pressStartTime >= holdThreshold)
            {
                state.awaitingDecision = false;
                BeginHold(organ, state);
            }
            // Zaten Hold modundaysak: nesne hâlâ menzildeyse VE engelde değilse HoldTick,
            // değilse tutma otomatik biter.
            else if (state.isHolding)
            {
                var carryable = state.heldTarget as CarryableItem;
                bool obstructed = carryable != null && carryable.IsObstructed;

                if (IsStillInRange(state.heldTargetTransform) && !obstructed)
                {
                    SendPhase(state.heldTarget, organ, InteractionPhase.HoldTick);
                }
                else
                {
                    SendPhase(state.heldTarget, organ, InteractionPhase.HoldEnd);
                    PlayOrganAnimation(organ, InteractionPhase.HoldEnd);
                    state.isHolding = false;
                    state.heldTarget = null;
                    state.heldTargetTransform = null;
                }
            }

            if (wasReleasedThisFrame)
            {
                if (state.isHolding)
                {
                    SendPhase(state.heldTarget, organ, InteractionPhase.HoldEnd);
                    PlayOrganAnimation(organ, InteractionPhase.HoldEnd);
                    state.isHolding = false;
                    state.heldTarget = null;
                    state.heldTargetTransform = null;
                }
                else if (state.awaitingDecision)
                {
                    // Eşiğe ulaşmadan bırakıldı -> kısa basış (Tap)
                    TryTap(organ);
                }

                state.awaitingDecision = false;
            }
        }

        // Organ + faz kombinasyonuna göre kaplumbağanın KENDİ animasyonunu tetikler.
        // Hedef nesneden bağımsızdır — neye vurursanız vurun, Headbutt hep aynı hareket.
        private void PlayOrganAnimation(InteractionOrgan organ, InteractionPhase phase)
        {
            if (animator == null) return;

            if (organ == InteractionOrgan.Body)
            {
                switch (phase)
                {
                    case InteractionPhase.Tap: animator.SetTrigger("Headbutt"); break;
                    case InteractionPhase.HoldStart: animator.SetBool("IsPushing", true); break;
                    case InteractionPhase.HoldEnd: animator.SetBool("IsPushing", false); break;
                }
            }
            else // Mouth
            {
                switch (phase)
                {
                    case InteractionPhase.Tap: animator.SetTrigger("Bite"); break;
                    case InteractionPhase.HoldStart: animator.SetBool("IsCarrying", true); break;
                    case InteractionPhase.HoldEnd: animator.SetBool("IsCarrying", false); break;
                }
            }
        }

        private void TryTap(InteractionOrgan organ)
        {
            var context = new InteractionContext
            {
                Initiator = gameObject,
                Organ = organ,
                Phase = InteractionPhase.Tap,
                AttachPoint = organ == InteractionOrgan.Mouth ? mouthAttachPoint : null
            };
            var (target, targetTransform) = FindNearestInteractable(context);
            if (target == null) return;

            FaceTarget(targetTransform.position);
            PlayOrganAnimation(organ, InteractionPhase.Tap);
            target.OnInteract(context);
        }

        private void BeginHold(InteractionOrgan organ, OrganState state)
        {
            var context = new InteractionContext
            {
                Initiator = gameObject,
                Organ = organ,
                Phase = InteractionPhase.HoldStart,
                AttachPoint = organ == InteractionOrgan.Mouth ? mouthAttachPoint : null
            };
            var (target, targetTransform) = FindNearestInteractable(context);
            if (target == null) return; // tutacak bir şey yoksa Hold hiç başlamaz

            FaceTarget(targetTransform.position);
            PlayOrganAnimation(organ, InteractionPhase.HoldStart);
            state.isHolding = true;
            state.heldTarget = target;
            state.heldTargetTransform = targetTransform;
            target.OnInteract(context);
        }

        private void SendPhase(IInteractable target, InteractionOrgan organ, InteractionPhase phase)
        {
            if (target == null) return;
            var context = new InteractionContext
            {
                Initiator = gameObject,
                Organ = organ,
                Phase = phase,
                AttachPoint = organ == InteractionOrgan.Mouth ? mouthAttachPoint : null
            };
            target.OnInteract(context);
        }

        // Hold sırasında nesnenin hâlâ menzil VE görüş açısı içinde olup olmadığını kontrol eder.
        private bool IsStillInRange(Transform target)
        {
            if (target == null) return false;

            Vector3 toTarget = target.position - transform.position;
            if (toTarget.magnitude > interactionRadius) return false;

            float angle = Vector3.Angle(transform.forward, toTarget);
            return angle <= interactionAngle * 0.5f;
        }

        private (IInteractable interactable, Transform targetTransform) FindNearestInteractable(InteractionContext context)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius, interactableMask);

            IInteractable closest = null;
            Transform closestTransform = null;
            float closestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                IInteractable candidate = hit.GetComponent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(context)) continue;

                Vector3 toCandidate = hit.transform.position - transform.position;

                float angle = Vector3.Angle(transform.forward, toCandidate);
                if (angle > interactionAngle * 0.5f) continue;

                float distance = toCandidate.magnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                    closestTransform = hit.transform;
                }
            }

            return (closest, closestTransform);
        }

        private void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            Gizmos.color = Color.cyan;
            Quaternion leftRot = Quaternion.AngleAxis(-interactionAngle * 0.5f, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(interactionAngle * 0.5f, Vector3.up);
            Gizmos.DrawRay(transform.position, leftRot * transform.forward * interactionRadius);
            Gizmos.DrawRay(transform.position, rightRot * transform.forward * interactionRadius);
        }
    }
}