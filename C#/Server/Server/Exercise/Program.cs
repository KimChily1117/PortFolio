using System;
using System.Collections.Generic;

namespace Exercise
{
    class Graph
    {
        // 1️⃣ 배열로 구현
        int[,] adj = new int[6, 6]  // 방향 없는 그래프라 서로 대칭되는 것을 볼 수 있다.
        {
            { 0, 1, 0, 1, 0, 0 },
            { 1, 0, 1, 1, 0, 0 },
            { 0, 1, 0, 0, 0, 0 },
            { 1, 1, 0, 0, 1, 0 },
            { 0, 0, 0, 1, 0, 1 },
            { 0, 0, 0, 0, 1, 0 },
        };

        // 2️⃣ 리스트로 구현
        List<int>[] adj2 = new List<int>[]
        {
            new List<int>() { 1, 3 },
            new List<int>() { 0, 2, 3 },
            new List<int>() { 1 },
            new List<int>() { 0, 1, 4 },
            new List<int>() { 3, 5 },
            new List<int>() { 4 },
        };

        
        // Param : 시작할 정점의 위치
        
        // 1. 우선 now부터 방문 하고
        // 2. now와 연결된 정점들을 하나씩 확인해서 [아직 미발견(미방문) 상태라면 ]방문한다.

        //dp, dfs bfs, map
        
        private bool[] visited = new bool [6];
        public void DFS(int now)
        {
                
            
        }
        
        
        
        
    }
    
    
    
    // 1,2,3,4
    
    // 스택 : LIFO(후입선출)
    // 큐 : FIFO(선입선출)
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}