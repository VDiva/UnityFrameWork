using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_HeiShiShop
	{
		/// <summary>
		/// Key
		/// </summary>
		public string Key;

		/// <summary>
		/// 可随机的黑市道具
		/// </summary>
		public string[] Items;

		public Xlsx_HeiShiShop(string Key,string[] Items){
			this.Key=Key;
			this.Items=Items;

		}
		public Xlsx_HeiShiShop(){}	}
}
