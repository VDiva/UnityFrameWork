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

        /// <summary>
        /// 删除一个定时器
        /// </summary>
        /// <param name="timeData"></param>
        public void DestroyTimer(TimeData timeData)
        {
            if (_timeDatas.Contains(timeData))
            {
                _objectPool.EnQueue(timeData);
                _timeDatas.Remove(timeData);
            }
        }

        /// <summary>
        /// 循环调用
        /// </summary>
        /// <param name="time">多久调用一次</param>
        /// <param name="call"></param>
        public TimeData IntervalCall(float time,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Init(true,time,-1,call);
            _timeDatas.Add(data);
            return data;
        }
        
        /// <summary>
        /// 每个时间调用一次 再一定时间内
        /// </summary>
        /// <param name="time">多久调用一次</param>
        /// <param name="intervalTime">持续多久</param>
        /// <param name="call"></param>
        public TimeData IntervalCallAsTime(float time,float intervalTime,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Init(true,time,intervalTime,call);
            _timeDatas.Add(data);
            return data;
        }
        
        /// <summary>
        /// 延迟调用
        /// </summary>
        /// <param name="time">延迟时间</param>
        /// <param name="call"></param>
        public TimeData DelayCall(float time,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Init(false,time,-1,call);
            _timeDatas.Add(data);
            return data;
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