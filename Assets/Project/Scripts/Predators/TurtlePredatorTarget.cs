using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Character;

namespace Game.Predators
{
    /// <summary>
    /// Kaplumbağa üzerine eklenen component. Predator'ların (yengeç vb.) kaplumbağa ile
    /// etkileşime geçtiği TEK nokta burasıdır - predator'lar TurtleController'a doğrudan
    /// erişmez, hep bu component üzerinden konuşur.
    ///
    /// Sorumlulukları:
    ///   - Birden fazla predator'ın aynı anda tutması (stack) durumunu yönetmek
    ///   - TurtleController.IsRestrained / RestrainedSpeedMultiplier'ı güncellemek
    ///   - Kaçış (escape) input'unu (E tuşu, çift basış aralığı) okumak
    ///   - Kurtulamama durumunda yakalanma (capture) event'ini fırlatmak
    ///   - Yakalama noktası (bacak) istendiğinde TurtleFootIK'tan rastgele bir referans vermek
    ///
    /// NOT: Bu sınıf herhangi bir predator AI mantığı BİLMEZ - sadece "şu an kaç predator
    /// tutuyor, tutma noktası nerede, kurtulma başarılı mı" sorularına cevap verir.
    /// </summary>
    [RequireComponent(typeof(TurtleController))]
    public class TurtlePredatorTarget : MonoBehaviour
    {
        private TurtleController turtleController;
        private TurtleFootIK footIK;

        // Bir requester'ın (predator) tutuş bilgisi: ne kadar yavaşlattığı + hangi bacağı tuttuğu.
        private struct RestrainEntry
        {
            public float SlowdownRatio;
            public Transform GrabLeg;
        }

        // Requester (genelde PredatorController) -> tutuş bilgisi. Birden fazla predator
        // tutuyorsa (stackable), hepsinin çarpanı çarpılarak birleşir; her biri farklı bir
        // bacağa atanmaya çalışılır (mümkünse aynı bacağı iki predator paylaşmaz).
        private readonly Dictionary<Component, RestrainEntry> activeRestrainers = new Dictionary<Component, RestrainEntry>();

        // Basitlik için V1'de: escape zamanlama ayarları (min/max aralık) en son tutan
        // predator'ın PredatorData'sından okunur. Birden fazla farklı tipte predator aynı anda
        // tutarsa (örn. bir yengeç + ileride farklı bir tip), bu basitleştirme yeterli;
        // gerekirse ileride her requester'ın kendi escape'ini ayrı saymaya genişletilebilir.
        private PredatorData escapeSettingsSource;

        private float escapeFirstPressTime = -1f;
        private float captureTimer = -1f;

        public int ActiveRestrainerCount => activeRestrainers.Count;
        public bool IsCurrentlyRestrained => activeRestrainers.Count > 0;

        /// <summary>
        /// Kaplumbağa şu an yakalanabilir mi? Gömülüyken (Burrow, IsHidden) veya kabuktayken
        /// (ShellEnter/ShellIdle/ShellExit) predator'lar onu ne görebilir ne yakalayabilir.
        /// </summary>
        /// <summary>Predator'ların "görebiliyor muyum" kontrolü için passthrough (Burrow/IsHidden).</summary>
        public bool IsHidden => turtleController.IsHidden;

        public bool IsVulnerable
        {
            get
            {
                if (turtleController.IsHidden) return false;
                CharacterState state = turtleController.CurrentState;
                return state != CharacterState.ShellEnter &&
                       state != CharacterState.ShellIdle &&
                       state != CharacterState.ShellExit &&
                       state != CharacterState.ShellSlide;
            }
        }

        /// <summary>Kurtulma başarılı olduğunda fırlatılır. Parametre, TAM OLARAK hangi predator'ın
        /// bırakıldığını bildirir (biz burada zaten SADECE BİRİNİ bıraktık) - dinleyenler kendi
        /// requester'ları bu mu diye kontrol etmeli.</summary>
        public event Action<Component> OnEscapeSucceeded;
        /// <summary>Yanlış zamanlamayla basıldığında fırlatılır (çok hızlı ya da çok yavaş).</summary>
        public event Action OnEscapeFailed;
        /// <summary>Süre dolup kurtulamayınca fırlatılır - checkpoint sistemini tetiklemek isteyen taraf bunu dinler.</summary>
        public event Action OnCaptureRequested;

        private void Awake()
        {
            turtleController = GetComponent<TurtleController>();
            footIK = GetComponent<TurtleFootIK>();
        }

        private void Update()
        {
            if (activeRestrainers.Count == 0) return;

            ReadEscapeInput();

            // Capture tehdidi SADECE bu predator tipinin gerektirdiği stack sayısına
            // ulaşıldığında geçerli olur (örn. küçük yengeç tek başına asla yakalayamaz,
            // 3'ü birden tutunca sayaç işlemeye başlar). Eşik altına düşülürse sayaç durur.
            bool captureThreatActive = escapeSettingsSource != null &&
                                        activeRestrainers.Count >= escapeSettingsSource.requiredStackToCapture;

            if (!captureThreatActive)
            {
                captureTimer = -1f;
                return;
            }

            if (captureTimer < 0f)
            {
                captureTimer = escapeSettingsSource.captureDelay;
            }

            captureTimer -= Time.deltaTime;
            if (captureTimer <= 0f)
            {
                captureTimer = -1f;
                OnCaptureRequested?.Invoke();
            }
        }

        /// <summary>
        /// Verilen konuma (genelde predator'ın kendi pozisyonu) en yakın, başka bir
        /// predator tarafından tutulmayan (boş) bacağı bulur. Hepsi doluysa en yakın
        /// dolu bacağı paylaşımlı döndürür (null yerine, nadir bir durum).
        /// </summary>
        private Transform PickNearestFreeLeg(Vector3 fromPosition)
        {
            if (footIK == null) return null;

            var occupiedLegs = new HashSet<Transform>();
            foreach (RestrainEntry entry in activeRestrainers.Values)
            {
                if (entry.GrabLeg != null) occupiedLegs.Add(entry.GrabLeg);
            }

            Transform[] allLegs = footIK.GetAllFootTransforms();
            Transform bestFree = null;
            Transform bestAny = null;
            float bestFreeDistSqr = float.MaxValue;
            float bestAnyDistSqr = float.MaxValue;

            foreach (Transform leg in allLegs)
            {
                if (leg == null) continue;
                float distSqr = (leg.position - fromPosition).sqrMagnitude;

                if (distSqr < bestAnyDistSqr)
                {
                    bestAnyDistSqr = distSqr;
                    bestAny = leg;
                }

                if (!occupiedLegs.Contains(leg) && distSqr < bestFreeDistSqr)
                {
                    bestFreeDistSqr = distSqr;
                    bestFree = leg;
                }
            }

            return bestFree != null ? bestFree : bestAny;
        }

        /// <summary>
        /// Predator'ların (örn. Approach Target node'u) yaklaşırken hedefleyeceği en yakın
        /// boş bacağı sormasını sağlar - herhangi bir rezervasyon YAPMAZ, sadece bakar.
        /// </summary>
        public Transform PeekNearestFreeLeg(Vector3 fromPosition)
        {
            return PickNearestFreeLeg(fromPosition);
        }

        /// <summary>
        /// Bir predator kaplumbağayı tutmaya başladığında çağrılır. Aynı requester ikinci kez
        /// çağırırsa yok sayılır. Stackable olmayan bir predator zaten tutuyorken yeni bir
        /// tutma isteği (V1 basit kuralı) reddedilir.
        /// </summary>
        public bool BeginRestrain(Component requester, PredatorData data, Vector3 requesterPosition, out Transform grabPoint)
        {
            grabPoint = null;
            if (requester == null || data == null)
            {
                Debug.Log($"[Predator-Debug] BeginRestrain REDDEDİLDİ (requester/data null) - requester={requester}, data={data}");
                return false;
            }
            if (activeRestrainers.ContainsKey(requester))
            {
                Debug.Log($"[Predator-Debug] BeginRestrain REDDEDİLDİ (zaten bu requester tutuyor) - requester={requester.name}");
                return false;
            }
            if (!data.stackable && activeRestrainers.Count > 0)
            {
                Debug.Log($"[Predator-Debug] BeginRestrain REDDEDİLDİ (stackable={data.stackable}, zaten {activeRestrainers.Count} tutan var) - requester={requester.name}");
                return false;
            }
            if (!IsVulnerable)
            {
                Debug.Log($"[Predator-Debug] BeginRestrain REDDEDİLDİ (kaplumbağa Hidden/Shell'de, yakalanamaz) - requester={requester.name}");
                return false;
            }

            grabPoint = PickNearestFreeLeg(requesterPosition);

            activeRestrainers[requester] = new RestrainEntry
            {
                SlowdownRatio = data.slowdownRatio,
                GrabLeg = grabPoint
            };
            escapeSettingsSource = data;

            RecalculateSpeedMultiplier();
            turtleController.IsRestrained = true;

            Debug.Log($"[Predator-Debug] BeginRestrain BAŞARILI - requester={requester.name}, bacak={(grabPoint != null ? grabPoint.name : "YOK")}, toplam tutan={activeRestrainers.Count}, stackable={data.stackable}");
            return true;
        }

        /// <summary>Bir predator kaplumbağayı bıraktığında (kaçış başarılı, yem olma, vs.) çağrılır.</summary>
        public void EndRestrain(Component requester)
        {
            if (requester == null || !activeRestrainers.Remove(requester)) return;

            if (activeRestrainers.Count == 0)
            {
                turtleController.IsRestrained = false;
                turtleController.RestrainedSpeedMultiplier = 1f;
                turtleController.SuppressNextSandInput = true;
                captureTimer = -1f;
                escapeFirstPressTime = -1f;
                escapeSettingsSource = null;
            }
            else
            {
                RecalculateSpeedMultiplier();
            }
        }

        /// <summary>
        /// Aktif tutuculardan BİRİNİ (hangisi olduğu önemli değil, ilk bulunan) serbest bırakır
        /// ve mevcut EndRestrain temizleme mantığını (multiplier yeniden hesaplama, tamamen
        /// boşaldıysa flag/timer sıfırlama) yeniden kullanır.
        /// </summary>
        private Component ReleaseOneRestrainer()
        {
            if (activeRestrainers.Count == 0) return null;

            Component toRelease = null;
            foreach (Component key in activeRestrainers.Keys)
            {
                toRelease = key;
                break;
            }

            if (toRelease != null)
            {
                EndRestrain(toRelease);
            }

            return toRelease;
        }

        private void RecalculateSpeedMultiplier()
        {
            float combined = 1f;
            foreach (RestrainEntry entry in activeRestrainers.Values)
            {
                combined *= entry.SlowdownRatio;
            }
            turtleController.RestrainedSpeedMultiplier = combined;
        }

        /// <summary>
        /// Yengeç kaçışı: E tuşuna iki kez basış arasındaki süre PredatorData'daki
        /// escapeIntervalMin/Max aralığına düşerse başarılı sayılır (ne çok hızlı ne çok yavaş).
        /// </summary>
        private void ReadEscapeInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || escapeSettingsSource == null) return;
            if (!keyboard.eKey.wasPressedThisFrame) return;

            if (escapeFirstPressTime < 0f)
            {
                escapeFirstPressTime = Time.time;
                return;
            }

            float gap = Time.time - escapeFirstPressTime;
            escapeFirstPressTime = -1f;

            if (gap >= escapeSettingsSource.escapeIntervalMin && gap <= escapeSettingsSource.escapeIntervalMax)
            {
                // Hepsi değil, SADECE BİR predator bırakılır - stack birer birer azalır.
                Component released = ReleaseOneRestrainer();
                OnEscapeSucceeded?.Invoke(released);
            }
            else
            {
                OnEscapeFailed?.Invoke();
            }
        }
    }
}
