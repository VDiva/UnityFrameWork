using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_PlayerData
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 初始移动速度
		/// </summary>
		public float Speed;

		/// <summary>
		/// 初始攻击
		/// </summary>
		public float Atk;

		/// <summary>
		/// 攻击距离
		/// </summary>
		public float AtkRange;

		/// <summary>
		/// 预制体
		/// </summary>
		public string Prefab;

		/// <summary>
		/// 基础生命
		/// </summary>
		public float Hp;

		/// <summary>
		/// 基础法力值
		/// </summary>
		public float Mp;

		/// <summary>
		/// 职业名字
		/// </summary>
		public string OccName;

		/// <summary>
		/// 骨骼文件
		/// </summary>
		public string Skeleton;

		public Xlsx_PlayerData(string Key,float Speed,float Atk,float AtkRange,string Prefab,float Hp,float Mp,string OccName,string Skeleton){
			this.Key=Key;
			this.Speed=Speed;
			this.Atk=Atk;
			this.AtkRange=AtkRange;
			this.Prefab=Prefab;
			this.Hp=Hp;
			this.Mp=Mp;
			this.OccName=OccName;
			this.Skeleton=Skeleton;

		}
		public Xlsx_PlayerData(){}	}
}
