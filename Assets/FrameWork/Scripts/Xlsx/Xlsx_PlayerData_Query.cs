using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_PlayerData_Query
	{
		public static List<Xlsx_PlayerData> data=new List<Xlsx_PlayerData>();
		public static XlsxData<string,Xlsx_PlayerData> XlsxDataAsOneKey;
		public static XlsxData<string,float,Xlsx_PlayerData> XlsxDataAsTowKey;
		static Xlsx_PlayerData_Query()
		{
			var xlsx=Tool.LoadXlsx("Xlsx_PlayerData");
			var itemData=xlsx.Split('\n');
			var fileNames = itemData[2].Split('|');
			var fileTypes = itemData[1].Split('|');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split('|');
				var xlsxData = new Xlsx_PlayerData();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_PlayerData>("Key", data);
		}
	}
}
