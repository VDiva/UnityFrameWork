using System.Collections.Generic;
using UnityEngine;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_cs_Query:SingletonAsClass<Xlsx_cs_Query>
	{
		public List<Xlsx_cs> data=new List<Xlsx_cs>();
		public XlsxData<string,Xlsx_cs> XlsxDataAsOneKey;
		public Xlsx_cs_Query()
		{
			var xlsx=AssetBundlesLoad.LoadAsset<TextAsset>("xlsx", "Xlsx_cs");
			var itemData=xlsx.text.Split('\n');
			var fileNames = itemData[2].Split(' ');
			var fileTypes = itemData[1].Split(' ');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split(' ');
				var xlsxData = new Xlsx_cs();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_cs>("key", data);
		}
	}
}
