using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls MUSA's movement and animation.
///
/// Behaviour summary:
///   • When the visitor ENTERS a room  → MUSA walks to that room's fixed waypoint.
///   • While INSIDE a room             → MUSA stays at the waypoint; only rotates to face visitor.
///   • When the visitor EXITS all rooms → MUSA follows the visitor (same logic as before),
///                                        respecting NavMesh obstacles.
///
/// 
/// Setup:
///   1. Add a NavMeshAgent component to MUSA's GameObject.
///   2. Bake a NavMesh in your scene (Window → AI → Navigation → Bake).
///   3. In the Inspector, populate the "Room Waypoints" list with
///      one entry per room: the room tag and a Transform placed where MUSA should stand.
///   4. Make sure each room collider has the matching tag.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MusaFollower : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Visitor")]
    [SerializeField] private Transform target;

    [Header("Room waypoints — one per exhibition room")]
    [SerializeField] private List<RoomWaypoint> roomWaypoints = new();

    [Header("Follow behaviour (used outside rooms)")]
    [SerializeField] private float followDistance = 3.5f;  // Start walking when farther than this
    [SerializeField] private float stopDistance   = 2.0f;  // Stop this far from the visitor
    [SerializeField] private float frontOffset    = 1.8f;  // Aim this far in front of the visitor

    [Header("Rotation (always active)")]
    [SerializeField] private float rotationSpeed  = 3.0f;
    [SerializeField] private float turnThreshold  = 25f;   // Min angle to trigger turn animation

    [Header("Animation")]
    [SerializeField] private AnimationTransitioner anim;

    // -----------------------------------------------------------------------
    // Internal types
    // -----------------------------------------------------------------------

    [System.Serializable]
    public class RoomWaypoint
    {
        public string    roomTag;    // Must match the room collider's tag
        public Transform waypoint;  // Where MUSA should stand in that room
    }

    private enum MusaAnim { Idle, Walking, Talking, Thinking }

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private NavMeshAgent _agent;

    // Room state
    private bool      _inRoom          = false;
    private Transform _roomWaypoint    = null;
    private bool      _reachedWaypoint = false;
    private float     _pathSettleTimer = 0f;  // Waits for NavMesh path to become valid

    // Animation
    private MusaAnim  _currentAnim   = MusaAnim.Idle;
    private MusaAnim  _preWalkAnim   = MusaAnim.Idle;
    private bool      _isTurningL    = false;
    private bool      _isTurningR    = false;

    // Waypoint lookup built from the list
    private Dictionary<string, Transform> _waypointMap = new();
    public static MusaFollower instance;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        instance = this;
        _agent = GetComponent<NavMeshAgent>();

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        // NavMeshAgent handles translation; we handle rotation manually.
        _agent.updateRotation = false;

        // Build lookup
        foreach (var rw in roomWaypoints)
            if (!string.IsNullOrWhiteSpace(rw.roomTag) && rw.waypoint != null)
                _waypointMap[rw.roomTag] = rw.waypoint;
    }

    private void Update()
    {
        if (target == null) return;

        if (_inRoom)
            UpdateInRoom();
        else
            UpdateFollowing();

        if (_inRoom && !_reachedWaypoint && _roomWaypoint != null)
            RotateTowards(_roomWaypoint.position);
        else
            RotateTowardsVisitor();
    }

    // -----------------------------------------------------------------------
    // Public API — called by moving_script (room triggers)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Call this when the visitor enters a room collider.
    /// </summary>
    public void OnRoomEntered(string roomTag)
    {
        if (!_waypointMap.TryGetValue(roomTag, out Transform wp))
        {
            Debug.LogWarning($"[MusaFollower] No waypoint defined for room '{roomTag}'.");
            return;
        }
        Debug.Log($"[MusaFollower] Visitor entered room '{roomTag}', walking to waypoint '{wp.name}'.");

        _inRoom          = true;
        _roomWaypoint    = wp;
        _reachedWaypoint = false;
        _pathSettleTimer = 0f;

        WalkTo(wp.position);
    }

    /// <summary>
    /// Call this when the visitor exits a room collider.
    /// </summary>
    public void OnRoomExited()
    {
        Debug.Log($"[MusaFollower] Visitor exited room '{_roomWaypoint?.tag ?? "unknown"}'.");
        _inRoom       = false;
        _roomWaypoint = null;
    }

    // -----------------------------------------------------------------------
    // Public API — animation state (called by AgentManager / SoundManager)
    // -----------------------------------------------------------------------

    public void SetTalking(bool value)
    {
        if (value)
        {
            _currentAnim = MusaAnim.Talking;
            if (_currentAnim != MusaAnim.Walking) ApplyAnim(MusaAnim.Talking);
        }
        else if (_currentAnim == MusaAnim.Talking)
        {
            _currentAnim = MusaAnim.Idle;
            if (_currentAnim != MusaAnim.Walking) ApplyAnim(MusaAnim.Idle);
        }
    }

    public void SetThinking(bool value)
    {
        if (value)
        {
            _currentAnim = MusaAnim.Thinking;
            if (_currentAnim != MusaAnim.Walking) ApplyAnim(MusaAnim.Thinking);
        }
        else if (_currentAnim == MusaAnim.Thinking)
        {
            _currentAnim = MusaAnim.Idle;
            if (_currentAnim != MusaAnim.Walking) ApplyAnim(MusaAnim.Idle);
        }
    }

    // -----------------------------------------------------------------------
    // Movement — in-room (walk to fixed waypoint, then stay)
    // -----------------------------------------------------------------------

    private void UpdateInRoom()
    {
        if (_roomWaypoint == null) return;

        if (!_reachedWaypoint)
        {
            // Give the NavMesh path one frame to initialise before we check distance.
            // remainingDistance is 0 or Infinity until pathPending flips to false.
            _pathSettleTimer += Time.deltaTime;
            if (_pathSettleTimer < 0.1f) return; // wait ~2 frames at 60 fps

            bool  pathPending   = _agent.pathPending;
            float remainingDist = _agent.remainingDistance;

            // Also guard against Infinity (path not found yet)
            bool arrived = !pathPending
                           && remainingDist != Mathf.Infinity
                           && remainingDist <= _agent.stoppingDistance + 0.1f;

            if (arrived)
            {
                _reachedWaypoint = true;
                _agent.ResetPath();
                StopWalking();
            }
        }
        // Once reached, MUSA stands at waypoint — rotation handled in RotateTowardsVisitor()
    }

    // -----------------------------------------------------------------------
    // Movement — following (outside rooms)
    // -----------------------------------------------------------------------

    private void UpdateFollowing()
    {
        Vector3 myFlat     = Flat(transform.position);
        Vector3 targetFlat = Flat(target.position);

        // Aim in front of the visitor
        Vector3 userForward = Flat(target.forward).normalized;
        if (userForward.sqrMagnitude < 0.1f)
            userForward = (myFlat - targetFlat).normalized;

        Vector3 destination = targetFlat - userForward * frontOffset;

        // Map destination to NavMesh surface
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            destination = hit.position;

        float dist = Vector3.Distance(myFlat, Flat(destination));

        if (dist > followDistance)
        {
            // Walk towards the visitor's front position
            Vector3 dir      = (Flat(destination) - myFlat).normalized;
            Vector3 stopGoal = Flat(destination) - dir * Mathf.Min(stopDistance, dist * 0.5f);
            WalkTo(new Vector3(stopGoal.x, transform.position.y, stopGoal.z));
            UpdateTurnAnims();
        }
        else
        {
            // Close enough — stop
            if (_currentAnim == MusaAnim.Walking)
            {
                _agent.ResetPath();
                _currentAnim = _preWalkAnim;
                ApplyAnim(_currentAnim);
                StopTurnAnims();
            }
        }
    }

    // -----------------------------------------------------------------------
    // Rotation — always faces the visitor
    // -----------------------------------------------------------------------

    private void RotateTowardsVisitor()
    {
        RotateTowards(target.position);
    }

    private void RotateTowards(Vector3 worldPos)
    {
        Vector3 lookDir = Flat(worldPos - transform.position);
        if (lookDir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    // -----------------------------------------------------------------------
    // Turn animations (walking mode only)
    // -----------------------------------------------------------------------

    private void UpdateTurnAnims()
    {
        if (anim == null) return;

        Vector3 toUser = Flat(target.position - transform.position).normalized;
        Vector3 fwd    = Flat(transform.forward).normalized;
        float   angle  = Vector3.SignedAngle(fwd, toUser, Vector3.up);

        bool wantL = angle < -turnThreshold;
        bool wantR = angle >  turnThreshold;

        if (wantL != _isTurningL) { _isTurningL = wantL; anim.SetTurnL(wantL); }
        if (wantR != _isTurningR) { _isTurningR = wantR; anim.SetTurnR(wantR); }
    }

    private void StopTurnAnims()
    {
        if (_isTurningL) { _isTurningL = false; anim?.SetTurnL(false); }
        if (_isTurningR) { _isTurningR = false; anim?.SetTurnR(false); }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void WalkTo(Vector3 position)
    {
        if (_currentAnim != MusaAnim.Walking)
        {
            _preWalkAnim = _currentAnim;
            _currentAnim = MusaAnim.Walking;
            ApplyAnim(MusaAnim.Walking);
        }

        _agent.SetDestination(position);
    }

    private void StopWalking()
    {
        _currentAnim = _preWalkAnim;
        ApplyAnim(_currentAnim);
        StopTurnAnims();
    }

    private void ApplyAnim(MusaAnim state)
    {
        if (anim == null) return;
        anim.SetWalking( state == MusaAnim.Walking);
        anim.SetTalking( state == MusaAnim.Talking);
        anim.SetThinking(state == MusaAnim.Thinking);
        anim.SetTurnL(false);
        anim.SetTurnR(false);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}