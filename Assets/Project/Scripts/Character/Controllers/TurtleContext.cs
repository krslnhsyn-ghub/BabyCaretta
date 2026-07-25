using UnityEngine;

namespace Game.Character.States
{
    /// <summary>
    /// State'lerin ihtiyaç duyduğu paylaşılan referanslar ve o anki (o karedeki) veriler burada
    /// toplanır. TurtleController Awake()'te bir kere oluşturur; her Update()'te güncel input/zemin
    /// bilgisini buraya yazar. State'ler SADECE bu sınıf (ve Controller üzerindeki public
    /// property'ler) üzerinden okur/yazar - TurtleController'ın private alanlarına doğrudan erişemez.
    ///
    /// NOT: Hız/eğim/yürüme gibi [SerializeField] ayarları burada KOPYALANMIYOR. Bunun yerine
    /// Controller üzerinde public property olarak duruyorlar (örn. context.Controller.MoveSpeed).
    /// Böylece Inspector'dan runtime'da değer değiştirirsen state anında günceli görür.
    /// </summary>
    public class TurtleContext
    {
        public TurtleController Controller { get; }
        public CharacterController CharController { get; }
        public Animator Animator { get; }
        public Transform Transform { get; }

        // ---- Her karede TurtleController.Update() tarafından güncellenen paylaşılan veri ----
        public Vector2 MoveInput { get; set; }
        public float SlopeAngle { get; set; }
        public Vector3 SlideDirection { get; set; }
        public Vector3 SlideSideDirection { get; set; }
        public bool HasGroundContact { get; set; }

        public TurtleContext(TurtleController controller, CharacterController charController, Animator animator)
        {
            Controller = controller;
            CharController = charController;
            Animator = animator;
            Transform = controller.transform;
        }
    }
}