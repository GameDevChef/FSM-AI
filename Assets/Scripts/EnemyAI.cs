using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour {

    [Header("Referances")]
    public Transform Target;
    public Transform TargetHead;
    private NavMeshAgent m_agent;
    Transform m_transform;
    public Transform EyesTransform;
    AIDirector aiDirector;
    SkinnedMeshRenderer m_renderer;
    EnemyAnimations m_enemyAnim;

    [Header("Materials")]
    public Material ChaseMaterial;
    public Material CoverMaterial;
    public Material PatrolMaterial;
    public Material InRangeMaterial;
    public Material SearchMaterial;

    [Header("Waypoints and Covers")]
    public List<Waypoint> m_waypointList;    
    public float MaxDistanceToCover;
    Cover m_currentCover;
    int m_wayPointIndex;
    float m_currentWaitTime;

    [Header("Search")]
    public Vector2 SearchCountRange;  
    int m_searchCount;
    int m_currentSearchCount;

    public Vector2 SearchPointWaitRange;
    float m_SearchPointWaitTime;
    float m_currentSearchPointWaitTime;

    public Vector2 XZOffsetRange;
    Vector3 m_searchPosition;
    

    [Header("Angle")]
    public float ViewAngle;

    Vector3 m_direction;
    Vector3 m_rotationDirection;
    float m_currentAngle;   
    bool m_isInAngle;

    [Header("Distance")]
    public float ViewDistance;
    public float StoppingDistance;
    bool m_isInRange;
    float m_currentViewDistance;

    [Header("Speed")]
    public float WalkSpeed;
    public float RunSpeed;

    [Header("Obstruction")]
    bool m_isNotObstructed;

    [Header("Delegates rates")]
    public float LateFrameTime;
    public float MidFrameTime;
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


    private void Awake()
    {
        m_transform = GetComponent<Transform>();
        m_agent = GetComponent<NavMeshAgent>();
        m_renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        m_enemyAnim = GetComponent<EnemyAnimations>();
    }

    private void Start()
    {
        aiDirector = AIDirector.Instance;
        ChangeState(AI_STATE.PATROL);
    }

    private void Update()
    {
        MonitorStates();

        m_lateFrameCurrentTime += Time.deltaTime;
        m_midFrameCurrentTime += Time.deltaTime;

        if(m_lateFrameCurrentTime >= LateFrameTime)
        {
            m_lateFrameCurrentTime = 0f;
            if (m_lateFrame != null)
            {
                m_lateFrame();
            }
        }
        if (m_midFrameCurrentTime >= MidFrameTime)
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

    private void MonitorStates()
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

        switch (_targetState)
        {
            case AI_STATE.IN_SEARCH:
                m_renderer.material = SearchMaterial;
                m_currentInViewSubState = IN_VIEW_SUB_STATE.NONE;
                SetStartSearchValues();
                m_midFrame = Sight;
                m_midFrame += Search;
                m_everyFrame += PlayNormalAnimations;
                m_agent.speed = RunSpeed;
                break;

            case AI_STATE.PATROL:
                m_renderer.material = PatrolMaterial;
                m_currentInViewSubState = IN_VIEW_SUB_STATE.NONE;
                MoveToPosition(m_waypointList[m_wayPointIndex].WaypointTransform.position);
                m_agent.speed = WalkSpeed;
                m_lateFrame = PartolLateBehaviors;
                m_everyFrame += PlayNormalAnimations;
                break;

            case AI_STATE.IN_RANGE:
                m_renderer.material = InRangeMaterial;
                m_currentInViewSubState = IN_VIEW_SUB_STATE.NONE;
                MoveToPosition(m_waypointList[m_wayPointIndex].WaypointTransform.position);
                m_agent.speed = WalkSpeed;
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
                m_agent.stoppingDistance = 8f;
                m_agent.updateRotation = true;
                m_agent.speed = RunSpeed;
                m_lastKnownPosition = Target.position;
                MoveToPosition(m_lastKnownPosition);
                m_midFrame += Chase;
                m_everyFrame += PlayNormalAnimations;
                break;

            case IN_VIEW_SUB_STATE.GET_COVER:
                m_agent.stoppingDistance = .5f;
                m_agent.updateRotation = false;
                m_agent.speed = RunSpeed;
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

    //Search
    void SetStartSearchValues()
    {
        m_currentSearchCount = 0;
        m_searchPosition = -Vector3.one;
        m_searchCount = Mathf.RoundToInt(UnityEngine.Random.Range(SearchCountRange.x, SearchCountRange.y));
        
    }

    void SetSearchPosition()
    {
        
        m_currentSearchPointWaitTime = 0f;
        m_SearchPointWaitTime = Mathf.RoundToInt(UnityEngine.Random.Range(SearchPointWaitRange.x, SearchPointWaitRange.y));
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
            m_currentSearchPointWaitTime += MidFrameTime;
            if(m_currentSearchPointWaitTime > m_SearchPointWaitTime)
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
            m_currentWaitTime += LateFrameTime;
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
            m_renderer.material = ChaseMaterial;
            ChangeInViewBehaviors(IN_VIEW_SUB_STATE.CHASE);
            return;
        }

        m_currentCover = aiDirector.GetClosestCover(m_transform.position, MaxDistanceToCover);
        if (m_currentCover == null)
        {
            m_renderer.material = ChaseMaterial;
            ChangeInViewBehaviors(IN_VIEW_SUB_STATE.CHASE);
            return;
        }

        float angle = Vector3.Angle(m_direction, m_currentCover.coverTransform.forward);
        if(angle > 75)
        {
            m_renderer.material = ChaseMaterial;
            ChangeInViewBehaviors(IN_VIEW_SUB_STATE.CHASE);
            return;
        }
        m_renderer.material = CoverMaterial;
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
            ChangeState(AI_STATE.IN_VIEW);
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
        m_isInRange = m_currentViewDistance <= ViewDistance;
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
        m_isInAngle = m_currentAngle <= ViewAngle;
    }

    void CheckObstruction()
    {
        m_isNotObstructed = false;
        if (!m_isInAngle)
            return;
        Debug.DrawRay(EyesTransform.position, m_direction, Color.white);
        RaycastHit hit;
        if(Physics.Raycast(EyesTransform.position, m_direction, out hit, ViewDistance - 1f))
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
        if(Vector3.Distance(m_lastKnownPosition, _position) > 2f)
        {
            MoveToPosition(_position);
            m_lastKnownPosition = _position;
        }
    }

    //Animations
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
