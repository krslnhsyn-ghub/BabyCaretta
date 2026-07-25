using UnityEngine;

namespace Game.Character.States
{
    /// <summary>
    /// Hop (Space): kısa bir zıplama/ileri atılış. Yükseklik (Y) animasyondan gelir, ileri
    /// mesafe (X/Z) burada kod kontrollüdür. Süre dolunca otomatik olarak Idle'a döner.
    ///
    /// NOT: Enter() burada gerçekten iş yapıyor (LocomotionState'in aksine) - hopTimer'ı
    /// sıfırlıyor ve Hop animasyon trigger'ını burada tetikliyoruz. Bunlar eskiden
    /// TurtleController.Update() içinde, CurrentState = Hop atanır atanmaz inline çalışıyordu.
    /// </summary>
    public class HopState : ITurtleState
    {
        private float hopTimer;

        public void Enter(TurtleContext context)
        {
            hopTimer = 0f;
            context.Animator.SetTrigger(TurtleController.HopHash);
        }

        public void Tick(TurtleContext context)
        {
            var controller = context.Controller;

            hopTimer += Time.deltaTime;
            if (hopTimer >= controller.HopForwardDelay)
            {
                context.CharController.Move(context.Transform.forward * controller.HopMoveSpeed * Time.deltaTime);
            }
            if (hopTimer >= controller.HopDuration)
            {
                controller.CurrentState = CharacterState.Idle;
            }
        }

        public void Exit(TurtleContext context) { }
    }
}
