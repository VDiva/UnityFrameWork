using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FrameWork
{
    public class PassMouseEvent : MonoBehaviour,IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            PassEvent(eventData,ExecuteEvents.pointerClickHandler);
        }
        
        private static bool hasPassedEvent=false;
        private static void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function) where T: IEventSystemHandler
        {
            if(hasPassedEvent)
            {
                return;
            }
            hasPassedEvent = true;
            var result = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,result);
            var current = data.pointerCurrentRaycast.gameObject;

            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].gameObject!=current)
                {
                    ExecuteEvents.Execute(result[i].gameObject, data, function);
                }
            }
            result.Clear();
            hasPassedEvent = false;
        }
        
        
        private static void PassEventOne<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function) where T: IEventSystemHandler
        {
            if(hasPassedEvent)
            {
                return;
            }
            hasPassedEvent = true;
            var result = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,result);
            var current = data.pointerCurrentRaycast.gameObject;
            int index = 0;
            for (int i = 0; i < result.Count; i++)
            {
                if (index>0)break;
                if (result[i].gameObject!=current&& result[i].gameObject.GetComponent<Button>()!=null||result[i].gameObject.GetComponent<Button>()!=null)
                {
                    ExecuteEvents.Execute(result[i].gameObject, data, function);
                }
                else
                {
                    continue;
                }

                index += 1;
            }
            result.Clear();
            hasPassedEvent = false;
        }
    }
}