using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Equip
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 装备名字
		/// </summary>
		public string EquipName;

		/// <summary>
		/// 装备图标
		/// </summary>
		public string Icon;

		/// <summary>
		/// 装备职业归属
		/// </summary>
		public string Occ;

		/// <summary>
		/// 装备属于那个套装
		/// </summary>
		public string Suit;

		/// <summary>
		/// 装备部位(WEAPONHAND武器 WEAPONOFFHAND副手 HEAD头盔 BODY衣服 FOOT鞋子 HAND手套 NECK项链 YS药水)
		/// </summary>
		public string Part;

		/// <summary>
		/// 装备等级区间
		/// </summary>
		public int[] LvRange;

		/// <summary>
		/// 掉落关卡区间
		/// </summary>
		public string[] LevelRange;

		/// <summary>
		/// 每一级属性
		/// </summary>
		public float LvValue;

		/// <summary>
		/// 装备的品质
		/// </summary>
		public int[] Pz;

		/// <summary>
		/// 装备权重
		/// </summary>
		public float Range;

		/// <summary>
		/// 描述
		/// </summary>
		public string Desc;

		public Xlsx_Equip(string Key,string EquipName,string Icon,string Occ,string Suit,string Part,int[] LvRange,string[] LevelRange,float LvValue,int[] Pz,float Range,string Desc){
			this.Key=Key;
			this.EquipName=EquipName;
			this.Icon=Icon;
			this.Occ=Occ;
			this.Suit=Suit;
			this.Part=Part;
			this.LvRange=LvRange;
			this.LevelRange=LevelRange;
			this.LvValue=LvValue;
			this.Pz=Pz;
			this.Range=Range;
			this.Desc=Desc;

		}
		public Xlsx_Equip(){}	}
}
