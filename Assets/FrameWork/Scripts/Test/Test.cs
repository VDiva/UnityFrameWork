using System;
using Cysharp.Threading.Tasks;
using FrameWork;
using UnityEngine;
using Xlsx;

namespace HotUpdate.Test
{
    public class Test : MonoBehaviour
    {
        private void Start()
        {
            ExTest().Forget();
        }

        private async UniTask ExTest()
        {
            await Tool.LoadAllXlsx();
            Debug.Log(Xlsx_Test_Query.XlsxDataAsOneKey.ByKeyGetValue(nameof(Xlsx_Test_Key.Test)).Key);
        }
    }
}