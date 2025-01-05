using System;
using System.Collections;
using System.Collections.Generic;
using FrameWork;
using NetWorkClient;
using Riptide.Utils;
using UnityEngine;
using UnityEngine.UI;

public class cs : MonoBehaviour
{
    public Image _image;
    private void Start()
    {
        
        GetImg();
    }


    public async void GetImg()
    {
        var url = "https://w.wallhaven.cc/full/rr/wallhaven-rrmg11.jpg";
        var texture=await RequestTool.Create(url, Methods.Get).SendTaskAsTexture();
        _image.sprite = texture;
    }
    
}
