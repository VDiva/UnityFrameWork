

using System;
using System.Collections;
using UnityEngine;

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
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}