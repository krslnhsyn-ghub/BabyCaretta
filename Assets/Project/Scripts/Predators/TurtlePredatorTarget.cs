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

        // Requester (genelde ileride PredatorController) -> o predator'ın slowdownRatio'su.
        // Birden fazla predator tutuyorsa (stackable), hepsinin çarpanı çarpılarak birleşir.
        private readonly Dictionary<Component, float> activeRestrainers = new Dictionary<Component, float>();

        // Basitlik için V1'de: escape zamanlama ayarları (min/max aralık) en son tutan
        // predator'ın PredatorData'sından okunur. Birden fazla farklı tipte predator aynı anda
        // tutarsa (örn. bir yengeç + ileride farklı bir tip), bu basitleştirme yeterli;
        // gerekirse ileride her requester'ın kendi escape'ini ayrı saymaya genişletilebilir.
        private PredatorData escapeSettingsSource;

        private float escapeFirstPressTime = -1f;
        private float captureTimer = -1f;

        public int ActiveRestrainerCount => activeRestrainers.Count;
        public bool IsCurrentlyRestrained => activeRestrainers.Count > 0;

        /// <summary>Kurtulma başarılı olduğunda fırlatılır - hangi predator'ın bırakılacağına çağıran taraf karar verir.</summary>
        public event Action OnEscapeSucceeded;
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

            if (captureTimer >= 0f)
            {
                captureTimer -= Time.deltaTime;
                if (captureTimer <= 0f)
                {
                    captureTimer = -1f;
                    OnCaptureRequested?.Invoke();
                }
            }
        }

        /// <summary>
        /// Yakalama noktası ister (rastgele bir bacağın gerçek kemik referansı). TurtleFootIK
        /// yoksa veya bacak dizisi boşsa null döner - çağıran taraf null kontrolü yapmalı.
        /// </summary>
        public Transform RequestGrabPoint()
        {
            return footIK != null ? footIK.GetRandomFootTransform() : null;
        }

        /// <summary>
        /// Bir predator kaplumbağayı tutmaya başladığında çağrılır. Aynı requester ikinci kez
        /// çağırırsa yok sayılır. Stackable olmayan bir predator zaten tutuyorken yeni bir
        /// tutma isteği (V1 basit kuralı) reddedilir.
        /// </summary>
        public bool BeginRestrain(Component requester, PredatorData data)
        {
            if (requester == null || data == null) return false;
            if (activeRestrainers.ContainsKey(requester)) return false;
            if (!data.stackable && activeRestrainers.Count > 0) return false;

            activeRestrainers[requester] = data.slowdownRatio;
            escapeSettingsSource = data;
            captureTimer = data.captureDelay;

            RecalculateSpeedMultiplier();
            turtleController.IsRestrained = true;
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
                captureTimer = -1f;
                escapeFirstPressTime = -1f;
                escapeSettingsSource = null;
            }
            else
            {
                RecalculateSpeedMultiplier();
            }
        }

        private void RecalculateSpeedMultiplier()
        {
            float combined = 1f;
            foreach (float ratio in activeRestrainers.Values)
            {
                combined *= ratio;
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
                OnEscapeSucceeded?.Invoke();
            }
            else
            {
                OnEscapeFailed?.Invoke();
            }
        }
    }
}
