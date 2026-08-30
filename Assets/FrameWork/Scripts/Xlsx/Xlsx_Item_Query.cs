using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_Item_Query
	{
		public static List<Xlsx_Item> data=new List<Xlsx_Item>();
		public static XlsxData<string,Xlsx_Item> XlsxDataAsOneKey;
		public static XlsxData<string,string,Xlsx_Item> XlsxDataAsTowKey;
		static Xlsx_Item_Query()
		{
			var xlsx=Tool.LoadXlsx("Xlsx_Item");
			var itemData=xlsx.Split('\n');
			var fileNames = itemData[2].Split('|');
			var fileTypes = itemData[1].Split('|');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split('|');
				var xlsxData = new Xlsx_Item();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_Item>("Key", data);
		}
	}
}
