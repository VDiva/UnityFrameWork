using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_Public_Query
	{
		public static List<Xlsx_Public> data=new List<Xlsx_Public>();
		public static XlsxData<string,Xlsx_Public> XlsxDataAsOneKey;
		public static XlsxData<string,string,Xlsx_Public> XlsxDataAsTowKey;
		static Xlsx_Public_Query()
		{
			var xlsx=Tool.LoadXlsx("Xlsx_Public");
			var itemData=xlsx.Split('\n');
			var fileNames = itemData[2].Split('|');
			var fileTypes = itemData[1].Split('|');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split('|');
				var xlsxData = new Xlsx_Public();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_Public>("Key", data);
		}
	}
}
