using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Shop
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 道具类型
		/// </summary>
		public int Type;

		/// <summary>
		/// 道具key
		/// </summary>
		public string ItemKey;

		/// <summary>
		/// 道具数量
		/// </summary>
		public int Count;

		/// <summary>
		/// 道具价格
		/// </summary>
		public int Price;

		/// <summary>
		/// 道具最大数量
		/// </summary>
		public int MaxGetCount;

		/// <summary>
		/// 描述
		/// </summary>
		public string Desc;

		public Xlsx_Shop(string Key,int Type,string ItemKey,int Count,int Price,int MaxGetCount,string Desc){
			this.Key=Key;
			this.Type=Type;
			this.ItemKey=ItemKey;
			this.Count=Count;
			this.Price=Price;
			this.MaxGetCount=MaxGetCount;
			this.Desc=Desc;

		}
		public Xlsx_Shop(){}	}
}
