using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Game.Predators;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Approach Target", story: "Moves toward the [Target] at [Speed] until within [GrabRange]", category: "Action", id: "903afefafd8c644103c0fad39095ef03")]
public partial class ApproachTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;
    [SerializeReference] public BlackboardVariable<float> Speed;
    [SerializeReference] public BlackboardVariable<float> GrabRange;

    // Bu çalışma boyunca (OnStart -> OnEnd) geçici önbellek, serialize edilmiyor.
    private PredatorController cachedController;
    private Animator cachedAnimator;

    protected override Status OnStart()
    {
        if (Target.Value == null) return Status.Failure;
        cachedController = GameObject.GetComponent<PredatorController>();
        cachedAnimator = GameObject.GetComponent<Animator>();
        cachedAnimator?.SetBool("IsMoving", true);
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target.Value == null) return Status.Failure;

        Transform self = GameObject.transform;

        // Kaplumbağanın merkezi yerine, en yakın BOŞ bacağı hedefliyoruz - böylece
        // Grab anında ışınlanma/atlama olmaz, zaten o bacağın yanındayızdır.
        Transform nearestLeg = cachedController != null && cachedController.Target != null
            ? cachedController.Target.PeekNearestFreeLeg(self.position)
            : null;
        Vector3 targetPosition = nearestLeg != null ? nearestLeg.position : Target.Value.position;

        // GrabRange'e girdik mi? Girdiysek bu node başarıyla bitiyor, sıradaki
        // (Grab Target) node'a geçilecek.
        float distance = Vector3.Distance(self.position, targetPosition);
        if (distance <= GrabRange.Value)
        {
            return Status.Success;
        }

        // Düz çizgide hedefe doğru ilerle (NavMesh yok, basit hareket).
        self.position = Vector3.MoveTowards(self.position, targetPosition, Speed.Value * Time.deltaTime);

        // Hedefe dönük dursun (Y ekseni sabit, sadece yatayda döner).
        Vector3 lookTarget = new Vector3(targetPosition.x, self.position.y, targetPosition.z);
        if (lookTarget != self.position)
        {
            self.LookAt(lookTarget);
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        cachedAnimator?.SetBool("IsMoving", false);
    }
}

