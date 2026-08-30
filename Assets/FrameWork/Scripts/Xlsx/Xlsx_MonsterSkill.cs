using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_MonsterSkill
	{
		/// <summary>
		/// 技能key
		/// </summary>
		public string Key;

		/// <summary>
		/// 技能状态(创建一个状态类)
		/// </summary>
		public string SkillState;

		/// <summary>
		/// 技能伤害(基于怪的百分比)
		/// </summary>
		public float Atk;

		/// <summary>
		/// 技能释放距离
		/// </summary>
		public float ExRange;

		/// <summary>
		/// 技能范围
		/// </summary>
		public float Range;

		/// <summary>
		/// 技能初始CD
		/// </summary>
		public float StartCD;

		/// <summary>
		/// 技能CD
		/// </summary>
		public float CD;

		/// <summary>
		/// 是否是被动技能
		/// </summary>
		public int IsPassiveSkill;

		/// <summary>
		/// 描述
		/// </summary>
		public string Desc;

		public Xlsx_MonsterSkill(string Key,string SkillState,float Atk,float ExRange,float Range,float StartCD,float CD,int IsPassiveSkill,string Desc){
			this.Key=Key;
			this.SkillState=SkillState;
			this.Atk=Atk;
			this.ExRange=ExRange;
			this.Range=Range;
			this.StartCD=StartCD;
			this.CD=CD;
			this.IsPassiveSkill=IsPassiveSkill;
			this.Desc=Desc;

		}
		public Xlsx_MonsterSkill(){}	}
}
