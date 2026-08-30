using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_MonsterBox_Query
	{
		public static List<Xlsx_MonsterBox> data=new List<Xlsx_MonsterBox>();
		public static XlsxData<string,Xlsx_MonsterBox> XlsxDataAsOneKey;
		public static XlsxData<string,int,Xlsx_MonsterBox> XlsxDataAsTowKey;
		static Xlsx_MonsterBox_Query()
		{
			var xlsx=Tool.LoadXlsx("Xlsx_MonsterBox");
			var itemData=xlsx.Split('\n');
			var fileNames = itemData[2].Split('|');
			var fileTypes = itemData[1].Split('|');
			for (int i = 3; i < itemData.Length; i++)
			{
				if (string.IsNullOrEmpty(itemData[i]))continue;
				var items = itemData[i].Split('|');
				var xlsxData = new Xlsx_MonsterBox();
				var type=xlsxData.GetType();
				for (int j = 0; j < items.Length; j++)
				{
					type.GetField(fileNames[j]).SetValue(xlsxData,Tool.ConversionType(fileTypes[j],items[j]));
				}
				data.Add(xlsxData);
			}
			XlsxDataAsOneKey = new XlsxData<string,Xlsx_MonsterBox>("Key", data);
		}
	}
}
