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


            int maxCount = (int)(_count - (_spawnCount-1));
            int minCount = (int)((_spawnCount-1));
            if (startCount<=maxCount&& startCount>=minCount)
            {
                startCount =index- Mathf.FloorToInt( (_spawnCount-1) / 2f);
            }else if (startCount>=_count-_spawnCount)
            {
                startCount = _count - _spawnCount;
            }else if (startCount<(_spawnCount-1))
            {
                startCount = 0;
            }
            
            _lastLoc=-GetItemPos(startCount);
            _fistIndex = startCount;
            _lastIndex=startCount+_spawnCount-1;
            _scrollRect.content.anchoredPosition = new Vector2(0, _lastLoc.y);
            if (index>_count-(_spawnCount-1))
            {
                _scrollRect.verticalNormalizedPosition = 0;
            }
            
            // if (index<_spawnCount-1)
            // {
            //     _scrollRect.verticalNormalizedPosition = 1;
            // }
            
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
            int spawnCount = (int)(_scrollRectTransform.rect.height / _prefabRect.rect.height+1)*horCount;
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
        public void OnValueChanged(Vector2 pos)
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
                    if (loc.y> _prefabRect.rect.height)
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
                    if (Mathf.Abs(loc.y)>_scrollRectTransform.rect.height)
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


        // public void InitLoc()
        // {
        //     var loc = _lastLoc;
        //     while (!Mathf.Approximately(loc.x,_scrollRect.content.localPosition.x)|| !Mathf.Approximately(loc.y,_scrollRect.content.localPosition.y))
        //     {
        //         OnValueChanged(Vector2.zero);
        //     }
        // }
        
        public Vector2 GetItemPos(int index)
        {
            float c = index;
            var y = (int)(c / horCount) * (_prefabRect.rect.height + space);
            var leftLoc = (horCount-1) / 2f * _prefabRect.rect.width;
            var x = -leftLoc + c % horCount * (_prefabRect.rect.width + space);
            return new Vector2(x, -y);
        }
        
    }
}
