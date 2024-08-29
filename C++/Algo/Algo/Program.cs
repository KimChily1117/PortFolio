using System;

namespace Algo
{
    internal class Program
    {   

        static void Main(string[] args)
        {   
            Console.WriteLine("노드 갯수 입력하셈");
            int N = Int32.Parse(Console.ReadLine());

            Console.WriteLine("간선 갯수 입력하셈");
            int M = Int32.Parse(Console.ReadLine());

            int[,] adj = new int[N + 1 , N + 1];

            int x, y;
            //for (int i = 0; i < M; i++)
            //{
            //    Console.WriteLine("그래프 1번째 인덱스");
            //    x = Int32.Parse(Console.ReadLine());

            //    Console.WriteLine("그래프 2번째 인덱스");
            //    y = Int32.Parse(Console.ReadLine());

            //    adj[x, y] = adj[y, x] = 1; 
            //}

            //dfs d = new dfs(N, M, adj);

            //d.ExcuteDFS(1);

            //Console.WriteLine($"답 : {d.answer - 1}");

            bfs b = new bfs(6,6,adj);

            b.ExcuteBFS(0);

            dijistra djistra = new dijistra(); 

            djistra.ExcuteDijistra(0);



            PriorityQueue<Job> jopQueue = new PriorityQueue<Job>();

            Job j1 = new Job { exeTick = 10 };
            Job j2 = new Job { exeTick = 30 };
            Job j3 = new Job { exeTick = 40 };
            Job j4 = new Job { exeTick = 50 };
            Job j5 = new Job { exeTick = 90 };

            jopQueue.Push(j1);
            jopQueue.Push(j2);
            jopQueue.Push(j3);
            jopQueue.Push(j4);
            jopQueue.Push(j5);



            Board board = new Board();

            


            while (true)
            {

            }

        }
    }
}
