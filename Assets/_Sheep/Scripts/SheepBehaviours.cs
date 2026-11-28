using UnityEngine;
using UnityEngine.AI;
using static SheepInstance;

public class SheepBehaviours : MonoBehaviour
{
    //References
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public SheepDefinition definition;
    [HideInInspector] public Transform playerTransform;

    //Position targeting
    [HideInInspector] public Vector3 wanderTarget;
    [HideInInspector] public Vector3 spawnPosition;

    //Behaviour variables
    private float stateTimer;
    private bool isIdling = false;

    public void GetWanderTarget(Vector3 centerPoint, float radius)
    {
        Vector2 randomFlat = Random.insideUnitCircle * radius;
        Vector3 target = centerPoint + new Vector3(randomFlat.x, 0, randomFlat.y);

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            wanderTarget = hit.position;
            agent.SetDestination(wanderTarget);
        }
    }
    public void WanderingState(Vector3 centerPoint, float radius)
    {
        if (agent == null || definition == null) return;

        //wandering logic around specific point either spawn point OR player transform
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (!isIdling)
            {
                isIdling = true;
                stateTimer = Random.Range(2f, 5f);
                agent.isStopped = true;
            }

            else
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    isIdling = false;
                    agent.isStopped = false;
                    GetWanderTarget(centerPoint, radius);
                }
            }
        }
    }

    public void FollowingState()

    {
        //following while wandering around the shepherd randomly
        if (playerTransform == null || agent == null || definition == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float leashRadius = definition != null ? definition.wanderRadius : 5f;

        if (distanceToPlayer > leashRadius * 1.5f)

        {
            //player has outrun sheep, sheep runs to catch up
            agent.isStopped = false;
            agent.speed = definition.movementSpeed * 1.5f;
            agent.SetDestination(playerTransform.position);
        }

        else

        {
            //player walking normally, sheep keep pace and wander around
            agent.isStopped = false;
            agent.speed = definition.movementSpeed;

            //selects new wander when approaching previous goal
            if (!agent.pathPending && agent.remainingDistance < 1f)

            {
                GetWanderTarget(playerTransform.position, leashRadius);
            }
        }
    }

    public void EnterCombatScene(Vector3 clumpPosition)
    {
        //freezes sheep in place, defining clump position will put them in spot in question
        agent.isStopped = true;
        agent.Warp(clumpPosition);
    }

    public void ScaredOff(GameObject selfObject)
    {
        //should trigger on hit, runs the sheep returning to camp

        Debug.Log($"{selfObject.name} was scared off! Go back to camp to get them.");
        //Need to add a line that communicates to sheep tracking script that sheep has been hit and despawned.
        Destroy(selfObject);
    }

    public void KeptInCrook(GameObject selfObject)
    {
        //Code to send magical sheep into sheep inventory system.
        Destroy(selfObject);
    }

}