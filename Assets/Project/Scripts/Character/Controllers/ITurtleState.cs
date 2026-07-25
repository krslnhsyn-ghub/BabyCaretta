namespace Game.Character.States
{
    /// <summary>
    /// Karakterin girebileceği her state (Idle/Walk/Turn, ileride Hop/Shell/Slide vb.) bu
    /// arayüzü uygular. TurtleStateMachine aktif state'i bu arayüz üzerinden çağırır.
    ///
    /// - Enter : bu state'e YENİ girildiğinde bir kere çalışır (kurulum/reset için).
    /// - Tick  : bu state aktif olduğu SÜRECE her karede çalışır (eski Update() mantığının yeri).
    /// - Exit  : bu state'ten ÇIKILIRKEN bir kere çalışır (temizlik için).
    /// </summary>
    public interface ITurtleState
    {
        void Enter(TurtleContext context);
        void Tick(TurtleContext context);
        void Exit(TurtleContext context);
    }
}
