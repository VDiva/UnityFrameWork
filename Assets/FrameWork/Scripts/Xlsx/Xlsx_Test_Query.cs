using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public static class Xlsx_Test_Query
	{
		public static List<Xlsx_Test> data=new List<Xlsx_Test>();
		public static XlsxData<string,Xlsx_Test> XlsxDataAsOneKey;
		static Xlsx_Test_Query()
		{
			Update();
		}
		public static void Update()
		{
			var nextData = Tool.LoadXlsxAsset<XlsxAsset_Test>("XlsxAsset_Test").Rows;
			var nextIndex = new XlsxData<string,Xlsx_Test>("Key", nextData);
			data = nextData;
			XlsxDataAsOneKey = nextIndex;
		}
	}
}
