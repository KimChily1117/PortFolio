using System;
using System.Collections.Generic;
using System.Text;

namespace Algo
{
    public class Job : IComparable<Job>
    {
        public int exeTick { get; set; }

        public int CompareTo(Job other)
        {

            if(exeTick == other.exeTick)
                return 0;
            // return id > other.id ? 1 : -1;
            return this.exeTick > other.exeTick ? 1 : -1; 
        }
    }






    public class PriorityQueue<T> where T : IComparable<T>    
    {

        public List<T> _heap = new List<T>();


        public void Push(T data)
        {

            _heap.Add(data);

            int now = _heap.Count - 1; // 추가한 노드(데이터)의 위치. 힙의 맨끝에서 시작.


            // 위로 도장꺠기 시작
            while (now > 0)
            {
                int next = (now - 1) / 2; // 부모 노드
                if (_heap[now].CompareTo(_heap[next]) < 0)
                    break;

                T temp = _heap[now];
                _heap[now] = _heap[next];
                _heap[next] = temp;
                now = next;
            }
        }


        public T Pop()
        {
            T ret = _heap[0];

            int lastidx = _heap.Count - 1;

            _heap[0] = _heap[lastidx];
            _heap.RemoveAt(lastidx);
            lastidx--;



            int now = 0;

            while (true)
            {
                int left = 2 * now + 1;
                int right = 2 * now + 2;

                int next = now;

                if(left <= lastidx && _heap[next].CompareTo(_heap[left]) < 0)
                    next = left;
                // 오른쪽 값이 현재값(왼쪽 이동 포함)보다 크면, 오른쪽으로 이동
                if (right <= lastidx && _heap[next].CompareTo(_heap[right]) < 0)
                    next = right;
                // 왼쪽/오른쪽 모두 현재값보다 작으면 종료
                if (next == now)
                    break;

                // 두 값 서로 자리 바꿈
                T temp = _heap[now];
                _heap[now] = _heap[next];
                _heap[next] = temp;

                // 검사 위치로 이동한다.
                now = next;
            }

            return ret;
        }

        public int Count()
        {
            return _heap.Count;
        }
    }
}
