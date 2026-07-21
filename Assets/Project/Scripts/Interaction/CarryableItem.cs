// ============================================================
// CarryableItem.cs
// ------------------------------------------------------------
// Ne işe yarar:
//   Mouth (LMB) ile Hold edilip taşınabilen nesne. Tap davranışı
//   yok, sadece taşınıyor. Obje ağız noktasına parent'lanıyor ve
//   kinematic yapılıyor (gerçek fizik çarpışması yok, stabil).
//   Kendi kendine KARAR VERMEZ — sadece "şu an bir engelin
//   içindeyim" bilgisini IsObstructed üzerinden dışarı açar.
//   Taşımayı sonlandırma kararı InteractionController'a ait.
// ============================================================
using UnityEngine;

namespace Game.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class CarryableItem : MonoBehaviour, IInteractable
    {
        [Header("Engel Kontrolü")]
        [SerializeField] private float obstacleCheckRadius = 0.15f;
        [SerializeField] private LayerMask obstacleMask; // Inspector'dan "Ground", "Wall" vb. seçilecek

        private Rigidbody rb;
        private Collider col;
        private Transform originalParent;
        private bool isBeingCarried;

        // InteractionController bu property'yi her karede okuyup karar veriyor.
        public bool IsObstructed { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public bool CanInteract(InteractionContext context)
        {
            return context.Organ == InteractionOrgan.Mouth;
        }

        public void OnInteract(InteractionContext context)
        {
            switch (context.Phase)
            {
                case InteractionPhase.HoldStart:
                    BeginCarry(context);
                    break;

                case InteractionPhase.HoldEnd:
                    EndCarry();
                    break;
            }
        }

        private void BeginCarry(InteractionContext context)
        {
            if (context.AttachPoint == null)
            {
                Debug.LogWarning($"{gameObject.name}: AttachPoint null, taşıma başlatılamadı.");
                return;
            }

            originalParent = transform.parent;
            transform.SetParent(context.AttachPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            rb.isKinematic = true;
            col.enabled = false;

            isBeingCarried = true;
            IsObstructed = false;
        }

        private void EndCarry()
        {
            transform.SetParent(originalParent);
            rb.isKinematic = false;
            col.enabled = true;

            isBeingCarried = false;
            IsObstructed = false;
        }

        private void Update()
        {
            if (!isBeingCarried) return;

            IsObstructed = Physics.CheckSphere(transform.position, obstacleCheckRadius, obstacleMask);
        }
    }
}