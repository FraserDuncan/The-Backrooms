# The Backrooms

A first-person horror game in Unity 6. Procedurally generated infinite
levels, stealth AI, and no pre-authored maps.

## Worth looking at

**`Assets/Scripts/ChunkData.cs`** — chunked generation. Each chunk seeds
itself from a hash of world seed + coordinates + floor, so it generates
identically every time. Boundary seams are seeded from the shared edge
so adjacent chunks independently agree on where doorways go without
communicating.

**`Assets/Scripts/ChunkManager.cs`** — streaming. Loads chunks around the
player across a 3D grid (x, z and floor), builds them on a coroutine with
a per-frame budget to avoid frame spikes, and unloads what's out of range.

**`Assets/Scripts/EnemyController.cs`** — enemy AI. Patrol / investigate /
chase, vision cone with raycast line-of-sight, and hearing radius that
scales with whether the player is sprinting, walking or crouching. Chase
memory keeps it hunting your last known position after it loses you.

**`Assets/Scripts/GridPathfinder.cs`** — A* over the maze grid, traversing
by wall adjacency rather than cell occupancy.

## Status
In progress. Known work outstanding: the A* open set is a linear scan
where it should be a binary heap, and chunk geometry isn't mesh-combined,
so wall count drives draw calls.

## History
Developed locally before being pushed, so the commit history doesn't
reflect the work.
