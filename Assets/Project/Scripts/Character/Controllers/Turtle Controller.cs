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
//   nesneler verir. Shell (Q) ve Sand (E) ise DIŞ nesne aramaz,
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
//   İçerdiği fonksiyonlar:
//   - Awake()                 : referansları alır
//   - Update()                : input okur, switch ile state mantığını çalıştırır, etkileşimi iletir
//   - UpdateShellInput()      : Q tuşunu okur, Shell state geçişlerini tetikler (self-action)
//   - UpdateSandInput()       : E tuşunu okur, Dig(tap)/Burrow(hold) geçişlerini tetikler (self-action)
//   - OnAnimatorMove()        : root motion'ı (sadece Y) CharacterController'a aktarır
//   - ReadMoveInput()         : WASD/ok tuşlarından ham girdi okur
//   - ReadHopInput()          : Hop tuşunu (Space) okur
//   - GetGroundInfo()         : zemin eğimi, normal ve aşağı/yön vektörlerini döndürür
// ============================================================

// Fonksiyon Özetleri:
// - Awake()                 : CharacterController, Animator ve InteractionController referanslarını alır. Yerden uzaklık (groundOffset) ve yumuşatılmış zemin yüksekliği (smoothedGroundHeight) başlatılır.
// - Update()                : Her karede giriş okur, self-action kontrolü yapar, zemin bilgilerini alır, slide yönlerini günceller, Hop, Shell ve Sand girişlerini işler, etkileşimi iletir, durum makinesini çalıştırır, yerçekimi uygular ve gövdeyi zemine hizalar.
// - GetGroundInfo()         : Ön, arka, sol, sağ dört farklı pozisyondan aşağıya raycast atar. Çarpan tüm hit'lerin normalsini ve noktalarını ortalarak ortalama zemin normalsi, eğim açıği, aşağı yön ve yan yön vektörlerini döndürür. Ayrıca zemin yüksekliğini yumuşatır (smooth).
// - AlignBodyToGround()     : Gövdenin yerel "up" eksenini (pitch/roll) yumuşakça zemin normaline hizalar, yaw (bakış yönü) değişmeden korunur.
// - HandleWalkMovement()    : Normal yürüyüş hareketini yönetir; eğime göre hız çarpanını uygular, yukarı tırmanırken maksimum eğimi aşarsa kayma (Slide) durumuna geçer.
// - HandleSimpleSlide()     : Yürüyüşte aşın eğimde kayma (gravity‑based slide). Yerden düşük kayma hızıyla hareket eder, eğim yumuşakça olunca Idle'a döner.
// - HandleShellSlide()      : Kabukta aşın eğimde kayma. A/D tuşları karakteri döndürmez, sadece yan ağırlık verir; ivmelenme, sürtünme ve yan ağırlık etkisiyle hareket eder.
// - UpdateSmoothedSlideVectors(): Kayma yönlerini (slideDirection ve slideSideDirection) titreşimi azaltmak için yumuşatır.
// - UpdateShellInput()      : Q tuşuna basılı tutmamış basışları yakalar; ShellEnter, ShellIdle, ShellExit geçişlerini yönetir.
// - UpdateSandInput()       : E tuşunun basılı tutma süresine göre Dig (kısa basış) ve Burrow (uzun basış) geçişlerini yönetir.
// - TurnInPlace()           : Karakteri yerinde y ekseninde döndürür.
// - ApplyGravity()          : Yerçekimi etkisini uygular; grounded olduğundaverticalVelocity'yı sabitleyerek karakterin宙нун zemine yapışmasını sağlar.
// - ReadMoveInput()         : Klavye WASD ve ok tuşlarını okur, Vector2 olarak döndürür.
// - ReadHopInput()          : Space tuşunun bu karede basılıp basılmadığını kontrol eder.
// - OnDrawGizmosSelected()  : Play modunda dört tane zemin rayını (ön, arka, sol, sağ) Scene view'da gösterir; yeşil = hit, kırmızı = miss.

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
        [Tooltip("Yürüyüş kaymasında yön hizalama hızı (derece/sn)")]
        [SerializeField] private float walkSlideAlignSpeed = 5f;

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

        // ===================== Ground Detection =====================
        [Header("Ground Detection")]
        [Tooltip("How far down the rays are cast")]
        [SerializeField] private float groundRayLength = 5f;
        [Tooltip("Forward offset of the front ray from the center")]
        [SerializeField] private float frontRayOffset = 0.5f;
        [Tooltip("Back offset of the back ray from the center")]
        [SerializeField] private float backRayOffset = 0.5f;
        [Tooltip("Side offset of the left/right rays from the center")]
        [SerializeField] private float sideRayOffset = 0.5f;
        [Tooltip("Smoothing for ground height (vertical position)")]
        [SerializeField] private float groundHeightSmooth = 8f;

        // ===================== Other Systems =====================
        [Header("Yerçekimi")]
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Hop")]
        [SerializeField] private float hopDuration = 1.167f; // Loco_Jump klibinin gerçek süresi
        [SerializeField] private float hopMoveSpeed = 2f;    // Hop sırasında ileri gidiş hızı (kod kontrollü)
        [SerializeField] private float hopForwardDelay = 0.15f; // animasyonun "hazırlık" karesi bitene kadar ileri hareket başlar

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

        // Ground detection smoothed values
        private float smoothedGroundHeight;
        private float groundOffset;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            interactionController = GetComponent<InteractionController>();

            // Initialize ground offset (half height + center)
            groundOffset = (controller.height / 2f) + controller.center.y;
            smoothedGroundHeight = transform.position.y - groundOffset;
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
                                            out Vector3 downDir, out Vector3 sideDir);
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
                    // Normal yürüyüşte aşın eğimde kayma (gravity-based simple slide)
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

            // ---------- Align to ground height ----------
            // We rely on CharacterController's collision for vertical position;
            // ground height smoothing is not used to avoid conflicts.

            // ---------- Body tilt to ground ----------
            // Hop sırasında zemine tilt yapmıyoruz (havadayken anlamsız); diğer tüm state'lerde
            // (Slide/ShellSlide dahil) gövde zemin normaline yumuşakça uyum sağlar, yaw'a dokunmaz.
            if (currentState != CharacterState.Hop)
            {
                AlignBodyToGround(groundInfo, groundNormal);
            }
        }

        // ===================== Helper Methods =====================

        /// <summary>
        /// Ön, arka, sol, sağ dört farklı pozisyondan aşağıya raycast atar. Çarpan tüm hit'lerin normalsini ve noktalarını ortalarak ortalama zemin normalsi, eğim açıği, aşağı yön ve yan yön vektörlerini döndürür.
        /// </summary>
        private bool GetGroundInfo(out float slopeAngle, out Vector3 groundNormal,
                                   out Vector3 downDir, out Vector3 sideDir)
        {
            int hitCount = 0;
            Vector3 normalSum = Vector3.zero;
            Vector3 pointSum = Vector3.zero;
            bool anyHit = false;

            Vector3 baseOrigin = transform.position + Vector3.up * 0.5f;

            Vector3[] origins = {
                baseOrigin + transform.forward * frontRayOffset,
                baseOrigin - transform.forward * backRayOffset,
                baseOrigin + transform.right * sideRayOffset,
                baseOrigin - transform.right * sideRayOffset
            };

            foreach (var origin in origins)
            {
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayLength, ~0))
                {
                    anyHit = true;
                    hitCount++;
                    normalSum += hit.normal;
                    pointSum += hit.point;
                }
            }

            if (!anyHit)
            {
                slopeAngle = 0f;
                groundNormal = Vector3.up;
                downDir = Vector3.forward;
                sideDir = Vector3.right;
                return false;
            }

            Vector3 avgNormal = (normalSum / hitCount).normalized;
            Vector3 avgPoint = pointSum / hitCount;

            slopeAngle = Vector3.Angle(Vector3.up, avgNormal);
            groundNormal = avgNormal;
            downDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            sideDir = Vector3.Cross(groundNormal, downDir).normalized;

            // Smooth ground height for vertical position
            smoothedGroundHeight = Mathf.Lerp(smoothedGroundHeight, avgPoint.y, groundHeightSmooth * Time.deltaTime);

            return true;
        }

        /// <summary>
        /// Gövdenin yerel "up" eksenini (pitch/roll) yumuşakça zemin normaline hizalar, yaw (bakış yönü) değişmeden korunur.
        /// Zemin normali önce yumuşatılır (groundNormalSmoothing) - köşeli/engebeli objelerin üzerinden
        /// geçerken raycast'ten gelen ani normal değişimleri titreşime yol açmasın diye.
        /// </summary>
        private void AlignBodyToGround(bool grounded, Vector3 groundNormal)
        {
            Vector3 targetNormal = grounded ? groundNormal : Vector3.up;
            // Smooth the normal to avoid jitter on uneven terrain
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, targetNormal, groundNormalSmoothing * Time.deltaTime).normalized;

            // Preserve current yaw: project forward onto the plane defined by the smoothed up vector
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, smoothedGroundNormal);
            if (projectedForward.sqrMagnitude < 0.0001f) return; // forward nearly parallel to up (rare), skip this frame

            Quaternion targetRot = Quaternion.LookRotation(projectedForward.normalized, smoothedGroundNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, groundAlignSpeed * Time.deltaTime);
        }

        // We keep a private field for smoothed normal used in AlignBodyToGround
        private Vector3 smoothedGroundNormal = Vector3.up;

        /// <summary>
        /// Normal yürüyüş hareketini yönetir; eğime göre hız çarpanını uygular, yukarı tırmanırken maksimum eğimi aşarsa kayma (Slide) durumuna geçer.
        /// </summary>
        private void HandleWalkMovement(float slopeAngle, float forwardInput)
        {
            // Determine if moving uphill: dot between forward direction and downhill direction
            bool movingUphill = forwardInput > 0f && Vector3.Dot(transform.forward, slideDirection) < 0f;
            // Compute speed multiplier from slope (0 = flat, 1 = maxWalkableSlope) only if moving uphill
            float slopeFactor = 0f;
            if (movingUphill)
            {
                slopeFactor = Mathf.InverseLerp(0f, maxWalkableSlope, slopeAngle);
                slopeFactor = Mathf.Clamp01(slopeFactor);
            }
            float speedMultiplier = walkSpeedBySlope.Evaluate(slopeFactor);
            // NOT: shellSlideSpeedMultiplier buraya KASITLI olarak katılmıyor - yürüme hızı
            // slide ayarlarından tamamen bağımsız olmalı.
            float effectiveSpeed = moveSpeed * speedMultiplier;

            // Sadece GERÇEKTEN yukarı tırmanmaya çalışırken (movingUphill) ve eşik aşılmışsa kay.
            // Aynı dik zeminde aşağı inerken (movingUphill == false) bu tetiklenmemeli.
            if (movingUphill && slopeAngle > maxWalkableSlope)
            {
                // Enter simple slide state (gravity-driven)
                currentState = CharacterState.Slide;
                // Ani sıfırlama yerine mevcut yürüme hızını devral - duraksama hissini azaltır
                currentSlideSpeed = effectiveSpeed;
                smoothedSlideDirection = Vector3.zero; // yön yumuşatmasını yeniden senkronla
                // slideDirection already set in Update()
                return;
            }

            // Normal movement: apply forward movement with potentially reduced speed
            Vector3 moveDir = transform.forward * forwardInput;
            controller.Move(moveDir.normalized * effectiveSpeed * Time.deltaTime);

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
        /// Yürüyüşte aşın eğimde kayma (gravity‑based slide). Yerden düşük kayma hızıyla hareket eder, eğim yumuşakça olunca Idle'a döner.
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

            // Optional: align character to slope direction (smooth)
            if (smoothedSlideDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(smoothedSlideDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, walkSlideAlignSpeed * Time.deltaTime);
            }

            animator.SetBool(IsSlidingHash, true);
            animator.SetBool(IsRunnerHash, false);
        }

        /// <summary>
        /// Kabukta aşın eğimde kayma. A/D tuşları karakteri döndürmez, sadece yan ağırlık verir; ivmelenme, sürtünme ve yan ağırlık etkisiyle hareket eder.
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
        /// Kayma yönlerini (slideDirection ve slideSideDirection) titreşimi azaltmak için yumuşatır.
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

            if (keyboard.eKey.wasPressedThisFrame)
            {
                sandPressStartTime = Time.time;
                sandAwaitingDecision = true;
            }

            if (sandAwaitingDecision && keyboard.eKey.isPressed && Time.time - sandPressStartTime >= sandHoldThreshold)
            {
                sandAwaitingDecision = false;
                if (canStart)
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
                else if (sandAwaitingDecision && canStart)
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

        // ===================== Gizmos =====================
        private void OnDrawGizmosSelected()
        {
            // Only draw in play mode so we see the actual rays used
            if (!Application.isPlaying) return;

            Vector3 baseOrigin = transform.position + Vector3.up * 0.5f;
            Vector3[] origins = {
                baseOrigin + transform.forward * frontRayOffset,
                baseOrigin - transform.forward * backRayOffset,
                baseOrigin + transform.right * sideRayOffset,
                baseOrigin - transform.right * sideRayOffset
            };

            // Draw the center point
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(baseOrigin, 0.02f);

            // Draw each ray
            foreach (var origin in origins)
            {
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayLength, ~0))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(origin, origin + Vector3.down * hit.distance);
                    Gizmos.DrawSphere(hit.point, 0.02f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin, origin + Vector3.down * groundRayLength);
                }
            }
        }
    }
}