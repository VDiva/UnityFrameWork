using System;
using System.Collections.Generic;

namespace FrameWork
{
    public class EntityManager: SingletonAsClass<EntityManager>
    {
        private Dictionary<string, ObjectPool<Actor>> _objectPools=new Dictionary<string, ObjectPool<Actor>>();

        public Actor CreateEntity<T>() where T: Actor, new()
        {
            var type = typeof(T);
            var scriptName = type.Namespace+"."+type.Name;
            if (_objectPools.TryGetValue(scriptName,out var objectPool))
            {
                if (objectPool.GetSize()>0)
                {
                    var actor = objectPool.DeQueue();
                    actor.GetGameObject().SetActive(true);
                    return actor;
                }
            }
            return (Actor)type.Assembly.CreateInstance(scriptName);
        }
        
        public Actor CreateEntity(Type type)
        {
            var scriptName = type.Namespace+"."+type.Name;
            if (_objectPools.TryGetValue(scriptName,out var objectPool))
            {
                if (objectPool.GetSize()>0)
                {
                    var actor = objectPool.DeQueue();
                    actor.GetGameObject().SetActive(true);
                    return actor;
                }
            }
            return (Actor)type.Assembly.CreateInstance(scriptName);
        }
        
        public void EnQueue(Actor actor)
        {
            var scriptName = actor.GetType().Name;
            if (_objectPools.TryGetValue(scriptName,out var objectPool))
            {
                objectPool.EnQueue(actor);
                actor.GetGameObject().SetActive(false);
            }
            else
            {
                var pool = new ObjectPool<Actor>();
                pool.EnQueue(actor);
                _objectPools.Add(scriptName,pool);
            }
        }
    }
}