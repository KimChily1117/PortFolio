using System;
using System.Collections.Generic;
using System.Text;

namespace Algo
{
    public class dfs
    {
        // DFS는 그럼 인접한 노드 갯수를 구할수도 있는건가
        public dfs(int graph, int edge, int[,] adj)
        {
            this.graph = graph;
            this.edge = edge;
            //this.adj = adj;

            visited = new bool[graph];
        }


        public int graph;
        public int edge; 


       
        int[,] adj = new int[6, 6]
        {
            {0,1,0,1,0,0}, 
            {1,0,1,1,0,0}, 
            {0,1,0,0,0,0},
            {1,1,0,0,1,0}, 
            {0,0,0,1,0,1},
            {0,0,0,0,1,0}
        };

        bool[] visited;

        public int answer;


        public void ExcuteDFS(int now)
        {
            
            Console.WriteLine($"visit : {now}");

            answer++;

            visited[now] = true;


            for (int next = 1; next <= visited.Length; next++)
            {
                if (adj[now, next] == 0)
                    continue;
                if (visited[next] == true)
                    continue;
                ExcuteDFS(next);
            }
        }


        public void SearchAll()
        {



        }

    }
}
