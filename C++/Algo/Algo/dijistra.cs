using System;
using System.Collections.Generic;
using System.Text;

namespace Algo
{
    internal class dijistra
    {
        int[,] _adj = new int[6, 6]
       {
            { -1, 15, -1, 35, -1, -1},
            { 15, -1, 5, 10, -1, -1 },
            { -1, 5, -1, -1, -1, -1 },
            { 35, 10, -1, -1, 5, -1 },
            { -1, -1, -1, 5, -1, 5 },
            { -1, -1, -1, -1, 5, -1 },
       };


        public void ExcuteDijistra(int start)
        {
            bool[] visited = new bool[6];
            int[] distance = new int[6];

            Array.Fill(distance, Int32.MaxValue);
            int[] parent = new int[6];


            distance[start] = 0;

            parent[start] = start;

            while (true)
            {
                // 제일 좋은 후보를 찾는다.

                // 가장 유력한 정점의 거리와 번호를 저장한다.

                int closet = Int32.MaxValue;

                int now = -1;

                for (int i = 0; i < visited.Length; i++)
                {
                    if (visited[i] == true)
                    {
                        continue;
                    }
                    // 이미 방문한 정점은 방문 x


                    if (distance[i] == Int32.MaxValue || distance[i] >= closet)
                    {
                        continue;
                    }

                    closet = distance[i];
                    now = i;
                }

                if (now == -1)
                    break;
                // 다음으로 갈 후보가 아예없기 때문에 While문을 종료한다.


                // 제일 좋은 후보를 찾았으니까 방문한다.
                visited[now] = true;

                // 방문한 정점과 연결하여 있는 정점들을 조사해서
                // 상황에 따라 발견한 최단 거리를 갱신한다.

                for (int next = 0; next < visited.Length; next++)
                {
                    // 연결되지 않은 정점은 스킵
                    if (_adj[now, next] == -1)
                    {
                        continue;
                    }

                    // 이미 방문한 정점은 스킵
                    if (visited[next] == true)
                        continue;

                    //새로 조사된 정점의 최단 거리를 계산한다.
                    int nextDist = distance[now] + _adj[now, next];

                    //만약에 기존에 발견한 최단 거리가 새로 조사된 최단거리보다 크면 정보를 갱신
                    if (nextDist < distance[next])
                    {
                        distance[next] = nextDist;
                        parent[next] = now;
                    }
                }


            }


        }



    }
}
