using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_Testss_Query
	{
		public static List<Xlsx_Testss> data=new List<Xlsx_Testss>();
		public static XlsxData<string,Xlsx_Testss> XlsxDataAsOneKey;
		static Xlsx_Testss_Query()
		{
			var xlsx=ABLoad.LoadAsset<TextAsset>(Tool.GetMd5AsString("Xlsx_Testss"), "Xlsx_Testss");
			var itemData=xlsx.text.Split('\n');
			var fileNames = itemData[2].Split('|');
			var fileTypes = itemData[1].Split('|');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split('|');
				var xlsxData = new Xlsx_Testss();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					if (string.IsNullOrEmpty(items[j]))continue;
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_Testss>("Key", data);
		}
	}
}
