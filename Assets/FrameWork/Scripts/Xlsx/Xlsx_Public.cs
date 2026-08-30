using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Public
	{
		/// <summary>
		/// Key
		/// </summary>
		public string Key;

		/// <summary>
		/// 内容
		/// </summary>
		public string Content;

		/// <summary>
		/// 内容
		/// </summary>
		public int ContentInt;

		public Xlsx_Public(string Key,string Content,int ContentInt){
			this.Key=Key;
			this.Content=Content;
			this.ContentInt=ContentInt;

		}
		public Xlsx_Public(){}	}
}
