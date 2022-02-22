using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

public class Grid : MonoBehaviour
{
    class SimplePriorityQueue<T> : IEnumerable
    {
        private List<KeyValuePair<T, float>> items = new List<KeyValuePair<T, float>>();
        public int Count => items.Count;


        public SimplePriorityQueue()
        {
        }


        public void Enqueue(KeyValuePair<T, float> item)
        {
            items.Add(item);

            var currentIndex = Count - 1;
            var parentIndex = GetParentIndex(currentIndex);

            while (currentIndex > 0 && items[parentIndex].Value > items[currentIndex].Value)
            {
                Swap(currentIndex, parentIndex);

                currentIndex = parentIndex;
                parentIndex = GetParentIndex(currentIndex);
            }
        }

        public KeyValuePair<T, float> Dequeue()
        {
            var result = items[0];
            items[0] = items[Count - 1];
            items.RemoveAt(Count - 1);
            Sort(0);
            return result;
        }

        private void Sort(int curentIndex)
        {
            int maxIndex = curentIndex;
            int leftIndex;
            int rightIndex;

            while (curentIndex < Count)
            {
                leftIndex = 2 * curentIndex + 1;
                rightIndex = 2 * curentIndex + 2;

                if (leftIndex < Count && items[leftIndex].Value < items[maxIndex].Value)
                {
                    maxIndex = leftIndex;
                }

                if (rightIndex < Count && items[rightIndex].Value < items[maxIndex].Value)
                {
                    maxIndex = rightIndex;
                }

                if (maxIndex == curentIndex)
                {
                    break;
                }

                Swap(curentIndex, maxIndex);
                curentIndex = maxIndex;
            }
        }

        private void Swap(int currentIndex, int parentIndex)
        {
            KeyValuePair<T, float> temp = items[currentIndex];
            items[currentIndex] = items[parentIndex];
            items[parentIndex] = temp;
        }

        private int GetParentIndex(int currentIndex)
        {
            return (currentIndex - 1) / 2;
        }

        public IEnumerator GetEnumerator()
        {
            while (Count > 0)
            {
                yield return Dequeue();
            }
        }
    }


    //  Модель для отрисовки узла сетки
    public GameObject nodeModel;

    //  Ландшафт (Terrain) на котором строится путь
    [SerializeField] private Terrain landscape = null;

    //  Шаг сетки (по x и z) для построения точек
    [SerializeField] private int gridDelta = 20;

    //  Номер кадра, на котором будет выполнено обновление путей
    private int updateAtFrame = 0;

    //  Массив узлов - создаётся один раз, при первом вызове скрипта
    private PathNode[,] grid = null;

    private void CheckWalkableNodes()
    {
        foreach (PathNode node in grid)
        {
            //  Пока что считаем все вершины проходимыми, без учёта препятствий
            node.walkable = true;
            node.walkable = !Physics.CheckSphere(node.body.transform.position, 1);
            if (node.walkable)
                node.Fade();
            else
            {
                node.Illuminate();
                Debug.Log("Not walkable!");
            }
        }
    }


    // Метод вызывается однократно перед отрисовкой первого кадра
    void Start()
    {
        //  Создаём сетку узлов для навигации - адаптивную, под размер ландшафта
        Vector3 terrainSize = landscape.terrainData.bounds.size;
        int sizeX = (int)(terrainSize.x / gridDelta);
        int sizeZ = (int)(terrainSize.z / gridDelta);
        //  Создаём и заполняем сетку вершин, приподнимая на 25 единиц над ландшафтом
        grid = new PathNode[sizeX, sizeZ];
        for (int x = 0; x < sizeX; ++x)
            for (int z = 0; z < sizeZ; ++z)
            {
                Vector3 position = new Vector3(x * gridDelta, 0, z * gridDelta);
                position.y = landscape.SampleHeight(position) + 25;
                grid[x, z] = new PathNode(nodeModel, false, position);
                grid[x, z].ParentNode = null;
                grid[x, z].Fade();
            }
    }
    /// <summary>
    /// Получение списка соседних узлов для вершины сетки
    /// </summary>
    /// <param name="current">индексы текущей вершины </param>
    /// <returns></returns>
    private List<Vector2Int> GetNeighbours(Vector2Int current)
    {
        List<Vector2Int> nodes = new List<Vector2Int>();
        for (int x = current.x - 1; x <= current.x + 1; ++x)
            for (int y = current.y - 1; y <= current.y + 1; ++y)
                if (x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && (x != current.x || y != current.y))
                    nodes.Add(new Vector2Int(x, y));
        return nodes;
    }

    /// <summary>
    /// Вычисление "кратчайшего" между двумя вершинами сетки
    /// </summary>
    /// <param name="startNode">Координаты начального узла пути (индексы элемента в массиве grid)</param>
    /// <param name="finishNode">Координаты конечного узла пути (индексы элемента в массиве grid)</param>
    void calculatePath(Vector2Int startNode, Vector2Int finishNode)
    {
        foreach (var node in grid)
        {
            node.Fade();
            node.ParentNode = null;
        }

        CheckWalkableNodes();
        PathNode start = grid[startNode.x, startNode.y];

        start.ParentNode = null;
        start.Distance = 0;
        start.F = start.Distance + GetHeuristic(startNode, finishNode);


        SimplePriorityQueue<Vector2Int> nodes = new SimplePriorityQueue<Vector2Int>();
        var temp = new KeyValuePair<Vector2Int, float>(startNode, 0);
        nodes.Enqueue(temp);
        while (nodes.Count != 0)
        {
            Vector2Int current = nodes.Dequeue().Key;
            if (current == finishNode) break;
            //  Получаем список соседей
            var neighbours = GetNeighbours(current);
            foreach (var node in neighbours)
            {
                var distance = grid[current.x, current.y].Distance + PathNode.Dist(grid[node.x, node.y], grid[current.x, current.y]);
                if (grid[node.x, node.y].walkable && grid[node.x, node.y].Distance > distance)
                {
                    grid[node.x, node.y].ParentNode = grid[current.x, current.y];
                    grid[node.x, node.y].Distance = distance;
                    grid[node.x, node.y].F = grid[node.x, node.y].Distance + GetHeuristic(node, finishNode);
                    nodes.Enqueue(new KeyValuePair<Vector2Int, float>(node, grid[node.x, node.y].F));
                }
            }
        }
        //  Восстанавливаем путь от целевой к стартовой
        var pathElem = grid[finishNode.x, finishNode.y];
        while (pathElem != null)
        {
            pathElem.Illuminate();
            pathElem = pathElem.ParentNode;
        }
    }

    static public float GetHeuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // Метод вызывается каждый кадр
    void Update()
    {
        if (Time.frameCount < updateAtFrame) return;
        updateAtFrame = Time.frameCount + 100;

        calculatePath(new Vector2Int(0, 0), new Vector2Int(grid.GetLength(0) - 1, grid.GetLength(1) - 1));
    }
}
