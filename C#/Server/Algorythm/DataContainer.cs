using System;
using System.Collections.Generic;
using System.Text;

namespace Algorythm
{

    // List 구현하기(연속 메모리형)


    public class MyList<T>
    {
        const int DEFAULT_SIZE = 1;
        T[] _data = new T[DEFAULT_SIZE];

        public int Count; // 실제로 사용중인 데이터 개수
        public int Capacity { get { return _data.Length; } } // 예약된 데이터 개수 (실질적인 데이터 갯수)


        public void Add(T item)
        {
            // 1. 공간이 남아 있는지 확인한다.
            if(Count >= Capacity)
            {
                // 공간을 다시 늘려서 확보한다.

                T[] newArray = new T[Count * 2]; // 일부러 Count의 2배정도의 여유공간을 마련한다.
                for(int i = 0; i < Count; i++)
                {
                    newArray[i] = _data[i];
                    _data = newArray; 
                }
            }


            // 2. 공간에다가 데이터를 넣어준다
            _data[Count] = item;

            Count++;
        }


    }





    // LinkedList만들기

    public class LinkedListNode<T>
    {
        public LinkedListNode<T> prev;
        public LinkedListNode<T> next;
        public T Data;
    }


    public class MyLinkedList<T>
    {
        public LinkedListNode<T> Head = null;
        public LinkedListNode<T> Tail = null;
        public int Count = 0;


        public LinkedListNode<T> AddLast(T data)
        {
            LinkedListNode<T> Node = new LinkedListNode<T>();

            Node.Data = data;


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

        public void Remove(T data)
        {




        }


    }



    class DataContainer
    {

        public void Initialize()
        {
            MyList<int> myList = new MyList<int> ();
            MyLinkedList<int> myLinkedList = new Algorythm.MyLinkedList<int>();



            myList.Add(1);
            myList.Add(2);
            myList.Add(3);


        }



    }
}
