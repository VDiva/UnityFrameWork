

using System;
using System.Collections;

namespace FrameWork
{
    public class Mono : SingletonAsMono<Mono>
    {
        public void Frame(Action action)
        {
            StartCoroutine(_Frame(action));
        }

        IEnumerator _Frame(Action action)
        {
            yield return null;
            action?.Invoke();
        }
    }
}