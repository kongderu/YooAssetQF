using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static  class GameConst 
{
       private static  readonly Dictionary<string, System.Object> _blackboard = new Dictionary<string, object>(100);
		public static  void SetBlackboardValue(string key, System.Object value)
		{
			if (_blackboard.ContainsKey(key) == false)
				_blackboard.Add(key, value);
			else
				_blackboard[key] = value;
		}

		public static   System.Object GetBlackboardValue(string key)
		{
			if (_blackboard.TryGetValue(key, out System.Object value))
			{
				return value;
			}
			else
			{
				Debug.LogWarning($"Not found blackboard value : {key}");
				return null;
			}
		}
}
