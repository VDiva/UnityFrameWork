using System.Collections;
using System.Collections.Generic;
using FrameWork;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewTest : MonoBehaviour
{
    public string[] img;

    Dictionary<int,Sprite> dic = new Dictionary<int, Sprite>(); 
    public ScrollRectTool scrollRectTool;
    // Start is called before the first frame update
    void Start()
    {
        scrollRectTool.Init(img.Length,UpdateItem);
    }


    public async void UpdateItem(int index, GameObject go)
    {
        //Debug.Log(index);
        if (dic.ContainsKey(index))
        {
            go.GetComponent<Image>().sprite = dic[index];
        }
        else
        {
            RequestTool requestTool=RequestTool.Create(img[index],Methods.Get);
            go.GetComponent<Image>().sprite = null;
            // requestTool.Send((Sprite s) =>
            // {
            //     go.GetComponent<Image>().sprite = s;
            // });
            var asTexture= await requestTool.SendTaskAsTexture();
            go.GetComponent<Image>().sprite = asTexture;
            dic.TryAdd(index, asTexture);
        }
        
        
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
