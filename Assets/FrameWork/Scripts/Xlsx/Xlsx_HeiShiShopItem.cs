using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_HeiShiShopItem
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 购买类型
		/// </summary>
		public int type;

		/// <summary>
		/// 道具key(道具表key)
		/// </summary>
		public string ItemKey;

		/// <summary>
		/// 获得道具的数量
		/// </summary>
		public int Count;

		/// <summary>
		/// 道具的价格
		/// </summary>
		public int Price;

		/// <summary>
		/// 最大购买次数
		/// </summary>
		public int MaxGetCount;

		/// <summary>
		/// 道具随机权重
		/// </summary>
		public int Range;

		/// <summary>
		/// 描述
		/// </summary>
		public string Desc;

		public Xlsx_HeiShiShopItem(string Key,int type,string ItemKey,int Count,int Price,int MaxGetCount,int Range,string Desc){
			this.Key=Key;
			this.type=type;
			this.ItemKey=ItemKey;
			this.Count=Count;
			this.Price=Price;
			this.MaxGetCount=MaxGetCount;
			this.Range=Range;
			this.Desc=Desc;

		}
		public Xlsx_HeiShiShopItem(){}	}
}
