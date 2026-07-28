using UnityEngine;
using UnityEngine.InputSystem;
using Game.Interaction;

namespace Game.Character.States
{
    /// <summary>
    /// Kum aksiyonları (E): Tap -> Dig (kısa, kum serpme + stun), Hold -> Burrow (gömülü kalma).
    ///
    /// HandleInput() diğer state'lerdeki gibi Tick() içinde DEĞİL - tıpkı ShellState.HandleInput()
    /// gibi, E'ye ne zaman basılacağı bu state aktif değilken de olabiliyor (Idle/Walk/Turn'dan
    /// giriş kararı veriliyor). Bu yüzden TurtleController.Update() bunu HER KAREDE, state ne
    /// olursa olsun çağırıyor.
    /// </summary>
    public class SandState : ITurtleState
    {
        private float sandPressStartTime;
        private bool sandAwaitingDecision;
        private float digTimer; // Dig animasyon süresi sayacı (eskiden paylaşılan actionTimer'dı)

        public void HandleInput(TurtleContext context)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var controller = context.Controller;
            bool canStart = controller.CurrentState == CharacterState.Idle || controller.CurrentState == CharacterState.Walk || controller.CurrentState == CharacterState.Turn;

            // Check if ground is sand
            bool isSand = false;
            if (canStart)
            {
                Vector3 origin = context.Transform.position + Vector3.up * 0.5f;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, controller.GroundCheckDistance, controller.GroundLayerMask))
                {
                    isSand = hit.collider.CompareTag("Sand");
                }
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                sandPressStartTime = Time.time;
                sandAwaitingDecision = true;
            }

            if (sandAwaitingDecision && keyboard.eKey.isPressed && Time.time - sandPressStartTime >= controller.SandHoldThreshold)
            {
                sandAwaitingDecision = false;
                if (canStart && isSand)
                {
                    controller.CurrentState = CharacterState.Burrow;
                    context.Animator.SetBool(TurtleController.IsBurrowingHash, true);
                    controller.IsHidden = true;

                    // Play burrow particle effect
                    if (controller.BurrowSpawnPoint != null && controller.SandBurrowParticle != null)
                    {
                        var ps = controller.SandBurrowParticle;
                        ps.transform.position = controller.BurrowSpawnPoint.position;
                        var rot = ps.transform.rotation;
                        rot = Quaternion.Euler(rot.eulerAngles.x, context.Transform.eulerAngles.y, rot.eulerAngles.z);
                        ps.transform.rotation = rot;
                        ps.Play();
                    }
                }
            }

            if (keyboard.eKey.wasReleasedThisFrame)
            {
                if (controller.CurrentState == CharacterState.Burrow)
                {
                    controller.CurrentState = CharacterState.Idle;
                    context.Animator.SetBool(TurtleController.IsBurrowingHash, false);
                    controller.IsHidden = false;
                }
                else if (sandAwaitingDecision && canStart && isSand)
                {
                    controller.CurrentState = CharacterState.Dig;
                    digTimer = 0f;
                    context.Animator.SetTrigger(TurtleController.DigHash);
                    ApplySandStunEffect(context);

                    // Play dig particle effect
                    if (controller.DigSpawnPoint != null && controller.SandDigParticle != null)
                    {
                        var ps = controller.SandDigParticle;
                        ps.transform.position = controller.DigSpawnPoint.position;
                        var rot = ps.transform.rotation;
                        rot = Quaternion.Euler(rot.eulerAngles.x, context.Transform.eulerAngles.y, rot.eulerAngles.z);
                        ps.transform.rotation = rot;
                        ps.Play();
                    }
                }

                sandAwaitingDecision = false;
            }

            // NOT: E bırakıldığında burada zaten Dig/Burrow kararı veriliyor (yukarıda) -
            // bu blok her karede E'nin durumuna göre karar üretir, aynen eski UpdateSandInput gibi.
        }

        public void Enter(TurtleContext context) { }

        public void Tick(TurtleContext context)
        {
            var controller = context.Controller;

            switch (controller.CurrentState)
            {
                case CharacterState.Dig:
                    digTimer += Time.deltaTime;
                    if (digTimer >= controller.DigDuration)
                    {
                        controller.CurrentState = CharacterState.Idle;
                    }
                    break;

                case CharacterState.Burrow:
                    // Hold süresince buradayız, çıkışı HandleInput() (E bırakılınca) tetikler.
                    break;
            }
        }

        public void Exit(TurtleContext context) { }

        /// <summary>
        /// E'ye kısa basış (Dig/kum serpme) anında, önümüzdeki koni içindeki tüm IStunnable
        /// nesneleri (ör. yengeç) sersemletir. Tepkiye (donma, kaçma, ses vb.) her nesne kendi
        /// script'inde karar verir - burada sadece "şu kadar süre sersemledin" bilgisi iletilir.
        /// (TurtleController.ApplySandStunEffect'ten birebir taşındı.)
        /// </summary>
        private void ApplySandStunEffect(TurtleContext context)
        {
            var controller = context.Controller;
            Collider[] hits = Physics.OverlapSphere(context.Transform.position, controller.SandEffectRadius, controller.StunnableMask);

            foreach (var hit in hits)
            {
                // Skip self (the turtle) to avoid stunning ourselves
                if (hit.gameObject == controller.gameObject)
                {
                    continue;
                }

                Vector3 toTarget = hit.transform.position - context.Transform.position;
                float angle = Vector3.Angle(context.Transform.forward, toTarget);
                if (angle > controller.SandEffectAngle * 0.5f) continue;

                IStunnable stunnable = hit.GetComponentInParent<IStunnable>();
                if (stunnable != null)
                {
                    Debug.Log($"Sand stun hitting {hit.name} (parent stunnable found)");
                    stunnable.Stun(controller.SandStunDuration);
                }
                else
                {
                    Debug.Log($"Sand hit {hit.name} but no IStunnable found in parents");
                }
            }
        }
    }
}
