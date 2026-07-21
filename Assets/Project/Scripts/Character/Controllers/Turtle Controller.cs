using UnityEngine;
using UnityEngine.InputSystem;
using Game.Interaction;

// ============================================================
// TurtleController.cs
// ------------------------------------------------------------
// Ne işe yarar:
//   Unity ile State/Interaction sistemleri arasındaki köprü. Bu sınıf
//   KARAR VERMEZ — hareket kararlarını switch mantığı, Mouth/Body
//   etkileşim kararlarını InteractionController + ilgili IInteractable
//   nesneler verir. Shell (Q) ve Sand (E) ise DIŞ nesne aramayan,
//   kaplumbağanın kendi üzerinde olan aksiyonlar (self-action) olduğu
//   için tamamen burada, kendi switch'i içinde yönetilir — Hop'la
//   aynı desen.
//
//   Hareket tarzı: Tank Kontrolü. Karakter her zaman kendi burnunun
//   dikine (transform.forward) göre ilerler/geri gider. Giderken A/D
//   ile kavisli döner (MoveForwardAndTurn), dururken A/D ile kendi
//   ekseninde döner (TurnInPlace).
//
//
// State'ler (CharacterState enum):
//   Idle -> Walk  : W/S basılınca
//   Idle -> Turn  : sadece A/D basılınca (W/S yokken)
//   Walk -> Idle  : W/S bırakılınca
//   Turn -> Idle  : A/D bırakılınca
//   Idle/Walk/Turn -> Hop  : Space'e basılınca (hopDuration sonra Idle'a döner)
//   Idle/Walk/Turn -> ShellEnter -> ShellIdle -> (tekrar Q) -> ShellExit -> Idle
//   Idle/Walk/Turn -> Dig  : E kısa basış (digDuration sonra Idle'a döner)
//   Idle/Walk/Turn -> Burrow : E basılı tutma (bırakılana kadar), bırakınca Idle
//   Idle/Walk/Turn -> Slide  : Aşın eğimde kayma başlar (normal yürüyüşte)
//   ShellIdle -> ShellSlide : Kabukta aşın eğimde kayma başlar
//   Yükseklik (Y) animasyondan, ileri mesafe (X/Z) koddan gelir (Hop için).
//
//
// Etkileşim (LMB = Mouth, RMB = Body — Tap ve Hold ikisi de destekleniyor):
//   Ham buton durumu (isPressed/wasPressed/wasReleased) her karede InteractionController'a
//   iletilir. Tap/Hold ayrımına InteractionController karar verir, bu sınıf sadece iletir.
//
//   NOT: Slide veya ShellSlide durumunda etkileşimi kapatmak isterseniz,
//   Update() içinde canInteract bayrağını kullanabilirsiniz.
//
// İçerdiği fonksiyonlar:
//   - Awake()                 : referansları alır
//   - Update()                : input okur, switch ile state mantığını çalıştırır, etkileşimi iletir
//   - UpdateShellInput()      : Q tuşunu okur, Shell state geçişlerini tetikler (self-action)
//   - UpdateSandInput()       : E tuşunu okur, Dig(tap)/Burrow(hold) geçişlerini tetikler (self-action)
//   - OnAnimatorMove()        : root motion'ı (sadece Y) CharacterController'a aktarır
//   - ReadMoveInput()         : WASD/ok tuşlarından ham girdi okur
//   - ReadHopInput()          : Hop tuşunu (Space) okur
//   - GetGroundInfo()         : zemin eğimi, normal ve aşağı/yön vektörlerini döndürür
// ============================================================
namespace Game.Character
{
    // Şu an sadece kara state'leri var. Swim ileride eklenecek.
    public enum CharacterState
    {
        Idle,
        Walk,
        Turn,
        Hop,
        // Kabuk (Q) — toggle, Hold gerekmiyor
        ShellEnter,
        ShellIdle,
        ShellExit,
        Slide,           // Normal yürüyüşte aşın eğimde kayma
        ShellSlide,      // Kabukta aşın eğimde kayma
        // Kum (E) — Tap=Dig, Hold=Burrow
        Dig,
        Burrow,
    }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class TurtleController : MonoBehaviour
    {
        // ===================== Movement Settings =====================
        [Header("Hareket Ayarları")]
        [SerializeField] private float moveSpeed = 3f;                // Düz düzlemde temel yürüme hızı
        [SerializeField] private float moveRotationSpeed = 10f;       // Giderken dönüş hızı çarpanı
        [SerializeField] private float turnInPlaceSpeed = 90f;        // Yerinde dönerken (derece/saniye)
        [Tooltip("Geri geri (S) yürürken hız çarpanı - 1 = ileri ile aynı hız, düşük değer = daha yavaş geri gitme")]
        [Range(0.1f, 1f)]
        [SerializeField] private float backwardMoveMultiplier = 0.6f;

        [Header("Hareket İvmelenmesi (Ease-in/out)")]
        [Tooltip("Durgunluktan hedef hıza ulaşma süresi (saniye) - 0'a yakın = anlık başlama, yüksek = ağır/yavaş ivmelenme")]
        [SerializeField] private float moveAccelerationTime = 0.15f;
        [Tooltip("Girdi bırakıldığında hızın 0'a inme süresi (saniye)")]
        [SerializeField] private float moveDecelerationTime = 0.12f;

        // ----------------- Walking slope handling -----------------
        [Header("Yürüyüş Eğimi Ayarları")]
        [Tooltip("Bu açıyı aşan yokuşlarda karakter yürüyemez ve kaymaya başlar")]
        [SerializeField] private float maxWalkableSlope = 45f;        // Maksimum yürünebilir eğim (derece)
        [Tooltip("Eğime göre hız çarpanı (0-1 eğim normalized)")]
        [SerializeField] private AnimationCurve walkSpeedBySlope = AnimationCurve.Linear(0f, 1f, 1f, 0f); // Eğim 0 => %100 hız, max => %0

        // ----------------- Walk Slide specific (yürürken aşırı eğimde) -----------------
        // NOT: Bu grup SADECE HandleSimpleSlide() içinde kullanılır; Shell Slide ile
        // hiçbir field paylaşılmaz, biri diğerini etkilemesin diye.
        [Header("Yürüyüş Kayması (Slide) Ayarları")]
        [Tooltip("Yürüyüş kayması sürtünmesi (m/s²)")]
        [SerializeField] private float walkSlideFriction = 4f;

        // ----------------- Shell Slide specific -----------------
        // NOT: Bu grup SADECE HandleShellSlide() içinde kullanılır.
        [Header("Kabuk Kayma (Shell Slide) Ayarları")]
        [Tooltip("Kabukta kaymaya başlayacak minimum eğim (derece)")]
        [SerializeField] private float shellSlideStartSlope = 20f;
        [Tooltip("Kabuk yönünü aşağı eğime hizalama hızını (derece/sn)")]
        [SerializeField] private float shellAlignSpeed = 8f;
        [Tooltip("Yan yönlü ağırlık etkisi (A/D) - başlangıç gücü")]
        [SerializeField] private float slideSideForce = 5f;
        [Tooltip("Hız arttıkça yan etkisi azalacak hız (0-1) - 1 = hız etkisi yok, 0 = tam etki)")]
        [Range(0f, 1f)]
        [SerializeField] private float sideInfluenceFade = 0.5f;
        [Tooltip("Kabuk kayması ivmelenmesi (m/s²)")]
        [SerializeField] private float shellSlideAcceleration = 6f;
        [Tooltip("Kabuk kayması maksimal hızı (m/s)")]
        [SerializeField] private float shellSlideMaxSpeed = 8f;
        [Tooltip("Kabuk kayması sürtünmesi (m/s²)")]
        [SerializeField] private float shellSlideFriction = 4f;
        [Tooltip("Runner modu gibi ekstra hız çarpanı - SADECE shell slide'ı etkiler")]
        [SerializeField] private float shellSlideSpeedMultiplier = 1f;
        [Tooltip("Q'ya basılı tutup kaymayı tetiklediğimiz anda verilecek küçük başlangıç hızı (itiliş hissi için)")]
        [SerializeField] private float shellSlideStartBoost = 1.2f;

        // ----------------- Slide direction smoothing (her iki slide için) -----------------
        [Header("Kayma Yönü Yumuşatma")]
        [Tooltip("Kayma sırasında zemin normal'inden gelen yön titremesini yumuşatma hızı (yüksek = daha hızlı takip, düşük titreşim)")]
        [SerializeField] private float slideDirectionSmoothing = 10f;

        // ----------------- Body tilt to ground (kabuk/gövde zemine uyum) -----------------
        [Header("Zemine Gövde Uyumu (Body Tilt)")]
        [Tooltip("Gövdenin zemin normaline dönüş (tilt) hızı - yaw'a (bakış yönü) dokunmaz, sadece pitch/roll")]
        [SerializeField] private float groundAlignSpeed = 6f;
        [Tooltip("Zemin normalini yumuşatma hızı - köşeli/engebeli geçişlerde titreşimi azaltır")]
        [SerializeField] private float groundNormalSmoothing = 8f;
        [Tooltip("Gövdenin düz zeminden en fazla kaç derece eğilebileceği - kenarlarda gövdenin abartılı 'asılmasını' engeller")]
        [SerializeField] private float maxGroundTiltAngle = 35f;

        // ----------------- Zemin örnekleme (kenar/köşe algılama) -----------------
        [Header("Zemin Örnekleme (Ground Sampling)")]
        [Tooltip("Kaç noktadan ray atılacak: 1 (tek nokta), 2 (ön-arka) veya 4 (ön-arka-sağ-sol). Performans endişesi olursa düşürülebilir.")]
        [SerializeField] private int groundSampleCount = 4;
        [Tooltip("Örnekleme noktalarının merkezden uzaklığı (genelde kabuk yarıçapına yakın bir değer)")]
        [SerializeField] private float groundSampleRadius = 0.4f;
        [Tooltip("Ray'in ne kadar aşağı ineceği")]
        [SerializeField] private float groundCheckDistance = 1.5f;
        [Tooltip("Sadece bu layer'lara ray at - su/foliage/interaction collider'larını dışarıda tutmak için")]
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [Tooltip("En yakın örnekleme noktasına göre bu kadar (metre) daha uzakta çarpan noktalar 'gerçek destek' sayılmaz - uçurum kenarında altta kalan uzak zeminin yanlışlıkla dikleştirmesini önler")]
        [SerializeField] private float maxSampleHeightVariance = 0.6f;

        // ===================== Other Systems =====================
        [Header("Yerçekimi")]
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Hop")]
        [SerializeField] private float hopDuration = 1.167f; // Loco_Jump klibinin gerçek süresi
        [SerializeField] private float hopMoveSpeed = 2f;    // Hop sırasında ileri gidiş hızı (kod kontrollü)
        [SerializeField] private float hopForwardDelay = 0.15f; // animasyonun "hazırlık" karesi bitene kadar ileri hareket başlamasın

        [Header("Kabuk (Q) — self-action, dış nesne aramaz")]
        [SerializeField] private float shellTransitionDuration = 0.5f; // Enter/Exit animasyon süresi (dummy tahmin)

        [Header("Kum (E) — self-action, dış nesne aramaz")]
        [SerializeField] private float digDuration = 1.0f;      // Dig (tap) animasyon süresi (dummy tahmin)
        [SerializeField] private float sandHoldThreshold = 0.2f; // bu süreden kısa E basışı Dig, uzununu Burrow sayılır

        // ===================== Cached Components =====================
        private CharacterController controller;
        private Animator animator;
        private InteractionController interactionController;
        private Vector3 verticalVelocity;

        // ===================== Animator Parameter Hashes =====================
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TurnDirectionHash = Animator.StringToHash("TurnDirection");
        private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        private static readonly int IsRunnerHash = Animator.StringToHash("IsRunner");
        private static readonly int IsBurrowingHash = Animator.StringToHash("IsBurrowing");
        private static readonly int ShellEnterHash = Animator.StringToHash("ShellEnter");
        private static readonly int ShellExitHash = Animator.StringToHash("ShellExit");
        private static readonly int DigHash = Animator.StringToHash("Dig");
        private static readonly int HopHash = Animator.StringToHash("Hop");

        // ===================== State Variables =====================
        private CharacterState currentState = CharacterState.Idle;
        private Vector2 moveInput;

        private float hopTimer;
        private float actionTimer; // Shell/Dig geçişleri için ortak zamanlayıcı (Hop'un hopTimer'ından ayrı)

        // E tuşu için Tap/Hold kararı bekleniyor mu
        private float sandPressStartTime;
        private bool sandAwaitingDecision;

        // Slide-specific variables
        private float currentSlideSpeed;      // hız aşağı doğru (m/s)
        private Vector3 slideDirection;       // ham aşağı yön (xz düzlemde normalize) - HandleWalkMovement'taki movingUphill hesabı bunu kullanır
        private Vector3 slideSideDirection;   // ham sağ-sol yön (düzlemde normalize)

        // Yumuşatılmış kayma yönleri - SADECE HandleSimpleSlide/HandleShellSlide içinde kullanılır,
        // titreşimi önlemek için ham slideDirection'ı yavaşça takip eder. movingUphill hesabını etkilemez.
        private Vector3 smoothedSlideDirection;
        private Vector3 smoothedSlideSideDirection;

        // Gövde-zemin uyumu için yumuşatılmış zemin normali (AlignBodyToGround() kullanır)
        private Vector3 smoothedGroundNormal = Vector3.up;

        // Yürüme ivmelenmesi için o anki (yumuşatılmış) hız - hedef hıza MoveTowards ile yaklaşır
        private float currentMoveSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            interactionController = GetComponent<InteractionController>();
        }

        private void Update()
        {
            // ---------- Input ----------
            moveInput = ReadMoveInput();
            bool hasForwardInput = Mathf.Abs(moveInput.y) > 0.01f;
            bool hasTurnInput = Mathf.Abs(moveInput.x) > 0.01f;

            // ---------- Self-action busy check ----------
            bool isBusyWithSelfAction =
                currentState == CharacterState.ShellEnter ||
                currentState == CharacterState.ShellIdle ||
                currentState == CharacterState.ShellExit ||
                currentState == CharacterState.Dig ||
                currentState == CharacterState.Burrow;

            // ---------- Ground info ----------
            bool groundInfo = GetGroundInfo(out float slopeAngle, out Vector3 groundNormal,
                                            out Vector3 downDir, out Vector3 sideDir, out float groundConfidence);
            // groundInfo false => treat as flat ground
            if (!groundInfo)
            {
                slopeAngle = 0f;
                groundNormal = Vector3.up;
                downDir = Vector3.forward;
                sideDir = Vector3.right;
            }

            // Update slide direction vectors each frame (used in slide states)
            slideDirection = downDir;
            slideSideDirection = sideDir;

            // ---------- Hop ----------
            if (ReadHopInput() && currentState != CharacterState.Hop && !isBusyWithSelfAction)
            {
                currentState = CharacterState.Hop;
                hopTimer = 0f;
                animator.SetTrigger(HopHash);
            }

            // ---------- Shell input ----------
            UpdateShellInput();

            // ---------- Sand input ----------
            UpdateSandInput();

            // ---------- Interaction ----------
            var mouse = Mouse.current;
            if (mouse != null && interactionController != null)
            {
                interactionController.UpdateOrganInput(InteractionOrgan.Mouth,
                                                        mouse.leftButton.isPressed,
                                                        mouse.leftButton.wasPressedThisFrame,
                                                        mouse.leftButton.wasReleasedThisFrame);
                interactionController.UpdateOrganInput(InteractionOrgan.Body,
                                                        mouse.rightButton.isPressed,
                                                        mouse.rightButton.wasPressedThisFrame,
                                                        mouse.rightButton.wasReleasedThisFrame);
            }

            // ---------- State machine ----------
            switch (currentState)
            {
                case CharacterState.Idle:
                    animator.SetFloat(SpeedHash, 0f);
                    animator.SetFloat(TurnDirectionHash, 0f);
                    animator.SetBool(IsSlidingHash, false);
                    animator.SetBool(IsRunnerHash, false);
                    if (hasForwardInput) currentState = CharacterState.Walk;
                    else if (hasTurnInput) currentState = CharacterState.Turn;
                    break;

                case CharacterState.Walk:
                    if (!hasForwardInput)
                    {
                        currentState = CharacterState.Idle;
                        currentMoveSpeed = 0f; // bir sonraki hareket başlangıcında ease-in sıfırdan başlasın
                        break;
                    }

                    // Handle slope-based speed modulation and possible slip
                    HandleWalkMovement(slopeAngle, moveInput.y);
                    break;

                case CharacterState.Turn:
                    if (hasForwardInput)
                    {
                        currentState = CharacterState.Walk;
                        break;
                    }
                    if (!hasTurnInput)
                    {
                        currentState = CharacterState.Idle;
                        break;
                    }
                    TurnInPlace(moveInput.x);
                    animator.SetFloat(TurnDirectionHash, moveInput.x);
                    animator.SetBool(IsSlidingHash, false);
                    animator.SetBool(IsRunnerHash, false);
                    break;

                case CharacterState.Hop:
                    hopTimer += Time.deltaTime;
                    if (hopTimer >= hopForwardDelay)
                    {
                        controller.Move(transform.forward * hopMoveSpeed * Time.deltaTime);
                    }
                    if (hopTimer >= hopDuration)
                    {
                        currentState = CharacterState.Idle;
                    }
                    break;

                case CharacterState.ShellEnter:
                    actionTimer += Time.deltaTime;
                    if (actionTimer >= shellTransitionDuration)
                    {
                        currentState = CharacterState.ShellIdle;
                    }
                    break;

                case CharacterState.ShellIdle:
                    // Kabuktayken Q'ya BASILI TUTMAK (ham eğim değil) kaymayı tetikler.
                    // Zemin yeterince dikse ve Q şu an basılıysa kaymaya başla.
                    bool qHeldForSlide = Keyboard.current != null && Keyboard.current.qKey.isPressed;
                    if (groundInfo && slopeAngle > shellSlideStartSlope && qHeldForSlide)
                    {
                        currentState = CharacterState.ShellSlide;
                        // Sıfırdan değil, küçük bir itiliş hızıyla başla - "kendini ittirmiş" hissi verir
                        currentSlideSpeed = shellSlideStartBoost;
                        smoothedSlideDirection = Vector3.zero; // yön yumuşatmasını yeniden senkronla
                        // İstersen burada kısa bir "itiliş" animasyon trigger'ı da tetikleyebilirsin,
                        // örn. animator.SetTrigger(ShellSlideStartHash) - Animator Controller'ına
                        // uygun bir trigger parametresi eklersen (root motion X/Z'yi etkilemeyecek şekilde).
                    }
                    break;

                case CharacterState.ShellExit:
                    actionTimer += Time.deltaTime;
                    if (actionTimer >= shellTransitionDuration)
                    {
                        currentState = CharacterState.Idle;
                    }
                    break;

                case CharacterState.Slide:
                    // Normal yürüyüşde aşın eğimde kayma (gravity-based simple slide)
                    HandleSimpleSlide(slopeAngle);
                    break;

                case CharacterState.ShellSlide:
                    // Kabuk kayması: yön hizalama, ivmelenme, sürtünme, yan ağırlık
                    HandleShellSlide(slopeAngle, moveInput.x);
                    break;

                case CharacterState.Dig:
                    actionTimer += Time.deltaTime;
                    if (actionTimer >= digDuration)
                    {
                        currentState = CharacterState.Idle;
                    }
                    break;

                case CharacterState.Burrow:
                    // Hold süresince buradayız, çıkışı UpdateSandInput() (E bırakılınca) tetikler.
                    break;
            }

            // ---------- Gravity ----------
            if (currentState != CharacterState.Hop && currentState != CharacterState.Slide && currentState != CharacterState.ShellSlide)
            {
                ApplyGravity();
            }

            // ---------- Body tilt to ground ----------
            // Hop sırasında zemine tilt yapmıyoruz (havadayken anlamsız); diğer tüm state'lerde
            // (Slide/ShellSlide dahil) gövde zemin normaline yumuşakça uyum sağlar, yaw'a dokunmadan.
            if (currentState != CharacterState.Hop)
            {
                AlignBodyToGround(groundInfo, groundNormal, groundConfidence);
            }
        }

        // ===================== Helper Methods =====================

        /// <summary>
        /// Karakterin altını groundSampleCount kadar noktadan (ön/arka/sağ/sol) tarar, normal'lerin
        /// ortalamasını döndürür. Kenarda/köşede bazı noktalar boşluğa denk gelirse groundConfidence
        /// düşer (0-1) - bu değeri AlignBodyToGround, emin olmadığımız durumlarda tilt'i azaltmak için kullanır.
        /// Returns true if at least one sample hit, and outputs slope angle (0 = flat), ground normal,
        /// down-slope direction (steepest descent, normalized) and side-slope direction (perp to down within plane, normalized).
        /// </summary>
        private bool GetGroundInfo(out float slopeAngle, out Vector3 groundNormal,
                                   out Vector3 downDir, out Vector3 sideDir, out float groundConfidence)
        {
            // Örnekleme yönlerini karakterin ANLIK yatay (tilt'siz) bakış yönüne göre kur.
            // transform.forward'ı doğrudan kullanmıyoruz çünkü o an zaten eğilmiş olabilir (AlignBodyToGround'dan) -
            // bu, önceki karenin tilt'ine göre bir sonraki örneklemeyi bozan bir geri besleme (feedback loop) yaratırdı.
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

            // Örnekleme noktalarının merkezden yatay ofsetleri (Y ekseni hariç)
            Vector3[] offsets;
            switch (groundSampleCount)
            {
                case 1:
                    offsets = new[] { Vector3.zero };
                    break;
                case 2:
                    offsets = new[] { flatForward * groundSampleRadius, -flatForward * groundSampleRadius };
                    break;
                default: // 4 (ya da başka bir değer girilirse yine 4 kabul et)
                    offsets = new[]
                    {
                        flatForward * groundSampleRadius,
                        -flatForward * groundSampleRadius,
                        flatRight * groundSampleRadius,
                        -flatRight * groundSampleRadius
                    };
                    break;
            }

            // 1. geçiş: tüm noktalardan ray at, sonuçları (mesafe dahil) topla ama henüz karara bağlama
            var hits = new (bool didHit, float distance, Vector3 normal, Vector3 origin, Vector3 point)[offsets.Length];
            float minDistance = float.MaxValue;

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 offset = offsets[i];
                Vector3 origin = transform.position + offset + Vector3.up * 0.5f; // hafif yukarıdan başlat, kendine çarpmasın
                bool didHit = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayerMask);
                hits[i] = (didHit, didHit ? hit.distance : -1f, didHit ? hit.normal : Vector3.up, origin, didHit ? hit.point : origin + Vector3.down * groundCheckDistance);
                if (didHit && hit.distance < minDistance) minDistance = hit.distance;
            }

            // 2. geçiş: en yakın hit'ten çok daha uzakta olan noktaları (örn. bir uçurumun dibindeki
            // zemin) GERÇEK destek saymıyoruz - normal yönü "yukarı" çıksa bile karakterin o noktada
            // fiilen desteklendiği anlamına gelmez. Bu sayede düz zeminin devam ettiği ama çok aşağıda
            // olduğu kenar durumlarında da confidence doğru şekilde düşüyor.
            Vector3 normalSum = Vector3.zero;
            int hitCount = 0;

#if UNITY_EDITOR
            if (debugGroundSamples == null || debugGroundSamples.Length != offsets.Length)
                debugGroundSamples = new GroundSampleDebug[offsets.Length];
#endif

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                bool countsAsSupport = h.didHit && (h.distance - minDistance) <= maxSampleHeightVariance;
                if (countsAsSupport)
                {
                    normalSum += h.normal;
                    hitCount++;
                }

#if UNITY_EDITOR
                debugGroundSamples[i].origin = h.origin;
                debugGroundSamples[i].endPoint = h.point;
                debugGroundSamples[i].hit = h.didHit;
                debugGroundSamples[i].discarded = h.didHit && !countsAsSupport; // hit oldu ama yükseklik farkı yüzünden sayılmadı
                debugGroundSamples[i].normal = h.normal;
#endif
            }

            groundConfidence = (float)hitCount / offsets.Length;

            if (hitCount == 0)
            {
                slopeAngle = 0f;
                groundNormal = Vector3.up;
                downDir = Vector3.forward;
                sideDir = Vector3.right;
                return false;
            }

            groundNormal = (normalSum / hitCount).normalized;
            slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            // Direction of steepest descent = projection of gravity onto the plane
            downDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            // Side direction: cross product of ground normal and down direction gives a vector tangent to the plane and perpendicular to down
            sideDir = Vector3.Cross(groundNormal, downDir).normalized;
            return true;
        }

        /// <summary>
        /// Gövdeyi zemin normaline göre yavaşça eğer (pitch/roll). Yaw (bakış yönü) turning/hareket
        /// kodundan zaten belirleniyor - buna dokunulmaz, sadece "up" ekseni zemine hizalanır.
        /// Zemin normali önce yumuşatılır (groundNormalSmoothing) - köşeli/engebeli objelerin üzerinden
        /// geçerken raycast'ten gelen ani normal değişimleri titreşime yol açmasın diye.
        /// confidence düşükse (kenar/köşe - bazı örnekler ıskaladıysa) hedef normal Vector3.up'a doğru çekilir,
        /// yani "emin olamadığımız" durumlarda abartılı eğilme yerine düz durmayı tercih ederiz.
        /// Ayrıca maxGroundTiltAngle ile düz zeminden sapma her koşulda sınırlanır (resimdeki "asılma" sorununu önler).
        /// </summary>
        private void AlignBodyToGround(bool grounded, Vector3 groundNormal, float confidence)
        {
            Vector3 rawTarget = grounded ? groundNormal : Vector3.up;
            // Düşük confidence'ta hedefi Vector3.up'a doğru harmanla (kenar/köşede aşırı tilt'i önler)
            Vector3 targetNormal = Vector3.Slerp(Vector3.up, rawTarget, confidence).normalized;
            // Ekstra güvenlik: düz zeminden sapmayı maxGroundTiltAngle ile sınırla
            targetNormal = Vector3.RotateTowards(Vector3.up, targetNormal, maxGroundTiltAngle * Mathf.Deg2Rad, 0f);

            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, targetNormal, groundNormalSmoothing * Time.deltaTime).normalized;

            // Mevcut yaw'ı koru: transform.forward'ı yeni "up" düzlemine izdüşür, sadece tilt değişir
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, smoothedGroundNormal);
            if (projectedForward.sqrMagnitude < 0.0001f) return; // forward normale tam paralelse (nadir), bu kareyi atla

            Quaternion targetRot = Quaternion.LookRotation(projectedForward.normalized, smoothedGroundNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, groundAlignSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Walking movement with slope-based speed modulation and automatic slip when slope > maxWalkableSlope.
        /// Speed reduction only when moving uphill; downhill or flat moves at base speed.
        /// </summary>
        private void HandleWalkMovement(float slopeAngle, float forwardInput)
        {
            // Gerçek hareket yönü: W ile ileri, S ile geri - transform.forward'ın işareti forwardInput'a göre değişir.
            // ÖNEMLİ: "yukarı tırmanma" tespitini forwardInput'un işaretine (W/S) göre DEĞİL, bu gerçek hareket
            // vektörünün eğime göre yönüne göre yapıyoruz. Aksi halde S ile geri geri yürüyerek (burun aşağı
            // bakarken) çok dik yokuşları da hiç yavaşlamadan/kaymadan tırmanmak mümkün oluyordu - bu bug'dı.
            Vector3 moveDirWorld = transform.forward * forwardInput;
            bool isMoving = Mathf.Abs(forwardInput) > 0.01f;
            bool movingUphill = isMoving && Vector3.Dot(moveDirWorld.normalized, slideDirection) < 0f;

            // Compute speed multiplier from slope (0 = flat, 1 = maxWalkableSlope) only if moving uphill
            float slopeFactor = 0f;
            if (movingUphill)
            {
                slopeFactor = Mathf.InverseLerp(0f, maxWalkableSlope, slopeAngle);
                slopeFactor = Mathf.Clamp01(slopeFactor);
            }
            float speedMultiplier = walkSpeedBySlope.Evaluate(slopeFactor);
            // NOT: shellSlideSpeedMultiplier buraya KASITLI olarak katılmıyor - yürüme hızı
            // shell slide ayarlarından tamamen bağımsız olmalı.
            // Geri geri (S) yürürken ayrı bir çarpan uygulanır - ileri hızdan bağımsız ayarlanabilir.
            float directionMultiplier = forwardInput < 0f ? backwardMoveMultiplier : 1f;
            float targetSpeed = moveSpeed * speedMultiplier * directionMultiplier;

            // Sadece GERÇEKTEN yukarı tırmanmaya çalışırken (movingUphill, W ya da S fark etmez) ve
            // eşik aşılmışsa kay. Aynı dik zeminde aşağı inerken (movingUphill == false) tetiklenmemeli.
            if (movingUphill && slopeAngle > maxWalkableSlope)
            {
                // Enter simple slide state (gravity-driven)
                currentState = CharacterState.Slide;
                // Ani sıfırlama yerine mevcut yürüme hızını devral - duraksama hissini azaltır
                currentSlideSpeed = currentMoveSpeed;
                smoothedSlideDirection = Vector3.zero; // yön yumuşatmasını yeniden senkronla
                // slideDirection already set in Update()
                return;
            }

            // Ease-in/out: anlık sıfırdan hıza zıplamak yerine hedef hıza yumuşakça yaklaş.
            // İvmelenirken moveAccelerationTime, yavaşlarken (girdi bırakılınca/tersine dönünce) moveDecelerationTime kullanılır.
            bool accelerating = Mathf.Abs(targetSpeed) > Mathf.Abs(currentMoveSpeed);
            float easeTime = Mathf.Max(0.0001f, accelerating ? moveAccelerationTime : moveDecelerationTime);
            float maxDelta = (moveSpeed / easeTime) * Time.deltaTime;
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, maxDelta);

            // Normal movement: apply forward movement with potentially reduced speed
            Vector3 moveDir = transform.forward * forwardInput;
            controller.Move(moveDir.normalized * currentMoveSpeed * Time.deltaTime);

            // Apply turning (A/D) while walking
            if (Mathf.Abs(moveInput.x) > 0.01f)
            {
                transform.Rotate(Vector3.up, moveInput.x * moveRotationSpeed * 10f * Time.deltaTime);
            }

            // Update animator
            animator.SetFloat(SpeedHash, Mathf.Abs(forwardInput));
            animator.SetFloat(TurnDirectionHash, moveInput.x);
            animator.SetBool(IsSlidingHash, false);
            animator.SetBool(IsRunnerHash, false);
        }

        /// <summary>
        /// Simple gravity‑based slide used when walking on too steep a slope.
        /// </summary>
        private void HandleSimpleSlide(float slopeAngle)
        {
            // If slope becomes shallow enough, exit slide
            if (slopeAngle <= maxWalkableSlope * 0.8f) // hysteresis to avoid jitter
            {
                currentState = CharacterState.Idle;
                animator.SetBool(IsSlidingHash, false);
                smoothedSlideDirection = Vector3.zero; // bir sonraki kayma için yeniden senkronla
                return;
            }

            UpdateSmoothedSlideVectors();

            // Accelerate due to gravity component along slope
            float gravityComponent = Mathf.Abs(gravity) * Mathf.Sin(slopeAngle * Mathf.Deg2Rad);
            currentSlideSpeed += gravityComponent * Time.deltaTime;
            // Apply friction to eventually stop if slope flattens
            currentSlideSpeed -= walkSlideFriction * Time.deltaTime;
            currentSlideSpeed = Mathf.Max(0f, currentSlideSpeed);

            // Move (yumuşatılmış yön - ham raycast normal titremesini azaltır)
            Vector3 motion = smoothedSlideDirection * currentSlideSpeed * Time.deltaTime;
            controller.Move(motion);

            // NOT: Buraya kayma yönüne dönme (facing rotation) eklenmiyor - kullanıcı isteğiyle
            // kaldırıldı. Karakter kayarken bakış yönünü korur, sadece pozisyon kayar.
            // Gövdenin zemine göre tilt'i (pitch/roll) zaten AlignBodyToGround() tarafından
            // ayrıca ve yaw'dan bağımsız olarak yapılıyor.

            animator.SetBool(IsSlidingHash, true);
            animator.SetBool(IsRunnerHash, false);
        }

        /// <summary>
        /// Shell slide behavior: align to down slope, accelerate, friction, side weighting.
        /// A/D tuşları karakteri döndürmez, sadece yan ağırlık verir.
        /// </summary>
        private void HandleShellSlide(float slopeAngle, float sideInput)
        {
            UpdateSmoothedSlideVectors();

            // Exit condition: slope too shallow
            if (slopeAngle <= shellSlideStartSlope * 0.8f) // hysteresis
            {
                // Smoothly decelerate to idle
                currentSlideSpeed -= shellSlideFriction * Time.deltaTime;
                if (currentSlideSpeed <= 0f)
                {
                    currentState = CharacterState.ShellIdle;
                    currentSlideSpeed = 0f;
                    smoothedSlideDirection = Vector3.zero; // bir sonraki kayma için yeniden senkronla
                }
                // Still apply remaining velocity this frame
                Vector3 motion = smoothedSlideDirection * currentSlideSpeed * Time.deltaTime;
                controller.Move(motion);
                // Align to slope gradually
                if (smoothedSlideDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(smoothedSlideDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, shellAlignSpeed * Time.deltaTime);
                }
                animator.SetBool(IsSlidingHash, true);
                animator.SetBool(IsRunnerHash, false);
                return;
            }

            // Acceleration along slope (gravity‑like but controllable)
            float accel = shellSlideAcceleration * shellSlideSpeedMultiplier;
            // According to spec, forward input does not affect slide speed; we ignore it.
            currentSlideSpeed += accel * Time.deltaTime;
            currentSlideSpeed = Mathf.Min(currentSlideSpeed, shellSlideMaxSpeed);

            // Apply friction when there is no longitudinal input
            currentSlideSpeed -= shellSlideFriction * Time.deltaTime;
            currentSlideSpeed = Mathf.Max(0f, currentSlideSpeed);

            // Sideways influence from A/D: stronger at low speed, fades as speed increases
            float sideInfluence = 1f - Mathf.Clamp01(currentSlideSpeed / shellSlideMaxSpeed) * (1f - sideInfluenceFade);
            float sideSpeed = sideInput * slideSideForce * sideInfluence; // NO extra Time.deltaTime here

            // Combine movement vectors (yumuşatılmış yönler - titreşimi azaltır)
            Vector3 movement = (smoothedSlideDirection * currentSlideSpeed + smoothedSlideSideDirection * sideSpeed) * Time.deltaTime;
            controller.Move(movement);

            // Align character to down slope (smooth, not instant)
            if (smoothedSlideDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(smoothedSlideDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, shellAlignSpeed * Time.deltaTime);
            }

            // animator
            animator.SetBool(IsSlidingHash, true);
            animator.SetBool(IsRunnerHash, false);
        }

        /// <summary>
        /// Ham slideDirection/slideSideDirection'ı (Update()'te raycast'ten anlık hesaplanır) yavaşça takip eden
        /// yumuşatılmış versiyonlarını günceller. Sadece slide handler'ları çağırır - movingUphill hesabını etkilemez.
        /// </summary>
        private void UpdateSmoothedSlideVectors()
        {
            if (smoothedSlideDirection.sqrMagnitude < 0.0001f)
            {
                // İlk kare / yeniden senkronlama sonrası: doğrudan ham değere atla
                smoothedSlideDirection = slideDirection;
                smoothedSlideSideDirection = slideSideDirection;
                return;
            }

            smoothedSlideDirection = Vector3.Slerp(smoothedSlideDirection, slideDirection, slideDirectionSmoothing * Time.deltaTime).normalized;
            smoothedSlideSideDirection = Vector3.Slerp(smoothedSlideSideDirection, slideSideDirection, slideDirectionSmoothing * Time.deltaTime).normalized;
        }

        // ===================== Input / Self-action =====================
        private void UpdateShellInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Q pressed down (yeni bir basış - basılı tutmak değil)
            if (keyboard.qKey.wasPressedThisFrame)
            {
                // If we are not in shell (Idle/Walk/Turn), enter shell
                if (currentState == CharacterState.Idle || currentState == CharacterState.Walk || currentState == CharacterState.Turn)
                {
                    currentState = CharacterState.ShellEnter;
                    actionTimer = 0f;
                    animator.SetTrigger(ShellEnterHash);
                    return;
                }
                // ShellIdle veya ShellSlide'dayken tekrar Q -> kabuktan çık (kayma varsa durdurulur).
                // Kaymayı durdurmak artık Q'yu BIRAKMAYA değil, tekrar BASMAYA bağlı.
                if (currentState == CharacterState.ShellIdle || currentState == CharacterState.ShellSlide)
                {
                    currentState = CharacterState.ShellExit;
                    actionTimer = 0f;
                    currentSlideSpeed = 0f;
                    smoothedSlideDirection = Vector3.zero;
                    animator.SetBool(IsSlidingHash, false);
                    animator.SetTrigger(ShellExitHash);
                    return;
                }
                // ShellEnter veya ShellExit sırasındaysak yoksay, geçiş bitsin
            }

            // NOT: Q bırakıldığında (wasReleasedThisFrame) artık HİÇBİR ŞEY yapmıyoruz.
            // Kayma, Q basılı tutulduğu sürece değil - tekrar Q'ya BASILANA kadar devam eder.
        }

        private void UpdateSandInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool canStart = currentState == CharacterState.Idle || currentState == CharacterState.Walk || currentState == CharacterState.Turn;

            // Check if ground is sand
            bool isSand = false;
            if (canStart)
            {
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayerMask))
                {
                    isSand = hit.collider.CompareTag("Sand");
                }
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                sandPressStartTime = Time.time;
                sandAwaitingDecision = true;
            }

            if (sandAwaitingDecision && keyboard.eKey.isPressed && Time.time - sandPressStartTime >= sandHoldThreshold)
            {
                sandAwaitingDecision = false;
                if (canStart && isSand)
                {
                    currentState = CharacterState.Burrow;
                    animator.SetBool(IsBurrowingHash, true);
                }
            }

            if (keyboard.eKey.wasReleasedThisFrame)
            {
                if (currentState == CharacterState.Burrow)
                {
                    currentState = CharacterState.Idle;
                    animator.SetBool(IsBurrowingHash, false);
                }
                else if (sandAwaitingDecision && canStart && isSand)
                {
                    currentState = CharacterState.Dig;
                    actionTimer = 0f;
                    animator.SetTrigger(DigHash);
                }

                sandAwaitingDecision = false;
            }
        }

        
        // ===================== Movement Helpers =====================
        private void TurnInPlace(float turnDirection)
        {
            transform.Rotate(Vector3.up, turnDirection * turnInPlaceSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (controller.isGrounded && verticalVelocity.y < 0)
            {
                verticalVelocity.y = groundedStickForce;
            }

            verticalVelocity.y += gravity * Time.deltaTime;
            controller.Move(verticalVelocity * Time.deltaTime);
        }

        // ===================== Input Readers =====================
        private Vector2 ReadMoveInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            return new Vector2(horizontal, vertical);
        }

        private bool ReadHopInput()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }

        // ============================================================================
        // ============ DEBUG - ZEMİN RAY GÖRSELLEŞTİRME (kolayca silinebilir) ========
        // ------------------------------------------------------------------------------
        // Bu bölüm sadece Scene/Game view'da ray'leri görebilmen için var, oyun mantığına
        // hiçbir etkisi yok. İşin bitince bu iki bölümü (üstteki #if UNITY_EDITOR ile
        // sarılı cache kodu + bu bölümün TAMAMI) silmen yeterli, başka hiçbir yeri
        // etkilemez.
        // ============================================================================
#if UNITY_EDITOR
        [Header("DEBUG - Zemin Ray Görselleştirme (silinebilir)")]
        [SerializeField] private bool showGroundRayGizmos = true;
        [SerializeField] private Color gizmoRayHitColor = Color.green;
        [SerializeField] private Color gizmoRayMissColor = Color.red;
        [SerializeField] private Color gizmoRayDiscardedColor = new Color(1f, 0.6f, 0f); // turuncu: hit oldu ama yükseklik farkı yüzünden sayılmadı
        [SerializeField] private float gizmoNormalLength = 0.5f;
        [SerializeField] private float gizmoHitSphereRadius = 0.05f;

        private struct GroundSampleDebug
        {
            public Vector3 origin;    // ray'in başladığı nokta
            public Vector3 endPoint;  // hit varsa çarpma noktası, yoksa ray'in gittiği en uzak nokta
            public bool hit;
            public bool discarded;    // hit oldu ama diğer noktalara göre çok uzaktaydı (uçurun altı vs.) - desteğe sayılmadı
            public Vector3 normal;    // hit varsa yüzey normali
        }

        private GroundSampleDebug[] debugGroundSamples;

        private void OnDrawGizmos()
        {
            if (!showGroundRayGizmos || debugGroundSamples == null) return;

            foreach (GroundSampleDebug sample in debugGroundSamples)
            {
                // Öncelik: gerçek miss (kırmızı) > yükseklik farkı yüzünden diskarte (turuncu) > geçerli hit (yeşil)
                Gizmos.color = !sample.hit ? gizmoRayMissColor : (sample.discarded ? gizmoRayDiscardedColor : gizmoRayHitColor);
                Gizmos.DrawLine(sample.origin, sample.endPoint);

                if (sample.hit)
                {
                    Gizmos.DrawSphere(sample.endPoint, gizmoHitSphereRadius);
                    // Yüzey normalini de çiz - hangi yönün "yukarı" sayıldığını görmek için
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(sample.endPoint, sample.endPoint + sample.normal * gizmoNormalLength);
                }
            }

            // Ortalama (kullanılan) yumuşatılmış zemin normalini de ayrı bir renkle göster
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + smoothedGroundNormal * (gizmoNormalLength * 1.5f));
        }
#endif
        // ============ DEBUG BÖLÜMÜ SONU ============================================
    }
}