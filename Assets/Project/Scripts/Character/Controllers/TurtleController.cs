using UnityEngine;
using UnityEngine.InputSystem;
using Game.Interaction;
using Game.Character.States;

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
//   - Awake()                 : referansları alır, State Pattern altyapısını (Context/StateMachine) kurar
//   - Update()                : input okur, HER CharacterState'i ilgili State'e devreder (artık eski
//                               switch yok - her state kendi dosyasında yaşıyor)
//   - OnAnimatorMove()        : root motion'ı (sadece Y) CharacterController'a aktarır
//   - ReadMoveInput()         : WASD/ok tuşlarından ham girdi okur
//   - ReadHopInput()          : Hop tuşunu (Space) okur
//   - GetGroundInfo()         : zemin eğimi, normal ve aşağı/yön vektörlerini döndürür
//   - EnterSlideFromLocomotion()    : LocomotionState çok dik yokuşta "devret" dediğinde çağrılır
//   - EnterShellSlideFromShell()    : ShellState (ShellIdle) çok dik yokuşta "devret" dediğinde çağrılır
//   - ResetSlideMotion()            : ShellState kabuktan çıkarken kaymayı sıfırlamak için çağırır
//
// NOT: Tüm state mantığı State Pattern'e taşındı, bu dosyada artık hiçbir state'in per-frame
// mantığı yok - sadece kurulum, input okuma ve State'lere devretme:
//   - ITurtleState.cs           : her state'in uyacağı arayüz (Enter/Tick/Exit)
//   - TurtleContext.cs          : state'lerin ihtiyaç duyduğu paylaşılan referanslar
//   - TurtleStateMachine.cs     : aktif state'i tutan ve Tick'i ona delege eden yapı
//   - States/LocomotionState.cs : Idle/Walk/Turn mantığının kendisi
//   - States/HopState.cs        : Hop mantığının kendisi
//   - States/ShellState.cs      : ShellEnter/ShellIdle/ShellExit (ShellState.HandleInput() Q tuşunu
//                                 okuyup Shell'e giriş/çıkışı tetikler - her karede çağrılır)
//   - States/SlideState.cs      : Slide + ShellSlide mantığının kendisi (ikisi kayma hızı/yönü
//                                 alanlarını paylaştığı için tek dosyada)
//   - States/SandState.cs       : Dig + Burrow (SandState.HandleInput() E tuşunu okuyup Dig/Burrow'a
//                                 giriş/çıkışı tetikler - her karede çağrılır)
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
        // NOT: Bu grup SADECE SlideState'in düz Slide mantığında kullanılır; Shell Slide ile
        // hiçbir field paylaşılmaz, biri diğerini etkilemesin diye.
        [Header("Yürüyüş Kayması (Slide) Ayarları")]
        [Tooltip("Yürüyüş kayması sürtünmesi (m/s²)")]
        [SerializeField] private float walkSlideFriction = 4f;

        // ----------------- Shell Slide specific -----------------
        // NOT: Bu grup SADECE SlideState'in ShellSlide mantığında kullanılır.
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

        [Header("Kum Serpme Etkisi (E - Tap/Dig anında, koni içindeki IStunnable'lara)")]
        [SerializeField] private float sandEffectRadius = 2f;
        [SerializeField] private float sandEffectAngle = 90f;   // derece, görüş açısı (koni)
        [SerializeField] private float sandStunDuration = 2f;
        [SerializeField] private LayerMask stunnableMask = ~0;

        [Header("Kum Parçacık Efektleri")]
        [SerializeField] private ParticleSystem sandDigParticle;
        [SerializeField] private ParticleSystem sandBurrowParticle;

        [Header("Kum Parçacık Spawn Noktaları")]
        [SerializeField] private Transform digSpawnPoint;
        [SerializeField] private Transform burrowSpawnPoint;

        // ===================== Cached Components =====================
        private CharacterController controller;
        private Animator animator;
        private InteractionController interactionController;
        private Vector3 verticalVelocity;

        // ===================== Animator Parameter Hashes =====================
        // NOT: Speed/TurnDirection/IsSliding/IsRunner/ShellEnter/ShellExit/Hop hash'leri
        // LocomotionState/HopState/ShellState tarafından da kullanıldığı için internal.
        internal static readonly int SpeedHash = Animator.StringToHash("Speed");
        internal static readonly int TurnDirectionHash = Animator.StringToHash("TurnDirection");
        internal static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        internal static readonly int IsRunnerHash = Animator.StringToHash("IsRunner");
        internal static readonly int IsBurrowingHash = Animator.StringToHash("IsBurrowing");
        internal static readonly int ShellEnterHash = Animator.StringToHash("ShellEnter");
        internal static readonly int ShellExitHash = Animator.StringToHash("ShellExit");
        internal static readonly int DigHash = Animator.StringToHash("Dig");
        internal static readonly int HopHash = Animator.StringToHash("Hop");

        // ===================== Locomotion Tuning'e Salt-Okunur Erişim =====================
        // NOT: Bu property'ler Inspector alanlarını KOPYALAMIYOR, doğrudan aynı alana bakıyor.
        // Böylece Play modunda Inspector'dan değer değiştirsen bile LocomotionState anında
        // güncel değeri görür (davranış hiçbir şekilde değişmez).
        public float MoveSpeed => moveSpeed;
        public float MoveRotationSpeed => moveRotationSpeed;
        public float TurnInPlaceSpeed => turnInPlaceSpeed;
        public float BackwardMoveMultiplier => backwardMoveMultiplier;
        public float MoveAccelerationTime => moveAccelerationTime;
        public float MoveDecelerationTime => moveDecelerationTime;
        public float MaxWalkableSlope => maxWalkableSlope;
        public AnimationCurve WalkSpeedBySlope => walkSpeedBySlope;

        // ===================== Hop Tuning'e Salt-Okunur Erişim =====================
        public float HopDuration => hopDuration;
        public float HopMoveSpeed => hopMoveSpeed;
        public float HopForwardDelay => hopForwardDelay;

        // ===================== Shell Tuning'e Salt-Okunur Erişim =====================
        // NOT: shellSlideStartSlope burada da lazım çünkü ShellIdle -> ShellSlide geçiş
        // kontrolü (çok dikte Q basılıyken kaymaya başlama) ShellState'e taşındı.
        public float ShellTransitionDuration => shellTransitionDuration;
        public float ShellSlideStartSlope => shellSlideStartSlope;
        public float ShellSlideStartBoost => shellSlideStartBoost;

        // ===================== Slide/ShellSlide Tuning'e Salt-Okunur Erişim =====================
        public float WalkSlideFriction => walkSlideFriction;
        public float Gravity => gravity;
        public float ShellAlignSpeed => shellAlignSpeed;
        public float ShellSlideAcceleration => shellSlideAcceleration;
        public float ShellSlideSpeedMultiplier => shellSlideSpeedMultiplier;
        public float ShellSlideMaxSpeed => shellSlideMaxSpeed;
        public float ShellSlideFriction => shellSlideFriction;
        public float SideInfluenceFade => sideInfluenceFade;
        public float SlideSideForce => slideSideForce;
        public float SlideDirectionSmoothing => slideDirectionSmoothing;

        // ===================== Sand (Dig/Burrow) Tuning'e Salt-Okunur Erişim =====================
        public float GroundCheckDistance => groundCheckDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public float SandHoldThreshold => sandHoldThreshold;
        public float DigDuration => digDuration;
        public float SandEffectRadius => sandEffectRadius;
        public float SandEffectAngle => sandEffectAngle;
        public float SandStunDuration => sandStunDuration;
        public LayerMask StunnableMask => stunnableMask;
        public ParticleSystem SandDigParticle => sandDigParticle;
        public ParticleSystem SandBurrowParticle => sandBurrowParticle;
        public Transform DigSpawnPoint => digSpawnPoint;
        public Transform BurrowSpawnPoint => burrowSpawnPoint;

        // ===================== State Variables =====================
        // NOT: Idle/Walk/Turn state'lerine artık States/LocomotionState.cs karar veriyor.
        // Bu property dışarıdan (LocomotionState dahil) okunup yazılabilsin diye public.
        public CharacterState CurrentState { get; set; } = CharacterState.Idle;
        private Vector2 moveInput;

        // Burrow state'indeyken true - ileride tehlike/predator AI'ları bu bayrağı okuyup
        // kaplumbağayı "görmezden gelecek". Şimdilik sadece dışarı açılan bir bilgi.
        public bool IsHidden { get; internal set; }

        // Predator sistemi tarafından ayarlanır: bir predator (yengeç vb.) kaplumbağayı
        // tutuyorken true olur. Hop/Shell/Sand aksiyonlarını engellemek ve yürüme hızını
        // düşürmek için LocomotionState ve Update() bu ikisini okur.
        public bool IsRestrained { get; internal set; }
        public float RestrainedSpeedMultiplier { get; internal set; } = 1f;


        // NOT: actionTimer, sandPressStartTime, sandAwaitingDecision artık burada değil -
        // States/SandState.cs'in kendi iç değişkenleri oldu (Dig/Burrow tamamen taşındı).

        // Slide-specific variables
        // NOT: currentSlideSpeed/smoothedSlideDirection/smoothedSlideSideDirection artık burada değil -
        // States/SlideState.cs'in kendi iç değişkenleri oldu (Slide/ShellSlide tamamen taşındı).
        private Vector3 slideDirection;       // ham aşağı yön (xz düzlemde normalize) - LocomotionState'teki movingUphill hesabı bunu kullanır
        private Vector3 slideSideDirection;   // ham sağ-sol yön (düzlemde normalize)

        // Gövde-zemin uyumu için yumuşatılmış zemin normali (AlignBodyToGround() kullanır)
        private Vector3 smoothedGroundNormal = Vector3.up;

        // ===================== State Pattern altyapısı =====================
        // NOT: currentMoveSpeed artık burada değil - LocomotionState'in kendi iç değişkeni oldu,
        // çünkü sadece yürüyüş ease-in/out'unu ilgilendiriyor.
        private TurtleContext context;
        private TurtleStateMachine stateMachine;
        private LocomotionState locomotionState;
        private HopState hopState;
        private ShellState shellState;
        private SlideState slideState;
        private SandState sandState;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            interactionController = GetComponent<InteractionController>();

            context = new TurtleContext(this, controller, animator);
            stateMachine = new TurtleStateMachine();
            locomotionState = new LocomotionState();
            hopState = new HopState();
            shellState = new ShellState();
            slideState = new SlideState();
            sandState = new SandState();
        }

        /// <summary>
        /// LocomotionState (Walk) çok dik bir yokuşta yukarı tırmanmaya çalışırken buraya
        /// "devret" der. Gerçek kayma mantığı artık SlideState'te yaşıyor; bu metod sadece
        /// state'i değiştirip başlangıç hızını devrediyor.
        /// </summary>
        public void EnterSlideFromLocomotion(float carryOverSpeed)
        {
            CurrentState = CharacterState.Slide;
            slideState.StartWalkSlide(carryOverSpeed);
        }

        /// <summary>
        /// ShellState (ShellIdle) kabuktayken çok dik bir zeminde Q basılı tutulursa buraya
        /// "devret" der. Gerçek kayma mantığı artık SlideState'te yaşıyor; bu metod sadece
        /// state'i değiştirip başlangıç hızını devrediyor.
        /// </summary>
        public void EnterShellSlideFromShell(float startBoostSpeed)
        {
            CurrentState = CharacterState.ShellSlide;
            slideState.StartShellSlide(startBoostSpeed);
        }

        /// <summary>
        /// ShellState, Q'ya tekrar basılıp kabuktan çıkılırken (ShellIdle/ShellSlide -> ShellExit)
        /// varsa devam eden kaymayı sıfırlamak için bunu çağırır. Kayma hızı/yönü artık SlideState'in
        /// private alanları olduğu için dışarıdan doğrudan erişilemiyor, SlideState üzerinden sıfırlanıyor.
        /// </summary>
        public void ResetSlideMotion()
        {
            slideState.ResetSlideMotion();
        }

        private void Update()
        {
            // ---------- Input ----------
            moveInput = ReadMoveInput();

            // ---------- Self-action busy check ----------
            bool isBusyWithSelfAction =
                CurrentState == CharacterState.ShellEnter ||
                CurrentState == CharacterState.ShellIdle ||
                CurrentState == CharacterState.ShellExit ||
                CurrentState == CharacterState.Dig ||
                CurrentState == CharacterState.Burrow ||
                IsRestrained;

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
            // NOT: hopTimer sıfırlama ve animator.SetTrigger artık burada değil - HopState.Enter()'da,
            // çünkü CurrentState = Hop olur olmaz state machine aynı karede Enter()'ı zaten çağıracak.
            if (ReadHopInput() && CurrentState != CharacterState.Hop && !isBusyWithSelfAction)
            {
                CurrentState = CharacterState.Hop;
            }

            // ---------- Shell input ----------
            // NOT: Restrained iken (bir predator tutuyorken) Shell/Sand aksiyonlarını
            // tetikleyemez - E tuşu bu sırada TurtlePredatorTarget tarafından escape
            // mekaniği için ayrıca okunuyor.
            if (!IsRestrained)
            {
                shellState.HandleInput(context);

                // ---------- Sand input ----------
                sandState.HandleInput(context);
            }

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
            // Artık her CharacterState bir state dosyasına karşılık geliyor:
            // Idle/Walk/Turn -> LocomotionState, Hop -> HopState, Shell* -> ShellState,
            // Slide/ShellSlide -> SlideState, Dig/Burrow -> SandState.
            context.MoveInput = moveInput;
            context.SlopeAngle = slopeAngle;
            context.SlideDirection = slideDirection;
            context.SlideSideDirection = slideSideDirection;
            context.HasGroundContact = groundInfo;

            ITurtleState desiredState;
            if (CurrentState == CharacterState.Idle || CurrentState == CharacterState.Walk || CurrentState == CharacterState.Turn)
            {
                desiredState = locomotionState;
            }
            else if (CurrentState == CharacterState.Hop)
            {
                desiredState = hopState;
            }
            else if (CurrentState == CharacterState.ShellEnter || CurrentState == CharacterState.ShellIdle || CurrentState == CharacterState.ShellExit)
            {
                desiredState = shellState;
            }
            else if (CurrentState == CharacterState.Slide || CurrentState == CharacterState.ShellSlide)
            {
                desiredState = slideState;
            }
            else // Dig veya Burrow
            {
                desiredState = sandState;
            }

            stateMachine.ChangeState(desiredState, context);
            stateMachine.Tick(context);

            // ---------- Gravity ----------
            if (CurrentState != CharacterState.Hop && CurrentState != CharacterState.Slide && CurrentState != CharacterState.ShellSlide)
            {
                ApplyGravity();
            }

            // ---------- Body tilt to ground ----------
            // Hop sırasında zemine tilt yapmıyoruz (havadayken anlamsız); diğer tüm state'lerde
            // (Slide/ShellSlide dahil) gövde zemin normaline yumuşakça uyum sağlar, yaw'a dokunmadan.
            if (CurrentState != CharacterState.Hop)
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

        // NOT: HandleWalkMovement buradan tamamen kaldırıldı - States/LocomotionState.cs içinde yaşıyor.

        // NOT: HandleSimpleSlide, HandleShellSlide, UpdateSmoothedSlideVectors buradan tamamen
        // kaldırıldı - States/SlideState.cs içinde yaşıyorlar.


        // NOT: UpdateShellInput buradan tamamen kaldırıldı - States/ShellState.cs içindeki
        // HandleInput() metoduna taşındı.

        // NOT: UpdateSandInput ve ApplySandStunEffect buradan tamamen kaldırıldı -
        // States/SandState.cs içinde yaşıyorlar.



        // NOT: TurnInPlace buradan tamamen kaldırıldı - States/LocomotionState.cs içinde yaşıyor.

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