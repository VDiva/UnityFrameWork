using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_Monster
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 基础血量
		/// </summary>
		public float Hp;

		/// <summary>
		/// 基础攻击
		/// </summary>
		public float Atk;

		/// <summary>
		/// 基础移速
		/// </summary>
		public float Speed;

		/// <summary>
		/// 攻击距离
		/// </summary>
		public float AtkDis;

		/// <summary>
		/// 技能
		/// </summary>
		public string[] Skill;

		/// <summary>
		/// 预制体
		/// </summary>
		public string Prefab;

		/// <summary>
		/// 检测玩家范围
		/// </summary>
		public float CheckRange;

		public Xlsx_Monster(string Key,float Hp,float Atk,float Speed,float AtkDis,string[] Skill,string Prefab,float CheckRange){
			this.Key=Key;
			this.Hp=Hp;
			this.Atk=Atk;
			this.Speed=Speed;
			this.AtkDis=AtkDis;
			this.Skill=Skill;
			this.Prefab=Prefab;
			this.CheckRange=CheckRange;

		}
		public Xlsx_Monster(){}	}
}
