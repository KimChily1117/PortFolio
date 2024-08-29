using System;
using System.Collections.Generic;
using System.Text;

namespace Algo
{
    internal class bfs
    {
        int _graph;
        int _edge;

        int[,] _adj = new int[6, 6]
        {
            {0,1,0,1,0,0},
            {1,0,1,1,0,0},
            {0,1,0,0,0,0},
            {1,1,0,0,1,0},
            {0,0,0,1,0,1},
            {0,0,0,0,1,0}
        };

        public bfs(int graph, int edge, int[,] adj)
        {
            _graph = graph;
            _edge = edge;
            //_adj = adj;
        }

        // 넓이 우선 탐색을 하려면, 다음 방문할 Node를 지정합니다.
        public void ExcuteBFS(int start)
        {
            bool[] found = new bool[_graph];// 정점 갯수 만큼 할당해줘야함
            int[] parents = new int[_graph];
            int[] distance = new int[_graph];

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);

            found[start] = true;
            parents[start] = start;
            distance[start] = 0;

            while (queue.Count > 0)
            {
                int now = queue.Dequeue();

                Console.WriteLine($"{now}");
                for (int next = 0; next < _graph; next++)
                {
                    if (_adj[now, next] == 0)
                        continue;
                    if (found[next] == true)
                        continue;

                    queue.Enqueue(next);
                    found[next] = true;
                    parents[next] = now;
                    distance[next] = distance[now] + 1;
                }
            }

        }

    }
}
