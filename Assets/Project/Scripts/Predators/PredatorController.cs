using UnityEngine;
using Game.Character;

namespace Game.Predators
{
    /// <summary>
    /// Her predator (yengeç vb.) prefabına eklenecek temel bileşen. Bu Milestone'da (Teknik
    /// Temel) HENÜZ hiçbir AI/hareket mantığı yok - sadece TurtlePredatorTarget ile konuşmak
    /// için gereken referans ve event altyapısı kuruluyor. Gerçek davranış (tespit, yaklaşma,
    /// yakalama kararı) Unity Behavior ile bir sonraki adımda bu sınıfın çağıracağı custom
    /// Action/Condition node'lar üzerinden gelecek.
    /// </summary>
    public class PredatorController : MonoBehaviour
    {
        [Header("Veri")]
        [SerializeField] private PredatorData data;

        [Header("Hedef (opsiyonel - boşsa Awake'te otomatik bulunur)")]
        [SerializeField] private TurtlePredatorTarget target;

        public PredatorData Data => data;
        public TurtlePredatorTarget Target => target;

        // Şu an bu predator'ın kaplumbağayı tutup tutmadığını dışarıdan (ör. ileride Unity
        // Behavior node'ları) sorgulayabilmek için basit bir bayrak.
        public bool IsRestrainingTurtle { get; private set; }

        private void Awake()
        {
            if (target == null)
            {
                target = FindFirstObjectByType<TurtlePredatorTarget>();
            }
        }

        private void OnEnable()
        {
            if (target != null)
            {
                target.OnEscapeSucceeded += HandleEscapeSucceeded;
                target.OnCaptureRequested += HandleCaptureRequested;
            }
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.OnEscapeSucceeded -= HandleEscapeSucceeded;
                target.OnCaptureRequested -= HandleCaptureRequested;
            }
        }

        /// <summary>
        /// Yakalama mesafesine girildiğinde çağrılır (ileride Unity Behavior Action node'u
        /// bunu tetikleyecek). Aynı anda zaten tutuyorsa tekrar çağırmak zararsız (BeginRestrain
        /// bunu kendi içinde reddediyor).
        /// </summary>
        public void TryGrabTurtle()
        {
            if (target == null || data == null) return;
            if (target.BeginRestrain(this, data))
            {
                IsRestrainingTurtle = true;
            }
        }

        /// <summary>Bu predator'ın kendi isteğiyle bırakması gerektiğinde (ör. AI kararıyla) çağrılır.</summary>
        public void ReleaseTurtle()
        {
            if (target == null || !IsRestrainingTurtle) return;
            target.EndRestrain(this);
            IsRestrainingTurtle = false;
        }

        // NOT: Kaçış başarılı olduğunda TÜM aktif predator'lar bu event'i alır (TurtlePredatorTarget
        // requester ayrımı yapmıyor, genel bir "kurtuldu" event'i). Bu yüzden sadece kendisi
        // gerçekten tutuyorsa bırakır - tutmayan bir predator (örn. henüz yaklaşmamış başka bir
        // yengeç) bundan etkilenmez.
        private void HandleEscapeSucceeded()
        {
            if (!IsRestrainingTurtle) return;
            target.EndRestrain(this);
            IsRestrainingTurtle = false;
        }

        private void HandleCaptureRequested()
        {
            // NOT: Checkpoint/respawn tetikleme mantığı henüz bu Milestone'un kapsamında değil -
            // mevcut checkpoint sistemi bu event'i dinleyip kendi tarafında ele alacak.
            IsRestrainingTurtle = false;
        }
    }
}
