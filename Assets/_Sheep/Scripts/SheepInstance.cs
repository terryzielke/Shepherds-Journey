using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(SheepBehaviours))]
public class SheepInstance : MonoBehaviour, IInteractable
{
    [Header("Configuration")]
    public SheepDefinition definition;

    [Header("current State")]
    public SheepState currentState = SheepState.Wandering;

    //References
    private SheepBehaviours Behaviours;
    private NavMeshAgent agent;

    //State variables
    private bool inFlock = false;

    public enum SheepState
    {
        Wandering,  //uncollected sheep, wandering around spawn area
        Following,  //collected by shepherd
        Cowering,   //locked in place during combat
        Crooked,    //collected by shepherd & sent to the crook
    }

    void Awake()
    {
        Behaviours = GetComponent<SheepBehaviours>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        //setup handed off to behaviours script
        Behaviours.agent = agent;
        Behaviours.definition = definition;
        Behaviours.spawnPosition = transform.position;

        //PLAYER OBJECT MUST HAVE TAG PLAYER OR FOLLOWING DOES NOT WORK!
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            Behaviours.playerTransform = playerObj.transform;
        }

        if (definition != null)
        {
            //applies any specific physics/stats we want to apply to the magical sheep
            ApplyDefinitionStats();
        }

    }

    void ApplyDefinitionStats()
    {
        Behaviours.agent.speed = definition.movementSpeed;

        switch (definition.ability)
        {
            case SheepDefinition.SheepAbility.Heavy:
                //specific physics surrounding Heavy Sheep
                break;
            case SheepDefinition.SheepAbility.Bouncy:
                //specific physics surrounding Bouncy Sheep
                break;
            case SheepDefinition.SheepAbility.Magnetic:
                //specific physics surrounding Magnetic Sheep
                break;
            case SheepDefinition.SheepAbility.Projectile:
                //specific physics surrounding Projectile Sheep
                break;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case SheepState.Wandering:              //controls all sheep wandering around their initial spawn point
                if (!inFlock)
                {
                    //Wandering around spawn point
                    Behaviours.WanderingState(Behaviours.spawnPosition, definition.wanderRadius);
                }
                break;

            case SheepState.Following:              //controls normal sheep wandering around player after collection
                if (Behaviours.playerTransform == null) break;

                float distanceToPlayer = Vector3.Distance(transform.position, Behaviours.playerTransform.position);
                float leashRadius = definition != null ? definition.wanderRadius : 5f;

                //player movement check informs wether sheep is actively following or idling nearby
                bool playerIsMoving = false;
                CharacterController cc = Behaviours.playerTransform.GetComponent<CharacterController>();

                if (cc != null && cc.velocity.magnitude > 0.05f)
                {
                    playerIsMoving = true;
                }

                if (playerIsMoving || distanceToPlayer > leashRadius * 1.5f)
                {
                    Behaviours.FollowingState();
                }

                else
                {
                    Behaviours.WanderingState(Behaviours.playerTransform.position, definition.wanderRadius);
                }
                break;

            case SheepState.Cowering:               //for combat -> freezes sheep in set position
                Behaviours.agent.isStopped = true;
                break;
        }
    }

    #region Public Trigger & Actions

    public void OnInteract()
    {
        //player interacts with sheep in order to pick them up
        //SEPARATE input will be used for Special Sheep ability use
        if (currentState == SheepState.Wandering)
        {
            if (definition != null)
            {
                inFlock = true;
                JoinFlock();
                Debug.Log($"{gameObject.name} has joined your flock!");
            }

            else Debug.Log($"{gameObject.name} refuses to follow!");
        }
    }

    public void JoinFlock()
    {
        //switches sheep wander target to shepherd (state goes from wandering to following)
        if (definition.canFollow)
        {
            currentState = SheepState.Following;
            Debug.Log($"{gameObject.name} is following you!");
        }

        else
        {
            //magic sheep go in the crook
            currentState = SheepState.Crooked;
            Behaviours.KeptInCrook(gameObject);
            Debug.Log($"{gameObject.name} is nearby when ready.");
        }
    }

    public void EnterCombatScene(Vector3 clumpPosition)
    {
        currentState = SheepState.Cowering;
        Behaviours.EnterCombatScene(clumpPosition);
    }

    public void ScaredOff()
    {
        if (currentState == SheepState.Cowering)
        {
            Behaviours.ScaredOff(gameObject);
        }
    }
    #endregion
}