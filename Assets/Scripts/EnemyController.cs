using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Refs")]
    public BackroomsGenerator generator;
    public Transform player;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4.5f;
    public float turnSpeed = 8f;
    public float waypointTolerance = 0.2f;
    public float repathInterval = 0.4f;
    public float closeRange = 5f;          // within this, home straight at the player

    [Header("Vision")]
    public float viewDistance = 18f;
    public float fovAngle = 100f;
    public float eyeHeight = 1.6f;

    [Header("Hearing")]
    public float sprintHearRadius = 22f;
    public float walkHearRadius = 11f;
    public float crouchHearRadius = 4f;

    [Header("Chase")]
    public float chaseMemory = 4f;         // keeps hunting this long after losing you

    [Header("Catch")]
    public float catchDistance = 1.2f;

    enum State { Patrol, Investigate, Chase }
    State state = State.Patrol;

    List<Vector2Int> path;
    int pathIndex;
    float repathTimer;
    float alertTimer;
    Vector3 lastKnownPlayerPos;
    PlayerMovementController playerMove;

    GridPathfinder PF => generator ? generator.Pathfinder : null;

    void Start()
    {
        if (player) playerMove = player.GetComponent<PlayerMovementController>();
    }

    void Update()
    {
        if (PF == null || !player) return;

        if (CanSeePlayer() || CanHearPlayer())
        {
            alertTimer = chaseMemory;
            lastKnownPlayerPos = player.position;
            state = State.Chase;
        }
        else
        {
            alertTimer -= Time.deltaTime;
            if (alertTimer > 0f) state = State.Chase;
            else if (state == State.Chase) state = State.Investigate;
        }

        repathTimer -= Time.deltaTime;

        if (state == State.Chase)
        {
            if (Vector3.Distance(transform.position, player.position) < closeRange)
                MoveDirectlyTo(player.position, chaseSpeed);
            else
            {
                if (repathTimer <= 0f) { repathTimer = repathInterval; PathTo(PF.WorldToCell(player.position)); }
                MoveAlongPath(chaseSpeed);
            }
            FaceTarget(player.position);          // track the player, not the waypoint
        }
        else if (state == State.Investigate)
        {
            Vector2Int lk = PF.WorldToCell(lastKnownPlayerPos);
            if (PF.WorldToCell(transform.position) == lk) { state = State.Patrol; path = null; }
            else
            {
                if (repathTimer <= 0f) { repathTimer = repathInterval; PathTo(lk); }
                MoveAlongPath(patrolSpeed);
                FaceMovement();
            }
        }
        else // Patrol
        {
            if (path == null || pathIndex >= path.Count) PathTo(RandomCell());
            MoveAlongPath(patrolSpeed);
            FaceMovement();
        }

        TryCatch();
    }

    void PathTo(Vector2Int goal)
    {
        var p = PF.FindPath(PF.WorldToCell(transform.position), goal);
        if (p != null && p.Count > 1) { path = p; pathIndex = 1; }
    }

    void MoveAlongPath(float speed)
    {
        if (path == null || pathIndex >= path.Count) return;
        Vector3 wp = PF.CellToWorld(path[pathIndex], transform.position.y);
        transform.position = Vector3.MoveTowards(transform.position, wp, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, wp) <= waypointTolerance) pathIndex++;
    }

    void MoveDirectlyTo(Vector3 worldPos, float speed)
    {
        Vector3 t = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        transform.position = Vector3.MoveTowards(transform.position, t, speed * Time.deltaTime);
    }

    void FaceTarget(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    void FaceMovement()
    {
        if (path == null || pathIndex >= path.Count) return;
        FaceTarget(PF.CellToWorld(path[pathIndex], transform.position.y));
    }

    bool CanSeePlayer()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 to = (player.position + Vector3.up * eyeHeight) - eye;
        if (to.magnitude > viewDistance) return false;
        if (Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z)) > fovAngle * 0.5f) return false;
        if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, viewDistance))
            return hit.transform == player || hit.transform.IsChildOf(player);
        return false;
    }

    bool CanHearPlayer()
    {
        float radius = walkHearRadius;
        if (playerMove != null)
        {
            if (playerMove.IsSprinting) radius = sprintHearRadius;
            else if (playerMove.IsCrouching) radius = crouchHearRadius;
        }
        return Vector3.Distance(transform.position, player.position) <= radius;
    }

    Vector2Int RandomCell() =>
        new Vector2Int(Random.Range(0, generator.cells), Random.Range(0, generator.cells));

    void TryCatch()
    {
        if (state != State.Chase) return;
        if (Vector3.Distance(transform.position, player.position) <= catchDistance)
        {
            Debug.Log("Caught!");
            if (playerMove) playerMove.enabled = false;
        }
    }
}