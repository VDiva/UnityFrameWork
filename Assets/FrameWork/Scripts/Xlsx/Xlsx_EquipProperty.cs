using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_EquipProperty
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 属性名字
		/// </summary>
		public string Name;

		/// <summary>
		/// 加成类型(不填数值 1百分比)
		/// </summary>
		public int AddType;

		/// <summary>
		/// 每级加成
		/// </summary>
		public float LvAdd;

		/// <summary>
		/// 加成数值随机区间
		/// </summary>
		public float[] AddRange;

		/// <summary>
		/// 战力换算比例
		/// </summary>
		public float ZlRota;

		/// <summary>
		/// 战力换算值
		/// </summary>
		public float ZlValue;

		/// <summary>
		/// 权重
		/// </summary>
		public float Range;

		/// <summary>
		/// 部位可随机(WEAPONHAND武器 WEAPONOFFHAND副手 HEAD头盔 BODY衣服 FOOT鞋子 HAND手套 NECK项链 YS药水 RING戒指)
		/// </summary>
		public string[] Part;

		/// <summary>
		/// 描述
		/// </summary>
		public string desc;

		public Xlsx_EquipProperty(string Key,string Name,int AddType,float LvAdd,float[] AddRange,float ZlRota,float ZlValue,float Range,string[] Part,string desc){
			this.Key=Key;
			this.Name=Name;
			this.AddType=AddType;
			this.LvAdd=LvAdd;
			this.AddRange=AddRange;
			this.ZlRota=ZlRota;
			this.ZlValue=ZlValue;
			this.Range=Range;
			this.Part=Part;
			this.desc=desc;

		}
		public Xlsx_EquipProperty(){}	}
}
