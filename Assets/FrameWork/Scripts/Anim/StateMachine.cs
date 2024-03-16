
namespace FrameWork
{
    public class StateMachine
    {
        private IAnim _anim;
        
        public void RunAnim<T>() where T : IAnim,new()
        {
            _anim?.End((() =>
            {
                _anim = new T();
                _anim.Start();
            }));
        }


        public void Update()
        {
            _anim?.Update();
        }
    }
}