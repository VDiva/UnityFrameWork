using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Language
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 中文
		/// </summary>
		public string Chinese;

		public Xlsx_Language(string Key,string Chinese){
			this.Key=Key;
			this.Chinese=Chinese;

		}
		public Xlsx_Language(){}	}
}
