using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FrameWork
{
    public class PassMouseEvent : MonoBehaviour,IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            PassEvent(eventData,ExecuteEvents.pointerClickHandler);
        }
        //
        // public void OnPointerDown(PointerEventData eventData)
        // {
        //     PassEvent(eventData,ExecuteEvents.pointerDownHandler);
        // }
        //
        // public void OnPointerUp(PointerEventData eventData)
        // {
        //     PassEvent(eventData,ExecuteEvents.pointerUpHandler);
        // }

        private static void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function) where T: IEventSystemHandler
        {
            var result = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,result);
            var current = data.pointerCurrentRaycast.gameObject;
            foreach (var t in result.Where(t=>current!=t.gameObject))
            {
                ExecuteEvents.Execute(t.gameObject, data, function);
            }
        }
    }
}