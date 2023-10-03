using System;
using System.Collections.Generic;
using System.Text;

namespace Algorithm
{
    class Pos
    {
        public Pos(int y, int x) { Y = y; X = x; }
        public int Y;
        public int X;
    }


    public class Player
    {
        public int PosY { get; private set; }
        public int PosX { get; private set; }

        Random _random = new Random();

        Board _board;


        enum Dir
        {
            Up,
            Down,
            Left,
            Right
        }

        public int _dir = (int)Dir.Up;

        List<Pos> _points = new List<Pos>();
        // 이동경로를 위해 선언한 List;

        public void Initialize(int posY, int posX, Board board)
        {
            PosX = posX;
            PosY = posY;

            _board = board;


            //RightHand();
            BFS();

        }



        public void RightHand()
        {
            // 현재 바라보고 있는 방향을 기존으로 좌표 변화를 나타냄
            int[] frontY = new int[] { -1, 0, 1, 0 };
            int[] frontX = new int[] { 0, -1, 0, 1 };
            int[] rightY = new int[] { 0, -1, 0, 1 };
            int[] rightX = new int[] { 1, 0, -1, 0 };

            // 도착 좌표를 미리 계산한다.(미리 길을 찾아 놓음)
            while (PosY != _board.DestY || PosX != _board.DestX)
            {
                // 1. 현재 바라보는 방향을 기준으로 오른쪽으로 갈 수 있는지 확인
                if (_board._tile[PosY + rightY[_dir], PosX + rightX[_dir]] == Board.TileType.Empty)
                {
                    // 오른쪽으로 가기
                    // 1. 오른쪽 방향으로 90 도 회전
                    _dir = (_dir - 1 + 4) % 4;
                    // 2. 앞으로 한 보 전진
                    PosY = PosY + frontY[_dir];

                    PosX = PosX + frontX[_dir];

                    _points.Add(new Pos(PosY, PosX));
                }
                // 2. 현재 바라보는 방향을 기준으로 앞 쪽으로 갈 수 있는지
                else if (_board._tile[PosY + frontY[_dir], PosX + frontX[_dir]] == Board.TileType.Empty)
                {
                    // 앞으로 한 보 전진
                    PosY = PosY + frontY[_dir];
                    PosX = PosX + frontX[_dir];

                    _points.Add(new Pos(PosY, PosX));
                }
                else
                {
                    // 왼쪽 방향으로 90 도 회전 해주고 다음 반복 하러 (반시계방향으로 여러 방향 따져봄) 
                    _dir = (_dir + 1 + 4) % 4;
                }
            }
        }


        public void BFS()
        {

            // Dir 방향 up , left , down , right 순서대로 필요한 방향 백터의 집합을 표현함
            int[] deltaY = new int[] { -1, 0, 1, 0 };
            int[] deltaX = new int[] { 0, -1, 0, 1 };


            Queue<Pos> q = new Queue<Pos>();

            bool[,] found = new bool[_board._size, _board._size];
            Pos[,] Parent = new Pos[_board._size, _board._size];     


            q.Enqueue(new Pos(PosY, PosX));  // 시작점
            found[PosY, PosX] = true; // default
            Parent[PosY, PosX] = new Pos(PosY, PosX);


            while (q.Count > 0)
            {
                Pos pos = q.Dequeue();

                int nowY = pos.Y;
                int nowX = pos.X;
                // 현재 위치 하여 있는 좌표 값을 받아옵니다.


                // 상하좌우를 탐색하여서 이동 할 수있는 좌표를 탐색합니다.
                for (int i = 0; i < 4; i++)
                {
                    // 벡터에다가 방향을 곱해져서 4방향을 다 탐색을 시행 
                    int nextY = nowY + deltaY[i];
                    int nextX = nowX + deltaX[i];

                    if (nextY < 0 || nextY >= _board._size || nextX < 0 || nextX >= _board._size)
                        continue;
                    if (_board._tile[nextY, nextX] == Board.TileType.Wall)
                        continue;
                    if (found[nextY, nextX])
                        continue;


                    q.Enqueue(new Pos(nextY, nextX));
                    found[nextY, nextX] = true;
                    Parent[nextY, nextX] = new Pos(nowY, nowX);
                }




            }

            int y = _board.DestY;
            int x = _board.DestX;


            while (Parent[y,x].Y != y || Parent[y,x].X != x)
            {
                _points.Add(new Pos(y, x));
                Pos pos = Parent[y,x];
                y = pos.Y;
                x = pos.X;
            }

            _points.Add(new Pos(y, x));
            _points.Reverse();

        }



        const int MOVE_TICK = 100;   // 0.1 초 마다 움직이게
        int _sumTick = 0;
        int _lastIndex = 0;


        public void Update(int deltaTick)
        {
            if (_lastIndex >= _points.Count)
                return;

            _sumTick += deltaTick;
            if (_sumTick >= MOVE_TICK)  // 이부분은 0.1초마다 실행
            {
                _sumTick = 0;

                PosY = _points[_lastIndex].Y;
                PosX = _points[_lastIndex].X;
                _lastIndex++;
            }
        }
    }
}
