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

        
        public void StopIntervalCall(Action call)
        {
            for (int i = 0; i < _timeDatas.Count; i++)
            {
                if (_timeDatas[i].Action==call)
                {
                    _objectPool.EnQueue(_timeDatas[i]);
                    _timeDatas.Remove(_timeDatas[i]);
                }
            }
        }

        public void IntervalCall(float time,Action call)
        {
            var data=_objectPool.DeQueue();
            data.IsInterval = true;
            data.Time = time;
            data.Cd = time;
            data.Action = call;
            _timeDatas.Add(data);
        }
        
        public void IntervalCallAsTime(float time,float intervalTime,Action call)
        {
            var data=_objectPool.DeQueue();
            data.IsInterval = true;
            data.Time = time;
            data.Cd = time;
            data.Action = call;
            _timeDatas.Add(data);


            var delayData = _objectPool.DeQueue();
            delayData.Time = intervalTime;
            delayData.Cd = intervalTime;
            delayData.Action = (() => StopIntervalCall(call));
            _timeDatas.Add(delayData);
        }
        
        public void DelayCall(float time,Action call)
        {
            var data=_objectPool.DeQueue();
            data.Time = time;
            data.Cd = time;
            data.Action = call;
            _timeDatas.Add(data);
        }

        private List<TimeData> _deleteList = new List<TimeData>();
        IEnumerator TimeUpdate()
        {
            while (true)
            {
                yield return null;
                _deleteList.Clear();
                for (int i = 0; i < _timeDatas.Count; i++)
                {
                    var timeData = _timeDatas[i];
                    timeData.Time -= Time.deltaTime;
                    if (timeData.Time<=0)
                    {
                        if (timeData.IsInterval)
                        {
                            timeData.Time = timeData.Cd;
                            timeData.Action?.Invoke();
                            // _timeDatas.Remove(_timeDatas[i]);
                            // _objectPool.EnQueue(_timeDatas[i]);
                        }
                        else
                        {
                            //_objectPool.EnQueue(_timeDatas[i]);
                            timeData.Action?.Invoke();
                            //_timeDatas.Remove(_timeDatas[i]);
                            if (i<_timeDatas.Count)
                            {
                                _deleteList.Add(timeData);
                            }
                        }
                    }
                }

                if (_deleteList.Count>0)
                {
                    for (int i = 0; i < _deleteList.Count; i++)
                    {
                        _objectPool.EnQueue(_deleteList[i]);
                        _timeDatas.Remove(_deleteList[i]);
                    }
                }
            }
        }


        private void OnDestroy()
        {
            StopCoroutine(TimeUpdate());
        }
    }
}