using System;
using System.Collections.Generic;
using System.Text;
using Server.Game;
using ServerCore;

namespace Server
{
	struct JobTimerElem : IComparable<JobTimerElem>
	{
		public int execTick; // 실행 시간
		public IJob job;

		public int CompareTo(JobTimerElem other)
			{
				return execTick - other.execTick;
		}
	}

	class JobTimer
	{
		PriorityQueue<JobTimerElem> _pq = new PriorityQueue<JobTimerElem>();
		object _lock = new object();

		public void Push(IJob job, int tickAfter = 0)
		{
			JobTimerElem jobTimerElem;
            jobTimerElem.execTick = System.Environment.TickCount + tickAfter;
            jobTimerElem.job = job;

			lock (_lock)
			{
				_pq.Push(jobTimerElem);
			}
		}

		public void Flush()
		{
			while (true)
			{
				int now = System.Environment.TickCount;

				JobTimerElem job;

				lock (_lock)
				{
					if (_pq.Count == 0)
						break;

					job = _pq.Peek();
					if (job.execTick > now)
						break;

					_pq.Pop();
				}

				job.job.Execute();
			}
		}
	}
}
