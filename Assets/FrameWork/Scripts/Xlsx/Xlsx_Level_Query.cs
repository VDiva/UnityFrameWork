using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_Level_Query
	{
		public static List<Xlsx_Level> data=new List<Xlsx_Level>();
		public static XlsxData<string,Xlsx_Level> XlsxDataAsOneKey;
		public static XlsxData<string,int,Xlsx_Level> XlsxDataAsTowKey;
		static Xlsx_Level_Query()
		{
			var xlsx=Tool.LoadXlsx("Xlsx_Level");
			var itemData=xlsx.Split('\n');
			var fileNames = itemData[2].Split('|');
			var fileTypes = itemData[1].Split('|');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split('|');
				var xlsxData = new Xlsx_Level();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_Level>("Key", data);
		}
	}
}
