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
// State'ler (CharacterState enum):
//   Idle -> Walk  : W/S basılınca
//   Idle -> Turn  : sadece A/D basılınca (W/S yokken)
//   Walk -> Idle  : W/S bırakılınca
//   Turn -> Idle  : A/D bırakılınca
//   Idle/Walk/Turn -> Hop  : Space'e basılınca (hopDuration sonra Idle'a döner)
//   Idle/Walk/Turn -> ShellEnter -> ShellIdle -> (tekrar Q) -> ShellExit -> Idle
//   Idle/Walk/Turn -> Dig  : E kısa basış (digDuration sonra Idle'a döner)
//   Idle/Walk/Turn -> Burrow : E basılı tutma (bırakılana kadar), bırakınca Idle
//   Yükseklik (Y) animasyondan, ileri mesafe (X/Z) koddan gelir (Hop için).
//
// Etkileşim (LMB = Mouth, RMB = Body — Tap ve Hold ikisi de destekleniyor):
//   Ham buton durumu (isPressed/wasPressed/wasReleased) her karede InteractionController'a
//   iletilir. Tap/Hold ayrımına InteractionController karar verir, bu sınıf sadece iletir.
//
// İçerdiği fonksiyonlar:
//   - Awake()                 : referansları alır
//   - Update()                : input okur, switch ile state mantığını çalıştırır, etkileşimi iletir
//   - UpdateShellInput()      : Q tuşunu okur, Shell state geçişlerini tetikler (self-action)
//   - UpdateSandInput()       : E tuşunu okur, Dig(tap)/Burrow(hold) geçişlerini tetikler (self-action)
//   - OnAnimatorMove()        : root motion'ı (sadece Y) CharacterController'a aktarır
//   - ReadMoveInput()         : WASD/ok tuşlarından ham girdi okur
//   - ReadHopInput()          : Hop tuşunu (Space) okur
//   - MoveForwardAndTurn()    : giderken kendi ekseninde kavisli döner + ileri/geri gider
//   - TurnInPlace()           : hareket etmeden yerinde döner
//   - ApplyGravity()          : CharacterController'a yerçekimi ekler
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
        // Kum (E) — Tap=Dig, Hold=Burrow
        Dig,
        Burrow,
    }

    [RequireComponent(typeof(CharacterController))]
    public class TurtleController : MonoBehaviour
    {
        [Header("Hareket Ayarları")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float moveRotationSpeed = 10f; // giderken dönüş hızı çarpanı
        [SerializeField] private float turnInPlaceSpeed = 90f;  // derece/saniye, yerinde dönerken

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
        [SerializeField] private float sandHoldThreshold = 0.2f; // bu süreden kısa E basışı Dig, uzunu Burrow sayılır

        private CharacterController controller;
        private Animator animator;
        private InteractionController interactionController;
        private Vector3 verticalVelocity;

        private CharacterState currentState = CharacterState.Idle;
        private Vector2 moveInput;

        private float hopTimer;
        private float actionTimer; // Shell/Dig geçişleri için ortak zamanlayıcı (Hop'un hopTimer'ından ayrı)

        // E tuşu için Tap/Hold kararı bekleniyor mu
        private float sandPressStartTime;
        private bool sandAwaitingDecision;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>(); // rig/dummy karakter gelene kadar null olabilir, sorun değil
            interactionController = GetComponent<InteractionController>(); // henüz eklenmediyse null olabilir
        }

        private void Update()
        {
            moveInput = ReadMoveInput();
            bool hasForwardInput = Mathf.Abs(moveInput.y) > 0.01f;
            bool hasTurnInput = Mathf.Abs(moveInput.x) > 0.01f;

            // Bu "meşgul" state'lerdeyken Hop tetiklenemez (kabukta/kazarken/gömülüyken zıplama yok).
            bool isBusyWithSelfAction =
                currentState == CharacterState.ShellEnter || currentState == CharacterState.ShellIdle ||
                currentState == CharacterState.ShellExit || currentState == CharacterState.Dig ||
                currentState == CharacterState.Burrow;

            // Hop, Idle/Walk/Turn'den tetiklenebilir (zaten Hop'tayken ya da meşgulken hariç).
            if (ReadHopInput() && currentState != CharacterState.Hop && !isBusyWithSelfAction)
            {
                currentState = CharacterState.Hop;
                hopTimer = 0f;
                animator?.SetTrigger("Hop");
            }

            // Q: Kabuk toggle'ı — self-action, InteractionController'a hiç uğramaz.
            UpdateShellInput();

            // E: Dig(tap) / Burrow(hold) — self-action, InteractionController'a hiç uğramaz.
            UpdateSandInput();

            // Mouth/Body: dış nesne aranması gereken etkileşimler, InteractionController'a iletilir.
            var mouse = Mouse.current;
            if (mouse != null && interactionController != null)
            {
                interactionController.UpdateOrganInput(InteractionOrgan.Mouth, mouse.leftButton.isPressed, mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
                interactionController.UpdateOrganInput(InteractionOrgan.Body, mouse.rightButton.isPressed, mouse.rightButton.wasPressedThisFrame, mouse.rightButton.wasReleasedThisFrame);
            }

            switch (currentState)
            {
                case CharacterState.Idle:
                    animator?.SetFloat("Speed", 0f);
                    animator?.SetFloat("TurnDirection", 0f);
                    if (hasForwardInput) currentState = CharacterState.Walk;
                    else if (hasTurnInput) currentState = CharacterState.Turn;
                    break;

                case CharacterState.Walk:
                    if (!hasForwardInput)
                    {
                        currentState = CharacterState.Idle;
                        break;
                    }
                    MoveForwardAndTurn(moveInput);
                    animator?.SetFloat("Speed", Mathf.Abs(moveInput.y));
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
                    animator?.SetFloat("TurnDirection", moveInput.x);
                    break;

                case CharacterState.Hop:
                    hopTimer += Time.deltaTime;
                    // Animasyonun hazırlık karesi bitmeden ileri gitme — "önce kayıyor sonra zıplıyor" hissini önler.
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
                    // Kabuktayken oyuncu WASD ile hareket edemez (switch'te ilgili case yok).
                    // İleride: zemin eğimi kontrolü buraya eklenip eğim varsa ShellSlide'a geçilecek.
                    break;

                case CharacterState.ShellExit:
                    actionTimer += Time.deltaTime;
                    if (actionTimer >= shellTransitionDuration)
                    {
                        currentState = CharacterState.Idle;
                    }
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

            // Hop sırasında yerçekimini uygulamıyoruz — animasyon (Loco_Jump) zaten
            // baştan sona kendi yukarı-aşağı hareketini taşıyor. Aksi halde kod ile
            // animasyon aynı anda Y ekseninde birbiriyle "yarışır" ve zıplama boğulur.
            if (currentState != CharacterState.Hop)
            {
                ApplyGravity();
            }
        }

        // Q: basit aç/kapa. Idle/Walk/Turn'deyken kabuğa girer (refleks olsun diye
        // yürürken de çalışır), ShellIdle'dayken tekrar basınca çıkar.
        private void UpdateShellInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.qKey.wasPressedThisFrame) return;

            bool canEnter = currentState == CharacterState.Idle || currentState == CharacterState.Walk || currentState == CharacterState.Turn;

            if (canEnter)
            {
                currentState = CharacterState.ShellEnter;
                actionTimer = 0f;
                animator?.SetTrigger("ShellEnter");
            }
            else if (currentState == CharacterState.ShellIdle)
            {
                currentState = CharacterState.ShellExit;
                actionTimer = 0f;
                animator?.SetTrigger("ShellExit");
            }
        }

        // E: kısa basış Dig, basılı tutma Burrow. Mouth/Body'deki Tap/Hold mantığıyla
        // aynı desen, ama dış nesne aramadığı için InteractionController'a hiç uğramaz.
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
                    animator?.SetBool("IsBurrowing", true);
                }
            }

            if (keyboard.eKey.wasReleasedThisFrame)
            {
                if (currentState == CharacterState.Burrow)
                {
                    currentState = CharacterState.Idle;
                    animator?.SetBool("IsBurrowing", false);
                }
                else if (sandAwaitingDecision && canStart)
                {
                    currentState = CharacterState.Dig;
                    actionTimer = 0f;
                    animator?.SetTrigger("Dig");
                }

                sandAwaitingDecision = false;
            }
        }

        // Animator "Apply Root Motion" açık bir state oynatırken Unity bunu
        // otomatik çağırır. Sadece Y (yükseklik) eksenini animasyondan alıyoruz —
        // ileri mesafe (X/Z) Hop case'inde kod tarafından kontrol ediliyor.
        private void OnAnimatorMove()
        {
            if (animator == null) return;

            Vector3 delta = animator.deltaPosition;
            delta.x = 0f;
            delta.z = 0f;
            controller.Move(delta);
        }

        // WASD veya ok tuşlarını okur, -1..1 aralığında bir yön vektörü döner.
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

        // Şimdilik Space tuşu. İleride context-sensitive Interact tuşuna taşınabilir.
        private bool ReadHopInput()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }

        // W/S basılıyken çağrılır. 1) A/D varsa karakteri giderken kendi ekseninde
        // kavisli döndürür, 2) karakteri her zaman kendi burnunun dikine göre hareket ettirir.
        private void MoveForwardAndTurn(Vector2 input)
        {
            transform.Rotate(Vector3.up, input.x * moveRotationSpeed * 10f * Time.deltaTime);

            Vector3 direction = transform.forward * input.y;
            controller.Move(direction.normalized * moveSpeed * Time.deltaTime);
        }

        // Hareket etmeden, sadece kendi ekseninde döner (turn-in-place).
        // turnDirection: -1 (sola) .. 1 (sağa)
        private void TurnInPlace(float turnDirection)
        {
            transform.Rotate(Vector3.up, turnDirection * turnInPlaceSpeed * Time.deltaTime);
        }

        // CharacterController kendi başına yerçekimi uygulamaz, elle eklememiz gerekiyor.
        private void ApplyGravity()
        {
            if (controller.isGrounded && verticalVelocity.y < 0)
            {
                verticalVelocity.y = groundedStickForce;
            }

            verticalVelocity.y += gravity * Time.deltaTime;
            controller.Move(verticalVelocity * Time.deltaTime);
        }
    }
}