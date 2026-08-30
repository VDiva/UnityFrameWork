using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_EquipQuality
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 品质随机数值加成
		/// </summary>
		public float[] AddPzValue;

		/// <summary>
		/// 属性词条数
		/// </summary>
		public int PropertyCount;

		/// <summary>
		/// 品质名字
		/// </summary>
		public string PzName;

		/// <summary>
		/// 描述
		/// </summary>
		public string Desc;

		public Xlsx_EquipQuality(string Key,float[] AddPzValue,int PropertyCount,string PzName,string Desc){
			this.Key=Key;
			this.AddPzValue=AddPzValue;
			this.PropertyCount=PropertyCount;
			this.PzName=PzName;
			this.Desc=Desc;

		}
		public Xlsx_EquipQuality(){}	}
}
