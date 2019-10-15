using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class ThreadedDataRequester : MonoBehaviour { // MonoBehavior is used to allow for async.
	// Singleton monobehavior instance attached to gamemanager allows for cross scene use without duplicates.
	public static ThreadedDataRequester Instance;
	
	// Categorized queue holds data requests and makes sure it's loaded by category priority.
	Dictionary<QueueCategory, SpecialQueue<ThreadInfo>[]> categorizedQueues = new Dictionary<QueueCategory, SpecialQueue<ThreadInfo>[]>();
	
	bool activeThreadDequeueLoop;
	float threadDequeuesPerSecond = 50; // We will change this depending if the player is loading or playing.
	float threadDequeueLoopSpeed => 1/threadDequeuesPerSecond; // Dequeue Speed for threaded data requests.

	public enum QueueCategory
	{
		// These iterate in this order so order them by load priority.

		Terrain,
		Entity,
		Default
	}
	
	public enum QueuePriority
	{
		// These iterate in this order so dont mix them up
		
		High, // Load first.
		Medium, // Load once high priority object have loaded.
		Low, // Load after everything else has loaded.
	}

	// Implement a loading screen that appears and pauses gameplay while queue has more
	// than like 20 requests then resumes after that drops below 5.
	// When game is paused let the threads request as fast as they want.

	private void Start()
	{
		if(!Instance) Instance = this;

		Setup();
		
		if(!activeThreadDequeueLoop)StartCoroutine("ThreadDequeueLoop");
	}

	private void Setup()
	{
		if (categorizedQueues.Count <= 0) // Make sure the queues have not yet been initalized
		{
			foreach (QueueCategory queueCategory in Enum.GetValues(typeof(QueueCategory)))
			{
				// Gives each queue category its own array of priority queues.
				categorizedQueues.Add(queueCategory, new SpecialQueue<ThreadInfo>[Enum.GetValues(typeof(QueuePriority)).Length]);
				
				for (var i = 0; i < categorizedQueues[queueCategory].Length; i++) {
					categorizedQueues[queueCategory][i] = new SpecialQueue<ThreadInfo>();
				}
				
				Debug.Log("Category Added: "+queueCategory);
			}
		}
	}

	void OnEnable()
	{
		Start();
	}
	
	public void SetThreadDequeuesPerSecond(float value)
	{
		threadDequeuesPerSecond = value;
	}

	public static ThreadInfo RequestData(Func<object> generateData, Action<object> callback, QueueCategory queueCategory = QueueCategory.Default, QueuePriority queuePriority = QueuePriority.High)
	{
		ThreadInfo queueThreadInfo = new ThreadInfo();
			
		ThreadStart threadStart = delegate {
			queueThreadInfo = Instance.DataThread (generateData, callback, queueCategory, queuePriority);
		};

		new Thread (threadStart).Start ();

		return queueThreadInfo;
	}

	ThreadInfo DataThread(Func<object> generateData, Action<object> callback, QueueCategory queueCategory, QueuePriority queuePriority)
	{
		object data = generateData();
		ThreadInfo queueThreadInfo = new ThreadInfo(callback, data);

		lock (categorizedQueues[queueCategory][(int)queuePriority])
		{
			categorizedQueues[queueCategory][(int)queuePriority].Enqueue(queueThreadInfo);
		}

		return queueThreadInfo;
	}

	IEnumerator ThreadDequeueLoop()
	{
		activeThreadDequeueLoop = true;
		while (enabled)
		{
			// Loop through category queues.
			foreach(QueueCategory queueCategory in Enum.GetValues(typeof(QueueCategory)))
			{
				// Loop through priority queues within category.
				for (int i = 0; i < categorizedQueues[queueCategory].Length; i++)
				{
					if (categorizedQueues[queueCategory][i].Count > 0)
					{
						ThreadDequeue(categorizedQueues[queueCategory][i]);
						break;
						// Let while loop finish and start the next top priority queue search.
					}
				}
			}

			yield return new WaitForSeconds(threadDequeueLoopSpeed);
		}
		activeThreadDequeueLoop = false;
	}

	private int TotalHighPriorityQueueCount()
	{
		// Should just introduce counter to enqueue and dequeue.
		int totalHighPriorityQueueCount = 0;
		foreach(QueueCategory enumType in Enum.GetValues(typeof(QueueCategory)))
        {
        	// Loop through priority queues within category.
        	for (int i = 0; i < categorizedQueues[enumType].Length; i++)
        	{
        		if (i == 0) totalHighPriorityQueueCount += categorizedQueues[enumType][i].Count;
        	}
        }
		return totalHighPriorityQueueCount;
	}

//	private void StartLoading()
//	{
//		loading = true;
//		if (OnStartedLoading != null) OnStartedLoading();
//	}
//
//	private void StopLoading()
//	{
//		loading = false;
//		if (OnFinishedLoading != null) OnFinishedLoading();
//	}

	private void ThreadDequeue(SpecialQueue<ThreadInfo> dataQueue)
	{
			ThreadInfo threadInfo = dataQueue.Dequeue ();
			
			if (threadInfo.parameter == null || threadInfo.callback == null) return;
			
			threadInfo.callback (threadInfo.parameter);
	}

	public struct ThreadInfo {
		public readonly Action<object> callback;
		public readonly object parameter;

		public ThreadInfo (Action<object> callback, object parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}

	}

	public void ClearTerrainQueues()
	{
		foreach (SpecialQueue<ThreadInfo> Queue in categorizedQueues[QueueCategory.Terrain])
		{
			Queue.Clear();
		}
	}

	public bool StillInQueue(ThreadInfo requestedMeshThreadInfo)
	{
		// To check for requests that no longer need to be loaded.
		bool stillInQueue = false;
		// Loop through priority queues within category.
        for (int i = 0; i < categorizedQueues[QueueCategory.Terrain].Length; i++)
        {
	        if (categorizedQueues[QueueCategory.Terrain][i].Contains(requestedMeshThreadInfo))
	        {
		        stillInQueue = true;
		        break;
	        }
        }

        return stillInQueue;
	}

	public void RemoveFromQueue(ThreadInfo requestedMeshThreadInfo)
	{
		// To cancel requests that no longer need to be loaded.
        for (int i = 0; i < categorizedQueues[QueueCategory.Terrain].Length; i++)
        {
	        if (categorizedQueues[QueueCategory.Terrain][i].Contains(requestedMeshThreadInfo))
	        {
		        categorizedQueues[QueueCategory.Terrain][i].Remove(requestedMeshThreadInfo);
		        break;
	        }
        }
	}
}

public class SpecialQueue<T>
{
	LinkedList<T> list = new LinkedList<T>();

	public void Enqueue(T t)
	{
		list.AddLast(t);
	}

	public T Dequeue()
	{
		if (list.First == null) return default;
		var result = list.First.Value;
		list.RemoveFirst();
		return result;
	}

	public T Peek()
	{
		return list.First.Value;
	}

	public bool Remove(T t)
	{
		return list.Remove(t);
	}

	public int Count { get { return list.Count; } }

	public bool Contains(T t)
	{
		return list.Contains(t);
	}
	
	public void Clear()
	{
		list = new LinkedList<T>();
	}
}