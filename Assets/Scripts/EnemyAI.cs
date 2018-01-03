using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour {

    [Header("References")]

    public Transform Target;

    public Transform TargetHead;

    public Transform EyesTransform;

    AIDirector aiDirector;

    Transform m_transform;

    SkinnedMeshRenderer m_renderer;

    EnemyAnimations m_enemyAnim;

    NavMeshAgent m_agent;

    [Header("Materials")]

    [SerializeField]
    Material m_chaseMaterial;

    [SerializeField]
    Material m_coverMaterial;

    [SerializeField]
    Material m_patrolMaterial;

    [SerializeField]
    Material m_inRangeMateril;

    [SerializeField]
    Material m_searchMaterial;

    [Header("Waypoints and Covers")]

    [SerializeField]
    List<Waypoint> m_waypointList;  

    [SerializeField]  
    float m_maxDistanceToCover;

    int m_wayPointIndex;

    float m_currentWaitTime;

    Cover m_currentCover;

    [Header("Search")]

    [SerializeField]
    public Vector2 m_searchCountRange; 
     
    int m_searchCount;

    int m_currentSearchCount;

    [SerializeField]
    Vector2 m_searchPointWaitRange;

    float m_searchPointWaitTime;

    float m_currentSearchPointWaitTime;

    public Vector2 XZOffsetRange;

    Vector3 m_searchPosition; 

    [Header("Angle Check")]

    [SerializeField]
    public float m_viewAngle;

    Vector3 m_direction;

    Vector3 m_rotationDirection;

    float m_currentAngle;   

    bool m_isInAngle;

    bool m_isNotObstructed;

    [Header("Distance Check")]

    [SerializeField]
    public float m_viewDistance;

    [SerializeField]
    public float m_stoppingDistance;

    bool m_isInRange;

    float m_currentViewDistance;

    [Header("Speeds")]

    [SerializeField]
    public float m_walkSpeed;

    [SerializeField]
    public float m_runSpeed;

    [Header("Delegates rates")]

    [SerializeField]
    float m_lateFrameTime;

    [SerializeField]
    public float m_midFrameTime;

    float m_lateFrameCurrentTime;

    float m_midFrameCurrentTime;

    public delegate void LateFrame();
    LateFrame m_lateFrame;

    public delegate void MidFrame();
    LateFrame m_midFrame;

    public delegate void EveryFrame();
    LateFrame m_everyFrame;

    Vector3 m_lastKnownPosition;

    AI_STATE m_currentAIstate;

    IN_VIEW_SUB_STATE m_currentInViewSubState;

    void Awake()
    {
        GetReferences();
    }

    void GetReferences()
    {
        m_transform = GetComponent<Transform>();
        m_agent = GetComponent<NavMeshAgent>();
        m_renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        m_enemyAnim = GetComponent<EnemyAnimations>();
    }

    void Start()
    {
        aiDirector = AIDirector.Instance;
        ChangeState(AI_STATE.PATROL);
    }

    void Update()
    {
        MonitorStates();

        m_lateFrameCurrentTime += Time.deltaTime;
        m_midFrameCurrentTime += Time.deltaTime;

        if(m_lateFrameCurrentTime >= m_lateFrameTime)
        {
            m_lateFrameCurrentTime = 0f;
            if (m_lateFrame != null)
            {
                m_lateFrame();
            }
        }
        if (m_midFrameCurrentTime >= m_midFrameTime)
        {
            m_midFrameCurrentTime = 0f;
            if (m_midFrame != null)
            {
                m_midFrame();
            }
        }
        if (m_everyFrame != null)
        {
            m_everyFrame();
        }         
    }

    void MonitorStates()
    {
        switch (m_currentAIstate)
        {
            case AI_STATE.PATROL:
                if (m_isInRange)
                    ChangeState(AI_STATE.IN_RANGE);
                break;

            case AI_STATE.IN_RANGE:
                if (!m_isInRange)
                    ChangeState(AI_STATE.PATROL);
                if(m_isNotObstructed)
                    ChangeState(AI_STATE.IN_VIEW);
                break;

            case AI_STATE.IN_VIEW:
                
                if (!m_isInRange)
                    ChangeState(AI_STATE.IN_SEARCH);
                if (!m_isInAngle)
                    ChangeState(AI_STATE.IN_SEARCH);
                if (!m_isNotObstructed)
                    ChangeState(AI_STATE.IN_SEARCH);
                break;

            case AI_STATE.IN_SEARCH:

                if (m_isNotObstructed)
                    ChangeState(AI_STATE.IN_VIEW);
                break;

            default:
                break;
        }
    }

    void ChangeState(AI_STATE _targetState)
    {
        m_everyFrame = null;
        m_lateFrame = null;
        m_midFrame = null;
        m_agent.stoppingDistance = .5f;
        m_agent.updateRotation = true;
        m_currentWaitTime = 0f;
        m_enemyAnim.SetAlertBool(false);
        if (m_currentCover != null)
            m_currentCover.isOcupied = false;
        switch (_targetState)
        {
            case AI_STATE.IN_SEARCH:
                m_renderer.material = m_searchMaterial;
                m_currentInViewSubState = IN_VIEW_SUB_STATE.NONE;
                SetStartSearchValues();
                m_midFrame = Sight;
                m_midFrame += Search;
                m_everyFrame += PlayNormalAnimations;
                m_agent.speed = m_runSpeed;
                break;

            case AI_STATE.PATROL:
                m_renderer.material = m_patrolMaterial;
                m_currentInViewSubState = IN_VIEW_SUB_STATE.NONE;
                MoveToPosition(m_waypointList[m_wayPointIndex].WaypointTransform.position);
                m_agent.speed = m_walkSpeed;
                m_lateFrame = PartolLateBehaviors;
                m_everyFrame += PlayNormalAnimations;
                break;

            case AI_STATE.IN_RANGE:
                m_renderer.material = m_inRangeMateril;
                m_currentInViewSubState = IN_VIEW_SUB_STATE.NONE;
                MoveToPosition(m_waypointList[m_wayPointIndex].WaypointTransform.position);
                m_agent.speed = m_walkSpeed;
                m_midFrame = InRangeMidBehaviors;
                m_everyFrame += PlayNormalAnimations;
                break;

            case AI_STATE.IN_VIEW:
                m_everyFrame = GetDirecton;
                m_midFrame = Sight;
                if(m_currentInViewSubState != IN_VIEW_SUB_STATE.IN_COVER)
                     LookForCover();            
                break;

            default:
                break;
        }

        m_currentAIstate = _targetState;
    }

    void ChangeInViewBehaviors(IN_VIEW_SUB_STATE _targetState)
    {
        switch (_targetState)
        {
            case IN_VIEW_SUB_STATE.NONE:
                break;

            case IN_VIEW_SUB_STATE.CHASE:
                m_renderer.material = m_chaseMaterial;
                m_agent.stoppingDistance = 8f;
                m_agent.updateRotation = true;
                m_agent.speed = m_runSpeed;
                m_lastKnownPosition = Target.position;
                MoveToPosition(m_lastKnownPosition);
                m_midFrame += Chase;
                m_everyFrame += PlayNormalAnimations;
                break;

            case IN_VIEW_SUB_STATE.GET_COVER:
                m_renderer.material = m_coverMaterial;
                m_currentCover.isOcupied = true;
                m_agent.stoppingDistance = .5f;
                m_agent.updateRotation = false;
                m_agent.speed = m_runSpeed;
                m_lastKnownPosition = Target.position;
                MoveToPosition(m_currentCover.coverTransform.position);
                m_midFrame += GoToCover;
                m_enemyAnim.SetAlertBool(true);
                m_everyFrame += PlayAlertAnimations;
                m_everyFrame += TurnToPlayer;
                break;

            case IN_VIEW_SUB_STATE.IN_COVER:
                // m_agent.speed = 0f;             
                
                m_agent.updateRotation = false;
                m_midFrame += InCover;
                m_everyFrame += PlayAlertAnimations;
                m_everyFrame += TurnToPlayer;
                break;
            default:
                break;
        }
        m_currentInViewSubState = _targetState;
    }

    void SetStartSearchValues()
    {
        m_currentSearchCount = 0;
        m_searchPosition = -Vector3.one;
        m_searchCount = Mathf.RoundToInt(UnityEngine.Random.Range(m_searchCountRange.x, m_searchCountRange.y));
        
    }

    void SetSearchPosition()
    {
        
        m_currentSearchPointWaitTime = 0f;
        m_searchPointWaitTime = Mathf.RoundToInt(UnityEngine.Random.Range(m_searchPointWaitRange.x, m_searchPointWaitRange.y));
        NavMeshHit hit;
        if (NavMesh.SamplePosition(m_lastKnownPosition, out hit, 3f, NavMesh.AllAreas))
        {
            float ZOffset = UnityEngine.Random.Range(XZOffsetRange.x, XZOffsetRange.y);
            float XOffset = UnityEngine.Random.Range(XZOffsetRange.x, XZOffsetRange.y);

            m_searchPosition = hit.position;
            m_searchPosition.x += XOffset;
            m_searchPosition.z += ZOffset;
        }
        else
        {
            Debug.Log("Search");
            m_searchPosition = m_lastKnownPosition;
        }

        MoveToPosition(m_searchPosition);
        
    }

    void Search()
    {
        if(m_searchPosition == -Vector3.one)
        {
            SetSearchPosition();
            
        }
        float distance = Vector3.Distance(m_transform.position, m_searchPosition);
      
        if(distance < 2.5f)
        {
            m_currentSearchPointWaitTime += m_midFrameTime;
            if(m_currentSearchPointWaitTime > m_searchPointWaitTime)
            {
                SetSearchPosition();
                m_currentSearchCount++;
            }
        }
        if(m_currentSearchCount > m_searchCount)
        {
            ChangeState(AI_STATE.PATROL);
        }
    }

    void Patrol()
    {
        Waypoint waypoint = m_waypointList[m_wayPointIndex];
        float distanceToWaypoint = Vector3.Distance(m_transform.position, waypoint.WaypointTransform.position);

        if(distanceToWaypoint <= 2)
        {
            m_currentWaitTime += m_lateFrameTime;
            if(m_currentWaitTime >= waypoint.WaitTime)
            {
                m_currentWaitTime = 0f;
                m_wayPointIndex++;
                if (m_wayPointIndex >= m_waypointList.Count)
                {
                    m_wayPointIndex = 0;
                }
                MoveToPosition(m_waypointList[m_wayPointIndex].WaypointTransform.position);
            }
        }
    }

    void LookForCover()
    {
        if(m_currentViewDistance < 10f)
        {
           
            ChangeInViewBehaviors(IN_VIEW_SUB_STATE.CHASE);
            return;
        }

        m_currentCover = aiDirector.GetClosestCover(m_transform.position, m_maxDistanceToCover);
        if (m_currentCover == null)
        {
          
            ChangeInViewBehaviors(IN_VIEW_SUB_STATE.CHASE);
            return;
        }

        float angle = Vector3.Angle(m_direction, m_currentCover.coverTransform.forward);
        if(angle > 75)
        {
            
            ChangeInViewBehaviors(IN_VIEW_SUB_STATE.CHASE);
            return;
        }
        m_renderer.material = m_coverMaterial;
        
        ChangeInViewBehaviors(IN_VIEW_SUB_STATE.GET_COVER);
    }
    
    void InRangeMidBehaviors()
    {
        GetDirecton();
        Sight();
        Patrol();
    }

    void Sight()
    {
        GetDirecton();
        CheckDistance();
        CheckAngle();
        CheckObstruction();
    }

    void Chase()
    {
        MonitorPlayerPosition(Target.position);       
    }

    void GoToCover()
    {       
        float distanceToCover = Vector3.Distance(m_transform.position, m_currentCover.coverTransform.position);
      
        if (distanceToCover < 1f)
        {
            if (m_currentAIstate != AI_STATE.IN_VIEW)
                ChangeState(AI_STATE.IN_VIEW);
            if (m_currentInViewSubState != IN_VIEW_SUB_STATE.IN_COVER)
                ChangeInViewBehaviors(IN_VIEW_SUB_STATE.IN_COVER);
        }
    }

    void InCover()
    {
        m_lastKnownPosition = Target.position;
    }

    void PartolLateBehaviors()
    {
        CheckDistance();
        Patrol();
    }

    void CheckDistance()
    {
        m_currentViewDistance = Vector3.Distance(Target.position, m_transform.position);
        m_isInRange = m_currentViewDistance <= m_viewDistance;
    }

    void GetDirecton()
    {
        
        m_direction = TargetHead.position - EyesTransform.position;
        m_rotationDirection = m_direction;
        m_rotationDirection.y = 0f;
    }

    void CheckAngle()
    {     
        m_currentAngle = Vector3.Angle(m_transform.forward, m_rotationDirection);
        m_isInAngle = m_currentAngle <= m_viewAngle;
    }

    void CheckObstruction()
    {
        m_isNotObstructed = false;
        if (!m_isInAngle)
            return;
        Debug.DrawRay(EyesTransform.position, m_direction, Color.white);
        RaycastHit hit;
        if(Physics.Raycast(EyesTransform.position, m_direction, out hit, m_viewDistance - 1f))
        {
            if (hit.collider.CompareTag("Player"))
            {
                m_isNotObstructed = true;
            }
            
        }
    }

    void TurnToPlayer()
    {
        Quaternion rotation = Quaternion.LookRotation(m_rotationDirection);
        m_transform.rotation = Quaternion.Slerp(m_transform.rotation, rotation, .5f);
    }

    void MoveToPosition(Vector3 _position)
    {
        m_agent.SetDestination(_position);           
    }

    void StopMoving()
    {
        m_agent.isStopped = true;
    }

    void MonitorPlayerPosition(Vector3 _position)
    {
        if(Vector3.Distance(m_lastKnownPosition, _position) > 10f)
        {
            MoveToPosition(_position);
            m_lastKnownPosition = _position;
        }
    }

    void PlayNormalAnimations()
    {
        bool alert = m_currentAIstate == AI_STATE.IN_VIEW || m_currentAIstate == AI_STATE.IN_SEARCH;
        m_enemyAnim.AnimatiansNormal(m_agent.desiredVelocity, alert);
    }

    void PlayAlertAnimations()
    {
        m_enemyAnim.AnimatiansAlert(m_agent.desiredVelocity);
    }
}

public enum AI_STATE
{
    PATROL,
    IN_RANGE,
    IN_VIEW,
    IN_SEARCH
}

public enum IN_VIEW_SUB_STATE
{
    NONE,
    CHASE,
    GET_COVER,
    IN_COVER
}

[Serializable]
public class Waypoint
{
    public Transform WaypointTransform;
    public float WaitTime;
}
[Serializable]
public class Cover
{
    public Transform coverTransform;
    public bool isOcupied;
}
