using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class Timer: SingletonAsMono<Timer>
    {
        private List<TimeData> _timeDatas = new List<TimeData>();
        private ObjectPool<TimeData> _objectPool = new ObjectPool<TimeData>();
        private void Awake()
        {
            StartCoroutine(TimeUpdate());
        }

        
        public void DestroyTimer(TimeData timeData)
        {
            if (_timeDatas.Contains(timeData))
            {
                _timeDatas.Remove(timeData);
            }
        }

        public void IntervalCall(float time,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Init(true,time,-1,call);
            _timeDatas.Add(data);
        }
        
        public void IntervalCallAsTime(float time,float intervalTime,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Init(true,time,intervalTime,call);
            _timeDatas.Add(data);
        }
        
        public void DelayCall(float time,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Init(false,time,-1,call);
            _timeDatas.Add(data);
        }

        private List<TimeData> _deleteList = new List<TimeData>();
        IEnumerator TimeUpdate()
        {
            while (true)
            {
                yield return null;
                for (int i = 0; i < _timeDatas.Count; i++)
                {
                    _timeDatas[i].Update(Time.deltaTime);
                }
            }
        }


        private void OnDestroy()
        {
            StopCoroutine(TimeUpdate());
        }
    }
}