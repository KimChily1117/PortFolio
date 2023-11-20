using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algorithm
{
    public class PriorityQueue
    {
        List<int> _heap = new List<int>();

        public void Push(int data)
        {
            // 힙의 맨 끝에 새로운 데이터를 삽입한다.
            _heap.Add(data);

            // ** heap 트리에서 현재 노트번호를 부여해서 노드를 계산하도록 한다.
            int now = _heap.Count - 1;  // 추가한 노드의 위치. 힙의 맨 끝에서 시작.


            while (now > 0)
            {

                int next = (now - 1) / 2;  // 부모 노드
                if (_heap[now] < _heap[next]) 
                {
                    break;
                }


                // 두 값을 서로 자리 바꿈
                int temp = _heap[now];
                _heap[now] = _heap[next];
                _heap[next] = temp;

                // 검사 위치로 이동한다.
                now = next;

            }

        }


        // 최댓값을 뽑아냄.
        public int Pop()
        {
            // 반환할 데이터를 따로 저장
            // why? 최초 맨위에 있는 값이 제일 크거든 
            int ret = _heap[0];


            // 1. 새로운 트리를 만들어야함 

            // 마지막 데이터를 루트로 이동시킨다.
            int lastIndex = _heap.Count - 1;
            _heap[0] = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);
            lastIndex--;



            return ret;
        }

        public int Count()
        {
            return _heap.Count;
        }



    }
}
