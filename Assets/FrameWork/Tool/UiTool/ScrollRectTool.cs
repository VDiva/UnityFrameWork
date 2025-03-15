using System;
using UnityEngine;
using UnityEngine.UI;

namespace FrameWork
{
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollRectTool : MonoBehaviour
    {
        public GameObject prefab;
        public int horCount=1;
        public float space = 5;
        private RectTransform _prefabRect;
        private ScrollRect _scrollRect;
        private RectTransform _scrollRectTransform;
        private Action<int,GameObject> _scrollItemCallback;
        private int _count;

        
        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
            if (prefab==null&&_scrollRect.content.childCount>0)
            {
                prefab=_scrollRect.content.GetChild(0).gameObject;
            }
            _prefabRect=prefab.GetComponent<RectTransform>();
            _scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
            _scrollRect.horizontal=false;
            _scrollRect.vertical = true;
            _scrollRect.content.pivot = new Vector2(0.5f,1);
            _scrollRect.onValueChanged.AddListener(OnValueChanged);
        }

        public void Init(int count,Action<int,GameObject> callback=null)
        {
            _count = count;
            _scrollItemCallback = callback;
            float c = _count;
            _scrollRect.content.pivot = new Vector2(0.5f,1);
            var width=(_prefabRect.rect.width+space)*horCount;
            var height=(_prefabRect.rect.height+space)*(c/horCount);
            height += _prefabRect.sizeDelta.y;
            
            
            _scrollRect.content.sizeDelta=new Vector2(width,height);
            OnValueChanged(Vector2.zero);
            int spawnCount = (int)(_scrollRectTransform.sizeDelta.y / _prefabRect.sizeDelta.y+1)*horCount;
            for (int i = 0; i < spawnCount; i++)
            {
                Transform tran = null;
                if (i < _scrollRect.content.childCount)
                    tran = _scrollRect.content.GetChild(i);
                else
                    tran = GameObject.Instantiate(prefab, _scrollRect.content).transform;
                tran.localPosition = GetItemPos(i);
                _scrollItemCallback?.Invoke(i,tran.gameObject);
            }

            _fistIndex = 0;
            _lastIndex=spawnCount-1;
        }
        
        private Vector2 _lastLoc;
        private int _fistIndex;
        private int _lastIndex;
        private void OnValueChanged(Vector2 pos)
        {
            bool isUp = _scrollRect.content.localPosition.y > _lastLoc.y;
            for (int i = 0; i < _scrollRect.content.childCount; i++)
            {
                var item = _scrollRect.content.GetChild(i);
                var loc=_scrollRect.viewport.InverseTransformPoint(item.position);
                if (i==0)
                {
                    Debug.Log(loc+"---"+_scrollRectTransform.sizeDelta+"--"+_fistIndex+"--"+_lastIndex);
                }
                if (isUp)
                {
                    if (_lastIndex>=_count)return;
                    if (loc.y> _prefabRect.sizeDelta.y)
                    {
                        _fistIndex += 1;
                        _lastIndex += 1;
                        item.GetComponent<RectTransform>().anchoredPosition = GetItemPos(_lastIndex);
                        _scrollItemCallback?.Invoke(_lastIndex,item.gameObject);
                    }
                }
                else
                {
                    if (_fistIndex<=0)return;
                    if (Mathf.Abs(loc.y)>_scrollRectTransform.sizeDelta.y)
                    {
                        _fistIndex -= 1;
                        _lastIndex -= 1;
                        
                        item.GetComponent<RectTransform>().anchoredPosition = GetItemPos(_fistIndex);
                        _scrollItemCallback?.Invoke(_fistIndex,item.gameObject);
                    }
                }
                
            }

            _lastLoc = _scrollRect.content.localPosition;
        }
        
        
        public Vector2 GetItemPos(int index)
        {
            float c = index;
            var y = (int)(c / horCount) * (_prefabRect.sizeDelta.y + space);
            var leftLoc = (horCount-1) / 2f * _prefabRect.sizeDelta.x;
            var x = -leftLoc + c % horCount * (_prefabRect.sizeDelta.x + space);
            return new Vector2(x, -y);
        }
        
    }
}