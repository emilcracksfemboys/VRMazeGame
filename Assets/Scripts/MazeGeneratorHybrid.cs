using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class MazeGeneratorHybrid : MonoBehaviour
{
    [Header("Maze Size")]
    public int width = 15;
    public int height = 15;
    public float cellSize = 4f;

    [Header("Randomness")]
    public bool useSeed = false;
    public int seed = 12345;

    [Header("Tile Prefabs (default openings face +Z)")]
    public GameObject deadEndPrefab;   // opening: Front
    public GameObject straightPrefab;  // openings: Front & Back
    public GameObject cornerPrefab;    // openings: Front & Right
    public GameObject tPrefab;         // openings: Left, Front, Right (no Back)
    public GameObject crossPrefab;     // all 4 openings

    [Header("Optional Markers")]
    public NavMeshSurface navMeshSurface;
    public GameObject startMarkerPrefab;
    public GameObject exitMarkerPrefab;

    // Internal maze cell representation
    private Cell[,] grid;
    private Cell[,] roomGrid;

    private struct Cell
    {
        public bool visited;
        // Openings: true means corridor is open to that direction
        public bool openN; // +Z
        public bool openE; // +X
        public bool openS; // -Z
        public bool openW; // -X

        public bool isRoom; // whether this cell is part of a room
    }

    void Start()
    {
        Generate();
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        // Clean previous build
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        if (useSeed) Random.InitState(seed);

        // 1) Build perfect maze data using DFS
        grid = new Cell[width, height];
        BuildMazeDFS();

        // Optional: add some rooms and passages
        AddRooms();
        AddExtraConnections();

        // 2) Instantiate tiles
        BuildTiles();

        // 3) Place start & exit (farthest path)
        PlaceStartAndExit();

        // 4) Update the NavMesh
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
    }

    private void BuildMazeDFS()
    {
        // Start from (0,0) (bottom-left). Can be randomised if desired
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int current = new Vector2Int(0, 0);
        grid[current.x, current.y].visited = true;
        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            List<(Vector2Int dir, System.Action carve)> neighbors = new List<(Vector2Int, System.Action)>();

            // Check unvisited neighbors and prepare carving lambdas
            TryAddNeighbor(current, Vector2Int.up, () => { grid[current.x, current.y].openN = true; grid[current.x, current.y + 1].openS = true; }, neighbors);   // N (+Z)
            TryAddNeighbor(current, Vector2Int.right, () => { grid[current.x, current.y].openE = true; grid[current.x + 1, current.y].openW = true; }, neighbors);   // E (+X)
            TryAddNeighbor(current, Vector2Int.down, () => { grid[current.x, current.y].openS = true; grid[current.x, current.y - 1].openN = true; }, neighbors);   // S (-Z)
            TryAddNeighbor(current, Vector2Int.left, () => { grid[current.x, current.y].openW = true; grid[current.x - 1, current.y].openE = true; }, neighbors);   // W (-X)

            if (neighbors.Count > 0)
            {
                // Pick a random unvisited neighbor and carve to it
                var choice = neighbors[Random.Range(0, neighbors.Count)];
                choice.carve();
                Vector2Int next = current + choice.dir;
                grid[next.x, next.y].visited = true;
                stack.Push(next);
            }
            else
            {
                // Backtrack
                stack.Pop();
            }
        }
    }

    private void TryAddNeighbor(Vector2Int c, Vector2Int dir, System.Action carve, List<(Vector2Int, System.Action)> list)
    {
        int nx = c.x + dir.x;
        int ny = c.y + dir.y;
        if (nx >= 0 && nx < width && ny >= 0 && ny < height && !grid[nx, ny].visited)
        {
            list.Add((dir, carve));
        }
    }

    private void BuildTiles()
    {
        // Iterate grid and drop the correct prefab with the right rotation
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell cell = grid[x, y];
                int openings = CountOpenings(cell);
                GameObject prefab = null;
                float yRot = 0f;

                // Determine which piece and rotation we need
                if (openings == 4)
                {
                    prefab = crossPrefab;
                    yRot = 0f;
                }
                else if (openings == 3)
                {
                    // T-junction: our T prefab is open Left, Front, Right (closed Back).
                    // Rotate so the CLOSED side matches the side that is NOT open.
                    // Find the missing direction:
                    bool missBack = !cell.openS; // Back = -Z
                    bool missRight = !cell.openE; // Right = +X
                    bool missFront = !cell.openN; // Front = +Z
                    bool missLeft = !cell.openW; // Left = -X

                    prefab = tPrefab;
                    if (missBack) yRot = 0f;    // already closed Back by default
                    else if (missRight) yRot = 270f; // rotate so closed side goes to Right
                    else if (missFront) yRot = 180f; // closed Front
                    else if (missLeft) yRot = 90f;  // closed Left
                }
                else if (openings == 2)
                {
                    // Two openings: either Straight (opposite) or Corner (adjacent)
                    bool oppNS = cell.openN && cell.openS; // Straight (Z axis)
                    bool oppEW = cell.openE && cell.openW; // Straight (X axis)

                    if (oppNS || oppEW)
                    {
                        prefab = straightPrefab;
                        yRot = oppNS ? 0f : 90f; // 0: along Z, 90: along X
                    }
                    else
                    {
                        // Corner: default opens Front & Right; rotate to match the two open directions
                        prefab = cornerPrefab;
                        if (cell.openN && cell.openE) yRot = 0f;   // Front+Right (default)
                        else if (cell.openE && cell.openS) yRot = 90f;  // Right+Back
                        else if (cell.openS && cell.openW) yRot = 180f; // Back+Left
                        else if (cell.openW && cell.openN) yRot = 270f; // Left+Front
                    }
                }
                else if (openings == 1)
                {
                    // Dead end: default opens Front; rotate to point the opening to the open side
                    prefab = deadEndPrefab;
                    if (cell.openN) yRot = 0f;   // Front
                    else if (cell.openE) yRot = 90f;  // Right
                    else if (cell.openS) yRot = 180f; // Back
                    else if (cell.openW) yRot = 270f; // Left
                }
                else
                {
                    // Isolated (shouldn’t happen in a perfect maze). As a fallback, drop a cross or skip.
                    prefab = crossPrefab;
                    yRot = 0f;
                }

                // Instantiate at world position centered on the grid
                Vector3 pos = new Vector3(x * cellSize, 0f, y * cellSize);
                if (prefab != null)
                {
                    var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yRot, 0f), transform);
                    go.name = $"Cell_{x}_{y}";
                    go.isStatic = true;
                }
            }
        }
    }

    private int CountOpenings(in Cell c)
    {
        int n = 0;
        if (c.openN) n++;
        if (c.openE) n++;
        if (c.openS) n++;
        if (c.openW) n++;
        return n;
    }

    private void PlaceStartAndExit()
    {
        if (startMarkerPrefab == null && exitMarkerPrefab == null) return;

        // Start at (0,0). Use BFS to find farthest reachable cell for exit.
        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int far = FarthestFrom(start);

        // Drop markers slightly above ground
        Vector3 startPos = new Vector3(start.x * cellSize, 0.01f, start.y * cellSize);
        Vector3 exitPos = new Vector3(far.x * cellSize, 0.01f, far.y * cellSize);

        if (startMarkerPrefab)
            Instantiate(startMarkerPrefab, startPos, Quaternion.identity, transform);
        if (exitMarkerPrefab)
            Instantiate(exitMarkerPrefab, exitPos, Quaternion.identity, transform);
    }

    private Vector2Int FarthestFrom(Vector2Int root)
    {
        // BFS over the maze graph using openings
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();
        q.Enqueue(root);
        dist[root] = 0;
        Vector2Int far = root;

        while (q.Count > 0)
        {
            var c = q.Dequeue();
            int d = dist[c];
            if (d > dist[far]) far = c;

            var cell = grid[c.x, c.y];
            // For each open direction, enqueue neighbor if not visited in BFS
            TryBfsNeighbor(c, new Vector2Int(0, 1), cell.openN, dist, q); // N
            TryBfsNeighbor(c, new Vector2Int(1, 0), cell.openE, dist, q); // E
            TryBfsNeighbor(c, new Vector2Int(0, -1), cell.openS, dist, q); // S
            TryBfsNeighbor(c, new Vector2Int(-1, 0), cell.openW, dist, q); // W
        }

        return far;
    }

    private void TryBfsNeighbor(Vector2Int c, Vector2Int dir, bool open, Dictionary<Vector2Int, int> dist, Queue<Vector2Int> q)
    {
        if (!open) return;
        Vector2Int n = c + dir;
        if (n.x < 0 || n.x >= width || n.y < 0 || n.y >= height) return;
        if (dist.ContainsKey(n)) return;
        dist[n] = dist[c] + 1;
        q.Enqueue(n);
    }

    private void AddRooms()
    {
        // Try to place a few 2x2 - 3x3 rooms
        int attempts = 8;
        for (int i = 0; i < attempts; i++)
        {
            int rw = Random.Range(2, 4); // room width 2–3
            int rh = Random.Range(2, 4); // room height 2–3
            int rx = Random.Range(0, width - rw);
            int ry = Random.Range(0, height - rh);
            MakeRoom(rx, ry, rw, rh);
        }
    }

    private void MakeRoom(int rx, int ry, int rw, int rh)
    {
        // mark cells as room
        for (int x = rx; x < rx + rw; x++)
        {
            for (int y = ry; y < ry + rh; y++)
            {
                grid[x, y].isRoom = true;
            }
        }

        // open only internal walls between adjacent room cells
        for (int x = rx; x < rx + rw; x++)
        {
            for (int y = ry; y < ry + rh; y++)
            {
                // if neighbour also inside the same rectangle, open the wall
                if (x + 1 < rx + rw) // east neighbour
                {
                    grid[x, y].openE = true;
                    grid[x + 1, y].openW = true;
                }
                if (y + 1 < ry + rh) // north neighbour
                {
                    grid[x, y].openN = true;
                    grid[x, y + 1].openS = true;
                }
            }
        }
    }

    private void AddExtraConnections()
    {
        // randomly knock out some walls to create loops / parallel corridors
        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                if (Random.value < 0.05f) // 5% chance
                {
                    // open a wall between (x,y) and (x+1,y)
                    grid[x, y].openE = true;
                    grid[x + 1, y].openW = true;
                }

                if (Random.value < 0.05f)
                {
                    // open a wall between (x,y) and (x,y+1)
                    grid[x, y].openN = true;
                    grid[x, y + 1].openS = true;
                }
            }
        }
    }
}

