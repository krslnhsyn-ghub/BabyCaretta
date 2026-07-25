namespace Game.Character.States
{
    /// <summary>
    /// Aktif state'i tutar ve her karede Tick'i ona delege eder. TurtleController, hangi
    /// CharacterState'in aktif olduğuna göre ChangeState() ile hangi ITurtleState'in
    /// çalışacağını belirler (örn. Idle/Walk/Turn iken LocomotionState).
    /// </summary>
    public class TurtleStateMachine
    {
        public ITurtleState CurrentState { get; private set; }

        /// <summary>
        /// Yeni bir state'e geçer: eski state varsa Exit() çağrılır, yeni state varsa Enter() çağrılır.
        /// newState olarak null verilirse (örn. Hop/Shell gibi henüz taşınmamış bir state'e geçilirken)
        /// sadece mevcut state'ten çıkılır, state machine "boşta" kalır.
        /// </summary>
        public void ChangeState(ITurtleState newState, TurtleContext context)
        {
            if (CurrentState == newState) return;

            CurrentState?.Exit(context);
            CurrentState = newState;
            CurrentState?.Enter(context);
        }

        /// <summary>Aktif state varsa onun Tick'ini çalıştırır; yoksa hiçbir şey yapmaz.</summary>
        public void Tick(TurtleContext context)
        {
            CurrentState?.Tick(context);
        }
    }
}
