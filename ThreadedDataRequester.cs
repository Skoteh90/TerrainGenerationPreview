using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class ThreadedDataRequester : MonoBehaviour {

	static ThreadedDataRequester instance;
	Queue<ThreadInfo> dataQueue = new Queue<ThreadInfo>();

	bool disabled;
	
	float internalLogicUpdateSpeed = 0.04f; // Dequeue Speed for threaded data requests.

	private void Start()
	{
		StartCoroutine("LogicUpdate");
	}

	IEnumerator LogicUpdate() {
		while (!disabled)
		{
			yield return new WaitForSeconds(internalLogicUpdateSpeed);
			if (dataQueue.Count > 0) {
				ThreadInfo threadInfo = dataQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}
	}
	
	public void SetInternalLogicUpdateSpeed(float value)
	{
		internalLogicUpdateSpeed = value;
	}
	
	void Awake() {
		instance = FindObjectOfType<ThreadedDataRequester>();
	}

	public static void RequestData(Func<object> generateData, Action<object> callback) {
		ThreadStart threadStart = delegate {
			instance.DataThread (generateData, callback);
		};

		new Thread (threadStart).Start ();
	}

	void DataThread(Func<object> generateData, Action<object> callback) {
		object data = generateData ();
		lock (dataQueue) {
			dataQueue.Enqueue (new ThreadInfo (callback, data));
		}
	}

	struct ThreadInfo {
		public readonly Action<object> callback;
		public readonly object parameter;

		public ThreadInfo (Action<object> callback, object parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}

	}
}