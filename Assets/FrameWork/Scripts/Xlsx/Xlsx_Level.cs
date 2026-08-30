using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Level
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 关卡类型(11普通关卡)
		/// </summary>
		public int LevelType;

		/// <summary>
		/// 关卡攻击加成
		/// </summary>
		public double AtkAdd;

		/// <summary>
		/// 关卡生命加成
		/// </summary>
		public double HpAdd;

		/// <summary>
		/// 地图预制体
		/// </summary>
		public string Prefab;

		/// <summary>
		/// Boss出现进度值
		/// </summary>
		public int BossProgress;

		/// <summary>
		/// 游戏时长
		/// </summary>
		public float LevelTime;

		/// <summary>
		/// boss战斗时间
		/// </summary>
		public float BossTime;

		/// <summary>
		/// 关卡名字
		/// </summary>
		public string LevelName;

		/// <summary>
		/// 关卡描述
		/// </summary>
		public string LevelInfo;

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

		public Xlsx_Level(string Key,int LevelType,double AtkAdd,double HpAdd,string Prefab,int BossProgress,float LevelTime,float BossTime,string LevelName,string LevelInfo,float BAIGL,float LANGL,float ZIGL,float CHENGGL,float YINGL,float LVGL,float FENGL,float JINGGL){
			this.Key=Key;
			this.LevelType=LevelType;
			this.AtkAdd=AtkAdd;
			this.HpAdd=HpAdd;
			this.Prefab=Prefab;
			this.BossProgress=BossProgress;
			this.LevelTime=LevelTime;
			this.BossTime=BossTime;
			this.LevelName=LevelName;
			this.LevelInfo=LevelInfo;
			this.BAIGL=BAIGL;
			this.LANGL=LANGL;
			this.ZIGL=ZIGL;
			this.CHENGGL=CHENGGL;
			this.YINGL=YINGL;
			this.LVGL=LVGL;
			this.FENGL=FENGL;
			this.JINGGL=JINGGL;

		}
		public Xlsx_Level(){}	}
}
