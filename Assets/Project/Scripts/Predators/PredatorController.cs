using UnityEngine;
using Game.Character;
using Game.Interaction;

namespace Game.Predators
{
    /// <summary>
    /// Her predator (yengeç vb.) prefabına eklenecek temel bileşen. Bu Milestone'da (Teknik
    /// Temel) HENÜZ hiçbir AI/hareket mantığı yok - sadece TurtlePredatorTarget ile konuşmak
    /// için gereken referans ve event altyapısı kuruluyor. Gerçek davranış (tespit, yaklaşma,
    /// yakalama kararı) Unity Behavior ile bir sonraki adımda bu sınıfın çağıracağı custom
    /// Action/Condition node'lar üzerinden gelecek.
    /// </summary>
    public class PredatorController : MonoBehaviour, IStunnable
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

        // Yakalama anında atanan bacak referansı - Hold Until Released node'u buraya
        // kilitlenip yengeci o bacakta "yapışık" tutacak.
        public Transform GrabPoint { get; private set; }

        // Prefab'ın sahneye yerleştirildiği konum - "vazgeçip eve dön" davranışı için.
        public Vector3 SpawnPosition { get; private set; }

        // Kum atma (E) konisine girince true olur - Behavior graph bu süre boyunca
        // hiçbir şey yapmadan donacak (yeni bir Guard ile kontrol edilecek).
        public bool IsStunned { get; private set; }
        private float stunTimer;

        private void Awake()
        {
            SpawnPosition = transform.position;

            if (target == null)
            {
                target = FindFirstObjectByType<TurtlePredatorTarget>();
            }
        }

        private void Update()
        {
            if (!IsStunned) return;

            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                stunTimer = 0f;
                IsStunned = false;
                Debug.Log($"[{name}] Stun finished. IsStunned set to false.");
            }
        }

        /// <summary>IStunnable - kum atma konisi buna girip çağırır.</summary>
        public void Stun(float duration)
        {
            stunTimer = Mathf.Max(stunTimer, duration);
            IsStunned = true;
            Debug.Log($"[{name}] Stunned for {duration}s. IsStunned = true, stunTimer = {stunTimer}");
            // If currently holding the turtle, release it when stunned
            if (IsRestrainingTurtle)
            {
                ReleaseTurtle();
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
            if (target.BeginRestrain(this, data, transform.position, out Transform grabPoint))
            {
                IsRestrainingTurtle = true;
                GrabPoint = grabPoint;
            }
        }

        /// <summary>Bu predator'ın kendi isteğiyle bırakması gerektiğinde (ör. AI kararıyla) çağrılır.</summary>
        public void ReleaseTurtle()
        {
            if (target == null || !IsRestrainingTurtle) return;
            target.EndRestrain(this);
            IsRestrainingTurtle = false;
            GrabPoint = null;
        }

        // NOT: Kaçış artık sadece BİR predator'ı serbest bırakıyor (TurtlePredatorTarget kendi
        // içinde hangisini bırakacağını seçip zaten EndRestrain'i çağırıyor). Buradaki tek işimiz,
        // bırakılan requester GERÇEKTEN BİZSEK kendi yerel bayraklarımızı temizlemek - EndRestrain'i
        // TEKRAR çağırmıyoruz (zaten çağrıldı), sadece kendi durumumuzu güncelliyoruz.
        private void HandleEscapeSucceeded(Component releasedRequester)
        {
            if (releasedRequester != (Component)this) return;
            IsRestrainingTurtle = false;
            GrabPoint = null;
        }

        private void HandleCaptureRequested()
        {
            // NOT: Checkpoint/respawn tetikleme mantığı henüz bu Milestone'un kapsamında değil -
            // mevcut checkpoint sistemi bu event'i dinleyip kendi tarafında ele alacak.
            // ÖNEMLİ: EndRestrain'i MUTLAKA çağırmalıyız, yoksa TurtlePredatorTarget bizi hâlâ
            // "tutuyor" sanmaya devam eder - kaplumbağa hiç normale dönmez, biz de tekrar
            // yakalamaya çalışınca reddedilir.
            if (IsRestrainingTurtle)
            {
                target.EndRestrain(this);
            }
            IsRestrainingTurtle = false;
            GrabPoint = null;
        }
    }
}
