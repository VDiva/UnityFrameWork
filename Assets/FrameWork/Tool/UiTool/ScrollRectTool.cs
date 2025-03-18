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
        public bool isInit;
        
        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
            if (prefab==null&&_scrollRect.content.childCount>0)
            {
                prefab=_scrollRect.content.GetChild(0).gameObject;
            }
            _prefabRect=prefab.GetComponent<RectTransform>();
            _prefabRect.pivot = new Vector2(0.5f, 1);
            _scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
            _scrollRect.horizontal=false;
            _scrollRect.vertical = true;
            _scrollRect.content.pivot = new Vector2(0.5f,1);
            _scrollRect.onValueChanged.AddListener(OnValueChanged);
            //_scrollRect.content.SetActive(false);
        }

        public void MoveTo(int index)
        {
            int startCount = index;
            _lastLoc=GetItemPos(startCount);
            _fistIndex = index;
            _lastIndex=index+_spawnCount-1;
            _scrollRect.content.anchoredPosition = new Vector2(0, -_lastLoc.y);
            for (int i = 0; i < _spawnCount; i++)
            {
                var j = startCount + i;
                Transform tran = null;
                if (i < _content.childCount)
                    tran = _content.GetChild(i);
                else
                    tran = GameObject.Instantiate(prefab, _content).transform;
                tran.localPosition = GetItemPos(j);
                _scrollItemCallback?.Invoke(j,tran.gameObject);
                tran.SetActive(true);
            }

        }
        

        private int _spawnCount;
        private RectTransform _content;
        public void Init(int count,Action<int,GameObject> callback)
        {
            _count = count;
            _content = _scrollRect.content;
            _scrollItemCallback = callback;
            float c = _count;
            _content.pivot = new Vector2(0.5f,1);
            var width=(_prefabRect.rect.width+space)*horCount;
            var height=(_prefabRect.rect.height+space)*(c/horCount);
            _content.sizeDelta=new Vector2(width,height);
            int spawnCount = (int)(_scrollRectTransform.sizeDelta.y / _prefabRect.sizeDelta.y+1)*horCount;
            spawnCount = Mathf.Min(spawnCount, count);
            _spawnCount = spawnCount;
            Tool.HideAllChild(_content);
            for (int i = 0; i < spawnCount; i++)
            {
                Transform tran = null;
                if (i < _content.childCount)
                    tran = _content.GetChild(i);
                else
                    tran = GameObject.Instantiate(prefab, _content).transform;
                tran.localPosition = GetItemPos(i);
                _scrollItemCallback?.Invoke(i,tran.gameObject);
                tran.SetActive(true);
            }

            _content.offsetMin = new Vector2(0, _content.offsetMin.y);
            _content.offsetMax = new Vector2(0, _content.offsetMax.y);
            _content.anchoredPosition=Vector2.zero;
            _lastLoc = Vector2.zero;
            _fistIndex = 0;
            _lastIndex=spawnCount-1;
            isInit=true;
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
                // if (i==0)
                // {
                //     Debug.Log(loc+"---"+_scrollRectTransform.sizeDelta+"--"+_fistIndex+"--"+_lastIndex);
                // }
                if (isUp)
                {
                    if (_lastIndex>=_count-1)return;
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