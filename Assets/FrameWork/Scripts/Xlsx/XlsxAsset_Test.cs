using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace Xlsx
{
    public sealed class XlsxAsset_Test : ScriptableObject
    {
        [LabelText("配置表数据行")] public List<Xlsx_Test> Rows = new List<Xlsx_Test>();
    }
}
