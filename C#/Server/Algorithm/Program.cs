using System;
using System.IO;

namespace Algorithm
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var sw = new StreamWriter(Console.OpenStandardOutput()))
            {
                // 입력부 
                string arrInput = Console.ReadLine();
                string[] arr = arrInput.Split(' ');

                int N = Convert.ToInt32(arr[0]);
                int M = Convert.ToInt32(arr[1]);
                int L = N + 1;


                string elementInput = Console.ReadLine();
                string[] tmp = elementInput.Split(' ');


                int[] eleArr = new int[L];

                eleArr[0] = 0;
                for (int i = 1; i < L; i++)
                {
                    eleArr[i] = Convert.ToInt32(tmp[i - 1]);
                }


                int[] sumArr = new int[L];


                sumArr[0] = eleArr[0];
                for (int i = 1; i < sumArr.Length; i++)
                {
                    sumArr[i] = sumArr[i - 1] + eleArr[i];
                }

                // 연산부


                for (int k = 0; k < M; k++)
                {
                    string ij = Console.ReadLine();
                    string[] ijArr = ij.Split(' ');
                    int I = Convert.ToInt32(ijArr[0]);
                    int J = Convert.ToInt32(ijArr[1]);


                    int result = sumArr[J] - sumArr[I - 1];

                    Console.WriteLine(result);
                }




                //Board board = new Board();
                //Player player = new Player();
                //board.Initialize(25, player);
                //player.Initialize(1, 1, board);

                ////board.BFSTest(0);
                //Console.CursorVisible = false;

                //const int WAIT_TICK = 1000 / 30;

                //int lastTick = 0;

                //while (true)
                //{
                //    #region 프레임 관리
                //    // 만약에 경과한 시간이 1/30초보다 작다면
                //    int currentTick = System.Environment.TickCount;				
                //    int deltaTime = currentTick - lastTick;

                //    if (deltaTime < WAIT_TICK)
                //        continue;

                //    lastTick = currentTick;

                //    #endregion

                //    // 입력

                //    // 로직


                //    player.Update(deltaTime);

                //    // 렌더링
                //    Console.SetCursorPosition(0, 0);
                //    board.Render();

                //}
            }
        }
    }
}

