using System;

namespace FrameWork
{
    public class TimeData
    {
        private bool _isInterval;
        private float _cd;
        private float _curCd;
        private float _time;
        private Action _action;

        public void Init(bool isInterval,float cd,float time,Action action)
        {
            _isInterval = isInterval;
            _cd = cd;
            _curCd = cd;
            _time = time;
            _action = action;
        }


        public void Update(float deltaTime)
        {
            
            _curCd -= deltaTime;
            if (_curCd<=0)
            {
                _action?.Invoke();
                if (_isInterval)
                    _curCd = _cd;
                else
                    Timer.Instance.DestroyTimer(this);
            }

            if (_time!=-1)
            {
                _time -= deltaTime;
                if (_time<=0)
                {
                    Timer.Instance.DestroyTimer(this);
                } 
            }
        }
        
    }
}