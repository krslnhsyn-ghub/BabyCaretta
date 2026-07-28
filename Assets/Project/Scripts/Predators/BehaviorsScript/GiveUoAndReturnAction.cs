using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Game.Predators;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Give uo And Return", story: "Waits then returns to Spawn Position", category: "Action", id: "00596f9660a920e65d975783fc990b21")]
public partial class GiveUoAndReturnAction : Action
{
    [SerializeReference] public BlackboardVariable<float> BurrowedWaitDelay;
    [SerializeReference] public BlackboardVariable<float> ShellWaitDelay;
    [SerializeReference] public BlackboardVariable<float> Speed;
    [SerializeReference] public BlackboardVariable<Transform> Target;
    [SerializeReference] public BlackboardVariable<float> DetectRange;

    // Bu çalışma boyunca geçici durum, serialize edilmiyor.
    private PredatorController cachedController;
    private UnityEngine.AI.NavMeshAgent cachedAgent;
    private float elapsed;
    private float chosenDelay;
    private bool isReturning;

protected override Status OnStart()
    {
        cachedController = GameObject.GetComponent<PredatorController>();
        cachedAgent = cachedController != null ? cachedController.Agent : GameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (cachedAgent != null)
        {
            cachedAgent.isStopped = true;
        }
        elapsed = 0f;
        isReturning = false;

        // Neden vazgeçtiğimize göre bekleme süresini seç: hedef gizliyse/menzil dışındaysa
        // (görmüyoruz) kısa süre (Lost); görüyoruz ama Shell'de olduğu için yakalayamıyorsak
        // uzun süre (Blocked).
        bool isBlockedBySheltering = cachedController != null && cachedController.Target != null
            && !cachedController.Target.IsHidden && !cachedController.Target.IsVulnerable;

        if (isBlockedBySheltering)
        {
            chosenDelay = ShellWaitDelay != null ? ShellWaitDelay.Value : 12f;
        }
        else
        {
            chosenDelay = BurrowedWaitDelay != null ? BurrowedWaitDelay.Value : 4f;
        }

        return Status.Running;
    }

protected override Status OnUpdate()
    {
        if (cachedController == null) return Status.Failure;

        // Beklerken ya da dönerken hedef tekrar avlanabilir hale geldiyse (görünür,
        // korunmasız, menzilde) hemen vazgeç - Try In Order bir sonraki Repeat turunda
        // Hunt dalını tekrar deneyecek.
        if (cachedController.Target != null && Target.Value != null)
        {
            bool huntableAgain = !cachedController.Target.IsHidden
                && cachedController.Target.IsVulnerable
                && Vector3.Distance(GameObject.transform.position, Target.Value.position) <= (DetectRange != null ? DetectRange.Value : float.MaxValue);

            if (huntableAgain)
            {
                return Status.Failure;
            }
        }

        if (!isReturning)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= chosenDelay)
            {
                isReturning = true;
                GameObject.GetComponent<Animator>()?.SetBool("IsMoving", true);
                if (cachedAgent != null)
                {
                    cachedAgent.isStopped = false;
                    cachedAgent.speed = Speed != null ? Speed.Value : 2f;
                }
            }
            return Status.Running;
        }

        Transform self = GameObject.transform;
        Vector3 spawnPosition = cachedController.SpawnPosition;

        float distance = Vector3.Distance(self.position, spawnPosition);
        if (distance <= 0.3f)
        {
            return Status.Success;
        }

        if (cachedAgent != null)
        {
            cachedAgent.SetDestination(spawnPosition);
        }
        else
        {
            float speed = Speed != null ? Speed.Value : 2f;
            self.position = Vector3.MoveTowards(self.position, spawnPosition, speed * Time.deltaTime);
        }
        return Status.Running;
    }

protected override void OnEnd()
    {
        GameObject.GetComponent<Animator>()?.SetBool("IsMoving", false);
        if (cachedAgent != null)
        {
            cachedAgent.isStopped = true;
        }
    }
}

