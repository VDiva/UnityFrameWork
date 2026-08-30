using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Item
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 道具名字
		/// </summary>
		public string Name;

		/// <summary>
		/// 道具描述
		/// </summary>
		public string Info;

		/// <summary>
		/// 道具获取途径
		/// </summary>
		public string GetInfo;

		/// <summary>
		/// 图片名字
		/// </summary>
		public string Icon;

		/// <summary>
		/// 品质
		/// </summary>
		public int Pz;

		public Xlsx_Item(string Key,string Name,string Info,string GetInfo,string Icon,int Pz){
			this.Key=Key;
			this.Name=Name;
			this.Info=Info;
			this.GetInfo=GetInfo;
			this.Icon=Icon;
			this.Pz=Pz;

		}
		public Xlsx_Item(){}	}
}
