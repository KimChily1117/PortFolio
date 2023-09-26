using System;

namespace Algorithm
{

    public class MyList<T>
    {
        private const int DEFAULT_SIZE = 1;

        private T[] _data = new T[DEFAULT_SIZE];

        public int Count;

        public int Capacity
        {
            get { return _data.Length; }
        }


        public void Add(T item)
        {
            // 1. 공간이 남아있는지 Check
            if (Count >= Capacity)
            {
                T[] newArray = new T[Count * 2];
                for (int i = 0; i < Count; i++)
                {
                    // 배열 자체의 복사를 하려면 이렇게 선언하여야한다?
                    newArray[i] = _data[i];
                    _data = newArray;
                }
            }
            _data[Count] = item;
            Count++;
        }



        public T this[int index]
        {
            get
            {
                return _data[index];
            }

            set
            {
                _data[index] = value;
            }
        }

        public void RemoveAt(int index)
        {
            //1 2 3 4 5 6

            // Count - 1 을 하는 이유 Count는 Index와 다르게 총 갯수고. 끝번호 Index까지 접근하기 위해서 -1을 선언하는것  
            for (int i = index; i < Count - 1; i++)
            {
                _data[i] = _data[i + 1];
            }

            _data[Count - 1] = default(T);
            Count--;

        }




    }


    public class LinkedListNode<T>
    {
        public LinkedListNode<T> prev;
        public LinkedListNode<T> next;
        public T Data;
    }

    public class MyLinkedList<T>
    {
        private LinkedListNode<T> Head;
        private LinkedListNode<T> Tail;
        public int Count = 0;


        public LinkedListNode<T> AddLast(T data)
        {
            LinkedListNode<T> Node = new LinkedListNode<T>();

            Node.Data = data;

            // 최초 Data add시 -> Head가 없다는건 아무것도 데이터가 없었다는 뜻
            if (Head == null)
            {
                Head = Node;
            }

            if (Tail != null)
            {
                Tail.next = Node;
                Node.prev = Tail;
            }

            Tail = Node;
            Count++;

            return Node;
        }

        // 101 102 103 104 105
        public void Remove(LinkedListNode<T> data)
        {
            if (data == Head)
            {
                Head = data.next;
            }

            if (data == Tail)
            {
                Tail = data.prev;
            }

            data.prev.next = data.next;
            data.next.prev = data.prev;

            Count--;
        }
    }

    public enum TileType
    {
        Empty,   // 갈 수 있는 타일
        Wall,    // 갈 수 없는 타일
    }


    public class Board
    {
        const char CIRCLE = '\u25cf';
        public TileType[,] _tile;
        public int _size;
        Player _player;



        MyLinkedList<int> myTest = new MyLinkedList<int>();

        public void Initialize(int size , Player player)
        {

            if (size % 2 == 0) // 가장자리는 항상 홀수 여야 하기에. 매개변수로 짝수가 넘어오면 함수 종료
                return;

            _player = player

            _size = size;

            _tile = new TileType[size, size];

            // 2차원 배열에서는 [열,행] 순으로 배치하고 for문도
            // y,x로 해야함

            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    if (x % 2 == 0 || y % 2 == 0)   // 가장 자리의 타일들을 벽으로 정의
                        _tile[y, x] = TileType.Wall;
                    else   // 가장 자리가 아닌 타일들은 갈 수 있는 곳으로 정의
                        _tile[y, x] = TileType.Empty;
                }
            } //미로를 만들기 전에 모든길을 막아버린다.           


            //GenerateByBinaryTree();
            GenerateBySideWinder();
            /*
            myTest.AddLast(101);
            myTest.AddLast(102);
            LinkedListNode<int> delnode = myTest.AddLast(103);
            myTest.AddLast(104);
            myTest.AddLast(105);
            myTest.Remove(delnode);
            */
        }

        void GenerateBySideWinder()
        {
            // 길을 다 막아버리는 작업
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    if (x % 2 == 0 || y % 2 == 0)
                        _tile[y, x] = TileType.Wall;
                    else
                        _tile[y, x] = TileType.Empty;
                }
            }

            // 길을 반반 확률로 뚫는 작업
            Random rand = new Random();
            for (int y = 0; y < _size; y++)
            {
                int count = 1;  // 연속해서 몇 개의 오른쪽 벽을 길로 뚫었는지
                for (int x = 0; x < _size; x++)
                {
                    if (x % 2 == 0 || y % 2 == 0)
                        continue;

                    if (x == _size - 2 && y == _size - 2)
                        continue;

                    if (y == _size - 2)
                    {
                        _tile[y, x + 1] = TileType.Empty;
                        continue;
                    }

                    if (x == _size - 2)
                    {
                        _tile[y + 1, x] = TileType.Empty;
                        continue;
                    }

                    if (rand.Next(0, 2) == 0)
                    {
                        _tile[y, x + 1] = TileType.Empty;  // 오른쪽 뚫기
                        count++;
                    }
                    else
                    {
                        int randomIndex = rand.Next(0, count);
                        _tile[y + 1, x - randomIndex * 2] = TileType.Empty;  // 아래 뚫기
                        count = 1;
                    }
                }
            }
        }



        void GenerateByBinaryTree()
        {
            // 길을 다 막아버리는 작업
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    if (x % 2 == 0 || y % 2 == 0)
                        _tile[y, x] = TileType.Wall;
                    else
                        _tile[y, x] = TileType.Empty;
                }
            }

            // 길을 반반 확률로 뚫는 작업
            Random rand = new Random();
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    if (x % 2 == 0 || y % 2 == 0)
                        continue;

                    if (x == _size - 2 && y == _size - 2)
                        continue;

                    if (y == _size - 2)
                    {
                        _tile[y, x + 1] = TileType.Empty;
                        continue;
                    }

                    if (x == _size - 2)
                    {
                        _tile[y + 1, x] = TileType.Empty;
                        continue;
                    }

                    if (rand.Next(0, 2) == 0)
                    {
                        _tile[y, x + 1] = TileType.Empty;  // 오른쪽 뚫기
                    }
                    else
                    {
                        _tile[y + 1, x] = TileType.Empty;  // 아래 뚫기
                    }
                }
            }
        }
























        public void Render()
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    Console.ForegroundColor = GetTileColor(_tile[y, x]);
                    Console.Write(CIRCLE);  // 동그라미 1개 그림
                }
                Console.WriteLine();  // 개행
            }

            Console.ForegroundColor = prevColor;
        }

        ConsoleColor GetTileColor(TileType type)
        {
            switch (type)
            {
                case TileType.Empty:  // 갈 수 있는 곳이면 초록색 리턴
                    return ConsoleColor.Green;
                case TileType.Wall:  // 갈 수 없는 벽이면 빨간색 리턴
                    return ConsoleColor.Red;
                default:  // 디폴트는 초록색 리턴
                    return ConsoleColor.Green;
            }
        }


    }
}