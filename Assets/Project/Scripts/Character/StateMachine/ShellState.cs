using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Character.States
{
    /// <summary>
    /// Kabuk (Q) — toggle self-action. ShellEnter -> ShellIdle -> (tekrar Q) -> ShellExit -> Idle.
    ///
    /// NOT: ShellSlide BURAYA taşınmadı. ShellSlide, düz Slide state'iyle currentSlideSpeed /
    /// smoothedSlideDirection / UpdateSmoothedSlideVectors() alanlarını paylaşıyor; Slide henüz
    /// ayrılmadığı için ShellSlide de TurtleController'da (eski switch'te) kalıyor. Slide taşındığında
    /// ShellSlide de onunla birlikte taşınacak.
    ///
    /// HandleInput() diğer state'lerdeki gibi Tick() içinde DEĞİL - çünkü Q'ya ne zaman basılacağı
    /// (kabuğa girmek için Idle/Walk/Turn'deyken, çıkmak için ShellIdle/ShellSlide'dayken) bu state
    /// aktif değilken de olabiliyor. Bu yüzden TurtleController.Update() bunu HER KAREDE, state ne
    /// olursa olsun çağırıyor - tıpkı Hop'un giriş kontrolü gibi.
    /// </summary>
    public class ShellState : ITurtleState
    {
        private float shellActionTimer;

        public void HandleInput(TurtleContext context)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Q pressed down (yeni bir basış - basılı tutmak değil)
            if (!keyboard.qKey.wasPressedThisFrame) return;

            var controller = context.Controller;

            // If we are not in shell (Idle/Walk/Turn), enter shell
            if (controller.CurrentState == CharacterState.Idle || controller.CurrentState == CharacterState.Walk || controller.CurrentState == CharacterState.Turn)
            {
                controller.CurrentState = CharacterState.ShellEnter;
                shellActionTimer = 0f;
                context.Animator.SetTrigger(TurtleController.ShellEnterHash);
                return;
            }

            // ShellIdle veya ShellSlide'dayken tekrar Q -> kabuktan çık (kayma varsa durdurulur).
            // Kaymayı durdurmak artık Q'yu BIRAKMAYA değil, tekrar BASMAYA bağlı.
            if (controller.CurrentState == CharacterState.ShellIdle || controller.CurrentState == CharacterState.ShellSlide)
            {
                controller.CurrentState = CharacterState.ShellExit;
                shellActionTimer = 0f;
                controller.ResetSlideMotion();
                context.Animator.SetBool(TurtleController.IsSlidingHash, false);
                context.Animator.SetTrigger(TurtleController.ShellExitHash);
                return;
            }
            // ShellEnter veya ShellExit sırasındaysak yoksay, geçiş bitsin

            // NOT: Q bırakıldığında (wasReleasedThisFrame) artık HİÇBİR ŞEY yapmıyoruz.
            // Kayma, Q basılı tutulduğu sürece değil - tekrar Q'ya BASILANA kadar devam eder.
        }

        public void Enter(TurtleContext context) { }

        public void Tick(TurtleContext context)
        {
            var controller = context.Controller;

            switch (controller.CurrentState)
            {
                case CharacterState.ShellEnter:
                    shellActionTimer += Time.deltaTime;
                    if (shellActionTimer >= controller.ShellTransitionDuration)
                    {
                        controller.CurrentState = CharacterState.ShellIdle;
                    }
                    break;

                case CharacterState.ShellIdle:
                    // Kabuktayken Q'ya BASILI TUTMAK (ham eğim değil) kaymayı tetikler.
                    // Zemin yeterince dikse ve Q şu an basılıysa kaymaya başla.
                    bool qHeldForSlide = Keyboard.current != null && Keyboard.current.qKey.isPressed;
                    if (context.HasGroundContact && context.SlopeAngle > controller.ShellSlideStartSlope && qHeldForSlide)
                    {
                        // Sıfırdan değil, küçük bir itiliş hızıyla başla - "kendini ittirmiş" hissi verir
                        controller.EnterShellSlideFromShell(controller.ShellSlideStartBoost);
                        // İstersen burada kısa bir "itiliş" animasyon trigger'ı da tetikleyebilirsin,
                        // örn. animator.SetTrigger(ShellSlideStartHash) - Animator Controller'ına
                        // uygun bir trigger parametresi eklersen (root motion X/Z'yi etkilemeyecek şekilde).
                    }
                    break;

                case CharacterState.ShellExit:
                    shellActionTimer += Time.deltaTime;
                    if (shellActionTimer >= controller.ShellTransitionDuration)
                    {
                        controller.CurrentState = CharacterState.Idle;
                    }
                    break;
            }
        }

        public void Exit(TurtleContext context) { }
    }
}
