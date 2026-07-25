using UnityEngine;

namespace Game.Character.States
{
    /// <summary>
    /// Normal yürüyüş: Idle / Walk / Turn. Üç durumu da tek dosyada tutuyoruz çünkü aralarındaki
    /// geçişler (W'ye basınca Idle->Walk, bırakınca Walk->Idle, vb.) birbirine çok sıkı bağlı ve
    /// hepsi TurtleController'daki eski switch'in bir parçasıydı - davranış birebir buraya taşındı.
    ///
    /// NOT: Bu state'in Enter()/Exit() metodları şimdilik boş. Orijinal kodda Idle/Walk/Turn'e
    /// "girerken bir kere çalışacak" bir kurulum mantığı yoktu (her kare idempotent şekilde
    /// çalışıyordu) - o yüzden burada da eklemedik. İleride Hop/Shell gibi state'ler de
    /// ITurtleState'e taşınırsa, locomotion'a GERİ dönüşte Enter() anlamlı hale gelebilir.
    /// </summary>
    public class LocomotionState : ITurtleState
    {
        // Yürüme ivmelenmesi için o anki (yumuşatılmış) hız - hedef hıza MoveTowards ile yaklaşır.
        // Eskiden TurtleController'da bir field'dı, artık sadece bu state'i ilgilendiriyor.
        private float currentMoveSpeed;

        public void Enter(TurtleContext context) { }

        public void Exit(TurtleContext context) { }

        public void Tick(TurtleContext context)
        {
            var controller = context.Controller;
            bool hasForwardInput = Mathf.Abs(context.MoveInput.y) > 0.01f;
            bool hasTurnInput = Mathf.Abs(context.MoveInput.x) > 0.01f;

            switch (controller.CurrentState)
            {
                case CharacterState.Idle:
                    context.Animator.SetFloat(TurtleController.SpeedHash, 0f);
                    context.Animator.SetFloat(TurtleController.TurnDirectionHash, 0f);
                    context.Animator.SetBool(TurtleController.IsSlidingHash, false);
                    context.Animator.SetBool(TurtleController.IsRunnerHash, false);
                    if (hasForwardInput) controller.CurrentState = CharacterState.Walk;
                    else if (hasTurnInput) controller.CurrentState = CharacterState.Turn;
                    break;

                case CharacterState.Walk:
                    if (!hasForwardInput)
                    {
                        controller.CurrentState = CharacterState.Idle;
                        currentMoveSpeed = 0f; // bir sonraki hareket başlangıcında ease-in sıfırdan başlasın
                        break;
                    }

                    // Handle slope-based speed modulation and possible slip
                    HandleWalkMovement(context, context.SlopeAngle, context.MoveInput.y);
                    break;

                case CharacterState.Turn:
                    if (hasForwardInput)
                    {
                        controller.CurrentState = CharacterState.Walk;
                        break;
                    }
                    if (!hasTurnInput)
                    {
                        controller.CurrentState = CharacterState.Idle;
                        break;
                    }
                    TurnInPlace(context, context.MoveInput.x);
                    context.Animator.SetFloat(TurtleController.TurnDirectionHash, context.MoveInput.x);
                    context.Animator.SetBool(TurtleController.IsSlidingHash, false);
                    context.Animator.SetBool(TurtleController.IsRunnerHash, false);
                    break;
            }
        }

        /// <summary>
        /// Walking movement with slope-based speed modulation and automatic slip when slope > maxWalkableSlope.
        /// Speed reduction only when moving uphill; downhill or flat moves at base speed.
        /// (TurtleController.HandleWalkMovement'tan birebir taşındı.)
        /// </summary>
        private void HandleWalkMovement(TurtleContext context, float slopeAngle, float forwardInput)
        {
            var controller = context.Controller;

            // Gerçek hareket yönü: W ile ileri, S ile geri - transform.forward'ın işareti forwardInput'a göre değişir.
            // ÖNEMLİ: "yukarı tırmanma" tespitini forwardInput'un işaretine (W/S) göre DEĞİL, bu gerçek hareket
            // vektörünün eğime göre yönüne göre yapıyoruz. Aksi halde S ile geri geri yürüyerek (burun aşağı
            // bakarken) çok dik yokuşları da hiç yavaşlamadan/kaymadan tırmanmak mümkün oluyordu - bu bug'dı.
            Vector3 moveDirWorld = context.Transform.forward * forwardInput;
            bool isMoving = Mathf.Abs(forwardInput) > 0.01f;
            bool movingUphill = isMoving && Vector3.Dot(moveDirWorld.normalized, context.SlideDirection) < 0f;

            // Compute speed multiplier from slope (0 = flat, 1 = maxWalkableSlope) only if moving uphill
            float slopeFactor = 0f;
            if (movingUphill)
            {
                slopeFactor = Mathf.InverseLerp(0f, controller.MaxWalkableSlope, slopeAngle);
                slopeFactor = Mathf.Clamp01(slopeFactor);
            }
            float speedMultiplier = controller.WalkSpeedBySlope.Evaluate(slopeFactor);
            // NOT: shellSlideSpeedMultiplier buraya KASITLI olarak katılmıyor - yürüme hızı
            // shell slide ayarlarından tamamen bağımsız olmalı.
            // Geri geri (S) yürürken ayrı bir çarpan uygulanır - ileri hızdan bağımsız ayarlanabilir.
            float directionMultiplier = forwardInput < 0f ? controller.BackwardMoveMultiplier : 1f;
            float targetSpeed = controller.MoveSpeed * speedMultiplier * directionMultiplier * controller.RestrainedSpeedMultiplier;

            // Sadece GERÇEKTEN yukarı tırmanmaya çalışırken (movingUphill, W ya da S fark etmez) ve
            // eşik aşılmışsa kay. Aynı dik zeminde aşağı inerken (movingUphill == false) tetiklenmemeli.
            if (movingUphill && slopeAngle > controller.MaxWalkableSlope)
            {
                // Slide state'i bu refactor adımında henüz ayrılmadı - kontrolü Controller'a devret.
                // Ani sıfırlama yerine mevcut yürüme hızını devral - duraksama hissini azaltır.
                controller.EnterSlideFromLocomotion(currentMoveSpeed);
                return;
            }

            // Ease-in/out: anlık sıfırdan hıza zıplamak yerine hedef hıza yumuşakça yaklaş.
            // İvmelenirken moveAccelerationTime, yavaşlarken (girdi bırakılınca/tersine dönünce) moveDecelerationTime kullanılır.
            bool accelerating = Mathf.Abs(targetSpeed) > Mathf.Abs(currentMoveSpeed);
            float easeTime = Mathf.Max(0.0001f, accelerating ? controller.MoveAccelerationTime : controller.MoveDecelerationTime);
            float maxDelta = (controller.MoveSpeed / easeTime) * Time.deltaTime;
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, maxDelta);

            // Normal movement: apply forward movement with potentially reduced speed
            Vector3 moveDir = context.Transform.forward * forwardInput;
            context.CharController.Move(moveDir.normalized * currentMoveSpeed * Time.deltaTime);

            // Apply turning (A/D) while walking
            if (Mathf.Abs(context.MoveInput.x) > 0.01f)
            {
                context.Transform.Rotate(Vector3.up, context.MoveInput.x * controller.MoveRotationSpeed * 10f * Time.deltaTime);
            }

            // Update animator
            context.Animator.SetFloat(TurtleController.SpeedHash, Mathf.Abs(forwardInput));
            context.Animator.SetFloat(TurtleController.TurnDirectionHash, context.MoveInput.x);
            context.Animator.SetBool(TurtleController.IsSlidingHash, false);
            context.Animator.SetBool(TurtleController.IsRunnerHash, false);
        }

        /// <summary>(TurtleController.TurnInPlace'ten birebir taşındı.)</summary>
        private void TurnInPlace(TurtleContext context, float turnDirection)
        {
            context.Transform.Rotate(Vector3.up, turnDirection * context.Controller.TurnInPlaceSpeed * Time.deltaTime);
        }
    }
}
