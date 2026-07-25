using UnityEngine;

namespace Game.Character.States
{
    /// <summary>
    /// Slide (düz yürüyüş kayması) + ShellSlide (kabuk kayması) — ikisi tek dosyada çünkü
    /// currentSlideSpeed/smoothedSlideDirection/smoothedSlideSideDirection alanlarını paylaşıyorlar.
    /// Bu state'e girişin İKİ yolu var, ikisi de TurtleController üzerinden "devir" şeklinde olur:
    ///   - LocomotionState (Walk) çok dik yokuşta -> controller.EnterSlideFromLocomotion(hız)
    ///   - ShellState (ShellIdle) çok dik yokuşta Q basılıyken -> controller.EnterShellSlideFromShell(hız)
    /// Bu iki metod CurrentState'i değiştirip StartWalkSlide/StartShellSlide'ı çağırıyor.
    /// </summary>
    public class SlideState : ITurtleState
    {
        // Kayma hızı (m/s) - hem Slide hem ShellSlide bunu kullanır, ikisi aynı anda aktif olamayacağı için sorun değil
        private float currentSlideSpeed;

        // Yumuşatılmış kayma yönleri - ham slideDirection/slideSideDirection'ı (Update()'te raycast'ten
        // anlık hesaplanır) yavaşça takip eder, titreşimi azaltır. SADECE burada kullanılır -
        // LocomotionState'teki movingUphill hesabını etkilemez (o ham context.SlideDirection'ı kullanır).
        private Vector3 smoothedSlideDirection;
        private Vector3 smoothedSlideSideDirection;

        /// <summary>LocomotionState -> Slide devrinde çağrılır (controller.EnterSlideFromLocomotion üzerinden).</summary>
        public void StartWalkSlide(float carryOverSpeed)
        {
            currentSlideSpeed = carryOverSpeed;
            smoothedSlideDirection = Vector3.zero; // yön yumuşatmasını yeniden senkronla
        }

        /// <summary>ShellState -> ShellSlide devrinde çağrılır (controller.EnterShellSlideFromShell üzerinden).</summary>
        public void StartShellSlide(float startBoostSpeed)
        {
            currentSlideSpeed = startBoostSpeed;
            smoothedSlideDirection = Vector3.zero; // yön yumuşatmasını yeniden senkronla
        }

        /// <summary>ShellState, Q'ya basılıp kabuktan çıkarken devam eden kaymayı sıfırlamak için çağırır.</summary>
        public void ResetSlideMotion()
        {
            currentSlideSpeed = 0f;
            smoothedSlideDirection = Vector3.zero;
        }

        public void Enter(TurtleContext context) { }

        public void Tick(TurtleContext context)
        {
            switch (context.Controller.CurrentState)
            {
                case CharacterState.Slide:
                    HandleSimpleSlide(context, context.SlopeAngle);
                    break;

                case CharacterState.ShellSlide:
                    HandleShellSlide(context, context.SlopeAngle, context.MoveInput.x);
                    break;
            }
        }

        public void Exit(TurtleContext context) { }

        /// <summary>
        /// Simple gravity‑based slide used when walking on too steep a slope.
        /// (TurtleController.HandleSimpleSlide'tan birebir taşındı.)
        /// </summary>
        private void HandleSimpleSlide(TurtleContext context, float slopeAngle)
        {
            var controller = context.Controller;

            // If slope becomes shallow enough, exit slide
            if (slopeAngle <= controller.MaxWalkableSlope * 0.8f) // hysteresis to avoid jitter
            {
                controller.CurrentState = CharacterState.Idle;
                context.Animator.SetBool(TurtleController.IsSlidingHash, false);
                smoothedSlideDirection = Vector3.zero; // bir sonraki kayma için yeniden senkronla
                return;
            }

            UpdateSmoothedSlideVectors(context);

            // Accelerate due to gravity component along slope
            float gravityComponent = Mathf.Abs(controller.Gravity) * Mathf.Sin(slopeAngle * Mathf.Deg2Rad);
            currentSlideSpeed += gravityComponent * Time.deltaTime;
            // Apply friction to eventually stop if slope flattens
            currentSlideSpeed -= controller.WalkSlideFriction * Time.deltaTime;
            currentSlideSpeed = Mathf.Max(0f, currentSlideSpeed);

            // Move (yumuşatılmış yön - ham raycast normal titremesini azaltır)
            Vector3 motion = smoothedSlideDirection * currentSlideSpeed * Time.deltaTime;
            context.CharController.Move(motion);

            // NOT: Buraya kayma yönüne dönme (facing rotation) eklenmiyor - kullanıcı isteğiyle
            // kaldırıldı. Karakter kayarken bakış yönünü korur, sadece pozisyon kayar.
            // Gövdenin zemine göre tilt'i (pitch/roll) zaten AlignBodyToGround() tarafından
            // ayrıca ve yaw'dan bağımsız olarak yapılıyor.

            context.Animator.SetBool(TurtleController.IsSlidingHash, true);
            context.Animator.SetBool(TurtleController.IsRunnerHash, false);
        }

        /// <summary>
        /// Shell slide behavior: align to down slope, accelerate, friction, side weighting.
        /// A/D tuşları karakteri döndürmez, sadece yan ağırlık verir.
        /// (TurtleController.HandleShellSlide'tan birebir taşındı.)
        /// </summary>
        private void HandleShellSlide(TurtleContext context, float slopeAngle, float sideInput)
        {
            var controller = context.Controller;

            UpdateSmoothedSlideVectors(context);

            // Exit condition: slope too shallow
            if (slopeAngle <= controller.ShellSlideStartSlope * 0.8f) // hysteresis
            {
                // Smoothly decelerate to idle
                currentSlideSpeed -= controller.ShellSlideFriction * Time.deltaTime;
                if (currentSlideSpeed <= 0f)
                {
                    controller.CurrentState = CharacterState.ShellIdle;
                    currentSlideSpeed = 0f;
                    smoothedSlideDirection = Vector3.zero; // bir sonraki kayma için yeniden senkronla
                }
                // Still apply remaining velocity this frame
                Vector3 motion = smoothedSlideDirection * currentSlideSpeed * Time.deltaTime;
                context.CharController.Move(motion);
                // Align to slope gradually
                if (smoothedSlideDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(smoothedSlideDirection, Vector3.up);
                    context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation, targetRot, controller.ShellAlignSpeed * Time.deltaTime);
                }
                context.Animator.SetBool(TurtleController.IsSlidingHash, true);
                context.Animator.SetBool(TurtleController.IsRunnerHash, false);
                return;
            }

            // Acceleration along slope (gravity‑like but controllable)
            float accel = controller.ShellSlideAcceleration * controller.ShellSlideSpeedMultiplier;
            // According to spec, forward input does not affect slide speed; we ignore it.
            currentSlideSpeed += accel * Time.deltaTime;
            currentSlideSpeed = Mathf.Min(currentSlideSpeed, controller.ShellSlideMaxSpeed);

            // Apply friction when there is no longitudinal input
            currentSlideSpeed -= controller.ShellSlideFriction * Time.deltaTime;
            currentSlideSpeed = Mathf.Max(0f, currentSlideSpeed);

            // Sideways influence from A/D: stronger at low speed, fades as speed increases
            float sideInfluence = 1f - Mathf.Clamp01(currentSlideSpeed / controller.ShellSlideMaxSpeed) * (1f - controller.SideInfluenceFade);
            float sideSpeed = sideInput * controller.SlideSideForce * sideInfluence; // NO extra Time.deltaTime here

            // Combine movement vectors (yumuşatılmış yönler - titreşimi azaltır)
            Vector3 movement = (smoothedSlideDirection * currentSlideSpeed + smoothedSlideSideDirection * sideSpeed) * Time.deltaTime;
            context.CharController.Move(movement);

            // Align character to down slope (smooth, not instant)
            if (smoothedSlideDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(smoothedSlideDirection, Vector3.up);
                context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation, targetRot, controller.ShellAlignSpeed * Time.deltaTime);
            }

            // animator
            context.Animator.SetBool(TurtleController.IsSlidingHash, true);
            context.Animator.SetBool(TurtleController.IsRunnerHash, false);
        }

        /// <summary>
        /// Ham slideDirection/slideSideDirection'ı (Update()'te raycast'ten anlık hesaplanır) yavaşça takip eden
        /// yumuşatılmış versiyonlarını günceller. Sadece slide handler'ları çağırır - movingUphill hesabını etkilemez.
        /// (TurtleController.UpdateSmoothedSlideVectors'tan birebir taşındı.)
        /// </summary>
        private void UpdateSmoothedSlideVectors(TurtleContext context)
        {
            if (smoothedSlideDirection.sqrMagnitude < 0.0001f)
            {
                // İlk kare / yeniden senkronlama sonrası: doğrudan ham değere atla
                smoothedSlideDirection = context.SlideDirection;
                smoothedSlideSideDirection = context.SlideSideDirection;
                return;
            }

            smoothedSlideDirection = Vector3.Slerp(smoothedSlideDirection, context.SlideDirection, context.Controller.SlideDirectionSmoothing * Time.deltaTime).normalized;
            smoothedSlideSideDirection = Vector3.Slerp(smoothedSlideSideDirection, context.SlideSideDirection, context.Controller.SlideDirectionSmoothing * Time.deltaTime).normalized;
        }
    }
}
