using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_MonsterBox
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 没什么用就是看点位数量
		/// </summary>
		public int Point;

		/// <summary>
		/// 怪物表key
		/// </summary>
		public string MonsterKey;

		/// <summary>
		/// 怪物生成数量
		/// </summary>
		public int monsterCount;

		/// <summary>
		/// 增加进度值
		/// </summary>
		public int ProgressCount;

		/// <summary>
		/// Box类型(XG小怪 Box宝箱 SL首领)
		/// </summary>
		public string BoxType;

		/// <summary>
		/// 血球掉落概率
		/// </summary>
		public float XQGL;

		/// <summary>
		/// 血球恢复生命值(百分比)
		/// </summary>
		public float XQADD;

		/// <summary>
		/// 能量球掉落概率
		/// </summary>
		public float NLGL;

		/// <summary>
		/// 能量球恢复(百分比)
		/// </summary>
		public float NLADD;

		/// <summary>
		/// 白掉落概率
		/// </summary>
		public float BAIGL;

		/// <summary>
		/// 蓝掉落概率
		/// </summary>
		public float LANGL;

		/// <summary>
		/// 紫掉落概率
		/// </summary>
		public float ZIGL;

		/// <summary>
		/// 橙掉落概率
		/// </summary>
		public float CHENGGL;

		/// <summary>
		/// 银掉落概率
		/// </summary>
		public float YINGL;

		/// <summary>
		/// 绿掉落概率
		/// </summary>
		public float LVGL;

		/// <summary>
		/// 粉掉落概率
		/// </summary>
		public float FENGL;

		/// <summary>
		/// 金掉落概率
		/// </summary>
		public float JINGGL;

		/// <summary>
		/// 尝试掉落次数
		/// </summary>
		public int DlCount;

		/// <summary>
		/// 描述
		/// </summary>
		public string Desc;

		public Xlsx_MonsterBox(string Key,int Point,string MonsterKey,int monsterCount,int ProgressCount,string BoxType,float XQGL,float XQADD,float NLGL,float NLADD,float BAIGL,float LANGL,float ZIGL,float CHENGGL,float YINGL,float LVGL,float FENGL,float JINGGL,int DlCount,string Desc){
			this.Key=Key;
			this.Point=Point;
			this.MonsterKey=MonsterKey;
			this.monsterCount=monsterCount;
			this.ProgressCount=ProgressCount;
			this.BoxType=BoxType;
			this.XQGL=XQGL;
			this.XQADD=XQADD;
			this.NLGL=NLGL;
			this.NLADD=NLADD;
			this.BAIGL=BAIGL;
			this.LANGL=LANGL;
			this.ZIGL=ZIGL;
			this.CHENGGL=CHENGGL;
			this.YINGL=YINGL;
			this.LVGL=LVGL;
			this.FENGL=FENGL;
			this.JINGGL=JINGGL;
			this.DlCount=DlCount;
			this.Desc=Desc;

		}
		public Xlsx_MonsterBox(){}	}
}
